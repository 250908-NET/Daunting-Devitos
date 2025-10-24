using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Project.Api;
using Project.Api.Data;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Constants;
using Project.Api.Utilities.Enums;
using Project.Test.Helpers;
using Xunit;

namespace Project.Test.Integration;

public class BlackjackIntegrationTests(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    /// <summary>
    /// Simulates a complete blackjack game flow with three players for two rounds.
    /// </summary>
    [Fact]
    public async Task BlackjackGameFlow_ShouldCompleteSuccessfully()
    {
        // --- SETUP ---
        var sseService = new RoomSSEService();

        var cardSequence = new Queue<CardDTO>(
            [
                // Initial deal (in order: P0 -> P1 -> P2 -> Dealer -> P0 -> P1 -> P2 -> Dealer)
                new CardDTO
                {
                    Value = "5",
                    Suit = "HEARTS",
                    Code = "5H",
                    Image = "card01",
                }, // Player 0 first card
                new CardDTO
                {
                    Value = "10",
                    Suit = "DIAMONDS",
                    Code = "10D",
                    Image = "card03",
                }, // Player 1 first card
                new CardDTO
                {
                    Value = "4",
                    Suit = "HEARTS",
                    Code = "4H",
                    Image = "card05",
                }, // Player 2 first card
                new CardDTO
                {
                    Value = "10",
                    Suit = "HEARTS",
                    Code = "10H",
                    Image = "card07",
                }, // Dealer up card
                new CardDTO
                {
                    Value = "6",
                    Suit = "SPADES",
                    Code = "6S",
                    Image = "card02",
                }, // Player 0 second card (total: 11)
                new CardDTO
                {
                    Value = "6",
                    Suit = "CLUBS",
                    Code = "6C",
                    Image = "card04",
                }, // Player 1 second card (total: 16)
                new CardDTO
                {
                    Value = "5",
                    Suit = "SPADES",
                    Code = "5S",
                    Image = "card06",
                }, // Player 2 second card (total: 9)
                new CardDTO
                {
                    Value = "8",
                    Suit = "DIAMONDS",
                    Code = "8D",
                    Image = "card08",
                }, // Dealer hole card (total: 18)
                // Action phase cards
                new CardDTO
                {
                    Value = "8",
                    Suit = "DIAMONDS",
                    Code = "8D",
                    Image = "card09",
                }, // Player 0 hit card (11+8=19)
                new CardDTO
                {
                    Value = "3",
                    Suit = "HEARTS",
                    Code = "3H",
                    Image = "dcard10",
                }, // Player 1 double card (16+3=19)
                // No more cards needed:
                // - Player 2 surrenders (no cards needed)
                // - Dealer has 18, no additional draws needed
            ]
        );
        var mockDeckService = MockDeckAPIHelper.CreateMockDeckService(cardSequence);

        var dbName = $"InMemoryDb_{Guid.NewGuid()}";

        var appFactory = CreateConfiguredWebAppFactory(
            dbName,
            services =>
            {
                // Replace IRoomSSEService with test instance
                services.RemoveAll<IRoomSSEService>();
                services.AddSingleton<IRoomSSEService>(sseService);

                // Replace IDeckApiService with mock
                services.RemoveAll<IDeckApiService>();
                services.AddSingleton<IDeckApiService>(mockDeckService.Object);
            }
        );

        // --- CREATING A ROOM ---

        // ARRANGE:

        var players =
            new List<(
                Guid id,
                HttpClient client,
                StreamReader reader,
                CancellationTokenSource cts
            )>();

        // create first player
        var player0Id = Guid.CreateVersion7();
        var client0 = appFactory.CreateClient();

        await using (var scope = appFactory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(
                new User
                {
                    Id = player0Id,
                    Name = "HostPlayer",
                    Email = "host@example.com",
                }
            );
            await context.SaveChangesAsync();
        }

        // ACT: create new room
        var createRoomResponse = await client0.PostAsJsonAsync(
            "/api/room",
            new CreateRoomDTO
            {
                HostId = player0Id,
                GameMode = GameModes.Blackjack,
                MaxPlayers = 3,
            },
            _jsonOptions
        );
        createRoomResponse.EnsureSuccessStatusCode();

        // ASSERT: room was created
        var createdRoom = await createRoomResponse.Content.ReadFromJsonAsync<RoomDTO>();
        createdRoom.Should().NotBeNull();
        createdRoom!.Id.Should().NotBeEmpty();
        createdRoom!.HostId.Should().Be(player0Id);

        // ASSERT: room exists
        var roomId = createdRoom!.Id;
        var roomExistsResponse = await client0.GetAsync($"/api/room/{roomId}/exists");
        roomExistsResponse.EnsureSuccessStatusCode();
        var roomExists = await roomExistsResponse.Content.ReadFromJsonAsync<bool>();
        roomExists.Should().BeTrue();

        // --- CONNECTING TO SSE ---

        // connect first player to SSE
        var (reader0, cts0) = await client0.OpenSseConnection(roomId);
        players.Add((player0Id, client0, reader0, cts0));

        // create other two players and connect to SSE
        for (int i = 1; i < 3; i++)
        {
            var userId = Guid.CreateVersion7();
            var client = appFactory.CreateClient();
            var (reader, cts) = await client.OpenSseConnection(roomId);
            players.Add((userId, client, reader, cts));

            // add to user database
            await using var scope = appFactory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(
                new User
                {
                    Id = userId,
                    Name = $"Player{i}",
                    Email = $"{i}@example.com",
                }
            );
            await context.SaveChangesAsync();
        }

        // --- ADDING PLAYERS TO ROOM ---

        for (int i = 1; i < players.Count; i++)
        {
            var (playerId, client, _, _) = players[i];
            var joinRoomResponse = await client.PostAsJsonAsync(
                $"/api/room/{roomId}/join",
                new { UserId = playerId },
                _jsonOptions
            );
            joinRoomResponse.EnsureSuccessStatusCode();
        }

        // --- STARTING THE GAME ---
        var startGameResponse = await client0.PostAsJsonAsync<object>(
            $"/api/room/{roomId}/start",
            null!, // use default options
            _jsonOptions
        );
        startGameResponse.EnsureSuccessStatusCode();

        // ASSERT: Game started and betting stage began
        foreach (var (_, _, reader, _) in players)
        {
            var (eventType, eventData) = await SseTestHelper.ReadSseEventAsync(reader);
            eventType.Should().Be(RoomEventType.GameStateUpdate);
            var gameStateUpdate = SseTestHelper.Deserialize<GameStateUpdateEventData>(eventData);
            gameStateUpdate!.CurrentStage.Should().BeOfType<BlackjackBettingStage>();
        }

        // --- BETTING ---

        // All players bet 100 chips
        for (int i = 0; i < players.Count; i++)
        {
            var (playerId, client, _, _) = players[i];
            var betResponse = await client.PostAsJsonAsync(
                $"/api/room/{roomId}/player/{playerId}/action",
                new { Action = "bet", Data = new { Amount = 100 } },
                _jsonOptions
            );
            betResponse.EnsureSuccessStatusCode();
        }

        // ASSERT: All players receive bet actions and game state transitions to dealing
        foreach (var (_, _, reader, _) in players)
        {
            // Should get state transition after each bet
            for (int i = 0; i < players.Count; i++)
            {
                // First get the bet action
                var (actionEventType, actionEventData) = await SseTestHelper.ReadSseEventAsync(
                    reader
                );
                actionEventType.Should().Be(RoomEventType.PlayerAction);
                var action = SseTestHelper.Deserialize<PlayerActionEventData>(actionEventData);
                action!.Action.Should().Be("bet");
                action.Amount.Should().Be(100);

                // Then get the state update
                var (stateEventType, stateEventData) = await SseTestHelper.ReadSseEventAsync(
                    reader
                );
                stateEventType.Should().Be(RoomEventType.GameStateUpdate);
                var state = SseTestHelper.Deserialize<GameStateUpdateEventData>(stateEventData);
                state!.CurrentStage.Should().BeOfType<BlackjackBettingStage>();
            }

            // First get dealing stage transition
            var (dealingStateEventType, dealingStateData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            dealingStateEventType.Should().Be(RoomEventType.GameStateUpdate);
            var dealingState = SseTestHelper.Deserialize<GameStateUpdateEventData>(
                dealingStateData
            );
            dealingState!.CurrentStage.Should().BeOfType<BlackjackDealingStage>();

            // Should get player hands being dealt
            for (int i = 0; i < players.Count; i++)
            {
                // Each player gets their full hand of 2 cards at once
                var (revealEventType, revealEventData) = await SseTestHelper.ReadSseEventAsync(
                    reader
                );
                revealEventType.Should().Be(RoomEventType.PlayerReveal);
                var reveal = SseTestHelper.Deserialize<PlayerRevealEventData>(revealEventData);
                reveal.Should().NotBeNull();
                reveal!.PlayerHand.Should().NotBeNull();
                reveal.PlayerHand.Count.Should().Be(2); // Players get both cards at once
            }

            // Dealer gets two cards (second one face down)
            var (dealerRevealEventType, dealerRevealData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            dealerRevealEventType.Should().Be(RoomEventType.DealerReveal);
            var dealerReveal = SseTestHelper.Deserialize<DealerRevealEventData>(dealerRevealData);
            dealerReveal.Should().NotBeNull();
            dealerReveal!.DealerHand.Count.Should().Be(2);
            dealerReveal.DealerHand[1].IsFaceDown.Should().BeTrue();

            // Should transition to player action stage
            var (actionStateEventType, actionStateData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            actionStateEventType.Should().Be(RoomEventType.GameStateUpdate);
            var actionState = SseTestHelper.Deserialize<GameStateUpdateEventData>(actionStateData);
            var actionStage = actionState!
                .CurrentStage.Should()
                .BeOfType<BlackjackPlayerActionStage>()
                .Subject;
            actionStage.PlayerIndex.Should().Be(0);
            actionStage.HandIndex.Should().Be(0);
        }

        // --- PLAYER ACTIONS ---

        // Player 0 hits
        var hitResponse = await players[0]
            .client.PostAsJsonAsync(
                $"/api/room/{roomId}/player/{players[0].id}/action",
                new { Action = "hit", Data = new { } },
                _jsonOptions
            );
        hitResponse.EnsureSuccessStatusCode();

        // ASSERT: Hit action broadcast and stay on same player
        foreach (var (_, _, reader, _) in players)
        {
            // First get the hit action event
            var (hitEventType, hitEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            hitEventType.Should().Be(RoomEventType.PlayerAction);
            var hitAction = SseTestHelper.Deserialize<PlayerActionEventData>(hitEventData);
            hitAction!.Action.Should().Be("hit");
            hitAction.Cards.Should().NotBeEmpty();

            // Then get the player's updated hand reveal
            var (revealEventType, revealEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            revealEventType.Should().Be(RoomEventType.PlayerReveal);
            var reveal = SseTestHelper.Deserialize<PlayerRevealEventData>(revealEventData);
            reveal!.PlayerHand.Count.Should().Be(3);

            // Finally get the state update showing we stay on the same player
            var (stateEventType, stateEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            stateEventType.Should().Be(RoomEventType.GameStateUpdate);
            var gameState = SseTestHelper.Deserialize<GameStateUpdateEventData>(stateEventData);
            var stage = gameState!
                .CurrentStage.Should()
                .BeOfType<BlackjackPlayerActionStage>()
                .Subject;

            // After a hit that doesn't bust, we should stay on the same player and hand
            stage.PlayerIndex.Should().Be(0);
            stage.HandIndex.Should().Be(0);
        }

        // Player 0 stands
        var standResponse = await players[0]
            .client.PostAsJsonAsync(
                $"/api/room/{roomId}/player/{players[0].id}/action",
                new { Action = "stand", Data = new { } },
                _jsonOptions
            );
        standResponse.EnsureSuccessStatusCode();

        // ASSERT: Stand action broadcast and turn changes to player 1
        foreach (var (_, _, reader, _) in players)
        {
            await AssertPlayerActionAndTurnChange(
                reader,
                "stand",
                players[0].id,
                nextPlayerIndex: 1
            );
        }

        // Player 1 doubles
        var doubleResponse = await players[1]
            .client.PostAsJsonAsync(
                $"/api/room/{roomId}/player/{players[1].id}/action",
                new { Action = "double", Data = new { } },
                _jsonOptions
            );
        doubleResponse.EnsureSuccessStatusCode();

        // ASSERT: Double action broadcast and turn changes to player 2
        foreach (var (_, _, reader, _) in players)
        {
            // First get the double action event
            var (doubleEventType, doubleEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            doubleEventType.Should().Be(RoomEventType.PlayerAction);
            var doubleAction = SseTestHelper.Deserialize<PlayerActionEventData>(doubleEventData);
            doubleAction!.Action.Should().Be("double");
            doubleAction.PlayerId.Should().Be(players[1].id);
            doubleAction.Cards.Should().NotBeEmpty();

            // Then get the player's updated hand reveal
            var (revealEventType, revealEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            revealEventType.Should().Be(RoomEventType.PlayerReveal);
            var reveal = SseTestHelper.Deserialize<PlayerRevealEventData>(revealEventData);
            reveal!.PlayerId.Should().Be(players[1].id);
            reveal.PlayerHand.Count.Should().Be(3);

            // Finally get the state update showing turn change
            var (stateEventType, stateEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            stateEventType.Should().Be(RoomEventType.GameStateUpdate);
            var gameState = SseTestHelper.Deserialize<GameStateUpdateEventData>(stateEventData);
            var stage = gameState!
                .CurrentStage.Should()
                .BeOfType<BlackjackPlayerActionStage>()
                .Subject;
            stage.PlayerIndex.Should().Be(2);
            stage.HandIndex.Should().Be(0);
        }

        // Player 2 surrenders
        var surrenderResponse = await players[2]
            .client.PostAsJsonAsync(
                $"/api/room/{roomId}/player/{players[2].id}/action",
                new { Action = "surrender", Data = new { } },
                _jsonOptions
            );
        surrenderResponse.EnsureSuccessStatusCode();

        // ASSERT: Surrender action broadcast
        foreach (var (_, _, reader, _) in players)
        {
            // Get the surrender action event
            var (surrenderEventType, surrenderEventData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            surrenderEventType.Should().Be(RoomEventType.PlayerAction);
            var surrenderAction = SseTestHelper.Deserialize<PlayerActionEventData>(
                surrenderEventData
            );
            surrenderAction!.Action.Should().Be("surrender");
            surrenderAction.PlayerId.Should().Be(players[2].id);
        }

        // --- ROUND COMPLETION ---

        // ASSERT: Game transitions to finish round stage
        foreach (var (_, _, reader, _) in players)
        {
            var (finishEventType, finishEventData) = await SseTestHelper.ReadSseEventAsync(reader);
            finishEventType.Should().Be(RoomEventType.GameStateUpdate);
            var finishState = SseTestHelper.Deserialize<GameStateUpdateEventData>(finishEventData);
            finishState!.CurrentStage.Should().BeOfType<BlackjackFinishRoundStage>();

            // Should reveal dealer's full hand
            var (dealerRevealEventType, dealerRevealData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            dealerRevealEventType.Should().Be(RoomEventType.DealerReveal);
            var dealerReveal = SseTestHelper.Deserialize<DealerRevealEventData>(dealerRevealData);
            dealerReveal!.DealerHand.Should().NotContain(c => c.IsFaceDown);

            // Should reveal all player hands
            for (int i = 0; i < players.Count; i++)
            {
                var (revealEventType, revealEventData) = await SseTestHelper.ReadSseEventAsync(
                    reader
                );
                revealEventType.Should().Be(RoomEventType.PlayerReveal);
            }

            // Should transition to next betting round
            var (nextRoundEventType, nextRoundEventData) = await SseTestHelper.ReadSseEventAsync(
                reader
            );
            nextRoundEventType.Should().Be(RoomEventType.GameStateUpdate);
            var nextRoundState = SseTestHelper.Deserialize<GameStateUpdateEventData>(
                nextRoundEventData
            );
            nextRoundState!.CurrentStage.Should().BeOfType<BlackjackBettingStage>();
        }

        // Clean up SSE connections
        foreach (var (_, _, _, cts) in players)
        {
            cts.Cancel();
        }
    }

    private static async Task AssertPlayerActionAndTurnChange(
        StreamReader reader,
        string expectedAction,
        Guid expectedPlayerId,
        int nextPlayerIndex
    )
    {
        // Assert: Player action was broadcast
        var (actionEventType, actionEventData) = await SseTestHelper.ReadSseEventAsync(reader);
        actionEventType.Should().Be(RoomEventType.PlayerAction);
        var playerAction = SseTestHelper.Deserialize<PlayerActionEventData>(actionEventData);
        playerAction!.Action.Should().Be(expectedAction);
        playerAction.PlayerId.Should().Be(expectedPlayerId);

        // Assert: Game state updated to next player's turn
        var (turnChangeEventType, turnChangeEventData) = await SseTestHelper.ReadSseEventAsync(
            reader
        );
        turnChangeEventType.Should().Be(RoomEventType.GameStateUpdate);
        var turnChangeUpdate = SseTestHelper.Deserialize<GameStateUpdateEventData>(
            turnChangeEventData
        );
        var nextActionStage = turnChangeUpdate!
            .CurrentStage.Should()
            .BeOfType<BlackjackPlayerActionStage>()
            .Subject;
        nextActionStage.PlayerIndex.Should().Be(nextPlayerIndex);
    }

    private static class SseTestHelper
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public static async Task<(RoomEventType type, string data)> ReadSseEventAsync(
            StreamReader reader
        )
        {
            var eventLine = await reader.ReadLineAsync();
            var dataLine = await reader.ReadLineAsync();
            await reader.ReadLineAsync(); // consume blank line

            if (eventLine == null || dataLine == null)
            {
                throw new EndOfStreamException("SSE stream ended unexpectedly.");
            }

            var eventTypeString = eventLine.Replace("event: ", "");
            Enum.TryParse<RoomEventType>(eventTypeString, true, out var eventType)
                .Should()
                .BeTrue($"because '{eventTypeString}' should be a valid RoomEventType");

            return (eventType, dataLine.Replace("data: ", ""));
        }

        public static T? Deserialize<T>(string data)
        {
            return JsonSerializer.Deserialize<T>(data, _jsonOptions);
        }
    }
}
