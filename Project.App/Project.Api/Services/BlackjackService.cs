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

    // JSON serialization options with type discriminators for polymorphic types
    private static readonly JsonSerializerOptions _gameStateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    /// <summary>
    /// Serialize game state with proper options to preserve type discriminators.
    /// </summary>
    private static string SerializeGameState(BlackjackState state)
    {
        return JsonSerializer.Serialize(state, _gameStateJsonOptions);
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
                await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));

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

                // move to dealing stage
                await DealCardsAsync(state, roomId);

                // move to player action stage
                state.CurrentStage = new BlackjackPlayerActionStage(
                    DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                    0
                );
                await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));

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
                // Get player's hand
                var doubleHands =
                    await _handRepository.GetHandsByRoomIdAsync(player.Id)
                    ?? throw new BadRequestException("No hand found for this player.");
                var doubleHand =
                    doubleHands.FirstOrDefault()
                    ?? throw new BadRequestException("No hand found for this player.");

                // Get room to access deck
                var doubleRoom =
                    await _roomRepository.GetByIdAsync(roomId)
                    ?? throw new BadRequestException("Room not found.");

                // Verify this is the first turn (hand has exactly 2 cards)
                var doubleCards = await _deckApiService.GetHandCards(
                    doubleRoom.DeckId!,
                    doubleHand.Id.ToString()
                );
                if (doubleCards.Count != 2)
                {
                    throw new BadRequestException(
                        "Double down can only be done on the first turn with exactly 2 cards."
                    );
                }

                // Check if player has enough chips to double their bet
                if (player.Balance < doubleHand.Bet)
                {
                    throw new BadRequestException(
                        $"Player {playerId} does not have enough chips to double down."
                    );
                }

                // Double the bet (deduct from balance and update hand)
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -doubleHand.Bet);
                doubleHand.Bet *= 2;
                await _handRepository.UpdateHandAsync(doubleHand.Id, doubleHand);

                // Draw exactly one card
                await _deckApiService.DrawCards(doubleRoom.DeckId!, doubleHand.Id.ToString(), 1);

                // Automatically stand after drawing (double down rule)
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case SplitAction splitAction:
                // Get player's hand
                var splitHands =
                    await _handRepository.GetHandsByRoomIdAsync(player.Id)
                    ?? throw new BadRequestException("No hand found for this player.");
                var splitHand =
                    splitHands.FirstOrDefault()
                    ?? throw new BadRequestException("No hand found for this player.");

                // Get room to access deck
                var splitRoom =
                    await _roomRepository.GetByIdAsync(roomId)
                    ?? throw new BadRequestException("Room not found.");

                // Verify this is the first turn (hand has exactly 2 cards with same value)
                var splitCards = await _deckApiService.GetHandCards(
                    splitRoom.DeckId!,
                    splitHand.Id.ToString()
                );
                if (splitCards.Count != 2)
                {
                    throw new BadRequestException(
                        "Split can only be done on the first turn with exactly 2 cards."
                    );
                }

                // Check if both cards have the same value
                string value1 = splitCards[0].Value.ToUpper();
                string value2 = splitCards[1].Value.ToUpper();

                // Normalize face cards to "10"
                int GetCardNumericValue(string val) =>
                    val switch
                    {
                        "JACK" or "QUEEN" or "KING" => 10,
                        _ => int.TryParse(val, out int v) ? v : (val == "ACE" ? 1 : 0),
                    };

                if (GetCardNumericValue(value1) != GetCardNumericValue(value2))
                {
                    throw new BadRequestException(
                        "Split can only be done when both cards have the same value."
                    );
                }

                // Check if player has enough chips for the new bet
                if (player.Balance < splitHand.Bet)
                {
                    throw new BadRequestException(
                        $"Player {playerId} does not have enough chips to split."
                    );
                }

                // Deduct bet for second hand
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -splitHand.Bet);

                // Create second hand with same bet (empty, we'll add a card from deck API)
                var secondHand = new Hand
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = player.Id,
                    Order = 1,
                    Bet = splitHand.Bet,
                };
                await _handRepository.CreateHandAsync(secondHand);

                // Note: In the Deck API, we can't move cards between piles, so we keep both cards
                // in the first hand and just create a new empty hand for the second
                // The split is logical - each hand will get additional cards drawn

                // Create empty pile for second hand and draw one card for each
                await _deckApiService.CreateEmptyHand(splitRoom.DeckId!, secondHand.Id.ToString());
                await _deckApiService.DrawCards(splitRoom.DeckId!, splitHand.Id.ToString(), 1);
                await _deckApiService.DrawCards(splitRoom.DeckId!, secondHand.Id.ToString(), 1);

                // Continue with player's turn (they can hit/stand on each hand)
                break;
            case SurrenderAction surrenderAction:
                // Get player's hands
                var surrenderHands =
                    await _handRepository.GetHandsByRoomIdAsync(player.Id)
                    ?? throw new BadRequestException("No hand found for this player.");

                // Not allowed after splitting (player should only have one hand)
                if (surrenderHands.Count() > 1)
                {
                    throw new BadRequestException("Surrender is not allowed after splitting.");
                }

                var surrenderHand =
                    surrenderHands.FirstOrDefault()
                    ?? throw new BadRequestException("No hand found for this player.");

                // Get room to access deck
                var surrenderRoom =
                    await _roomRepository.GetByIdAsync(roomId)
                    ?? throw new BadRequestException("Room not found.");

                // Verify this is the first turn (hand has exactly 2 cards)
                var surrenderCards = await _deckApiService.GetHandCards(
                    surrenderRoom.DeckId!,
                    surrenderHand.Id.ToString()
                );
                if (surrenderCards.Count != 2)
                {
                    throw new BadRequestException(
                        "Surrender can only be done on the first turn with exactly 2 cards."
                    );
                }

                // Refund half of player's bet
                long refund = surrenderHand.Bet / 2;
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, refund);

                // Mark hand as inactive or delete it
                await _handRepository.DeleteHandAsync(surrenderHand.Id);

                // Mark player as inactive for this round
                player.Status = Status.Inactive;
                await _roomPlayerRepository.UpdateAsync(player);

                // Move to next player or finish round
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
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

                    // Deal cards to all players and dealer
                    await DealCardsAsync(state, roomId);

                    // Move to player action stage
                    state.CurrentStage = new BlackjackPlayerActionStage(
                        DateTimeOffset.UtcNow + _config.TurnTimeLimit,
                        0
                    );
                    await _roomRepository.UpdateGameStateAsync(
                        roomId,
                        SerializeGameState(state)
                    );
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
            await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));
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
        await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));

        // Get room to access deck ID
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new BadRequestException("Room not found.");

        if (string.IsNullOrEmpty(room.DeckId))
        {
            throw new InternalServerException($"Deck for room {roomId} not found.");
        }

        // Dealer's hidden card is already revealed (it's in state.DealerHand)
        // Now dealer plays - hit until 17 or higher
        var dealerCards = state
            .DealerHand.Cast<JsonElement>()
            .Select(je => JsonSerializer.Deserialize<CardDTO>(je.GetRawText())!)
            .ToList();

        // Create dealer hand in deck API to draw more cards if needed
        var dealerHandId = Guid.NewGuid();
        await _deckApiService.CreateEmptyHand(room.DeckId, dealerHandId.ToString());

        // Dealer hits until 17 or higher
        while (CalculateHandValue(dealerCards) < 17)
        {
            var newCards = await _deckApiService.DrawCards(room.DeckId, dealerHandId.ToString(), 1);
            dealerCards.AddRange(newCards);
        }

        // Update dealer hand in state
        state.DealerHand = dealerCards.Cast<object>().ToList();
        await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));

        int dealerValue = CalculateHandValue(dealerCards);
        bool dealerBusted = dealerValue > 21;
        bool dealerHasBlackjack = IsBlackjack(dealerCards);

        // Get all active players
        IEnumerable<RoomPlayer> activePlayers =
            await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId);

        // Calculate winnings for each player
        foreach (RoomPlayer player in activePlayers)
        {
            // Get player hands from database
            var hands = await _handRepository.GetHandsByRoomIdAsync(player.Id);
            if (hands == null || !hands.Any())
                continue;

            foreach (var hand in hands)
            {
                // Get cards from Deck API
                List<CardDTO> playerCards;
                try
                {
                    playerCards = await _deckApiService.GetHandCards(
                        room.DeckId,
                        hand.Id.ToString()
                    );
                    if (!playerCards.Any())
                        continue;
                }
                catch
                {
                    continue; // Skip if can't get cards
                }

                int playerValue = CalculateHandValue(playerCards);
                bool playerBusted = playerValue > 21;
                bool playerHasBlackjack = IsBlackjack(playerCards);

                long winnings = 0;

                // Determine winnings
                if (playerBusted)
                {
                    // Player busted - loses bet (already deducted)
                    winnings = 0;
                }
                else if (dealerBusted)
                {
                    // Dealer busted - player wins
                    if (playerHasBlackjack)
                    {
                        // Blackjack pays 3:2
                        winnings = hand.Bet + (hand.Bet * 3 / 2);
                    }
                    else
                    {
                        // Regular win pays 1:1
                        winnings = hand.Bet * 2;
                    }
                }
                else if (playerHasBlackjack && !dealerHasBlackjack)
                {
                    // Player blackjack beats dealer non-blackjack - pays 3:2
                    winnings = hand.Bet + (hand.Bet * 3 / 2);
                }
                else if (dealerHasBlackjack && !playerHasBlackjack)
                {
                    // Dealer blackjack beats player non-blackjack - player loses
                    winnings = 0;
                }
                else if (playerValue > dealerValue)
                {
                    // Player wins - pays 1:1
                    winnings = hand.Bet * 2;
                }
                else if (playerValue < dealerValue)
                {
                    // Dealer wins - player loses
                    winnings = 0;
                }
                else
                {
                    // Push - return bet
                    winnings = hand.Bet;
                }

                // Update player balance
                if (winnings > 0)
                {
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, winnings);
                }
            }
        }

        // Initialize next betting stage
        state.CurrentStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow + _config.BettingTimeLimit,
            []
        );

        // Reset dealer hand for next round
        state.DealerHand = [];

        await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));
    }

    /// <summary>
    /// Deal initial cards to all active players (2 cards each) and dealer (2 cards, one hidden).
    /// </summary>
    private async Task DealCardsAsync(BlackjackState state, Guid roomId)
    {
        // Get room to access deck ID
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new BadRequestException("Room not found.");

        if (string.IsNullOrEmpty(room.DeckId))
        {
            throw new InternalServerException($"Deck for room {roomId} not found.");
        }

        // Get all active players who placed bets
        IEnumerable<RoomPlayer> activePlayers =
            await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId);

        // Deal 2 cards to each player
        foreach (var player in activePlayers)
        {
            // Get or create player's hand
            var hands = await _handRepository.GetHandsByRoomIdAsync(player.Id);
            var hand = hands?.FirstOrDefault();

            if (hand == null)
            {
                // Create new hand if it doesn't exist
                hand = new Hand
                {
                    Id = Guid.NewGuid(),
                    RoomPlayerId = player.Id,
                    Order = 0,
                    Bet = 0,
                };
                await _handRepository.CreateHandAsync(hand);

                // Create empty pile in Deck API
                await _deckApiService.CreateEmptyHand(room.DeckId, hand.Id.ToString());
            }

            // Draw 2 cards for the player
            await _deckApiService.DrawCards(room.DeckId, hand.Id.ToString(), 2);
        }

        // Deal 2 cards to dealer and store in game state
        // Create a temporary hand for the dealer
        var dealerHandId = Guid.NewGuid();
        await _deckApiService.CreateEmptyHand(room.DeckId, dealerHandId.ToString());
        var dealerCards = await _deckApiService.DrawCards(room.DeckId, dealerHandId.ToString(), 2);

        // Store dealer cards in state (first card is visible, second is hidden)
        state.DealerHand = dealerCards.Cast<object>().ToList();

        await _roomRepository.UpdateGameStateAsync(roomId, SerializeGameState(state));
    }

    /// <summary>
    /// Calculate the value of a hand, accounting for Aces (1 or 11).
    /// </summary>
    private int CalculateHandValue(List<CardDTO> cards)
    {
        int totalValue = 0;
        int aceCount = 0;

        foreach (var card in cards)
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
                    if (int.TryParse(card.Value, out int val))
                        totalValue += val;
                    break;
            }
        }

        // Adjust for Aces if busted
        while (totalValue > 21 && aceCount > 0)
        {
            totalValue -= 10;
            aceCount--;
        }

        return totalValue;
    }

    /// <summary>
    /// Check if a hand is a blackjack (Ace + 10-value card).
    /// </summary>
    private bool IsBlackjack(List<CardDTO> cards)
    {
        if (cards.Count != 2)
            return false;

        bool hasAce = cards.Any(c => c.Value.ToUpper() == "ACE");
        bool hasTen = cards.Any(c => c.Value.ToUpper() is "10" or "JACK" or "QUEEN" or "KING");

        return hasAce && hasTen;
    }
}
