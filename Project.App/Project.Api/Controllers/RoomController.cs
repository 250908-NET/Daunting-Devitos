using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.DTOs;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;

namespace Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all room endpoints
public class RoomController(
    IRoomService roomService,
    IRoomSSEService roomSSEService,
    ILogger<RoomController> logger
) : ControllerBase
{
    private readonly IRoomService _roomService = roomService;
    private readonly ILogger<RoomController> _logger = logger;

    // GET: api/room
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetAllRooms()
    {
        var rooms = await _roomService.GetAllRoomsAsync();
        return Ok(rooms);
    }

    // GET: api/room/active
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetActiveRooms()
    {
        var rooms = await _roomService.GetActiveRoomsAsync();
        return Ok(rooms);
    }

    // GET: api/room/public
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetPublicRooms()
    {
        var rooms = await _roomService.GetPublicRoomsAsync();
        return Ok(rooms);
    }

    // GET: api/room/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDTO>> GetRoomById(Guid id)
    {
        var room = await _roomService.GetRoomByIdAsync(id);
        if (room == null)
            throw new NotFoundException($"Room with ID {id} not found");
        return Ok(room);
    }

    // GET: api/room/host/{hostId}
    [HttpGet("host/{hostId}")]
    public async Task<ActionResult<RoomDTO>> GetRoomByHostId(Guid hostId)
    {
        var room = await _roomService.GetRoomByHostIdAsync(hostId);
        if (room == null)
            throw new NotFoundException($"Room with host ID {hostId} not found");
        return Ok(room);
    }

    // GET: api/room/{id}/exists
    [HttpGet("{id}/exists")]
    public async Task<ActionResult<bool>> RoomExists(Guid id)
    {
        var exists = await _roomService.RoomExistsAsync(id);
        return Ok(exists);
    }

    // GET: api/room/{id}/gamestate
    [HttpGet("{id}/gamestate")]
    public async Task<ActionResult<string>> GetGameState(Guid id)
    {
        var gameState = await _roomService.GetGameStateAsync(id);
        return Ok(gameState);
    }

    // GET: api/room/{id}/gameconfig
    [HttpGet("{id}/gameconfig")]
    public async Task<ActionResult<string>> GetGameConfig(Guid id)
    {
        var gameConfig = await _roomService.GetGameConfigAsync(id);
        return Ok(gameConfig);
    }

    // POST: api/room
    [HttpPost]
    public async Task<ActionResult<RoomDTO>> CreateRoom([FromBody] CreateRoomDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var room = await _roomService.CreateRoomAsync(dto);
        return CreatedAtAction(nameof(GetRoomById), new { id = room.Id }, room);
    }

    // PUT: api/room/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<RoomDTO>> UpdateRoom(Guid id, [FromBody] UpdateRoomDTO dto)
    {
        if (id != dto.Id)
            throw new BadRequestException("Room ID mismatch");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var room = await _roomService.UpdateRoomAsync(dto);
        if (room == null)
            throw new NotFoundException($"Room with ID {id} not found");

        return Ok(room);
    }

    // PUT: api/room/{id}/gamestate
    [HttpPut("{id}/gamestate")]
    public async Task<ActionResult> UpdateGameState(Guid id, [FromBody] string gameState)
    {
        var success = await _roomService.UpdateGameStateAsync(id, gameState);
        if (!success)
            throw new NotFoundException($"Room with ID {id} not found");

        return NoContent();
    }

    // PUT: api/room/{id}/gameconfig
    [HttpPut("{id}/gameconfig")]
    public async Task<ActionResult> UpdateGameConfig(Guid id, [FromBody] string gameConfig)
    {
        var success = await _roomService.UpdateGameConfigAsync(id, gameConfig);
        if (!success)
            throw new NotFoundException($"Room with ID {id} not found");

        return NoContent();
    }

    // DELETE: api/room/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRoom(Guid id)
    {
        var success = await _roomService.DeleteRoomAsync(id);
        if (!success)
            throw new NotFoundException($"Room with ID {id} not found");

        return NoContent();
    }

    // --- Game Management Endpoints ---

    // POST: api/room/{id}/start
    [HttpPost("{id}/start")]
    public async Task<ActionResult<RoomDTO>> StartGame(
        Guid id,
        [FromBody] string? gameConfigJson = null
    )
    {
        var room = await _roomService.StartGameAsync(id, gameConfigJson);
        return Ok(room);
    }

    // POST: api/room/{roomId}/player/{playerId}/action
    [HttpPost("{roomId}/player/{playerId}/action")]
    public async Task<ActionResult> PerformPlayerAction(
        Guid roomId,
        Guid playerId,
        [FromBody] PlayerActionRequest request
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _roomService.PerformPlayerActionAsync(roomId, playerId, request.Action, request.Data);
        return Ok(new { message = "Action performed successfully" });
    }

    // --- Player Management Endpoints ---

    // POST: api/room/{roomId}/join
    [HttpPost("{roomId}/join")]
    public async Task<ActionResult<RoomDTO>> JoinRoom(
        Guid roomId,
        [FromBody] JoinRoomRequest request
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var room = await _roomService.JoinRoomAsync(roomId, request.UserId);
        return Ok(room);
    }

    // POST: api/room/{roomId}/leave
    [HttpPost("{roomId}/leave")]
    public async Task<ActionResult<RoomDTO>> LeaveRoom(
        Guid roomId,
        [FromBody] LeaveRoomRequest request
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var room = await _roomService.LeaveRoomAsync(roomId, request.UserId);
        return Ok(room);
    }

    // GET: api/room/{roomId}/players
    [HttpGet("{roomId}/players")]
    public async Task<ActionResult> GetRoomPlayers(
        Guid roomId,
        [FromServices] IRoomPlayerRepository roomPlayerRepository
    )
    {
        var players = await roomPlayerRepository.GetByRoomIdAsync(roomId);
        var playerDtos = players.Select(p => new
        {
            id = p.Id,
            userId = p.UserId,
            userName = p.User?.Name ?? "Unknown",
            userEmail = p.User?.Email ?? "",
            role = p.Role.ToString(),
            status = p.Status.ToString(),
            balance = p.Balance,
            balanceDelta = p.BalanceDelta,
        });
        return Ok(playerDtos);
    }

    #region SSE

    private readonly IRoomSSEService _roomSSEService = roomSSEService;

    /// <summary>
    /// Long‑lived SSE endpoint for clients to subscribe to room events.
    /// </summary>
    /// <param name="roomId"></param>
    /// <returns></returns>
    [AllowAnonymous]
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

    #endregion
}

// Request DTOs
public class PlayerActionRequest
{
    public string Action { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
}

public class JoinRoomRequest
{
    public Guid UserId { get; set; }
}

public class LeaveRoomRequest
{
    public Guid UserId { get; set; }
}
