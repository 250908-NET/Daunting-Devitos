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
    IDeckApiService deckApiService,
    IRoomSSEService roomSSEService,
    ILogger<BlackjackService> logger
) : IBlackjackService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IHandRepository _handRepository = handRepository;
    private readonly IDeckApiService _deckApiService = deckApiService;
    private readonly IRoomSSEService _roomSSEService = roomSSEService;
    private readonly ILogger<BlackjackService> _logger = logger;

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

    public async Task SetupGameAsync(Guid roomId)
    {
        // create initial game state
        BlackjackState initialState = new()
        {
            CurrentStage = new BlackjackBettingStage(
                DateTimeOffset.UtcNow + _config.BettingTimeLimit,
                []
            ),
        };

        // create new deck
        string deckId = await _deckApiService.CreateDeck();

        // save to room
        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        room.DeckId = deckId;
        room.GameState = JsonSerializer.Serialize(initialState);

        await _roomRepository.UpdateAsync(room);
    }

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
                await ProcessBetAsync(state, roomId, player, betAction.Amount);

                // if not past deadline, do not move to next stage
                if (DateTime.UtcNow < ((BlackjackBettingStage)state.CurrentStage).Deadline)
                {
                    break;
                }

                // time is up, start the round
                await StartRoundAsync(state, roomId);
                break;
            case HitAction:
                bool busted = await DoHitAsync(state, roomId, player);

                if (busted)
                {
                    await NextHandOrFinishRoundAsync(state, roomId);
                }
                else
                {
                    // stay on the same player's turn, but reset the deadline
                    state.CurrentStage = new BlackjackPlayerActionStage(
                        DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                        ((BlackjackPlayerActionStage)state.CurrentStage).PlayerIndex,
                        ((BlackjackPlayerActionStage)state.CurrentStage).HandIndex
                    );
                    await _roomRepository.UpdateGameStateAsync(
                        roomId,
                        JsonSerializer.Serialize(state)
                    );
                }
                break;

            case StandAction:
                // do nothing :)

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case DoubleAction:
                await DoDoubleAsync(state, roomId, player);

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                throw new NotImplementedException();
            case SplitAction splitAction:
                await DoSplitAsync(state, roomId, player, splitAction.Amount);

                // stay on the same player's turn, but reset the deadline
                state.CurrentStage = new BlackjackPlayerActionStage(
                    DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                    ((BlackjackPlayerActionStage)state.CurrentStage).PlayerIndex,
                    ((BlackjackPlayerActionStage)state.CurrentStage).HandIndex
                );
                await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
                break;
            case SurrenderAction surrenderAction:
                await DoSurrenderAsync(state, roomId, player);

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case HurryUpAction hurryUpAction:
                switch (state.CurrentStage)
                {
                    case BlackjackBettingStage:
                        // start the round
                        await StartRoundAsync(state, roomId);
                        break;
                    case BlackjackPlayerActionStage:
                        // act as if the player stood
                        await NextHandOrFinishRoundAsync(state, roomId);
                        break;
                    default:
                        throw new BadRequestException("Nothing to hurry in the current stage.");
                }
                break;
            default:
                throw new BadRequestException("Unrecognized action type.");
        }
    }

    /// <summary>
    /// Process a player's bet during the betting stage.
    /// </summary>
    /// <exception cref="BadRequestException">Thrown if the player does not have enough chips to bet.</exception>
    private async Task ProcessBetAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player,
        long bet
    )
    {
        // check if player has enough chips
        if (player.Balance < bet)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to bet {bet}."
            );
        }

        BlackjackBettingStage stage = (BlackjackBettingStage)state.CurrentStage;

        // set bet in gamestate
        stage.Bets[player.Id] = bet;
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

        // update player status
        player.Status = Status.Active;
        await _roomPlayerRepository.UpdateAsync(player);
    }

    /// <summary>
    /// Start the round after betting is complete.
    /// Deduct bets from player balances and move to dealing stage.
    /// </summary>
    private async Task StartRoundAsync(BlackjackState state, Guid roomId)
    {
        // move to dealing stage
        state.CurrentStage = new BlackjackDealingStage();
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

        // get bets
        Dictionary<Guid, long> bets = ((BlackjackBettingStage)state.CurrentStage).Bets;

        // deduct bets from player balances and initialize hands
        int order = 0;
        foreach ((Guid better, long bet) in bets)
        {
            try
            {
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(better, -bet);

                await _handRepository.CreateHandAsync(
                    new Hand
                    {
                        RoomPlayerId = better,
                        Order = order++,
                        Bet = bet,
                    }
                );
            }
            catch (NotFoundException)
            {
                // a bet was recorded for a player who no longer exists?
                // should not happen, but just in case
                throw new InternalServerException(
                    $"Could not find player {better} to process their bet."
                );
            }
        }

        // get deck ID
        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");
        if (room.DeckId == null)
        {
            _logger.LogError(
                "Warning: Room {RoomId} has no deck ID. Attempting to create a new one...",
                roomId
            );

            // create new deck
            room.DeckId = await _deckApiService.CreateDeck();
            await _roomRepository.UpdateAsync(room);

            _logger.LogInformation(
                "Successfully created new deck {DeckId} for room {RoomId}!",
                room.DeckId,
                roomId
            );
        }

        // deal initial cards (2 to each player, 2 to dealer, one at a time)
        string deckId = room.DeckId.ToString();
        List<Hand> hands = await _handRepository.GetHandsByRoomIdAsync(roomId);

        if (hands.Count == 0)
        {
            throw new InternalServerException($"No hands found for room {roomId}.");
        }

        // one card at a time, deal 2 rounds to players and dealer
        for (int i = 0; i < 2; i++)
        {
            // deal one card to each hand in order
            foreach (Hand hand in hands.OrderBy(h => h.Order))
            {
                await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
            }

            // deal to dealer
            await _deckApiService.DrawCards(deckId, "dealer", 1);
        }

        // move to player action stage
        state.CurrentStage = new BlackjackPlayerActionStage(
            DateTimeOffset.UtcNow + _config.TurnTimeLimit,
            0,
            0
        );
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
    }

    private async Task<(BlackjackPlayerActionStage, Hand, string)> InitializePlayerActionAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player
    )
    {
        BlackjackPlayerActionStage stage =
            state.CurrentStage as BlackjackPlayerActionStage
            ?? throw new InternalServerException("Current stage is not player action stage.");

        Hand hand = await _handRepository.GetHandByRoomOrderAsync(
            roomId,
            stage.PlayerIndex,
            stage.HandIndex
        );

        // make sure player corresponds to the current turn's player
        if (hand.RoomPlayerId != player.Id)
        {
            throw new BadRequestException("It is not your turn.");
        }

        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        return (
            stage,
            hand,
            room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID.")
        );
    }

    /// <summary>
    /// Process a player's "hit" action during their turn.
    /// </summary>
    /// <returns>true if player busted</returns>
    private async Task<bool> DoHitAsync(BlackjackState state, Guid roomId, RoomPlayer player)
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        // draw one card and add it to player's hand
        List<CardDTO> handCards = await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);

        // check if player busted
        int totalValue = CalculateHandValue(handCards);

        return totalValue > 21;
    }

    /// <summary>
    /// Process a player's "double" action during their turn, doubling their bet and drawing one card.
    /// </summary>
    /// <remarks>Should end the player's turn.</remarks>
    private async Task DoDoubleAsync(BlackjackState state, Guid roomId, RoomPlayer player)
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        // can only be done on the player's first turn!
        // check if player only has two cards in hand and is on their first hand
        List<CardDTO> handCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        if (stage.HandIndex != 0 || handCards.Count > 2)
        {
            throw new BadRequestException("Double can only be done on the player's first turn.");
        }

        // check if player has enough chips to double their bet
        if (player.Balance < hand.Bet)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to double their bet."
            );
        }

        // double player's bet (deduct from balance and update gamestate)
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -hand.Bet);
        hand.Bet *= 2;
        await _handRepository.UpdateHandAsync(hand.Id, hand);

        // draw one card and add it to player's hand
        await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
    }

    /// <summary>
    /// Process a player's "split" action during their turn, splitting their hand into two hands.
    /// </summary>
    /// <remarks>Next turn should be the player's first hand.</remarks>
    private async Task DoSplitAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player,
        long amount
    )
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        // check if player has enough chips to do the new bet
        if (player.Balance < amount)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to split their bet with {amount}."
            );
        }

        // can only be done on the player's first turn!
        // check if player only has two cards in hand and is on their first hand
        List<CardDTO> handCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        if (stage.HandIndex != 0 || handCards.Count != 2)
        {
            throw new BadRequestException("Double can only be done on the player's first turn.");
        }

        // check if both cards are the same value
        if (handCards[0].Value != handCards[1].Value)
        {
            throw new BadRequestException("Can only split if both cards have the same value.");
        }

        // create new hand with second card
        await _handRepository.CreateHandAsync(
            new()
            {
                RoomPlayerId = hand.RoomPlayerId,
                Order = hand.Order,
                HandNumber = hand.HandNumber + 1,
                Bet = amount,
            }
        );
        Hand newHand = await _handRepository.GetHandByRoomOrderAsync(
            roomId,
            hand.Order,
            hand.HandNumber + 1
        );

        // move second card to new hand
        CardDTO cardToMove = handCards[1];
        await _deckApiService.RemoveFromHand(deckId, $"hand-{hand.Id}", cardToMove.Code);
        await _deckApiService.AddToHand(deckId, $"hand-{newHand.Id}", cardToMove.Code);

        // draw one card for each hand
        await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
        await _deckApiService.DrawCards(deckId, $"hand-{newHand.Id}", 1);

        // deduct bet from player's balance
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -amount);
    }

    /// <summary>
    /// Process a player's "surrender" action during their turn, forfeiting half their bet and ending their turn.
    /// </summary>
    private async Task DoSurrenderAsync(BlackjackState state, Guid roomId, RoomPlayer player)
    {
        // not allowed after splitting!
        // check if player only has one hand
        List<Hand> hands = await _handRepository.GetHandsByUserIdAsync(roomId, player.Id);
        if (hands.Count > 1)
        {
            throw new BadRequestException("Surrender is not allowed after splitting.");
        }

        // refund half of player's bet (add to balance and update gamestate)
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, hands[0].Bet / 2);
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

    static int CalculateHandValue(List<CardDTO> hand, int target = 21)
    {
        int totalValue = 0;
        int aceCount = 0;

        foreach (CardDTO card in hand)
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

        while (totalValue > target && aceCount > 0)
        {
            totalValue -= 10;
            aceCount--;
        }

        return totalValue;
    }
}
