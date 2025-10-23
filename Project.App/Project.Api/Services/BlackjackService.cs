using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Enums;
using Project.Api.Utilities.Extensions;

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
                    ((BlackjackPlayerActionStage)state.CurrentStage).ResetDeadline(
                        _config.TurnTimeLimit
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
                ((BlackjackPlayerActionStage)state.CurrentStage).ResetDeadline(
                    _config.TurnTimeLimit
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
                        // check if all active players have bet
                        List<RoomPlayer> activePlayersList =
                        [
                            .. await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId),
                        ];
                        if (activePlayersList.Count != state.Bets.Count)
                        {
                            throw new BadRequestException("Not all active players have bet yet.");
                        }

                        // start the round
                        await StartRoundAsync(state, roomId);
                        break;
                    case BlackjackPlayerActionStage:
                        // mark player as inactive
                        player.Status = Status.Inactive;
                        await _roomPlayerRepository.UpdateAsync(player);

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
        // get bets
        Dictionary<Guid, long> bets = ((BlackjackBettingStage)state.CurrentStage).Bets;

        // move to dealing stage
        state.CurrentStage = new BlackjackDealingStage();
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

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
        string deckId = await room.GetOrCreateDeckId(_deckApiService, _roomRepository, _logger);

        // deal initial cards (2 to each player, 2 to dealer, one at a time)
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

    /// <summary>
    /// Initializes a player action.
    /// </summary>
    /// <returns>
    /// A tuple containing the current player action stage, the player's hand, and the deck ID.
    /// </returns>
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

        BlackjackPlayerActionStage stage = (BlackjackPlayerActionStage)state.CurrentStage;

        // check if current player's next hand exists
        try
        {
            int nextHandIndex = stage.HandIndex + 1;
            Hand nextHand = await _handRepository.GetHandByRoomOrderAsync(
                roomId,
                stage.PlayerIndex,
                stage.HandIndex + 1
            );

            // if so, move to next hand
            state.CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                stage.PlayerIndex,
                stage.HandIndex + 1
            );
        }
        catch (NotFoundException)
        {
            // player has no more hands
        }

        // if not, check if next player's first hand exists
        try
        {
            int nextPlayerIndex = stage.PlayerIndex + 1;
            Hand nextPlayerFirstHand = await _handRepository.GetHandByRoomOrderAsync(
                roomId,
                nextPlayerIndex,
                0
            );

            // if so, move to next player
            state.CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                nextPlayerIndex,
                0
            );
            await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));
            return;
        }
        catch (NotFoundException)
        {
            // no more players
        }

        // all players have finished, move to dealer turn and finish round
        await FinishRoundAsync(state, roomId);
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
        // transition to finish round stage
        state.CurrentStage = new BlackjackFinishRoundStage();
        await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        List<CardDTO> dealerHand = await _deckApiService.ListHand(
            room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
            "dealer"
        );

        // hit until dealer has at least 17
        int dealerValue = CalculateHandValue(dealerHand);
        while (dealerValue < 17)
        {
            dealerHand = await _deckApiService.DrawCards(
                room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
                "dealer",
                1
            );
            dealerValue = CalculateHandValue(dealerHand);
        }

        // calculate winnings for each player hand
        List<Hand> hands = await _handRepository.GetHandsByRoomIdAsync(roomId);
        foreach (Hand hand in hands)
        {
            List<CardDTO> playerHand = await _deckApiService.ListHand(
                room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
                $"hand-{hand.Id}"
            );

            switch (CompareHands(dealerHand, playerHand))
            {
                case > 0:
                    // player wins
                    long winnings = hand.Bet * 2;
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(
                        hand.RoomPlayerId,
                        winnings
                    );
                    break;
                case 0:
                    // push
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(
                        hand.RoomPlayerId,
                        hand.Bet
                    );
                    break;
                case < 0:
                    // dealer wins, do nothing
                    break;
            }
        }

        // reset player hands
        foreach (Hand hand in hands)
        {
            await _handRepository.DeleteHandAsync(hand.Id);
        }

        // return all cards to deck and shuffle
        string deckId = await room.GetOrCreateDeckId(_deckApiService, _roomRepository, _logger);
        bool success = await _deckApiService.ReturnAllCardsToDeck(deckId);
        if (!success)
        {
            _logger.LogError("Failed to return cards to deck for room {RoomId}, skipping.", roomId);
        }

        // initialize next betting stage
        state.CurrentStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow + _config.BettingTimeLimit,
            []
        );
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

    /// <summary>
    /// Compares dealer and player hands.
    /// </summary>
    /// <param name="dealerHand"></param>
    /// <param name="playerHand"></param>
    /// <returns>Positive if player wins, negative if dealer wins, 0 if push</returns>
    static int CompareHands(List<CardDTO> dealerHand, List<CardDTO> playerHand)
    {
        int dealerValue = CalculateHandValue(dealerHand);
        int playerValue = CalculateHandValue(playerHand);

        // if player busts, dealer always wins
        if (playerValue > 21)
            return -1;

        // otherwise, if dealer busts, player wins
        if (dealerValue > 21)
            return 1;

        // blackjack beats a 21
        if (playerValue == 21 && dealerValue == 21)
        {
            return dealerHand.Count - playerHand.Count;
        }

        return playerValue - dealerValue;
    }
}
