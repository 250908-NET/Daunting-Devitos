using Project.Api.DTOs;
using Project.Api.Models.Games;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Enums;

namespace Project.Api.Utilities.Extensions;

public static class RoomSSEExtensions
{
    /// <summary>
    /// Broadcasts a hand of cards to the room using the given SSE service, optionally hiding certain cards.
    /// </summary>
    public static async Task BroadcastAsync(
        this List<CardDTO> hand,
        IRoomSSEService sseService,
        Guid roomId,
        Guid? playerId = null,
        int handIndex = 0,
        params int[] hiddenIndices
    )
    {
        List<CardDTO> handCopy =
        [
            .. hand.Select(
                (card, index) =>
                    hiddenIndices.Contains(index)
                        ? new CardDTO { IsFaceDown = true } // if face down, hide all info
                        : card
            ),
        ];

        if (playerId.HasValue)
        {
            await sseService.BroadcastEventAsync(
                roomId,
                RoomEventType.PlayerReveal,
                new PlayerRevealEventData
                {
                    PlayerId =
                        playerId
                        ?? throw new InternalServerException(
                            $"Player ID {playerId} is somehow both null and has a value. How did we get here?"
                        ),
                    HandIndex = handIndex,
                    PlayerHand = handCopy,
                    PlayerScore = hand.CalculateHandValue(),
                }
            );
        }
        else
        {
            // broadcast as dealer hand if no player ID given
            await sseService.BroadcastEventAsync(
                roomId,
                RoomEventType.DealerReveal,
                new DealerRevealEventData
                {
                    DealerHand = handCopy,
                    DealerScore = hand.CalculateHandValue(),
                }
            );
        }
    }

    /// <summary>
    /// Broadcasts a player action to the room using the given SSE service.
    /// </summary>
    public static async Task BroadcastPlayerActionAsync(
        this IRoomSSEService roomSSEService,
        Guid roomId,
        Guid userId,
        int handNumber,
        string action,
        long? amount = null,
        List<CardDTO>? cards = null,
        Guid? targetPlayerId = null,
        bool? success = null
    )
    {
        await roomSSEService.BroadcastEventAsync(
            roomId,
            RoomEventType.PlayerAction,
            new PlayerActionEventData
            {
                PlayerId = userId,
                HandIndex = handNumber,
                Action = action,
                Amount = amount,
                Cards = cards,
                TargetPlayerId = targetPlayerId,
                Success = success,
            }
        );
    }
}
