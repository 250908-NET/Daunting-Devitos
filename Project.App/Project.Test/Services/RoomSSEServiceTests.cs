using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Project.Api.Models.Games; // Add this using directive for IRoomEventData and concrete types
using Project.Api.Services;
using Project.Api.Utilities.Enums;
using Project.Api.Utilities.Extensions; // Add this using directive for RoomEventType

namespace Project.Test.Services;

public class RoomSSEServiceTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // use js convention instead of C#
    };

    /// <summary>
    /// Creates a mock HttpContext for testing SSE connections.
    /// </summary>
    private static (
        HttpContext context,
        CancellationTokenSource cts,
        MemoryStream stream
    ) CreateTestContext()
    {
        var stream = new MemoryStream();
        var cts = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext { RequestAborted = cts.Token };
        httpContext.Response.Body = stream;
        return (httpContext, cts, stream);
    }

    [Fact]
    public async Task AddConnectionAsync_ShouldSetHeadersAndSendConfirmation()
    {
        // Arrange
        var sseService = new RoomSSEService(NullLogger<RoomSSEService>.Instance);
        var (context, cts, stream) = CreateTestContext();
        var roomId = Guid.NewGuid();

        // Act
        var addConnectionTask = sseService.AddConnectionAsync(roomId, context.Response);

        // Allow the service time to write the confirmation message
        await Task.Delay(100);

        // Assert: Check that the correct SSE headers are set
        Assert.Equal("text/event-stream", context.Response.Headers.ContentType);
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal("keep-alive", context.Response.Headers.Connection);

        // Assert: Check that the initial connection confirmation was sent
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync();
        Assert.Equal(": connected", line);

        // Clean up: Simulate client disconnection and wait for the task to complete
        cts.Cancel();
        await addConnectionTask;
    }

    [Fact]
    public async Task BroadcastEventAsync_ShouldSendEventToAllClientsInRoom()
    {
        // Arrange
        var sseService = new RoomSSEService(NullLogger<RoomSSEService>.Instance);
        var roomId1 = Guid.NewGuid();
        var roomId2 = Guid.NewGuid();

        var (context1, cts1, stream1) = CreateTestContext();
        var (context2, cts2, stream2) = CreateTestContext();
        var (context3, cts3, stream3) = CreateTestContext();

        // Add two connections to room 1 and one connection to room 2
        var task1 = sseService.AddConnectionAsync(roomId1, context1.Response);
        var task2 = sseService.AddConnectionAsync(roomId1, context2.Response);
        var task3 = sseService.AddConnectionAsync(roomId2, context3.Response);

        // Allow connections to be established
        await Task.Delay(100);

        // Use the new RoomEventType enum and PlayerJoinEventData DTO
        var eventType = RoomEventType.PlayerJoin;
        var eventData = new PlayerJoinEventData
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "John Doe",
        };
        var expectedPayload =
            $"event: {eventType.ToString().ToSnakeCase()}\ndata: {JsonSerializer.Serialize(eventData, _jsonOptions)}\n\n";

        // Act
        await sseService.BroadcastEventAsync(roomId1, eventType, eventData);

        // Assert: Verify clients in room 1 received the event
        stream1.Position = 0;
        using var reader1 = new StreamReader(stream1, Encoding.UTF8, leaveOpen: true);
        await reader1.ReadLineAsync(); // Skip ": connected"
        var payload1 = await reader1.ReadToEndAsync();
        Assert.Equal(expectedPayload, payload1);

        stream2.Position = 0;
        using var reader2 = new StreamReader(stream2, Encoding.UTF8, leaveOpen: true);
        await reader2.ReadLineAsync(); // Skip ": connected"
        var payload2 = await reader2.ReadToEndAsync();
        Assert.Equal(expectedPayload, payload2);

        // Assert: Verify client in room 2 did NOT receive the event
        stream3.Position = 0;
        using var reader3 = new StreamReader(stream3, Encoding.UTF8, leaveOpen: true);
        await reader3.ReadLineAsync(); // Skip ": connected"
        var payload3 = await reader3.ReadToEndAsync();
        Assert.Empty(payload3);

        // Clean up
        cts1.Cancel();
        cts2.Cancel();
        cts3.Cancel();
        await Task.WhenAll(task1, task2, task3);
    }

    [Fact]
    public async Task BroadcastEventAsync_ShouldCleanupDisconnectedClients()
    {
        // Arrange
        var sseService = new RoomSSEService(NullLogger<RoomSSEService>.Instance);
        var roomId = Guid.NewGuid();

        var (context1, cts1, stream1) = CreateTestContext();
        var (context2, cts2, stream2) = CreateTestContext();

        var task1 = sseService.AddConnectionAsync(roomId, context1.Response);
        var task2 = sseService.AddConnectionAsync(roomId, context2.Response);
        await Task.Delay(100); // Allow connections to establish

        // Simulate one client disconnecting by disposing its response stream
        await stream2.DisposeAsync();

        // Use the new RoomEventType enum and MessageEventData DTO
        var eventType1 = RoomEventType.Chat;
        var eventData1 = new ChatEventData { Sender = "Test", Content = "Hello" };
        var expectedPayload1 =
            $"event: {eventType1.ToString().ToSnakeCase()}\ndata: {JsonSerializer.Serialize(eventData1, _jsonOptions)}\n\n";

        // Act: first broadcast
        await sseService.BroadcastEventAsync(roomId, eventType1, eventData1);

        // Assert: The open connection (stream1) received the event
        // Reset position to 0, then read all content
        stream1.Position = 0;
        using (var reader1 = new StreamReader(stream1, Encoding.UTF8, leaveOpen: true))
        {
            // Skip the “: connected” line
            var _ = await reader1.ReadLineAsync();
            var payload1 = await reader1.ReadToEndAsync();
            Assert.Equal(expectedPayload1, payload1);
        }

        // Mark current length (position for next read)
        long afterFirstLength = stream1.Length;

        // Act: second broadcast
        var eventType2 = RoomEventType.Chat;
        var eventData2 = new ChatEventData { Sender = "Test", Content = "Still here?" };
        var expectedPayload2 =
            $"event: {eventType2.ToString().ToSnakeCase()}\ndata: {JsonSerializer.Serialize(eventData2, _jsonOptions)}\n\n";
        await sseService.BroadcastEventAsync(roomId, eventType2, eventData2);

        // Assert: only the open connection receives the second event
        // Set position to the book-marked point so we only read the newly written part
        stream1.Position = afterFirstLength;
        using (var reader2 = new StreamReader(stream1, Encoding.UTF8, leaveOpen: true))
        {
            var payload2 = await reader2.ReadToEndAsync();
            Assert.Equal(expectedPayload2, payload2);
        }

        // Clean up
        cts1.Cancel();
        cts2.Cancel();
        await Task.WhenAll(task1, task2);
    }

    [Fact]
    public async Task BroadcastEventAsync_ShouldDoNothingForRoomWithNoConnections()
    {
        // Arrange
        var sseService = new RoomSSEService(NullLogger<RoomSSEService>.Instance);
        var roomId = Guid.NewGuid(); // A room with no connections

        // Use the new RoomEventType enum and MessageEventData DTO
        var eventType = RoomEventType.Chat;
        var eventData = new ChatEventData { Sender = "Test", Content = "empty" };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            sseService.BroadcastEventAsync(roomId, eventType, eventData)
        );

        // Assert
        Assert.Null(exception);
    }
}
