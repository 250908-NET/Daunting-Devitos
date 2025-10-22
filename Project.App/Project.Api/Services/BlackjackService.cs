using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Enums;

namespace Project.Api.Services;

/*

set up deck API connection
set up game configs

loop
    shuffle deck(?)

    loop
        if everyone has bet/left or time is up
            break
        end if
        wait
    end loop
    deduct bets

    deal 2 cards to each player
    deal 2 cards to dealer (one hidden)

    loop (foreach player)
        loop
            if hit
                deal card
            else if stand
                break
            end if
        end loop
    end loop

    deal to dealer (hit until 17)
    calculate scores
    determine outcomes
    distribute winnings
end loop

teardown
close room

*/

/*

problem:
  a REST API is stateless, so there's no way to have a "timer" for game phases.
  this could lead to a long delay if a player never moves.

solution (the realistic one):
  use something like Redis pub/sub to broadcast a delayed message that acts as a timer

solution (the hacky one):
  have the initial request handler that started the betting phase start a timer and trigger the next game phase
  (this could be brittle if the server crashes or restarts)

solution (the funny one):
  have a "hurry up" button that triggers the next game phase if the time is past the deadline
  (could be combined with prev, but could lead to a race condition)

*/

public class BlackjackService(
    IRoomRepository roomRepository,
    IRoomPlayerRepository roomPlayerRepository,
    IUserRepository userRepository,
    IHandRepository handRepository,
    IDeckApiService deckApiService
) : IBlackjackService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IHandRepository _handRepository = handRepository;
    private readonly IDeckApiService _deckApiService = deckApiService;

    private BlackjackConfig _config = new();
    public BlackjackConfig Config
    {
        get => _config;
        set => _config = value;
    }

    public async Task<BlackjackState> GetGameStateAsync(Guid roomId)
    {
        string stateString = await _roomRepository.GetGameStateAsync(roomId);

        return JsonSerializer.Deserialize<BlackjackState>(stateString)!;
    }

    public static bool IsActionValid(string action, BlackjackStage stage) =>
        action switch
        {
            "bet" => stage is BlackjackBettingStage,
            "hit" => stage is BlackjackPlayerActionStage,
            "stand" => stage is BlackjackPlayerActionStage,
            "double" => stage is BlackjackPlayerActionStage,
            "split" => stage is BlackjackPlayerActionStage,
            "surrender" => stage is BlackjackPlayerActionStage,
            "hurry_up" => stage is BlackjackBettingStage or BlackjackPlayerActionStage,
            _ => false,
        };

    public async Task PerformActionAsync(
        Guid roomId,
        Guid playerId,
        string action,
        JsonElement data
    )
    {
        // ensure action is valid for this stage
        BlackjackState state = await GetGameStateAsync(roomId);
        if (!IsActionValid(action, state.CurrentStage))
        {
            throw new BadRequestException(
                $"Action {action} is not a valid action for this game stage."
            );
        }

        // check if player is in the room
        RoomPlayer player =
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(roomId, playerId)
            ?? throw new BadRequestException($"Player {playerId} not found.");

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        // do the action :)
        switch (actionDTO)
        {
            case BetAction betAction:
                // check if player has enough chips
                if (player.Balance < betAction.Amount)
                {
                    throw new BadRequestException(
                        $"Player {playerId} does not have enough chips to bet {betAction.Amount}."
                    );
                }

                BlackjackBettingStage stage = (BlackjackBettingStage)state.CurrentStage;

                // set bet in gamestate
                stage.Bets[player.Id] = betAction.Amount;
                await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

                // update player status
                player.Status = Status.Active;
                await _roomPlayerRepository.UpdateAsync(player);

                // if not past deadline, do not move to next stage
                if (DateTime.UtcNow < stage.Deadline)
                {
                    break;
                }

                // time is up, process all bets
                foreach ((Guid better, long bet) in stage.Bets)
                {
                    try
                    {
                        await _roomPlayerRepository.UpdatePlayerBalanceAsync(better, -bet);
                    }
                    catch (NotFoundException)
                    {
                        // a bet was recorded for a player who no longer exists?
                        throw new InternalServerException(
                            $"Could not find player {better} to process their bet."
                        );
                    }
                }

                // move to next stage

                state.CurrentStage = new BlackjackPlayerActionStage(
                    DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                    0
                );
                await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

                // TODO: dealing stage

                // TODO: move to player action stage

                break;
            case HitAction hitAction:
                // Fetch player's hands
                var hands =
                    await _handRepository.GetHandsByRoomIdAsync(player.Id)
                    ?? throw new BadRequestException("No hand found for this player.");

                var hand =
                    hands.FirstOrDefault()
                    ?? throw new BadRequestException("No hand found for this player.");

                // Retrieve deck ID from room configuration or state
                var room =
                    await _roomRepository.GetByIdAsync(roomId)
                    ?? throw new BadRequestException("Room not found.");

                // Draw one card and add it to player's hand
                var drawnCards = await _deckApiService.DrawCards(
                    room.DeckId?.ToString()
                        ?? throw new InternalServerException($"Deck for room {roomId} not found."),
                    hand.Id.ToString(),
                    1
                );

                // --- Calculate total value and check for bust ---
                int totalValue = 0;
                int aceCount = 0;

                foreach (var card in drawnCards)
                {
                    switch (card.Value.ToUpper())
                    {
                        case "ACE":
                            aceCount++;
                            totalValue += 11;
                            break;
                        case "KING":
                        case "QUEEN":
                        case "JACK":
                            totalValue += 10;
                            break;
                        default:
                            //Number cards (2–10) → handled by:
                            if (int.TryParse(card.Value, out int val))
                                totalValue += val;
                            break;
                    }
                }

                while (totalValue > 21 && aceCount > 0)
                {
                    totalValue -= 10;
                    aceCount--;
                }

                if (totalValue > 21)
                {
                    await _roomPlayerRepository.UpdateAsync(player);
                    await NextHandOrFinishRoundAsync(state, roomId);
                }
                await NextHandOrFinishRoundAsync(state, roomId);
                break;

            case StandAction standAction:
                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case DoubleAction doubleAction:
                // can only be done on the player's first turn!

                // check if player has enough chips to double their bet

                // double player's bet (deduct from balance and update gamestate)

                // draw one card

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                throw new NotImplementedException();
            case SplitAction splitAction:
                // can only be done on the player's first turn!

                // check if player has enough chips to do the new bet

                // deal new hand (2 cards)

                // next turn should be player's first hand
                // can only be done on the player's first turn!

                // check if player has enough chips to do the new bet

                // deal new hand (2 cards)

                // next turn should be player's first hand
                throw new NotImplementedException();
            case SurrenderAction surrenderAction:
                // not allowed after splitting!
                //   maybe check if player only has one hand?

                // refund half of player's bet (deduct from balance and update gamestate)

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                // not allowed after splitting!
                //   maybe check if player only has one hand?

                // refund half of player's bet (deduct from balance and update gamestate)

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                throw new NotImplementedException();
            case HurryUpAction hurryUpAction:
                if (state.CurrentStage is BlackjackBettingStage bettingStage)
                {
                    // Check if deadline has passed
                    if (DateTime.UtcNow < bettingStage.Deadline)
                    {
                        throw new BadRequestException(
                            "Cannot hurry up - betting deadline has not passed yet."
                        );
                    }

                    // Process all bets
                    foreach ((Guid better, long bet) in bettingStage.Bets)
                    {
                        try
                        {
                            await _roomPlayerRepository.UpdatePlayerBalanceAsync(better, -bet);
                        }
                        catch (NotFoundException)
                        {
                            // a bet was recorded for a player who no longer exists?
                            throw new InternalServerException(
                                $"Could not find player {better} to process their bet."
                            );
                        }
                    }

                    // Move to next stage
                    state.CurrentStage = new BlackjackPlayerActionStage(
                        DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                        0
                    );
                    await _roomRepository.UpdateGameStateAsync(
                        roomId,
                        JsonSerializer.Serialize(state)
                    );

                    // TODO: dealing stage

                    // TODO: move to player action stage
                }
                else if (state.CurrentStage is BlackjackPlayerActionStage playerActionStage)
                {
                    // Check if deadline has passed
                    if (DateTime.UtcNow < playerActionStage.Deadline)
                    {
                        throw new BadRequestException(
                            "Cannot hurry up - player action deadline has not passed yet."
                        );
                    }

                    // Move to next player or finish round
                    await NextHandOrFinishRoundAsync(state, roomId);
                }
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Move to the next player/hand turn, or if no players/hands are left, move to next stage (dealer turn).
    /// </summary>
    private async Task NextHandOrFinishRoundAsync(BlackjackState state, Guid roomId)
    {
        if (state.CurrentStage is not BlackjackPlayerActionStage playerActionStage)
        {
            throw new InvalidOperationException(
                "Cannot move to next hand when not in player action stage."
            );
        }

        // Get all active players in the room
        IEnumerable<RoomPlayer> activePlayers =
            await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId);
        List<RoomPlayer> activePlayersList = activePlayers.ToList();

        // Move to next player
        int nextIndex = playerActionStage.Index + 1;

        // If there are more players, continue with player actions
        if (nextIndex < activePlayersList.Count)
        {
            state.CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                nextIndex
            );
            await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
        }
        else
        {
            // All players have finished, move to dealer turn and finish round
            await FinishRoundAsync(state, roomId);
        }
    }

    // After the players have finished playing, the dealer's hand is resolved by drawing cards until
    // the hand achieves a total of 17 or higher. If the dealer has a total of 17 including an ace valued as 11
    // (a "soft 17"), some games require the dealer to stand while other games require the dealer to hit.
    // The dealer never doubles, splits, or surrenders. If the dealer busts, all players who haven't busted win.
    // If the dealer does not bust, each remaining bet wins if its hand is higher than the dealer's and
    // loses if it is lower. In the case of a tie ("push" or "standoff"), bets are returned without adjustment.
    // A blackjack beats any hand that is not a blackjack, even one with a value of 21.
    private async Task FinishRoundAsync(BlackjackState state, Guid roomId)
    {
        // Transition to finish round stage
        state.CurrentStage = new BlackjackFinishRoundStage();
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

        // TODO: Reveal dealer's hidden card (requires deck API integration)

        // TODO: Dealer plays - hit until 17 or higher (requires deck API integration)
        // For now, calculate dealer value from existing hand
        // In a complete implementation:
        // while (CalculateHandValue(state.DealerHand) < 17)
        // {
        //     var card = await DrawCardFromDeck(roomId);
        //     state.DealerHand.Add(card);
        // }

        // TODO: Calculate winnings for each player hand
        // Get all active players
        IEnumerable<RoomPlayer> activePlayers =
            await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId);

        // For each player:
        // 1. Get their hands from the database
        // 2. Calculate hand value
        // 3. Compare with dealer hand
        // 4. Determine winnings (blackjack pays 3:2, regular win pays 1:1, push returns bet)
        // 5. Update player balance

        foreach (RoomPlayer player in activePlayers)
        {
            // TODO: Implement hand evaluation and payout logic
            // This requires:
            // - Getting player hands from database
            // - Parsing card data from JSON
            // - Comparing with dealer hand
            // - Updating balances via repository
        }

        // Initialize next betting stage
        state.CurrentStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow + _config.BettingTimeLimit,
            []
        );

        // Reset dealer hand for next round
        state.DealerHand = [];

        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
    }
}
