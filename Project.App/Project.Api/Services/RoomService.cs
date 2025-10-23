using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Constants;
using Project.Api.Utilities.Enums;

namespace Project.Api.Services;

public class RoomService(
    IRoomRepository roomRepository,
    IRoomPlayerRepository roomPlayerRepository,
    IBlackjackService blackjackService,
    IDeckApiService deckApiService,
    IRoomSSEService roomSSEService,
    AppDbContext dbContext,
    ILogger<RoomService> logger
) : IRoomService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository;
    private readonly IBlackjackService _blackjackService = blackjackService;
    private readonly IDeckApiService _deckApiService = deckApiService;
    private readonly IRoomSSEService _roomSSEService = roomSSEService;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<RoomService> _logger = logger;

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

        // Auto-create deck if DeckId is not provided
        string deckId = dto.DeckId;
        if (string.IsNullOrWhiteSpace(deckId))
        {
            _logger.LogInformation("Creating new deck for room");
            deckId = await _deckApiService.CreateDeck(numOfDecks: 6, enableJokers: false);
            _logger.LogInformation("Created deck with ID: {DeckId}", deckId);
        }

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
            DeckId = deckId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var createdRoom = await _roomRepository.CreateAsync(room);

        // Automatically add the host as a player with Admin role
        var hostPlayer = new RoomPlayer
        {
            Id = Guid.NewGuid(),
            RoomId = createdRoom.Id,
            UserId = dto.HostId,
            Role = Role.Admin,
            Status = Status.Active,
            Balance = 0, // Will be set when game starts
        };

        await _roomPlayerRepository.CreateAsync(hostPlayer);
        _logger.LogInformation(
            "Host {UserId} automatically added to room {RoomId} as Admin",
            dto.HostId,
            createdRoom.Id
        );

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

        // Initialize game state based on game mode
        string initialGameState;
        string finalGameConfig = room.GameConfig;

        switch (room.GameMode.ToLower())
        {
            case GameModes.Blackjack:
                BlackjackConfig config;

                // Apply custom config if provided
                if (!string.IsNullOrWhiteSpace(gameConfigJson))
                {
                    config =
                        JsonSerializer.Deserialize<BlackjackConfig>(gameConfigJson)
                        ?? throw new BadRequestException("Invalid game configuration JSON.");
                    GameConfigValidator.ValidateBlackjackConfig(config);
                    finalGameConfig = gameConfigJson;
                    _logger.LogInformation(
                        "Using custom config for room {RoomId}: StartingBalance={Balance}, MinBet={MinBet}",
                        roomId,
                        config.StartingBalance,
                        config.MinBet
                    );
                }
                else if (!string.IsNullOrWhiteSpace(room.GameConfig))
                {
                    config =
                        JsonSerializer.Deserialize<BlackjackConfig>(room.GameConfig)
                        ?? throw new BadRequestException("Invalid existing game configuration.");
                    GameConfigValidator.ValidateBlackjackConfig(config);
                    _logger.LogInformation("Using existing config for room {RoomId}", roomId);
                }
                else
                {
                    // Use default config
                    config = new BlackjackConfig();
                    finalGameConfig = JsonSerializer.Serialize(config);
                    _logger.LogInformation("Using default config for room {RoomId}", roomId);
                }

                // Batch update all players in the room
                await _roomPlayerRepository.UpdatePlayersInRoomAsync(
                    roomId,
                    player =>
                    {
                        player.Balance = config.StartingBalance;
                        player.Status = Status.Away; // Players start as Away, become Active when they bet
                    }
                );

                _logger.LogInformation(
                    "Initialized {PlayerCount} players with starting balance {Balance} in room {RoomId}",
                    playerCount,
                    config.StartingBalance,
                    roomId
                );

                // Initialize game state with BlackjackBettingStage
                var blackjackState = new BlackjackState
                {
                    CurrentStage = new BlackjackBettingStage(
                        DateTimeOffset.UtcNow + config.BettingTimeLimit,
                        new Dictionary<Guid, long>()
                    ),
                    DealerHand = [],
                };

                // Serialize with options to include type discriminators for polymorphism
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
                };
                initialGameState = JsonSerializer.Serialize(blackjackState, jsonOptions);
                _logger.LogInformation("Serialized game state: {GameState}", initialGameState);

                // Create empty hands in the deck for each player and the dealer
                if (string.IsNullOrWhiteSpace(room.DeckId))
                {
                    throw new InternalServerException(
                        "Room DeckId is null or empty after creation."
                    );
                }

                _logger.LogInformation(
                    "Creating empty hands for players and dealer in deck {DeckId}",
                    room.DeckId
                );

                // Get all players in the room
                var players = await _roomPlayerRepository.GetByRoomIdAsync(roomId);

                // Create empty hand for each player (using their UserId as the hand identifier)
                foreach (var player in players)
                {
                    await _deckApiService.CreateEmptyHand(room.DeckId, player.UserId.ToString());
                    _logger.LogInformation(
                        "Created empty hand for player {PlayerId} in deck {DeckId}",
                        player.UserId,
                        room.DeckId
                    );
                }

                // Create empty hand for dealer
                await _deckApiService.CreateEmptyHand(room.DeckId, "dealer");
                _logger.LogInformation(
                    "Created empty hand for dealer in deck {DeckId}",
                    room.DeckId
                );

                break;

            default:
                _logger.LogError(
                    "Unsupported game mode '{GameMode}' for room {RoomId}",
                    room.GameMode,
                    roomId
                );
                throw new BadRequestException($"Unsupported game mode: {room.GameMode}");
        }

        // Update room with game state and config
        room.GameState = initialGameState;
        room.GameConfig = finalGameConfig;
        room.StartedAt = DateTime.UtcNow;
        room.IsActive = true;
        room.Round = 1;

        var updatedRoom =
            await _roomRepository.UpdateAsync(room)
            ?? throw new InternalServerException("Failed to update room.");

        _logger.LogInformation("Successfully started game for room {RoomId}", roomId);

        // Broadcast game started event to all players via SSE
        await _roomSSEService.BroadcastEventAsync(
            roomId,
            "room_updated",
            MapToResponseDto(updatedRoom)
        );
        _logger.LogInformation("Broadcasted game start for room {RoomId}", roomId);

        return MapToResponseDto(updatedRoom);
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
        switch (room.GameMode.ToLower())
        {
            case GameModes.Blackjack:
                await _blackjackService.PerformActionAsync(roomId, playerId, action, data);
                _logger.LogInformation(
                    "Successfully performed action '{Action}' for player {PlayerId} in room {RoomId}",
                    action,
                    playerId,
                    roomId
                );

                // Broadcast game state update via SSE
                var updatedRoom = await _roomRepository.GetByIdAsync(roomId);
                if (updatedRoom != null)
                {
                    await _roomSSEService.BroadcastEventAsync(
                        roomId,
                        "room_updated",
                        MapToResponseDto(updatedRoom)
                    );
                }
                break;

            default:
                _logger.LogError(
                    "Unsupported game mode '{GameMode}' for room {RoomId}",
                    room.GameMode,
                    roomId
                );
                throw new BadRequestException($"Unsupported game mode: {room.GameMode}");
        }
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

        // Check if player is already in the room
        bool isAlreadyInRoom = await _roomPlayerRepository.IsPlayerInRoomAsync(roomId, userId);
        if (isAlreadyInRoom)
        {
            _logger.LogWarning("User {UserId} is already in room {RoomId}", userId, roomId);
            throw new ConflictException($"Player {userId} is already in room {roomId}.");
        }

        // Check if room is full
        int currentPlayerCount = await _roomPlayerRepository.GetPlayerCountInRoomAsync(roomId);
        if (currentPlayerCount >= room.MaxPlayers)
        {
            _logger.LogWarning(
                "Room {RoomId} is full ({CurrentCount}/{MaxPlayers}). User {UserId} cannot join",
                roomId,
                currentPlayerCount,
                room.MaxPlayers,
                userId
            );
            throw new ConflictException("Room is full.");
        }

        // Create new RoomPlayer
        var roomPlayer = new RoomPlayer
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = userId,
            Role = Role.Player,
            Status = room.StartedAt == null ? Status.Active : Status.Away,
            Balance = 0, // Will be set when game starts
        };

        try
        {
            await _roomPlayerRepository.CreateAsync(roomPlayer);
            _logger.LogInformation(
                "User {UserId} successfully joined room {RoomId}",
                userId,
                roomId
            );
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("IX_RoomPlayer_RoomId_UserId_Unique") == true)
        {
            // Handle race condition where two requests tried to join simultaneously
            _logger.LogWarning(
                "Race condition detected: User {UserId} attempted duplicate join to room {RoomId}",
                userId,
                roomId
            );
            throw new ConflictException($"Player {userId} is already in room {roomId}.");
        }

        // Refetch the room to get updated RoomPlayers collection
        var updatedRoom =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        _logger.LogInformation(
            "Broadcasting join event for room {RoomId}. MaxPlayers: {MaxPlayers}, PlayerCount: {PlayerCount}",
            roomId,
            updatedRoom.MaxPlayers,
            updatedRoom.RoomPlayers.Count
        );

        // Broadcast player joined event via SSE
        var roomDto = MapToResponseDto(updatedRoom);
        _logger.LogInformation(
            "RoomDTO to broadcast - MaxPlayers: {MaxPlayers}, MinPlayers: {MinPlayers}",
            roomDto.MaxPlayers,
            roomDto.MinPlayers
        );
        await _roomSSEService.BroadcastEventAsync(roomId, "room_updated", roomDto);

        // Return the updated room
        return roomDto;
    }

    public async Task<RoomDTO> LeaveRoomAsync(Guid roomId, Guid userId)
    {
        _logger.LogInformation("User {UserId} attempting to leave room {RoomId}", userId, roomId);

        // Validate room exists
        var room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        // Get the player in the room
        var roomPlayer =
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(roomId, userId)
            ?? throw new NotFoundException($"Player {userId} is not in room {roomId}.");

        // Check if player is the host
        if (room.HostId == userId)
        {
            _logger.LogInformation(
                "Host {UserId} leaving room {RoomId}. Checking for other players...",
                userId,
                roomId
            );

            // Get all players in the room
            var allPlayers = await _roomPlayerRepository.GetByRoomIdAsync(roomId);
            var otherPlayers = allPlayers.Where(p => p.UserId != userId).ToList();

            // Host is leaving - check if there are other players
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                if (otherPlayers.Any())
                {
                    // Transfer host to the next player (first one we find)
                    var newHost = otherPlayers.First();
                    room.HostId = newHost.UserId;
                    await _roomRepository.UpdateAsync(room);

                    // Update the new host's role to Admin
                    newHost.Role = Role.Admin;
                    await _roomPlayerRepository.UpdateAsync(newHost);

                    // Remove the old host from players
                    await _roomPlayerRepository.DeleteAsync(roomPlayer.Id);

                    _logger.LogInformation(
                        "Transferred host of room {RoomId} from {OldHostId} to {NewHostId}",
                        roomId,
                        userId,
                        newHost.UserId
                    );
                }
                else
                {
                    // No other players - close the room
                    room.IsActive = false;
                    room.EndedAt = DateTime.UtcNow;
                    await _roomRepository.UpdateAsync(room);

                    // Remove the host (only player)
                    await _roomPlayerRepository.DeleteAsync(roomPlayer.Id);

                    _logger.LogInformation(
                        "Closed room {RoomId} as host {UserId} was the last player",
                        roomId,
                        userId
                    );
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Failed to handle host {UserId} leaving room {RoomId}",
                    roomId,
                    userId
                );
                throw;
            }
        }
        else
        {
            // Regular player leaving - remove them and check if room is now empty
            _logger.LogInformation("Player {UserId} leaving room {RoomId}", userId, roomId);
            await _roomPlayerRepository.DeleteAsync(roomPlayer.Id);

            // Check if room is now empty
            var remainingPlayers = await _roomPlayerRepository.GetByRoomIdAsync(roomId);
            if (!remainingPlayers.Any())
            {
                _logger.LogInformation("Room {RoomId} is now empty. Closing room.", roomId);
                room.IsActive = false;
                room.EndedAt = DateTime.UtcNow;
                await _roomRepository.UpdateAsync(room);
            }
        }

        // Refetch the room to get updated RoomPlayers collection
        var updatedRoom =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room with ID {roomId} not found.");

        _logger.LogInformation(
            "Broadcasting leave event for room {RoomId}. MaxPlayers: {MaxPlayers}, IsActive: {IsActive}",
            roomId,
            updatedRoom.MaxPlayers,
            updatedRoom.IsActive
        );

        // Broadcast player left event via SSE
        var roomDto = MapToResponseDto(updatedRoom);
        _logger.LogInformation(
            "RoomDTO to broadcast - MaxPlayers: {MaxPlayers}, IsActive: {IsActive}",
            roomDto.MaxPlayers,
            roomDto.IsActive
        );
        await _roomSSEService.BroadcastEventAsync(roomId, "room_updated", roomDto);

        // Return the updated room
        return roomDto;
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

        if (string.IsNullOrWhiteSpace(dto.GameState))
            throw new BadRequestException("Game state is required.");

        // DeckId is now optional - it will be auto-created if not provided

        if (dto.Description?.Length > 500)
            throw new BadRequestException("Description can't be longer than 500 characters.");
    }
}
