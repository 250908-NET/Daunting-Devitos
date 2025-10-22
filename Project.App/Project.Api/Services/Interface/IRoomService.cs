using System.Text.Json;
using Project.Api.DTOs;

namespace Project.Api.Services.Interface;

public interface IRoomService
{
    Task<RoomDTO?> GetRoomByIdAsync(Guid id);
    Task<IEnumerable<RoomDTO>> GetAllRoomsAsync();
    Task<IEnumerable<RoomDTO>> GetActiveRoomsAsync();
    Task<IEnumerable<RoomDTO>> GetPublicRoomsAsync();
    Task<RoomDTO?> GetRoomByHostIdAsync(Guid hostId);
    Task<RoomDTO> CreateRoomAsync(CreateRoomDTO dto);
    Task<RoomDTO?> UpdateRoomAsync(UpdateRoomDTO dto);
    Task<bool> DeleteRoomAsync(Guid id);
    Task<bool> RoomExistsAsync(Guid id);
    Task<string> GetGameStateAsync(Guid id);
    Task<bool> UpdateGameStateAsync(Guid id, string gameState);
    Task<string> GetGameConfigAsync(Guid id);
    Task<bool> UpdateGameConfigAsync(Guid id, string gameConfig);

    // Game functionality
    Task<RoomDTO> StartGameAsync(Guid roomId, string? gameConfigJson = null);
    Task PerformPlayerActionAsync(Guid roomId, Guid playerId, string action, JsonElement data);

    // Player management
    Task<RoomDTO> JoinRoomAsync(Guid roomId, Guid userId);
    Task<RoomDTO> LeaveRoomAsync(Guid roomId, Guid userId);
}
