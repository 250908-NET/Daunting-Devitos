using Project.Api.Models.Games;
using Project.Api.Utilities.Enums;

namespace Project.Api.Services.Interface;

public interface IRoomSSEService
{
    /// <summary>
    /// Adds a new client connection for a specific room and keeps it open.
    /// </summary>
    Task AddConnectionAsync(Guid roomId, HttpResponse response);

    /// <summary>
    /// Broadcasts an event to all clients connected to a specific room.
    /// </summary>
    Task BroadcastEventAsync(Guid roomId, RoomEventType eventName, IRoomEventData data);
}
