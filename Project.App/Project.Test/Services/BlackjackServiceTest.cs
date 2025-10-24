using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Constants;
using Project.Api.Utilities.Enums;

namespace Project.Test.Services;

public class BlackjackServiceTest
{
    private readonly Mock<IRoomRepository> _roomRepositoryMock;
    private readonly Mock<IRoomPlayerRepository> _roomPlayerRepositoryMock;
    private readonly Mock<IHandRepository> _handRepositoryMock;
    private readonly Mock<IDeckApiService> _deckApiServiceMock;
    private readonly Mock<IRoomSSEService> _roomSSEServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly BlackjackService _blackjackService;

    public BlackjackServiceTest()
    {
        _roomRepositoryMock = new Mock<IRoomRepository>();
        _roomPlayerRepositoryMock = new Mock<IRoomPlayerRepository>();
        _handRepositoryMock = new Mock<IHandRepository>();
        _deckApiServiceMock = new Mock<IDeckApiService>();
        _roomSSEServiceMock = new Mock<IRoomSSEService>();
        _userRepositoryMock = new Mock<IUserRepository>();

        var defaultConfig = new BlackjackConfig();
        var defaultConfigString = JsonSerializer.Serialize(defaultConfig);
        _roomRepositoryMock
            .Setup(r => r.GetGameConfigAsync(It.IsAny<Guid>()))
            .ReturnsAsync(defaultConfigString);

        _blackjackService = new BlackjackService(
            _roomRepositoryMock.Object,
            _roomPlayerRepositoryMock.Object,
            _handRepositoryMock.Object,
            _deckApiServiceMock.Object,
            _roomSSEServiceMock.Object,
            _userRepositoryMock.Object,
            new Mock<ILogger<BlackjackService>>().Object
        );
    }

    private static JsonElement CreateBetActionData(long amount)
    {
        var betAction = new BetAction(amount);
        var json = JsonSerializer.Serialize(betAction);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task PerformActionAsync_BetAction_Success_BeforeDeadline()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var roomPlayerId = Guid.NewGuid();
        var betAmount = 100L;

        var player = new RoomPlayer
        {
            Id = roomPlayerId,
            UserId = playerId,
            RoomId = roomId,
            Balance = 1000,
            Status = Status.Away,
        };

        // another player who has not bet yet
        // ensures round does not start prematurely
        var player2 = new RoomPlayer
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoomId = roomId,
            Balance = 1000,
            Status = Status.Away,
        };

