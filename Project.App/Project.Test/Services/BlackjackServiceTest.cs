using System.Text.Json;
using Moq;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Enums;

namespace Project.Test.Tests.Services;

public class BlackjackServiceTest
{
    private readonly Mock<IRoomRepository> _roomRepositoryMock;
    private readonly Mock<IRoomPlayerRepository> _roomPlayerRepositoryMock;
    private readonly Mock<IHandRepository> _handRepositoryMock;
    private readonly Mock<IDeckApiService> _deckApiServiceMock;
    private readonly BlackjackService _blackjackService;

    public BlackjackServiceTest()
    {
        _roomRepositoryMock = new Mock<IRoomRepository>();
        _roomPlayerRepositoryMock = new Mock<IRoomPlayerRepository>();
        _handRepositoryMock = new Mock<IHandRepository>();
        _deckApiServiceMock = new Mock<IDeckApiService>();
        // Mocking IUserRepository is not needed for the betting action logic
        _blackjackService = new BlackjackService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            new Mock<IUserRepository>().Object,
            _handRepositoryMock.Object,
            _deckApiServiceMock.Object
        );
    }

    private static JsonElement CreateBetActionData(long amount)
    {
        var betAction = new BetAction(amount);
        var json = JsonSerializer.Serialize(betAction);
        return JsonDocument.Parse(json).RootElement;
    }

    // [Fact]
    // public async Task PerformActionAsync_BetAction_Success_BeforeDeadline()
    // {
    //     // Arrange
    //     var roomId = Guid.NewGuid();
    //     var playerId = Guid.NewGuid();
    //     var roomPlayerId = Guid.NewGuid();
    //     var betAmount = 100L;

    //     var player = new RoomPlayer
    //     {
    //         Id = roomPlayerId,
    //         UserId = playerId,
    //         RoomId = roomId,
    //         Balance = 1000,
    //         Status = Status.Away,
    //     };

    //     var bettingStage = new BlackjackBettingStage(
    //         DateTimeOffset.UtcNow.AddMinutes(1),
    //         new Dictionary<Guid, long>()
    //     );
    //     var gameState = new BlackjackState { CurrentStage = bettingStage };
    //     var gameStateString = JsonSerializer.Serialize(gameState);

    //     _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
    //     _roomPlayerRepositoryMock
    //         .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, playerId))
    //         .ReturnsAsync(player);

    //     // Act
    //     await _blackjackService.PerformActionAsync(
    //         roomId,
    //         playerId,
    //         "bet",
    //         CreateBetActionData(betAmount)
    //     );

    //     // Assert
    //     _roomPlayerRepositoryMock.Verify(
    //         rp => rp.UpdateAsync(It.Is<RoomPlayer>(p => p.Status == Status.Active)),
    //         Times.Once
    //     );
    //     _roomRepositoryMock.Verify(
    //         r =>
    //             r.UpdateGameStateAsync(
    //                 roomId,
    //                 It.Is<string>(s =>
    //                     JsonSerializer
    //                         .Deserialize<BlackjackState>(s, (JsonSerializerOptions?)null)!
    //                         .CurrentStage is BlackjackBettingStage
    //                 )
    //             ),
    //         Times.Once
    //     );
    // }

    // [Fact]
    // public async Task PerformActionAsync_BetAction_Success_AfterDeadline_TransitionsStage()
    // {
    //     // Arrange
    //     var roomId = Guid.NewGuid();
    //     var actingPlayerId = Guid.NewGuid();
    //     var otherPlayerId = Guid.NewGuid();
    //     var betAmount = 100L;

    //     var actingPlayer = new RoomPlayer
    //     {
    //         Id = actingPlayerId,
    //         Balance = 1000,
    //         Status = Status.Away,
    //     };
    //     var otherPlayer = new RoomPlayer { Id = otherPlayerId, Balance = 1000 };

    //     var bettingStage = new BlackjackBettingStage(
    //         DateTimeOffset.UtcNow.AddMinutes(-1),
    //         new Dictionary<Guid, long> { { otherPlayer.Id, 50L } }
    //     );
    //     var gameState = new BlackjackState { CurrentStage = bettingStage };
    //     var gameStateString = JsonSerializer.Serialize(gameState);

    //     var room = new Room
    //     {
    //         Id = roomId,
    //         DeckId = "test-deck-123",
    //         GameMode = "Blackjack",
    //         GameState = "{}"
    //     };

    //     _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
    //     _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
    //     _roomPlayerRepositoryMock
    //         .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, actingPlayerId))
    //         .ReturnsAsync(actingPlayer);
    //     _roomPlayerRepositoryMock
    //         .Setup(r => r.GetByIdAsync(actingPlayer.Id))
    //         .ReturnsAsync(actingPlayer);
    //     _roomPlayerRepositoryMock
    //         .Setup(r => r.GetByIdAsync(otherPlayer.Id))
    //         .ReturnsAsync(otherPlayer);
    //     _roomPlayerRepositoryMock
    //         .Setup(r => r.GetActivePlayersInRoomAsync(roomId))
    //         .ReturnsAsync(new List<RoomPlayer> { actingPlayer, otherPlayer });
    //     _deckApiServiceMock
    //         .Setup(d => d.CreateEmptyHand(It.IsAny<string>(), It.IsAny<string>()))
    //         .ReturnsAsync(true);
    //     _deckApiServiceMock
    //         .Setup(d => d.DrawCards(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
    //         .ReturnsAsync(new List<CardDTO>());

    //     // Act
    //     await _blackjackService.PerformActionAsync(
    //         roomId,
    //         actingPlayerId,
    //         "bet",
    //         CreateBetActionData(betAmount)
    //     );

    //     // Assert
    //     // Verify balances are updated for all bettors
    //     _roomPlayerRepositoryMock.Verify(
    //         rp => rp.UpdatePlayerBalanceAsync(actingPlayer.Id, -betAmount),
    //         Times.Once
    //     );
    //     _roomPlayerRepositoryMock.Verify(
    //         rp => rp.UpdatePlayerBalanceAsync(otherPlayer.Id, -50L),
    //         Times.Once
    //     );

    //     // Verify stage transition
    //     _roomRepositoryMock.Verify(
    //         r =>
    //             r.UpdateGameStateAsync(
    //                 roomId,
    //                 It.Is<string>(s =>
    //                     JsonSerializer
    //                         .Deserialize<BlackjackState>(s, (JsonSerializerOptions?)null)!
    //                         .CurrentStage is BlackjackPlayerActionStage
    //                 )
    //             ),
    //         Times.Once
    //     );
    // }

    [Fact]
    public async Task PerformActionAsync_InvalidActionForStage_ThrowsBadRequestException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameState = new BlackjackState
        {
            CurrentStage = new BlackjackPlayerActionStage(DateTimeOffset.UtcNow.AddMinutes(1), 0),
        };
        var gameStateString = JsonSerializer.Serialize(gameState);

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _blackjackService.PerformActionAsync(
                roomId,
                Guid.NewGuid(),
                "bet",
                CreateBetActionData(100)
            )
        );
    }

    [Fact]
    public async Task PerformActionAsync_PlayerNotFound_ThrowsException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var bettingStage = new BlackjackBettingStage(DateTimeOffset.UtcNow.AddMinutes(1), []);
        var gameState = new BlackjackState { CurrentStage = bettingStage };
        var gameStateString = JsonSerializer.Serialize(gameState);

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, playerId))
            .ReturnsAsync((RoomPlayer?)null);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _blackjackService.PerformActionAsync(roomId, playerId, "bet", CreateBetActionData(100))
        );
    }

    [Fact]
    public async Task PerformActionAsync_BetAction_InsufficientBalance_ThrowsBadRequestException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var player = new RoomPlayer { Status = Status.Away, Balance = 50 }; // Not enough balance
        var bettingStage = new BlackjackBettingStage(DateTimeOffset.UtcNow.AddMinutes(1), []);
        var gameState = new BlackjackState { CurrentStage = bettingStage };
        var gameStateString = JsonSerializer.Serialize(gameState);

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, playerId))
            .ReturnsAsync(player);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _blackjackService.PerformActionAsync(roomId, playerId, "bet", CreateBetActionData(100))
        );
    }

    [Fact]
    public async Task PerformActionAsync_BetAction_AfterDeadline_BettingPlayerNotFound_ThrowsInternalServerException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var actingPlayerId = Guid.NewGuid();
        var missingPlayerId = Guid.NewGuid(); // This player bet but will not be found by the repo

        var actingPlayer = new RoomPlayer
        {
            Id = actingPlayerId,
            Balance = 1000,
            Status = Status.Away,
        };

        var bettingStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            new Dictionary<Guid, long> { { missingPlayerId, 50L } } // Bet from the missing player
        );
        var gameState = new BlackjackState { CurrentStage = bettingStage };
        var gameStateString = JsonSerializer.Serialize(gameState);

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, actingPlayerId))
            .ReturnsAsync(actingPlayer);

        // Setup the mock to throw when trying to update the missing player's balance
        _roomPlayerRepositoryMock
            .Setup(r => r.UpdatePlayerBalanceAsync(missingPlayerId, -50L))
            .ThrowsAsync(new NotFoundException("Player not found."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _blackjackService.PerformActionAsync(
                roomId,
                actingPlayerId,
                "bet",
                CreateBetActionData(100)
            )
        );
    }
}
