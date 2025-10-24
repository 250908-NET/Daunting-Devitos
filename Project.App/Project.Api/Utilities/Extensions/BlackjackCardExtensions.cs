using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Enums;

namespace Project.Api.Utilities.Extensions;

public static class BlackjackCardExtensions
{
    /// <summary>
    /// Calculates the total value of a hand of blackjack cards.
    /// </summary>
    public static int CalculateHandValue(
        this List<CardDTO> hand,
        int target = 21,
        bool countFlipped = false
    )
    {
        int totalValue = 0;
        int aceCount = 0;

        foreach (CardDTO card in hand)
        {
            if (card.IsFaceDown && !countFlipped)
                continue; // Do not count face-down cards for score calculation unless specified

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
    /// <returns>Positive if player wins, negative if dealer wins, 0 if push</returns>
    public static int CompareHand(this List<CardDTO> playerHand, List<CardDTO> dealerHand)
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

    /// <summary>
    /// Saves the current game state and broadcasts an update to connected clients, if the repository and/or service are provided.
    /// </summary>
    public static async Task SaveStateAndBroadcastAsync(
        this BlackjackState state,
        Guid roomId,
        IRoomRepository? roomRepository = null,
        IRoomSSEService? roomSSEService = null
    )
    {
        if (roomRepository is not null)
        {
            // save state
            string updatedGameState = JsonSerializer.Serialize(state);
            await roomRepository.UpdateGameStateAsync(roomId, updatedGameState);
        }

        if (roomSSEService is not null)
        {
            // broadcast state update
            await roomSSEService.BroadcastEventAsync(
                roomId,
                RoomEventType.GameStateUpdate,
                new GameStateUpdateEventData { CurrentStage = state.CurrentStage }
            );
        }
    }
}
