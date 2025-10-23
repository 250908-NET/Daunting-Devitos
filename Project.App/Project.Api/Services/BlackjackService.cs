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
        List<int> dealerHandValues = new();
        bool dealer = false;
        Room? room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null || room.DeckId == null)
        {
            throw new InternalServerException($"Room or DeckId not found for roomId: {roomId}");
        }
        // reveal dealer cards



        // do dealer turn, if needed (until dealer has 17 or higher)
        while (dealerHandValues.Sum() < 17)
        {
            if (!dealer)
            {
                foreach (var card in state.DealerHand)
                {
                    int cardValue = await GetCardValue(card);
                    dealerHandValues.Add(cardValue);
                }
                dealer = true;
            }
            else
            {
                // draw a card for dealer
            
                List<CardDTO> drawnCards = await _deckApiService.DrawCards(room.DeckId, "Dealer", 1);

                int cardValue = await GetCardValue(drawnCards.Last());
                dealerHandValues.Add(cardValue);
                state.DealerHand.Add(drawnCards.Last());
                if (dealerHandValues.Sum() > 21 && dealerHandValues.Contains(11))
                {
                    dealerHandValues[dealerHandValues.IndexOf(11)] = 1;
                }
            }
        }



        // Transition to finish round stage
        state.CurrentStage = new BlackjackFinishRoundStage();
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

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
        List<CardDTO> playerHandCards = [];
        int totalValue = 0;
        int totalReward = 0;

        foreach (RoomPlayer player in activePlayers)
        {
            playerHandCards = await _deckApiService.ListHand(roomId.ToString(), player.Id.ToString());
            totalValue = await CalculateHandValue(playerHandCards);
            foreach (var hand in player.Hands)
            {
                totalReward = (totalValue > 21 || totalValue < dealerHandValues.Sum() || ((dealerHandValues.Sum() == 21 && totalValue == 21) && dealerHandValues.Count < playerHandCards.Count)) ? 0 : hand.Bet * 2;
                if (totalValue == dealerHandValues.Sum())
                {
                    totalReward /= 2; // Pay back the bet
                }
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, totalReward);
            }
            // TODO: Implement hand evaluation and payout logic
            // This requires:
            // - Getting player hands from database
            // - Parsing card data from JSON
            // - Comparing with dealer hand
            // - Updating balances via repository
        }


        // Initialize next betting stage

        // Clear hands for next round
        int i = 0;
        state.DealerHand = [];
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
        if (await _deckApiService.ReturnAllCardsToDeck(room.DeckId))
        {
            await _deckApiService.CreateEmptyHand(room.DeckId, "Dealer");
            List<CardDTO> dealerHand = await _deckApiService.DrawCards(room.DeckId, "Dealer", 2);
            foreach (var player in activePlayers)
            {
                foreach (var hand in player.Hands)
                {
                    await _handRepository.DeleteHandAsync(hand.Id);
                }
                await _deckApiService.CreateEmptyHand(room.DeckId, player.Id.ToString());
                await _handRepository.CreateHandAsync(new Hand
                {
                    Id = player.Id,
                    RoomPlayerId = player.Id,
                    Bet = 0,
                    Order = i++
                });
                await _deckApiService.DrawCards(room.DeckId, player.Id.ToString(), 2);
            }
            state.CurrentStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow + _config.BettingTimeLimit,
            []
        );

        }
        

       
    }

    private static async Task<int> GetCardValue(CardDTO card)
    {
        return card.Value switch
        {
            "ACE" => 11,
            "2" => 2,
            "3" => 3,
            "4" => 4,
            "5" => 5,
            "6" => 6,
            "7" => 7,
            "8" => 8,
            "9" => 9,
            "10" or "JACK" or "QUEEN" or "KING" => 10,
            _ => 0,
        };
    }
    private async Task<int> CalculateHandValue(List<CardDTO> hand)
    {
        int value = 0;
        int aceCount = 0;

        foreach (var card in hand)
        {
            int cardValue = await GetCardValue(card);
            value += cardValue;
            if (card.Value == "ACE")
            {
                aceCount++;
            }
        }

        // Adjust for aces if value exceeds 21
        while (value > 21 && aceCount > 0)
        {
            value -= 10; // Count ace as 1 instead of 11
            aceCount--;
        }

        return value;
    }
}
