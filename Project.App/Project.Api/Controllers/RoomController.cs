using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.DTOs;
using Project.Api.Services.Interface;
using Project.Api.Utilities;

namespace Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ILogger<RoomController> _logger;

    public RoomController(IRoomService roomService, ILogger<RoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    // GET: api/room
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetAllRooms()
    {
        try
        {
            var rooms = await _roomService.GetAllRoomsAsync();
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all rooms");
            return StatusCode(500, "An error occurred while retrieving rooms");
        }
    }

    // GET: api/room/active
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetActiveRooms()
    {
        try
        {
            var rooms = await _roomService.GetActiveRoomsAsync();
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active rooms");
            return StatusCode(500, "An error occurred while retrieving active rooms");
        }
    }

    // GET: api/room/public
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<RoomDTO>>> GetPublicRooms()
    {
        try
        {
            var rooms = await _roomService.GetPublicRoomsAsync();
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public rooms");
            return StatusCode(500, "An error occurred while retrieving public rooms");
        }
    }

    // GET: api/room/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDTO>> GetRoomById(Guid id)
    {
        try
        {
            var room = await _roomService.GetRoomByIdAsync(id);
            if (room == null)
                return NotFound($"Room with ID {id} not found");
            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving the room");
        }
    }

    // GET: api/room/host/{hostId}
    [HttpGet("host/{hostId}")]
    public async Task<ActionResult<RoomDTO>> GetRoomByHostId(Guid hostId)
    {
        try
        {
            var room = await _roomService.GetRoomByHostIdAsync(hostId);
            if (room == null)
                return NotFound($"Room with host ID {hostId} not found");
            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room for host {HostId}", hostId);
            return StatusCode(500, "An error occurred while retrieving the room");
        }
    }

    // GET: api/room/{id}/exists
    [HttpGet("{id}/exists")]
    public async Task<ActionResult<bool>> RoomExists(Guid id)
    {
        try
        {
            var exists = await _roomService.RoomExistsAsync(id);
            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if room {RoomId} exists", id);
            return StatusCode(500, "An error occurred while checking room existence");
        }
    }

    // GET: api/room/{id}/gamestate
    [HttpGet("{id}/gamestate")]
    public async Task<ActionResult<string>> GetGameState(Guid id)
    {
        try
        {
            var gameState = await _roomService.GetGameStateAsync(id);
            return Ok(gameState);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game state for room {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving game state");
        }
    }

    // GET: api/room/{id}/gameconfig
    [HttpGet("{id}/gameconfig")]
    public async Task<ActionResult<string>> GetGameConfig(Guid id)
    {
        try
        {
            var gameConfig = await _roomService.GetGameConfigAsync(id);
            return Ok(gameConfig);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game config for room {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving game config");
        }
    }

    // POST: api/room
    [HttpPost]
    public async Task<ActionResult<RoomDTO>> CreateRoom([FromBody] CreateRoomDTO dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.CreateRoomAsync(dto);
            return CreatedAtAction(nameof(GetRoomById), new { id = room.Id }, room);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, "An error occurred while creating the room");
        }
    }

    // PUT: api/room/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<RoomDTO>> UpdateRoom(Guid id, [FromBody] UpdateRoomDTO dto)
    {
        try
        {
            if (id != dto.Id)
                return BadRequest("Room ID mismatch");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.UpdateRoomAsync(dto);
            if (room == null)
                return NotFound($"Room with ID {id} not found");

            return Ok(room);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room {RoomId}", id);
            return StatusCode(500, "An error occurred while updating the room");
        }
    }

    // PUT: api/room/{id}/gamestate
    [HttpPut("{id}/gamestate")]
    public async Task<ActionResult> UpdateGameState(Guid id, [FromBody] string gameState)
    {
        try
        {
            var success = await _roomService.UpdateGameStateAsync(id, gameState);
            if (!success)
                return NotFound($"Room with ID {id} not found");

            return NoContent();
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game state for room {RoomId}", id);
            return StatusCode(500, "An error occurred while updating game state");
        }
    }

    // PUT: api/room/{id}/gameconfig
    [HttpPut("{id}/gameconfig")]
    public async Task<ActionResult> UpdateGameConfig(Guid id, [FromBody] string gameConfig)
    {
        try
        {
            var success = await _roomService.UpdateGameConfigAsync(id, gameConfig);
            if (!success)
                return NotFound($"Room with ID {id} not found");

            return NoContent();
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game config for room {RoomId}", id);
            return StatusCode(500, "An error occurred while updating game config");
        }
    }

    // DELETE: api/room/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRoom(Guid id)
    {
        try
        {
            var success = await _roomService.DeleteRoomAsync(id);
            if (!success)
                return NotFound($"Room with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room {RoomId}", id);
            return StatusCode(500, "An error occurred while deleting the room");
        }
    }

    // --- Game Management Endpoints ---

    // POST: api/room/{id}/start
    [HttpPost("{id}/start")]
    public async Task<ActionResult<RoomDTO>> StartGame(Guid id, [FromBody] string? gameConfigJson = null)
    {
        try
        {
            var room = await _roomService.StartGameAsync(id, gameConfigJson);
            return Ok(room);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InternalServerException ex)
        {
            _logger.LogError(ex, "Internal server error starting game for room {RoomId}", id);
            return StatusCode(500, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game for room {RoomId}", id);
            return StatusCode(500, "An error occurred while starting the game");
        }
    }

    // POST: api/room/{roomId}/player/{playerId}/action
    [HttpPost("{roomId}/player/{playerId}/action")]
    public async Task<ActionResult> PerformPlayerAction(
        Guid roomId,
        Guid playerId,
        [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _roomService.PerformPlayerActionAsync(roomId, playerId, request.Action, request.Data);
            return Ok(new { message = "Action performed successfully" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error performing action {Action} for player {PlayerId} in room {RoomId}",
                request.Action,
                playerId,
                roomId
            );
            return StatusCode(500, "An error occurred while performing the action");
        }
    }

    // --- Player Management Endpoints ---

    // POST: api/room/{roomId}/join
    [HttpPost("{roomId}/join")]
    public async Task<ActionResult<RoomDTO>> JoinRoom(Guid roomId, [FromBody] JoinRoomRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.JoinRoomAsync(roomId, request.UserId);
            return Ok(room);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room {RoomId} for user {UserId}", roomId, request.UserId);
            return StatusCode(500, "An error occurred while joining the room");
        }
    }

    // POST: api/room/{roomId}/leave
    [HttpPost("{roomId}/leave")]
    public async Task<ActionResult<RoomDTO>> LeaveRoom(Guid roomId, [FromBody] LeaveRoomRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.LeaveRoomAsync(roomId, request.UserId);
            return Ok(room);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room {RoomId} for user {UserId}", roomId, request.UserId);
            return StatusCode(500, "An error occurred while leaving the room");
        }
    }
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
