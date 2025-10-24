using System.Text.Json;
using Project.Api.Data;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Constants;

namespace Project.Api.Services;

public class RoomService(
    IRoomRepository roomRepository,
    IRoomPlayerRepository roomPlayerRepository,
    AppDbContext dbContext,
    IEnumerable<IGameService<IGameState, GameConfig>> gameServices,
    ILogger<RoomService> logger
) : IRoomService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly Dictionary<string, IGameService<IGameState, GameConfig>> _gameServices =
        gameServices.ToDictionary(s => s.GameMode.ToLowerInvariant(), s => s); // Initialize dictionary
    private readonly ILogger<RoomService> _logger = logger;

    // Helper method to get the correct game service
    private IGameService<IGameState, GameConfig> GetGameService(string gameMode)
    {
        if (!_gameServices.TryGetValue(gameMode.ToLowerInvariant(), out var service))
        {
            throw new BadRequestException($"Unsupported game mode: {gameMode}");
        }
        return service;
    }

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
        _logger.LogInformation("Creating new room...");

        Validate(dto);

        Room room = new()
        {
            Id = Guid.CreateVersion7(),
            HostId = dto.HostId,
            IsPublic = dto.IsPublic,
            GameMode = dto.GameMode,
            GameState = "{}", // empty initial state
            GameConfig = dto.GameConfig,
            Description = dto.Description,
            MaxPlayers = dto.MaxPlayers,
            MinPlayers = dto.MinPlayers,
            CreatedAt = DateTime.UtcNow,
            DeckId = "", // no deck yet
            IsActive = true,
        };

        // Get the game service for the room's game mode
        var gameService = GetGameService(room.GameMode);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            Room createdRoom = await _roomRepository.CreateAsync(room); // create room

            // Delegate host player creation to the game service
            await gameService.PlayerJoinAsync(createdRoom.Id, dto.HostId);

            await transaction.CommitAsync();

            _logger.LogInformation("Successfully created room with ID: {RoomId}", createdRoom.Id);
            return MapToResponseDto(createdRoom);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create room for host {HostId}", dto.HostId);
            throw;
        }
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
        existingRoom.GameConfig = dto.GameConfig;
        existingRoom.Description = dto.Description;
        existingRoom.MaxPlayers = dto.MaxPlayers;
        existingRoom.MinPlayers = dto.MinPlayers;

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
            throw new BadRequestException("Game state cannot be empty.");

        _logger.LogInformation("Updating game state for room {RoomId}", id);
        return await _roomRepository.UpdateGameStateAsync(id, gameState);
    }

    public async Task<string> GetGameConfigAsync(Guid id)
    {
        _logger.LogInformation("Getting game config for room {RoomId}", id);
        return await _roomRepository.GetGameConfigAsync(id);
    }

    public async Task<bool> UpdateGameConfigAsync(Guid id, string gameConfig)
    {
        if (string.IsNullOrWhiteSpace(gameConfig))
            throw new BadRequestException("Game config cannot be empty.");

        _logger.LogInformation("Updating game config for room {RoomId}", id);
        return await _roomRepository.UpdateGameConfigAsync(id, gameConfig);
    }

    // --- game functionality ---

    public async Task<RoomDTO> StartGameAsync(Guid roomId, string? gameConfigJson = null)
    {
        _logger.LogInformation("Starting game for room {RoomId}", roomId);

        // Get the room
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        // Validate room is not already started
        if (room.StartedAt != null)
        {
            _logger.LogWarning("Attempted to start already-started game in room {RoomId}", roomId);
            throw new BadRequestException("Game has already been started.");
        }

        // Get player count
        int playerCount = await _roomPlayerRepository.GetPlayerCountInRoomAsync(roomId);

        // Validate minimum players
        if (playerCount < room.MinPlayers)
        {
            _logger.LogWarning(
                "Cannot start game in room {RoomId}. Need {MinPlayers} players, have {PlayerCount}",
                roomId,
                room.MinPlayers,
                playerCount
            );
            throw new BadRequestException(
                $"Cannot start game. Minimum {room.MinPlayers} players required, but only {playerCount} present."
            );
        }

        // get game service
        var gameService = GetGameService(room.GameMode);

        // determine config based on game mode using a switch statement
        GameConfig config;
        try
        {
            switch (room.GameMode)
            {
                case GameModes.Blackjack:
                    if (!string.IsNullOrWhiteSpace(gameConfigJson))
                    {
                        // Deserialize to specific BlackjackConfig type
                        config =
                            JsonSerializer.Deserialize<BlackjackConfig>(gameConfigJson)
                            ?? throw new BadRequestException(
                                "Invalid Blackjack game configuration JSON."
                            );
                        _logger.LogInformation(
                            "Using custom Blackjack config for room {RoomId}",
                            roomId
                        );
                    }
                    else if (!string.IsNullOrWhiteSpace(room.GameConfig))
                    {
                        // Use existing config for Blackjack
                        config =
                            JsonSerializer.Deserialize<BlackjackConfig>(room.GameConfig)
                            ?? throw new BadRequestException(
                                "Invalid existing Blackjack game configuration."
                            );
                        _logger.LogInformation(
                            "Using existing Blackjack config for room {RoomId}",
                            roomId
                        );
                    }
                    else
                    {
                        // Use default Blackjack config
                        config = new BlackjackConfig();
                        _logger.LogInformation(
                            "Using default Blackjack config for room {RoomId}",
                            roomId
                        );
                    }
                    break;
                // Add cases for other game modes here if they have specific GameConfig types
                // case GameModes.Poker:
                //     // ... handle PokerConfig ...
                //     break;
                default:
                    // Fallback for unsupported game modes or if a specific config type isn't handled
                    // This should ideally be caught by GetGameService, but as a safeguard:
                    throw new BadRequestException(
                        $"Unsupported game mode for configuration: {room.GameMode}"
                    );
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize game configuration for room {RoomId}",
                roomId
            );
            throw new BadRequestException("Invalid game configuration format.");
        }

        // start the game!
        await gameService.StartGameAsync(roomId, config); // delegate setup to the generic game service

        _logger.LogInformation("Successfully started game for room {RoomId}", roomId);
        return MapToResponseDto(
            await _roomRepository.GetByIdAsync(roomId)
                ?? throw new NotFoundException($"Room with ID {roomId} not found.")
        );
    }

    public async Task PerformPlayerActionAsync(
        Guid roomId,
        Guid playerId,
        string action,
        JsonElement data
    )
    {
        _logger.LogInformation(
            "Player {PlayerId} performing action '{Action}' in room {RoomId}",
            playerId,
            action,
            roomId
        );

        // Validate room exists
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        // Validate game has started
        if (room.StartedAt == null)
        {
            _logger.LogWarning(
                "Player {PlayerId} attempted action in room {RoomId} before game started",
                playerId,
                roomId
            );
            throw new BadRequestException("Game has not been started yet.");
        }

        // Validate player is in the room
        bool isPlayerInRoom = await _roomPlayerRepository.IsPlayerInRoomAsync(roomId, playerId);
        if (!isPlayerInRoom)
        {
            _logger.LogWarning(
                "Player {PlayerId} not in room {RoomId} attempted action",
                playerId,
                roomId
            );
            throw new BadRequestException($"Player {playerId} is not in room {roomId}.");
        }

        // Delegate to the appropriate game service based on game mode
        var gameService = GetGameService(room.GameMode);
        await gameService.PerformActionAsync(roomId, playerId, action, data);
        _logger.LogInformation(
            "Successfully performed action '{Action}' for player {PlayerId} in room {RoomId}",
            action,
            playerId,
            roomId
        );
    }

    // --- player management ---

    public async Task<RoomDTO> JoinRoomAsync(Guid roomId, Guid userId)
    {
        _logger.LogInformation("User {UserId} attempting to join room {RoomId}", userId, roomId);

        // Validate room exists
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        // Validate room is active
        if (!room.IsActive)
        {
            _logger.LogWarning(
                "User {UserId} attempted to join inactive room {RoomId}",
                userId,
                roomId
            );
            throw new BadRequestException("Room is not active.");
        }

        // delegate to the appropriate game service
        // should include setting up the player, adding them to the room, etc.
        var gameService = GetGameService(room.GameMode);
        await gameService.PlayerJoinAsync(roomId, userId);

        // Return the room (no need to fetch again, we have it)
        return MapToResponseDto(room);
    }

    public async Task<RoomDTO> LeaveRoomAsync(Guid roomId, Guid userId)
    {
        _logger.LogInformation("User {UserId} attempting to leave room {RoomId}", userId, roomId);

        // Validate room exists
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        // delegate to the appropriate game service
        // should include removing the player from the room, selecting new host if necessary, etc.
        var gameService = GetGameService(room.GameMode);
        await gameService.PlayerLeaveAsync(roomId, userId);

        // Return the room (we already have it)
        return MapToResponseDto(room);
    }

    // TODO: replace with automapper implementation
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
            DeckId = room.DeckId ?? string.Empty,
            CreatedAt = room.CreatedAt,
            IsActive = room.IsActive,
        };
    }

    // TODO: replace with FLuent API validator
    private static void Validate(CreateRoomDTO dto)
    {
        if (dto.MinPlayers < 1)
            throw new BadRequestException("Minimum players must be at least 1.");

        if (dto.MaxPlayers < dto.MinPlayers)
            throw new BadRequestException("Maximum players must be >= minimum players.");

        if (string.IsNullOrWhiteSpace(dto.GameMode))
            throw new BadRequestException("Game mode is required.");

        // DeckId is now optional - it will be auto-created if not provided

        if (dto.Description?.Length > 500)
            throw new BadRequestException("Description can't be longer than 500 characters.");
    }
}
