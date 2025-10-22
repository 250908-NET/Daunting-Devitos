using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.DTOs;
using Project.Api.Services.Interface;
using Project.Api.Utilities;

namespace Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController(IRoomSSEService roomSSEService) : ControllerBase
{
    private readonly IRoomSSEService _roomSSEService = roomSSEService;

    /// <summary>
    /// Long‑lived SSE endpoint for clients to subscribe to room events.
    /// </summary>
    /// <param name="roomId"></param>
    /// <returns></returns>
    [HttpGet("{roomId}/events")]
    public async Task GetRoomEvents(Guid roomId)
    {
        if (HttpContext.Request.Headers.Accept.Contains("text/event-stream"))
        {
            await _roomSSEService.AddConnectionAsync(roomId, HttpContext.Response);
        }
        else
        {
            throw new BadRequestException(
                "This endpoint requires the header 'Accept: text/event-stream'."
            );
        }
    }

    /// <summary>
    /// Test endpoint to broadcast an event to all clients in a room.
    /// Authenticated per user, but allows anonymous users to send messages.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{roomId}/chat")]
    public async Task<IActionResult> BroadcastMessage(Guid roomId, [FromBody] MessageDTO message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            throw new BadRequestException("Message content cannot be empty.");
        }

        string name = User.Identity?.Name ?? "Anonymous";

        await _roomSSEService.BroadcastEventAsync(roomId, "message", $"{name}: {message.Content}");
        return Ok();
    }
}
