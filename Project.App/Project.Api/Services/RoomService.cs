using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

public class RoomService(IRoomRepository roomRepository, IBlackjackService blackjackService) : IRoomService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IBlackjackService _blackjackService = blackjackService;

    public async Task<RoomDTO?> GetRoomByIdAsync(Guid id)
    {
        var room = await _roomRepository.GetByIdAsync(id);
        return room is null ? null : MapToResponseDto(room);
    }

    public async Task<IEnumerable<RoomDTO>> GetAllRoomsAsync()
    {
        var rooms = await _roomRepository.GetAllAsync();
        return rooms.Select(MapToResponseDto);
    }

    public async Task<IEnumerable<RoomDTO>> GetActiveRoomsAsync()
    {
        var rooms = await _roomRepository.GetActiveRoomsAsync();
        return rooms.Select(MapToResponseDto);
    }

    public async Task<IEnumerable<RoomDTO>> GetPublicRoomsAsync()
    {
        var rooms = await _roomRepository.GetPublicRoomsAsync();
        return rooms.Select(MapToResponseDto);
    }

    public async Task<RoomDTO?> GetRoomByHostIdAsync(Guid hostId)
    {
        var room = await _roomRepository.GetByHostIdAsync(hostId);
        return room is null ? null : MapToResponseDto(room);
    }

    public async Task<RoomDTO> CreateRoomAsync(CreateRoomDTO dto)
    {
        Validate(dto);

        var room = new Room
        {
            Id = Guid.NewGuid(),
            HostId = dto.HostId,
            IsPublic = dto.IsPublic,
            GameMode = dto.GameMode,
            GameState = dto.GameState,
            GameConfig = dto.GameConfig,
            Description = dto.Description,
            MaxPlayers = dto.MaxPlayers,
            MinPlayers = dto.MinPlayers,
            DeckId = dto.DeckId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var createdRoom = await _roomRepository.CreateAsync(room);
        return MapToResponseDto(createdRoom);
    }

    public async Task<RoomDTO?> UpdateRoomAsync(UpdateRoomDTO dto)
    {
        Validate(dto);

        var existingRoom = await _roomRepository.GetByIdAsync(dto.Id);
        if (existingRoom is null)
            return null;

        existingRoom.HostId = dto.HostId;
        existingRoom.IsPublic = dto.IsPublic;
        existingRoom.GameMode = dto.GameMode;
        existingRoom.GameState = dto.GameState;
        existingRoom.GameConfig = dto.GameConfig;
        existingRoom.Description = dto.Description;
        existingRoom.MaxPlayers = dto.MaxPlayers;
        existingRoom.MinPlayers = dto.MinPlayers;
        existingRoom.DeckId = dto.DeckId;

        var updatedRoom = await _roomRepository.UpdateAsync(existingRoom);
        return updatedRoom is null ? null : MapToResponseDto(updatedRoom);
    }

    public async Task<bool> DeleteRoomAsync(Guid id)
    {
        return await _roomRepository.DeleteAsync(id);
    }

    public async Task<bool> RoomExistsAsync(Guid id)
    {
        return await _roomRepository.ExistsAsync(id);
    }

    public async Task<string> GetGameStateAsync(Guid id)
    {
        return await _roomRepository.GetGameStateAsync(id);
    }

    public async Task<bool> UpdateGameStateAsync(Guid id, string gameState)
    {
        if (string.IsNullOrWhiteSpace(gameState))
            throw new ArgumentException("Game state cannot be empty.", nameof(gameState));

        return await _roomRepository.UpdateGameStateAsync(id, gameState);
    }

    public async Task<string> GetGameConfigAsync(Guid id)
    {
        return await _roomRepository.GetGameConfigAsync(id);
    }

    public async Task<bool> UpdateGameConfigAsync(Guid id, string gameConfig)
    {
        if (string.IsNullOrWhiteSpace(gameConfig))
            throw new ArgumentException("Game config cannot be empty.", nameof(gameConfig));

        return await _roomRepository.UpdateGameConfigAsync(id, gameConfig);
    }

    // --- game functionality ---

    // start game
    // includes specifying game mode and setting optional game config

    // do player action


    private static RoomDTO MapToResponseDto(Room room)
    {
        return new RoomDTO
        {
            Id = room.Id,
            HostId = room.HostId,
            IsPublic = room.IsPublic,
            GameMode = room.GameMode,
            GameState = room.GameState,
            GameConfig = room.GameConfig,
            Description = room.Description,
            MaxPlayers = room.MaxPlayers,
            MinPlayers = room.MinPlayers,
            DeckId = room.DeckId,
            CreatedAt = room.CreatedAt,
            IsActive = room.IsActive
        };
    }

    private static void Validate(CreateRoomDTO dto)
    {
        if (dto.MinPlayers < 1)
            throw new ArgumentException("Minimum players must be at least 1.", nameof(dto.MinPlayers));

        if (dto.MaxPlayers < dto.MinPlayers)
            throw new ArgumentException("Maximum players must be >= minimum players.", nameof(dto.MaxPlayers));

        if (string.IsNullOrWhiteSpace(dto.GameMode))
            throw new ArgumentException("Game mode is required.", nameof(dto.GameMode));

        if (string.IsNullOrWhiteSpace(dto.GameState))
            throw new ArgumentException("Game state is required.", nameof(dto.GameState));

        if (string.IsNullOrWhiteSpace(dto.DeckId))
            throw new ArgumentException("DeckId is required.", nameof(dto.DeckId));

        if (dto.Description?.Length > 500)
            throw new ArgumentException("Description can't be longer than 500 characters.", nameof(dto.Description));
    }
}
