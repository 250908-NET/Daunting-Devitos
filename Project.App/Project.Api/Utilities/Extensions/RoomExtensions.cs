using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;

namespace Project.Api.Utilities.Extensions;

public static class RoomExtensions
{
    public static async Task<string> GetOrCreateDeckId(
        this Room room,
        IDeckApiService deckApiService,
        IRoomRepository roomRepository,
        ILogger? logger = null
    )
    {
        if (room.DeckId == null)
        {
            logger?.LogError(
                "Warning: Room {RoomId} has no deck ID. Attempting to create a new one...",
                room.Id
            );

            // create new deck
            room.DeckId = await deckApiService.CreateDeck();
            await roomRepository.UpdateAsync(room);

            logger?.LogInformation(
                "Successfully created new deck {DeckId} for room {RoomId}!",
                room.DeckId,
                room.Id
            );
        }

        return room.DeckId;
    }
}