        var bettingStage = new BlackjackBettingStage(DateTimeOffset.UtcNow.AddMinutes(1), []);
        var gameState = new BlackjackState { CurrentStage = bettingStage };
        var gameStateString = JsonSerializer.Serialize(gameState);
        var room = new Room
        {
            Id = roomId,
            GameMode = GameModes.Blackjack,
            GameState = gameStateString,
            DeckId = "test_deck",
        };

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, playerId))
            .ReturnsAsync(player);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAsync(roomId))
            .ReturnsAsync([player, player2]);

        // Act
        await _blackjackService.PerformActionAsync(
            roomId,
            playerId,
            "bet",
            CreateBetActionData(betAmount)
        );

        // Assert
        _roomPlayerRepositoryMock.Verify(
            rp => rp.UpdateAsync(It.Is<RoomPlayer>(p => p.Status == Status.Active)),
            Times.Once
        );
        _roomRepositoryMock.Verify(
            r =>
                r.UpdateGameStateAsync(
                    roomId,
                    It.Is<string>(s =>
                        JsonSerializer
                            .Deserialize<BlackjackState>(s, (JsonSerializerOptions?)null)!
                            .CurrentStage is BlackjackBettingStage
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PerformActionAsync_BetAction_Success_AfterDeadline_TransitionsStage()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var actingPlayerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();
        var betAmount = 100L;
        var deckId = "test_deck_id";

        var actingPlayer = new RoomPlayer
        {
            Id = actingPlayerId,
            UserId = actingPlayerId, // Ensure UserId is set for GetByRoomIdAndUserIdAsync
            Balance = 1000,
            Status = Status.Away,
        };
        var otherPlayer = new RoomPlayer
        {
            Id = otherPlayerId,
            UserId = otherPlayerId, // Ensure UserId is set
            Balance = 1000,
        };

        var room = new Room
        {
            Id = roomId,
            DeckId = deckId,
            GameMode = GameModes.Blackjack,
            GameState = "",
        };

        var bettingStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            new Dictionary<Guid, long> { { otherPlayer.Id, 50L } }
        );
        var gameState = new BlackjackState { CurrentStage = bettingStage };
        var gameStateString = JsonSerializer.Serialize(gameState);

        _roomRepositoryMock.Setup(r => r.GetGameStateAsync(roomId)).ReturnsAsync(gameStateString);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByRoomIdAndUserIdAsync(roomId, actingPlayerId))
            .ReturnsAsync(actingPlayer);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByIdAsync(actingPlayer.Id))
            .ReturnsAsync(actingPlayer);
        _roomPlayerRepositoryMock
            .Setup(r => r.GetByIdAsync(otherPlayer.Id))
            .ReturnsAsync(otherPlayer);
        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId)).ReturnsAsync(room);

        // Define card DTOs for clarity and to ensure non-null values
        var player1Card1 = new CardDTO
        {
            Code = "H2",
            Value = "2",
            Suit = "HEARTS",
        };
        var player2Card1 = new CardDTO
        {
            Code = "D3",
            Value = "3",
            Suit = "DIAMONDS",
        };
        var dealerCard1 = new CardDTO
        {
            Code = "S4",
            Value = "4",
            Suit = "SPADES",
        };
        var player1Card2 = new CardDTO
        {
            Code = "C5",
            Value = "5",
            Suit = "CLUBS",
        };
        var player2Card2 = new CardDTO
        {
            Code = "H6",
            Value = "6",
            Suit = "HEARTS",
        };
        var dealerCard2 = new CardDTO
        {
            Code = "SK",
            Value = "KING",
            Suit = "SPADES",
        };

        // Mock DrawCards to return specific cards for the initial deal sequence
        // (2 rounds * (2 players + 1 dealer) = 6 calls)
        _deckApiServiceMock
            .SetupSequence(d => d.DrawCards(It.IsAny<string>(), It.IsAny<string>(), 1))
            .ReturnsAsync([player1Card1]) // Round 1: Player 1
            .ReturnsAsync([player2Card1]) // Round 1: Player 2
            .ReturnsAsync([dealerCard1]) // Round 1: Dealer
            .ReturnsAsync([player1Card2]) // Round 2: Player 1
            .ReturnsAsync([player2Card2]) // Round 2: Player 2
            .ReturnsAsync([dealerCard2]); // Round 2: Dealer

        // Setup hand IDs for the hands that will be created
        var actingPlayerHandId = Guid.NewGuid();
        var otherPlayerHandId = Guid.NewGuid();

        // Mock CreateHandAsync to capture the created hand IDs and orders
        _handRepositoryMock
            .Setup(h => h.CreateHandAsync(It.IsAny<Hand>()))
            .Callback<Hand>(hand =>
            {
                if (hand.RoomPlayerId == actingPlayer.Id)
                {
                    hand.Id = actingPlayerHandId;
                    hand.Order = 0; // Assuming acting player is the first hand
                }
                else if (hand.RoomPlayerId == otherPlayer.Id)
                {
                    hand.Id = otherPlayerHandId;
                    hand.Order = 1; // Assuming other player is the second hand
                }
            });

        // Mock GetHandsByRoomIdAsync to return the expected hands after creation
        _handRepositoryMock
            .Setup(h => h.GetHandsByRoomIdAsync(roomId))
            .ReturnsAsync(
                [
                    new Hand
                    {
                        Id = actingPlayerHandId,
                        RoomPlayerId = actingPlayer.Id,
                        Order = 0,
                        Bet = betAmount,
                    },
                    new Hand
                    {
                        Id = otherPlayerHandId,
                        RoomPlayerId = otherPlayer.Id,
                        Order = 1,
                        Bet = 50L, // Bet from otherPlayer in bettingStage
                    },
                ]
            );

        // Mock ListHand to return the full hands after dealing for each player and dealer
        _deckApiServiceMock
            .Setup(d => d.ListHand(deckId, $"hand-{actingPlayerHandId}"))
            .ReturnsAsync([player1Card1, player1Card2]);

        _deckApiServiceMock
            .Setup(d => d.ListHand(deckId, $"hand-{otherPlayerHandId}"))
            .ReturnsAsync([player2Card1, player2Card2]);

        _deckApiServiceMock
            .Setup(d => d.ListHand(deckId, "dealer"))
            .ReturnsAsync([dealerCard1, dealerCard2]);

        // Act
        await _blackjackService.PerformActionAsync(
            roomId,
            actingPlayerId,
            "bet",
            CreateBetActionData(betAmount)
        );

        // Assert
        // Verify balances are updated for all bettors
        _roomPlayerRepositoryMock.Verify(
            rp => rp.UpdatePlayerBalanceAsync(actingPlayer.Id, -betAmount),
            Times.Once
        );
        _roomPlayerRepositoryMock.Verify(
            rp => rp.UpdatePlayerBalanceAsync(otherPlayer.Id, -50L),
            Times.Once
        );

        // Verify stage transition
        _roomRepositoryMock.Verify(
            r =>
                r.UpdateGameStateAsync(
                    roomId,
                    It.Is<string>(s =>
                        JsonSerializer
                            .Deserialize<BlackjackState>(s, (JsonSerializerOptions?)null)!
                            .CurrentStage is BlackjackPlayerActionStage
                    )
                ),
            Times.Once
        );

        // Verify SSE events for game state updates
        _roomSSEServiceMock.Verify(
            s =>
                s.BroadcastEventAsync(
                    roomId,
                    RoomEventType.GameStateUpdate,
                    It.IsAny<GameStateUpdateEventData>()
                ),
            Times.AtLeastOnce
        );

        // Verify SSE events for player reveals (for both players)
        _roomSSEServiceMock.Verify(
            s =>
                s.BroadcastEventAsync(
                    roomId,
                    RoomEventType.PlayerReveal,
                    It.Is<PlayerRevealEventData>(data =>
                        data.PlayerHand.SequenceEqual(new[] { player1Card1, player1Card2 })
                    )
                ),
            Times.Once
        );
        _roomSSEServiceMock.Verify(
            s =>
                s.BroadcastEventAsync(
                    roomId,
                    RoomEventType.PlayerReveal,
                    It.Is<PlayerRevealEventData>(data =>
                        data.PlayerHand.SequenceEqual(new[] { player2Card1, player2Card2 })
                    )
                ),
            Times.Once
        );

        // Verify SSE event for dealer reveal (initial, one card facedown)
        _roomSSEServiceMock.Verify(
            s =>
                s.BroadcastEventAsync(
                    roomId,
                    RoomEventType.DealerReveal,
                    It.Is<DealerRevealEventData>(data =>
                        data.DealerHand.Count == 2
                        && data.DealerHand[0].Code == dealerCard1.Code
                        && data.DealerHand[1].IsFaceDown == true
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PerformActionAsync_InvalidActionForStage_ThrowsBadRequestException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var gameState = new BlackjackState
        {
            CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow.AddMinutes(1),
                0,
                0
            ),
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
