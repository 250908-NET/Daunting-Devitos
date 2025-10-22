using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.Data;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Constants;
using Project.Api.Utilities.Enums;
using Project.Test.Helpers;

namespace Project.Test.Tests.Services;

public class RoomServiceTest
{
    private readonly Mock<IRoomRepository> _roomRepositoryMock;
    private readonly Mock<IRoomPlayerRepository> _roomPlayerRepositoryMock;
    private readonly Mock<IBlackjackService> _blackjackServiceMock;
    private readonly Mock<IDeckApiService> _deckApiServiceMock;
    private readonly Mock<ILogger<RoomService>> _loggerMock;
    private readonly AppDbContext _dbContext;
    private readonly RoomService _roomService;

    public RoomServiceTest()
    {
        _roomRepositoryMock = new Mock<IRoomRepository>();
        _roomPlayerRepositoryMock = new Mock<IRoomPlayerRepository>();
        _blackjackServiceMock = new Mock<IBlackjackService>();
        _deckApiServiceMock = new Mock<IDeckApiService>();
        _loggerMock = new Mock<ILogger<RoomService>>();
        _dbContext = RepositoryTestHelper.CreateInMemoryContext();

        _roomService = new RoomService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            _blackjackServiceMock.Object,
            _deckApiServiceMock.Object,
            _dbContext,
            _loggerMock.Object
        );
    }

    #region GetRoomByIdAsync Tests

    [Fact]
    public async Task GetRoomByIdAsync_ReturnsRoomDTO_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId);
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.GetRoomByIdAsync(roomId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(roomId);
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Once);
    }

    [Fact]
    public async Task GetRoomByIdAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act
        var result = await _roomService.GetRoomByIdAsync(roomId);

        // Assert
        result.Should().BeNull();
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Once);
    }

    #endregion

    #region GetAllRoomsAsync Tests

    [Fact]
    public async Task GetAllRoomsAsync_ReturnsAllRooms_WhenSuccessful()
    {
        // Arrange
        var rooms = new List<Room>
        {
            RepositoryTestHelper.CreateTestRoom(),
            RepositoryTestHelper.CreateTestRoom(),
            RepositoryTestHelper.CreateTestRoom(),
        };
        _roomRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);

        // Act
        var result = await _roomService.GetAllRoomsAsync();

        // Assert
        result.Should().HaveCount(3);
        _roomRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    #endregion

    #region GetActiveRoomsAsync Tests

    [Fact]
    public async Task GetActiveRoomsAsync_ReturnsActiveRooms_WhenSuccessful()
    {
        // Arrange
        var activeRooms = new List<Room>
        {
            RepositoryTestHelper.CreateTestRoom(isActive: true),
            RepositoryTestHelper.CreateTestRoom(isActive: true),
        };
        _roomRepositoryMock.Setup(r => r.GetActiveRoomsAsync()).ReturnsAsync(activeRooms);

        // Act
        var result = await _roomService.GetActiveRoomsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
        _roomRepositoryMock.Verify(r => r.GetActiveRoomsAsync(), Times.Once);
    }

    #endregion

    #region GetPublicRoomsAsync Tests

    [Fact]
    public async Task GetPublicRoomsAsync_ReturnsPublicRooms_WhenSuccessful()
    {
        // Arrange
        var publicRooms = new List<Room>
        {
            RepositoryTestHelper.CreateTestRoom(isPublic: true),
            RepositoryTestHelper.CreateTestRoom(isPublic: true),
        };
        _roomRepositoryMock.Setup(r => r.GetPublicRoomsAsync()).ReturnsAsync(publicRooms);

        // Act
        var result = await _roomService.GetPublicRoomsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.IsPublic.Should().BeTrue());
        _roomRepositoryMock.Verify(r => r.GetPublicRoomsAsync(), Times.Once);
    }

    #endregion

    #region GetRoomByHostIdAsync Tests

    [Fact]
    public async Task GetRoomByHostIdAsync_ReturnsRoomDTO_WhenRoomExists()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(hostId: hostId);
        _roomRepositoryMock.Setup(r => r.GetByHostIdAsync(hostId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.GetRoomByHostIdAsync(hostId);

        // Assert
        result.Should().NotBeNull();
        result!.HostId.Should().Be(hostId);
        _roomRepositoryMock.Verify(r => r.GetByHostIdAsync(hostId), Times.Once);
    }

    [Fact]
    public async Task GetRoomByHostIdAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByHostIdAsync(hostId)).ReturnsAsync((Room?)null);

        // Act
        var result = await _roomService.GetRoomByHostIdAsync(hostId);

        // Assert
        result.Should().BeNull();
        _roomRepositoryMock.Verify(r => r.GetByHostIdAsync(hostId), Times.Once);
    }

    #endregion

    #region CreateRoomAsync Tests

    [Fact]
    public async Task CreateRoomAsync_CreatesRoom_WithProvidedDeckId()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        var deckId = "test-deck-123";
        var createDto = new CreateRoomDTO
        {
            HostId = hostId,
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            Description = "Test Room",
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = deckId,
        };

        var createdRoom = RepositoryTestHelper.CreateTestRoom(
            hostId: hostId,
            gameMode: GameModes.Blackjack
        );
        _roomRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Room>())).ReturnsAsync(createdRoom);

        // Act
        var result = await _roomService.CreateRoomAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.HostId.Should().Be(hostId);
        _roomRepositoryMock.Verify(
            r => r.CreateAsync(It.Is<Room>(room => room.HostId == hostId && room.DeckId == deckId)),
            Times.Once
        );
        _deckApiServiceMock.Verify(
            d => d.CreateDeck(It.IsAny<int>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateRoomAsync_CreatesRoom_WithAutoDeckCreation_WhenDeckIdNotProvided()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        var autoDeckId = "auto-created-deck-456";
        var createDto = new CreateRoomDTO
        {
            HostId = hostId,
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            Description = "Test Room",
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = "", // Empty DeckId should trigger auto-creation
        };

        _deckApiServiceMock.Setup(d => d.CreateDeck(6, false)).ReturnsAsync(autoDeckId);

        var createdRoom = RepositoryTestHelper.CreateTestRoom(
            hostId: hostId,
            gameMode: GameModes.Blackjack
        );
        createdRoom.DeckId = autoDeckId;
        _roomRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Room>())).ReturnsAsync(createdRoom);

        // Act
        var result = await _roomService.CreateRoomAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.HostId.Should().Be(hostId);
        _deckApiServiceMock.Verify(d => d.CreateDeck(6, false), Times.Once);
        _roomRepositoryMock.Verify(
            r => r.CreateAsync(It.Is<Room>(room => room.DeckId == autoDeckId)),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsBadRequestException_WhenMinPlayersLessThanOne()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 0, // Invalid
            DeckId = "test-deck",
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsBadRequestException_WhenMaxPlayersLessThanMinPlayers()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            MaxPlayers = 2,
            MinPlayers = 5, // Invalid - greater than MaxPlayers
            DeckId = "test-deck",
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsBadRequestException_WhenGameModeIsEmpty()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = "", // Invalid
            GameState = "{}",
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = "test-deck",
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsBadRequestException_WhenDescriptionTooLong()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            Description = new string('a', 501), // 501 characters - too long
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = "test-deck",
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
    }

    #endregion

    #region UpdateRoomAsync Tests

    [Fact]
    public async Task UpdateRoomAsync_UpdatesRoom_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var existingRoom = RepositoryTestHelper.CreateTestRoom(id: roomId);
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = existingRoom.HostId,
            IsPublic = false, // Changed
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            Description = "Updated Description",
            MaxPlayers = 8,
            MinPlayers = 3,
            DeckId = "test-deck",
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(existingRoom);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(existingRoom);

        // Act
        var result = await _roomService.UpdateRoomAsync(updateDto);

        // Assert
        result.Should().NotBeNull();
        _roomRepositoryMock.Verify(
            r =>
                r.UpdateAsync(
                    It.Is<Room>(room =>
                        room.Id == roomId
                        && room.IsPublic == false
                        && room.Description == "Updated Description"
                        && room.MaxPlayers == 8
                        && room.MinPlayers == 3
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateRoomAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 2,
            DeckId = "test-deck",
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act
        var result = await _roomService.UpdateRoomAsync(updateDto);

        // Assert
        result.Should().BeNull();
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    #endregion

    #region DeleteRoomAsync Tests

    [Fact]
    public async Task DeleteRoomAsync_ReturnsTrue_WhenRoomIsDeleted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.DeleteAsync(roomId)).ReturnsAsync(true);

        // Act
        var result = await _roomService.DeleteRoomAsync(roomId);

        // Assert
        result.Should().BeTrue();
        _roomRepositoryMock.Verify(r => r.DeleteAsync(roomId), Times.Once);
    }

    [Fact]
    public async Task DeleteRoomAsync_ReturnsFalse_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.DeleteAsync(roomId)).ReturnsAsync(false);

        // Act
        var result = await _roomService.DeleteRoomAsync(roomId);

        // Assert
        result.Should().BeFalse();
        _roomRepositoryMock.Verify(r => r.DeleteAsync(roomId), Times.Once);
    }

    #endregion

    #region RoomExistsAsync Tests

    [Fact]
    public async Task RoomExistsAsync_ReturnsTrue_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.ExistsAsync(roomId)).ReturnsAsync(true);

        // Act
        var result = await _roomService.RoomExistsAsync(roomId);

        // Assert
        result.Should().BeTrue();
        _roomRepositoryMock.Verify(r => r.ExistsAsync(roomId), Times.Once);
    }

    [Fact]
    public async Task RoomExistsAsync_ReturnsFalse_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.ExistsAsync(roomId)).ReturnsAsync(false);

        // Act
        var result = await _roomService.RoomExistsAsync(roomId);

        // Assert
        result.Should().BeFalse();
        _roomRepositoryMock.Verify(r => r.ExistsAsync(roomId), Times.Once);
    }

    #endregion

    #region GetGameStateAsync Tests

    [Fact]
    public async Task GetGameStateAsync_ReturnsGameState_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameState = "{\"currentStage\":\"betting\"}";
        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameState);

        // Act
        var result = await _roomService.GetGameStateAsync(roomId);

        // Assert
        result.Should().Be(gameState);
        _roomRepositoryMock.Verify(r => r.GetGameStateAsync(roomId), Times.Once);
    }

    #endregion

    #region UpdateGameStateAsync Tests

    [Fact]
    public async Task UpdateGameStateAsync_UpdatesGameState_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameState = "{\"currentStage\":\"playing\"}";
        _roomRepositoryMock
            .Setup(r => r.UpdateGameStateAsync(roomId, gameState))
            .ReturnsAsync(true);

        // Act
        var result = await _roomService.UpdateGameStateAsync(roomId, gameState);

        // Assert
        result.Should().BeTrue();
        _roomRepositoryMock.Verify(r => r.UpdateGameStateAsync(roomId, gameState), Times.Once);
    }

    [Fact]
    public async Task UpdateGameStateAsync_ThrowsBadRequestException_WhenGameStateIsEmpty()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateGameStateAsync(roomId, "")
        );
    }

    [Fact]
    public async Task UpdateGameStateAsync_ThrowsBadRequestException_WhenGameStateIsWhitespace()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateGameStateAsync(roomId, "   ")
        );
    }

    #endregion

    #region GetGameConfigAsync Tests

    [Fact]
    public async Task GetGameConfigAsync_ReturnsGameConfig_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameConfig = "{\"startingBalance\":1000}";
        _roomRepositoryMock.Setup(r => r.GetGameConfigAsync(roomId)).ReturnsAsync(gameConfig);

        // Act
        var result = await _roomService.GetGameConfigAsync(roomId);

        // Assert
        result.Should().Be(gameConfig);
        _roomRepositoryMock.Verify(r => r.GetGameConfigAsync(roomId), Times.Once);
    }

    #endregion

    #region UpdateGameConfigAsync Tests

    [Fact]
    public async Task UpdateGameConfigAsync_UpdatesGameConfig_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameConfig = "{\"startingBalance\":2000}";
        _roomRepositoryMock
            .Setup(r => r.UpdateGameConfigAsync(roomId, gameConfig))
            .ReturnsAsync(true);

        // Act
        var result = await _roomService.UpdateGameConfigAsync(roomId, gameConfig);

        // Assert
        result.Should().BeTrue();
        _roomRepositoryMock.Verify(r => r.UpdateGameConfigAsync(roomId, gameConfig), Times.Once);
    }

    [Fact]
    public async Task UpdateGameConfigAsync_ThrowsBadRequestException_WhenGameConfigIsEmpty()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateGameConfigAsync(roomId, "")
        );
    }

    #endregion

    #region StartGameAsync Tests

    [Fact]
    public async Task StartGameAsync_StartsBlackjackGame_WithDefaultConfig()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            gameMode: GameModes.Blackjack,
            minPlayers: 2,
            maxPlayers: 6
        );
        room.StartedAt = null; // Game not started yet
        room.DeckId = "test-deck-123";

        var players = new List<RoomPlayer>
        {
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: Guid.NewGuid()),
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: Guid.NewGuid()),
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);
        _roomPlayerRepositoryMock.Setup(r => r.GetByRoomIdAsync(roomId)).ReturnsAsync(players);
        _roomPlayerRepositoryMock
            .Setup(r => r.UpdatePlayersInRoomAsync(roomId, It.IsAny<Action<RoomPlayer>>()))
            .Returns(Task.CompletedTask);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room);
        _deckApiServiceMock
            .Setup(d => d.CreateEmptyHand(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _roomService.StartGameAsync(roomId);

        // Assert
        result.Should().NotBeNull();
        _roomRepositoryMock.Verify(
            r =>
                r.UpdateAsync(
                    It.Is<Room>(r => r.StartedAt != null && r.Round == 1 && r.IsActive == true)
                ),
            Times.Once
        );
        _roomPlayerRepositoryMock.Verify(
            r => r.UpdatePlayersInRoomAsync(roomId, It.IsAny<Action<RoomPlayer>>()),
            Times.Once
        );
        _deckApiServiceMock.Verify(
            d => d.CreateEmptyHand("test-deck-123", It.IsAny<string>()),
            Times.Exactly(3) // 2 players + 1 dealer
        );
        _deckApiServiceMock.Verify(d => d.CreateEmptyHand("test-deck-123", "dealer"), Times.Once);
    }

    [Fact]
    public async Task StartGameAsync_StartsBlackjackGame_WithCustomConfig()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            gameMode: GameModes.Blackjack,
            minPlayers: 2,
            maxPlayers: 6
        );
        room.StartedAt = null;
        room.DeckId = "test-deck-123";

        var customConfig = new BlackjackConfig
        {
            StartingBalance = 5000,
            MinBet = 100,
            BettingTimeLimit = TimeSpan.FromSeconds(120),
        };
        var customConfigJson = JsonSerializer.Serialize(customConfig);

        var players = new List<RoomPlayer>
        {
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: Guid.NewGuid()),
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: Guid.NewGuid()),
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);
        _roomPlayerRepositoryMock.Setup(r => r.GetByRoomIdAsync(roomId)).ReturnsAsync(players);
        _roomPlayerRepositoryMock
            .Setup(r => r.UpdatePlayersInRoomAsync(roomId, It.IsAny<Action<RoomPlayer>>()))
            .Returns(Task.CompletedTask);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room);
        _deckApiServiceMock
            .Setup(d => d.CreateEmptyHand(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _roomService.StartGameAsync(roomId, customConfigJson);

        // Assert
        result.Should().NotBeNull();
        _roomRepositoryMock.Verify(
            r =>
                r.UpdateAsync(
                    It.Is<Room>(r =>
                        r.StartedAt != null && r.Round == 1 && r.GameConfig == customConfigJson
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task StartGameAsync_ThrowsNotFoundException_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _roomService.StartGameAsync(roomId));
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenGameAlreadyStarted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId);
        room.StartedAt = DateTime.UtcNow.AddMinutes(-10); // Game already started

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _roomService.StartGameAsync(roomId));
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenNotEnoughPlayers()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, minPlayers: 3, maxPlayers: 6);
        room.StartedAt = null;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2); // Only 2 players, need 3

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.StartGameAsync(roomId)
        );
        exception.Message.Should().Contain("Minimum 3 players required");
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenUnsupportedGameMode()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            gameMode: "UnsupportedGame",
            minPlayers: 2
        );
        room.StartedAt = null;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _roomService.StartGameAsync(roomId));
    }

    [Fact]
    public async Task StartGameAsync_ThrowsInternalServerException_WhenDeckIdIsNull()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            gameMode: GameModes.Blackjack,
            minPlayers: 2
        );
        room.StartedAt = null;
        room.DeckId = null; // Null DeckId should cause error

        var players = new List<RoomPlayer>
        {
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId),
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);
        _roomPlayerRepositoryMock.Setup(r => r.GetByRoomIdAsync(roomId)).ReturnsAsync(players);
        _roomPlayerRepositoryMock
            .Setup(r => r.UpdatePlayersInRoomAsync(roomId, It.IsAny<Action<RoomPlayer>>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _roomService.StartGameAsync(roomId)
        );
    }

    #endregion

    #region JoinRoomAsync Tests

    [Fact]
    public async Task JoinRoomAsync_AddsPlayerToRoom_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: true, maxPlayers: 6);
        room.StartedAt = null; // Game not started

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, userId))
            .ReturnsAsync(false);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);
        _roomPlayerRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<RoomPlayer>()))
            .ReturnsAsync(
                RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: userId)
            );

        // Act
        var result = await _roomService.JoinRoomAsync(roomId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(roomId);
        _roomPlayerRepositoryMock.Verify(
            r =>
                r.CreateAsync(
                    It.Is<RoomPlayer>(rp =>
                        rp.RoomId == roomId
                        && rp.UserId == userId
                        && rp.Role == Role.Player
                        && rp.Status == Status.Active
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task JoinRoomAsync_SetsStatusToAway_WhenGameAlreadyStarted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: true, maxPlayers: 6);
        room.StartedAt = DateTime.UtcNow.AddMinutes(-5); // Game already started

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, userId))
            .ReturnsAsync(false);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);
        _roomPlayerRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<RoomPlayer>()))
            .ReturnsAsync(
                RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId, userId: userId)
            );

        // Act
        var result = await _roomService.JoinRoomAsync(roomId, userId);

        // Assert
        result.Should().NotBeNull();
        _roomPlayerRepositoryMock.Verify(
            r => r.CreateAsync(It.Is<RoomPlayer>(rp => rp.Status == Status.Away)),
            Times.Once
        );
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsNotFoundException_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsBadRequestException_WhenRoomIsNotActive()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: false);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsConflictException_WhenPlayerAlreadyInRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: true);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, userId))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsConflictException_WhenRoomIsFull()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: true, maxPlayers: 4);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, userId))
            .ReturnsAsync(false);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(4); // Room is full

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
        exception.Message.Should().Contain("Room is full");
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsConflictException_OnRaceCondition()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, isActive: true);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, userId))
            .ReturnsAsync(false);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);

        // Simulate race condition with duplicate key exception
        var innerException = new Exception("IX_RoomPlayer_RoomId_UserId_Unique");
        var dbException = new DbUpdateException("Duplicate entry", innerException);
        _roomPlayerRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<RoomPlayer>()))
            .ThrowsAsync(dbException);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
    }

    #endregion

    #region LeaveRoomAsync Tests

    [Fact]
    public async Task LeaveRoomAsync_RemovesPlayer_WhenPlayerIsNotHost()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, hostId: hostId);
        var roomPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: roomId,
            userId: playerId
        );

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, playerId))
            .ReturnsAsync(roomPlayer);
        _roomPlayerRepositoryMock.Setup(r => r.DeleteAsync(roomPlayer.Id)).ReturnsAsync(true);

        // Act
        var result = await _roomService.LeaveRoomAsync(roomId, playerId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(roomId);
        _roomPlayerRepositoryMock.Verify(r => r.DeleteAsync(roomPlayer.Id), Times.Once);
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task LeaveRoomAsync_ClosesRoom_WhenHostLeaves()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        // Create a new in-memory database context that suppresses transaction warnings
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
                w.Ignore(
                    Microsoft
                        .EntityFrameworkCore
                        .Diagnostics
                        .InMemoryEventId
                        .TransactionIgnoredWarning
                )
            )
            .Options;
        var dbContext = new AppDbContext(options);

        var roomService = new RoomService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            _blackjackServiceMock.Object,
            _deckApiServiceMock.Object,
            dbContext,
            _loggerMock.Object
        );

        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, hostId: hostId);
        room.IsActive = true;

        var hostPlayer = RepositoryTestHelper.CreateTestRoomPlayer(
            roomId: roomId,
            userId: hostId,
            role: Role.Admin
        );

        var otherPlayers = new List<RoomPlayer>
        {
            hostPlayer,
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId),
            RepositoryTestHelper.CreateTestRoomPlayer(roomId: roomId),
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, hostId))
            .ReturnsAsync(hostPlayer);
        _roomPlayerRepositoryMock.Setup(r => r.GetByRoomIdAsync(roomId)).ReturnsAsync(otherPlayers);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        // Act
        var result = await roomService.LeaveRoomAsync(roomId, hostId);

        // Assert
        result.Should().NotBeNull();
        _roomRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<Room>(r => r.IsActive == false && r.EndedAt != null)),
            Times.Once
        );
        _roomPlayerRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>()),
            Times.Exactly(3) // All 3 players removed
        );
    }

    [Fact]
    public async Task LeaveRoomAsync_ThrowsNotFoundException_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _roomService.LeaveRoomAsync(roomId, userId)
        );
    }

    [Fact]
    public async Task LeaveRoomAsync_ThrowsNotFoundException_WhenPlayerNotInRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, userId))
            .ReturnsAsync((RoomPlayer?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _roomService.LeaveRoomAsync(roomId, userId)
        );
    }

    #endregion

    #region PerformPlayerActionAsync Tests

    [Fact]
    public async Task PerformPlayerActionAsync_DelegatesToBlackjackService_WhenGameModeIsBlackjack()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var action = "hit";
        var data = JsonDocument.Parse("{}").RootElement;

        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: GameModes.Blackjack);
        room.StartedAt = DateTime.UtcNow.AddMinutes(-5);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, playerId))
            .ReturnsAsync(true);
        _blackjackServiceMock
            .Setup(s => s.PerformActionAsync(roomId, playerId, action, data))
            .Returns(Task.CompletedTask);

        // Act
        await _roomService.PerformPlayerActionAsync(roomId, playerId, action, data);

        // Assert
        _blackjackServiceMock.Verify(
            s => s.PerformActionAsync(roomId, playerId, action, data),
            Times.Once
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsNotFoundException_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsBadRequestException_WhenGameNotStarted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId);
        room.StartedAt = null; // Game not started

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsBadRequestException_WhenPlayerNotInRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId);
        room.StartedAt = DateTime.UtcNow;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, playerId))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsBadRequestException_WhenUnsupportedGameMode()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: "UnsupportedGame");
        room.StartedAt = DateTime.UtcNow;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, playerId))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
    }

    #endregion
}
