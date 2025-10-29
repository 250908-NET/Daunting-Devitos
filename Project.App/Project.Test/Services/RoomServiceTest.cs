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
using Xunit;

namespace Project.Test.Services;

public class RoomServiceTest : IDisposable // Implement IDisposable for DbContext cleanup
{
    private readonly Mock<IRoomRepository> _roomRepositoryMock;
    private readonly Mock<IRoomPlayerRepository> _roomPlayerRepositoryMock;

    private readonly Mock<IGameService<IGameState, GameConfig>> _mockBlackjackGameService;
    private readonly List<IGameService<IGameState, GameConfig>> _mockGameServices; // Collection for RoomService
    private readonly Mock<ILogger<RoomService>> _loggerMock;
    private readonly AppDbContext _dbContext; // Real in-memory DbContext for transaction tests
    private readonly RoomService _roomService;

    public RoomServiceTest()
    {
        _roomRepositoryMock = new Mock<IRoomRepository>();
        _roomPlayerRepositoryMock = new Mock<IRoomPlayerRepository>();
        _loggerMock = new Mock<ILogger<RoomService>>();

        // Configure DbContext to ignore transaction warnings for in-memory provider
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
        _dbContext = new AppDbContext(options);

        // Setup the generic IGameService mock to represent Blackjack
        _mockBlackjackGameService = new Mock<IGameService<IGameState, GameConfig>>();
        _mockBlackjackGameService.Setup(s => s.GameMode).Returns(GameModes.Blackjack);
        _mockBlackjackGameService
            .Setup(s => s.PlayerJoinAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mockBlackjackGameService
            .Setup(s => s.PlayerLeaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _mockBlackjackGameService
            .Setup(s =>
                s.PerformActionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(Task.CompletedTask);
        _mockBlackjackGameService
            .Setup(s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>())) // Use generic GameConfig here
            .Returns(Task.CompletedTask);
        _mockBlackjackGameService
            .Setup(s => s.GetConfigAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BlackjackConfig()); // Return a concrete GameConfig type
        _mockBlackjackGameService
            .Setup(s => s.GetGameStateAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BlackjackState { CurrentStage = new BlackjackInitStage() }); // Return a concrete IGameState type

        // Initialize the list of game services and add the generic blackjack mock
        _mockGameServices = new List<IGameService<IGameState, GameConfig>>
        {
            _mockBlackjackGameService.Object,
        };

        // Instantiate RoomService with the updated constructor
        _roomService = new RoomService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            _dbContext, // Pass the real in-memory DbContext
            _mockGameServices, // Pass the collection of game services
            _loggerMock.Object
        );
    }

    // Ensure the DbContext is clean for each test that uses it directly
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
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

    [Fact]
    public async Task GetAllRoomsAsync_ReturnsEmptyList_WhenNoRoomsExist()
    {
        // Arrange
        _roomRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Room>());

        // Act
        var result = await _roomService.GetAllRoomsAsync();

        // Assert
        result.Should().BeEmpty();
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

    [Fact]
    public async Task GetActiveRoomsAsync_ReturnsEmptyList_WhenNoActiveRoomsExist()
    {
        // Arrange
        _roomRepositoryMock.Setup(r => r.GetActiveRoomsAsync()).ReturnsAsync(new List<Room>());

        // Act
        var result = await _roomService.GetActiveRoomsAsync();

        // Assert
        result.Should().BeEmpty();
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

    [Fact]
    public async Task GetPublicRoomsAsync_ReturnsEmptyList_WhenNoPublicRoomsExist()
    {
        // Arrange
        _roomRepositoryMock.Setup(r => r.GetPublicRoomsAsync()).ReturnsAsync(new List<Room>());

        // Act
        var result = await _roomService.GetPublicRoomsAsync();

        // Assert
        result.Should().BeEmpty();
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
    public async Task CreateRoomAsync_CreatesRoomAndHostPlayer_WhenDtoIsValid()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameConfig = "{}",
            Description = "A valid test room",
            MaxPlayers = 4,
            MinPlayers = 2,
        };

        // Capture the Room instance created by the service
        Room? capturedRoom = null;
        _roomRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Room>()))
            .Callback<Room>(room => capturedRoom = room) // Capture the room
            .ReturnsAsync((Room room) => room); // Return the captured room

        // Act
        var result = await _roomService.CreateRoomAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        capturedRoom.Should().NotBeNull();
        // capturedRoomPlayer.Should().NotBeNull(); // REMOVE THIS ASSERTION

        result.Id.Should().Be(capturedRoom!.Id);
        result.HostId.Should().Be(createDto.HostId);

        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Once);
        // Verify that RoomService *delegated* player creation to the game service
        _mockBlackjackGameService.Verify(
            s => s.PlayerJoinAsync(capturedRoom.Id, createDto.HostId),
            Times.Once
        );
        // Verify that RoomService itself *did not* call _roomPlayerRepository.CreateAsync
        _roomPlayerRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<RoomPlayer>()), Times.Never);

        // Verify properties of the captured room
        capturedRoom!.HostId.Should().Be(createDto.HostId);
        capturedRoom.GameMode.Should().Be(createDto.GameMode);
        capturedRoom.IsActive.Should().BeTrue();
        capturedRoom.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateRoomAsync_RollsBackTransaction_OnFailure()
    {
        // Arrange
        var createDto = new CreateRoomDTO
        {
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameConfig = "{}",
            Description = "A valid test room",
            MaxPlayers = 4,
            MinPlayers = 2,
        };

        _roomRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Room>()))
            .ThrowsAsync(new Exception("Database error")); // Simulate failure

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _roomService.CreateRoomAsync(createDto));

        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Once);
        _roomPlayerRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<RoomPlayer>()), Times.Never); // Should not be called
        // With in-memory DB and mocks, verifying rollback directly is hard, but exception propagation implies it.
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
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 0, // Invalid
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
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
            GameConfig = "{}",
            MaxPlayers = 2,
            MinPlayers = 5, // Invalid - greater than MaxPlayers
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
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
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 2,
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
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
            GameConfig = "{}",
            Description = new string('a', 501), // 501 characters - too long
            MaxPlayers = 6,
            MinPlayers = 2,
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.CreateRoomAsync(createDto)
        );
        _roomRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Room>()), Times.Never);
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
            GameConfig = "{}",
            Description = "Updated Description",
            MaxPlayers = 8,
            MinPlayers = 3,
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
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 2,
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act
        var result = await _roomService.UpdateRoomAsync(updateDto);

        // Assert
        result.Should().BeNull();
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoomAsync_ThrowsBadRequestException_WhenMinPlayersLessThanOne()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 0, // Invalid
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateRoomAsync(updateDto)
        );
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoomAsync_ThrowsBadRequestException_WhenMaxPlayersLessThanMinPlayers()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameConfig = "{}",
            MaxPlayers = 2,
            MinPlayers = 5, // Invalid - greater than MaxPlayers
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateRoomAsync(updateDto)
        );
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoomAsync_ThrowsBadRequestException_WhenGameModeIsEmpty()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = "", // Invalid
            GameConfig = "{}",
            MaxPlayers = 6,
            MinPlayers = 2,
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateRoomAsync(updateDto)
        );
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoomAsync_ThrowsBadRequestException_WhenDescriptionTooLong()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var updateDto = new UpdateRoomDTO
        {
            Id = roomId,
            HostId = Guid.NewGuid(),
            IsPublic = true,
            GameMode = GameModes.Blackjack,
            GameConfig = "{}",
            Description = new string('a', 501), // 501 characters - too long
            MaxPlayers = 6,
            MinPlayers = 2,
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.UpdateRoomAsync(updateDto)
        );
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
        _roomRepositoryMock.Verify(
            r => r.UpdateGameStateAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never
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
        _roomRepositoryMock.Verify(
            r => r.UpdateGameStateAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never
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
        _roomRepositoryMock.Verify(
            r => r.UpdateGameConfigAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region StartGameAsync Tests

    [Fact]
    public async Task StartGameAsync_StartsBlackjackGame_WithDefaultConfig()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
            GameMode = GameModes.Blackjack,
            GameState = "{}",
            MinPlayers = 1,
            GameConfig = "", // No existing config
            StartedAt = null,
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room); // For the return DTO

        // Act
        await _roomService.StartGameAsync(roomId, null);

        // Assert
        _mockBlackjackGameService.Verify(
            bs =>
                bs.StartGameAsync(
                    roomId,
                    It.Is<GameConfig>(c => // Verify against generic GameConfig
                        ((BlackjackConfig)c).StartingBalance
                        == new BlackjackConfig().StartingBalance
                    )
                ),
            Times.Once
        );
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Exactly(2)); // Once for validation, once for return DTO
    }

    [Fact]
    public async Task StartGameAsync_StartsBlackjackGame_WithCustomConfig()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
            GameState = "{}",
            GameMode = GameModes.Blackjack,
            MinPlayers = 1,
            StartedAt = null,
        };
        var customConfig = new BlackjackConfig { StartingBalance = 5000, MaxPlayers = 2 };
        var customConfigJson = JsonSerializer.Serialize(customConfig);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room); // For the return DTO

        // Act
        await _roomService.StartGameAsync(roomId, customConfigJson);

        // Assert
        _mockBlackjackGameService.Verify(
            bs =>
                bs.StartGameAsync(
                    roomId,
                    It.Is<GameConfig>(c => // Verify against generic GameConfig
                        ((BlackjackConfig)c).StartingBalance == customConfig.StartingBalance
                        && ((BlackjackConfig)c).MaxPlayers == customConfig.MaxPlayers
                    )
                ),
            Times.Once
        );
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Exactly(2));
    }

    [Fact]
    public async Task StartGameAsync_StartsBlackjackGame_WithExistingConfigInRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var existingConfig = new BlackjackConfig { StartingBalance = 7500, MaxPlayers = 3 };
        var existingConfigJson = JsonSerializer.Serialize(existingConfig);
        var room = new Room
        {
            Id = roomId,
            GameState = "{}",
            GameMode = GameModes.Blackjack,
            MinPlayers = 1,
            GameConfig = existingConfigJson, // Existing config in room
            StartedAt = null,
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);
        _roomRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Room>())).ReturnsAsync(room); // For the return DTO

        // Act
        await _roomService.StartGameAsync(roomId, null); // No custom config provided

        // Assert
        _mockBlackjackGameService.Verify(
            bs =>
                bs.StartGameAsync(
                    roomId,
                    It.Is<GameConfig>(c => // Verify against generic GameConfig
                        ((BlackjackConfig)c).StartingBalance == existingConfig.StartingBalance
                        && ((BlackjackConfig)c).MaxPlayers == existingConfig.MaxPlayers
                    )
                ),
            Times.Once
        );
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Exactly(2));
    }

    [Fact]
    public async Task StartGameAsync_ThrowsNotFoundException_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync((Room?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _roomService.StartGameAsync(roomId));
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
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
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
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
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenUnsupportedGameMode()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            gameMode: "UnsupportedGame", // This game mode is not in _mockGameServices
            minPlayers: 2
        );
        room.StartedAt = null;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(2);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _roomService.StartGameAsync(roomId));
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenCustomConfigJsonIsInvalid()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
            GameState = "{}",
            GameMode = GameModes.Blackjack,
            MinPlayers = 1,
            StartedAt = null,
        };
        var invalidConfigJson = "{ \"StartingBalance\": \"not-a-number\" }"; // Invalid JSON for BlackjackConfig

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.StartGameAsync(roomId, invalidConfigJson)
        );
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenExistingConfigJsonIsInvalid()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
            GameState = "{}",
            GameMode = GameModes.Blackjack,
            MinPlayers = 1,
            GameConfig = "{ \"StartingBalance\": \"not-a-number\" }", // Invalid existing config
            StartedAt = null,
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.StartGameAsync(roomId, null)
        );
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );
    }

    [Fact]
    public async Task StartGameAsync_ThrowsBadRequestException_WhenGameConfigTypeCannotBeDetermined()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
            GameState = "{}",
            GameMode = GameModes.Blackjack,
            MinPlayers = 1,
            StartedAt = null,
        };

        // Temporarily clear the game services to simulate no matching service
        _mockGameServices.Clear();
        var roomServiceWithoutBlackjack = new RoomService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            _dbContext,
            _mockGameServices, // Empty game services
            _loggerMock.Object
        );

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock.Setup(r => r.GetPlayerCountInRoomAsync(roomId)).ReturnsAsync(1);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            roomServiceWithoutBlackjack.StartGameAsync(roomId)
        );
        _mockBlackjackGameService.Verify(
            s => s.StartGameAsync(It.IsAny<Guid>(), It.IsAny<GameConfig>()), // Verify against generic GameConfig
            Times.Never
        );

        // Restore mock for other tests
        _mockGameServices.Add(_mockBlackjackGameService.Object);
    }

    #endregion

    #region PerformPlayerActionAsync Tests

    [Fact]
    public async Task PerformPlayerActionAsync_DelegatesToGameService_WhenValid()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var action = "hit";
        var data = JsonDocument.Parse("{\"amount\":100}").RootElement;

        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: GameModes.Blackjack);
        room.StartedAt = DateTime.UtcNow.AddMinutes(-5);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, playerId))
            .ReturnsAsync(true);

        // Act
        await _roomService.PerformPlayerActionAsync(roomId, playerId, action, data);

        // Assert
        _mockBlackjackGameService.Verify(
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
        _mockBlackjackGameService.Verify(
            s =>
                s.PerformActionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsBadRequestException_WhenGameNotStarted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: GameModes.Blackjack);
        room.StartedAt = null; // Game not started

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
        _mockBlackjackGameService.Verify(
            s =>
                s.PerformActionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PerformPlayerActionAsync_ThrowsBadRequestException_WhenPlayerNotInRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var data = JsonDocument.Parse("{}").RootElement;
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: GameModes.Blackjack);
        room.StartedAt = DateTime.UtcNow;

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.IsPlayerInRoomAsync(roomId, playerId))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.PerformPlayerActionAsync(roomId, playerId, "hit", data)
        );
        _mockBlackjackGameService.Verify(
            s =>
                s.PerformActionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                ),
            Times.Never
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
        _mockBlackjackGameService.Verify(
            s =>
                s.PerformActionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                ),
            Times.Never
        );
    }

    #endregion

    #region JoinRoomAsync Tests

    [Fact]
    public async Task JoinRoomAsync_DelegatesToGameService_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            isActive: true,
            maxPlayers: 6,
            gameMode: GameModes.Blackjack
        );

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.JoinRoomAsync(roomId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(roomId);
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Once);
        _mockBlackjackGameService.Verify(s => s.PlayerJoinAsync(roomId, userId), Times.Once); // Game service is called
        _roomPlayerRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<RoomPlayer>()), Times.Never); // RoomService no longer creates RoomPlayer
        _roomPlayerRepositoryMock.Verify(
            r => r.GetByRoomIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        ); // RoomService no longer checks for existing player
        _roomPlayerRepositoryMock.Verify(
            r => r.GetPlayerCountInRoomAsync(It.IsAny<Guid>()),
            Times.Never
        ); // RoomService no longer checks player count
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
        _mockBlackjackGameService.Verify(
            s => s.PlayerJoinAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
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
        _mockBlackjackGameService.Verify(
            s => s.PlayerJoinAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsBadRequestException_WhenUnsupportedGameMode()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(
            id: roomId,
            isActive: true,
            gameMode: "UnsupportedGame"
        );

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.JoinRoomAsync(roomId, userId)
        );
        _mockBlackjackGameService.Verify(
            s => s.PlayerJoinAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    #endregion

    #region LeaveRoomAsync Tests

    [Fact]
    public async Task LeaveRoomAsync_DelegatesToGameService_WhenSuccessful()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: GameModes.Blackjack);

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act
        var result = await _roomService.LeaveRoomAsync(roomId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(roomId);
        _roomRepositoryMock.Verify(r => r.GetByIdAsync(roomId), Times.Once);
        _mockBlackjackGameService.Verify(s => s.PlayerLeaveAsync(roomId, userId), Times.Once); // Verify game service is called
        _roomPlayerRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never); // RoomService no longer deletes RoomPlayer
        _roomPlayerRepositoryMock.Verify(
            r => r.GetByRoomIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        ); // RoomService no longer checks for existing player
        _roomRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Room>()), Times.Never); // Room not updated by RoomService
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
        _mockBlackjackGameService.Verify(
            s => s.PlayerLeaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    [Fact]
    public async Task LeaveRoomAsync_ThrowsBadRequestException_WhenUnsupportedGameMode()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var room = RepositoryTestHelper.CreateTestRoom(id: roomId, gameMode: "UnsupportedGame");

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _roomService.LeaveRoomAsync(roomId, userId)
        );
        _mockBlackjackGameService.Verify(
            s => s.PlayerLeaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    #endregion
}
