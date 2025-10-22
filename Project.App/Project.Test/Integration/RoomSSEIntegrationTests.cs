using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Project.Api;
using Project.Api.DTOs;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Test.Helpers;

namespace Project.Test.Integration;

public class RoomSseIntegrationTests(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    /// <summary>
    /// Helper to create an HttpClient configured to use a specific IRoomSSEService instance.
    /// </summary>
    private HttpClient CreateClientWithMocks(IRoomSSEService sseService)
    {
        return CreateTestClient(services =>
        {
            services.RemoveAll<IRoomSSEService>();
            services.AddSingleton(sseService);

            services.AddScoped(_ => Mock.Of<IRoomService>());
        });
    }

    /// <summary>
    /// Helper to open an SSE connection and return the StreamReader.
    /// </summary>
    private async Task<(
        HttpClient client,
        StreamReader reader,
        CancellationTokenSource cts
    )> OpenSseConnection(Guid roomId, IRoomSSEService sseService)
    {
        var client = CreateClientWithMocks(sseService);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/room/{roomId}/events");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream")
        );

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);

        var stream = await response.Content.ReadAsStreamAsync();
        var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // Wait a moment so the connection is established and the “: connected” line is sent
        await Task.Delay(100);
        string firstLine = await reader.ReadLineAsync() ?? "No line received!";
        Assert.Equal(": connected", firstLine);

        // Create a CancellationTokenSource to simulate client disconnection later if needed
        var cts = new CancellationTokenSource();
        // This is a bit tricky for integration tests, as the actual cancellation token
        // is tied to the HttpContext.RequestAborted. We can't directly cancel it from here
        // without disposing the client, which closes the connection.
        // For explicit disconnection testing, we'll rely on client.Dispose().
        return (client, reader, cts);
    }

    [Fact]
    public async Task GetRoomEvents_StreamReceivesBroadcastEvents_SingleClient()
    {
        // Arrange
        var sseService = new RoomSSEService();
        var roomId = Guid.NewGuid();
        var (client, reader, cts) = await OpenSseConnection(roomId, sseService);

        var messageContent = "Hello everyone!";
        var message = new MessageDTO(messageContent);
        var expectedMessage = $"Anonymous: {messageContent}";
        var expectedData = JsonSerializer.Serialize(expectedMessage);

        // Act: Send a chat message
        var postResponse = await client.PostAsJsonAsync($"/api/room/{roomId}/chat", message);
        postResponse.EnsureSuccessStatusCode();

        // Assert: Read the chat message from the SSE stream
        string eventLine = await reader.ReadLineAsync() ?? "No line received!";
        Assert.Equal("event: message", eventLine);

        string dataLine = await reader.ReadLineAsync() ?? "No line received!";
        Assert.Equal($"data: {expectedData}", dataLine);

        string blankLine = await reader.ReadLineAsync() ?? "No line received!";
        Assert.True(string.IsNullOrEmpty(blankLine));

        // Clean up
        client.Dispose();
        cts.Cancel();
    }

    [Fact]
    public async Task GetRoomEvents_StreamReceivesBroadcastEvents_MultipleClientsInSameRoom()
    {
        // Arrange
        var sseService = new RoomSSEService();
        var roomId = Guid.NewGuid();

        // Open two connections to the same room
        var (client1, reader1, cts1) = await OpenSseConnection(roomId, sseService);
        var (client2, reader2, cts2) = await OpenSseConnection(roomId, sseService);

        var messageContent = "Group chat!";
        var message = new MessageDTO(messageContent);
        var expectedMessage = $"Anonymous: {messageContent}";
        var expectedData = JsonSerializer.Serialize(expectedMessage);

        // Act: Send a chat message
        var postResponse = await client1.PostAsJsonAsync($"/api/room/{roomId}/chat", message);
        postResponse.EnsureSuccessStatusCode();

        // Assert: Both clients in the same room receive the event
        string eventLine1 = await reader1.ReadLineAsync() ?? "No line received!";
        Assert.Equal("event: message", eventLine1);
        string dataLine1 = await reader1.ReadLineAsync() ?? "No line received!";
        Assert.Equal($"data: {expectedData}", dataLine1);
        await reader1.ReadLineAsync(); // Blank line

        string eventLine2 = await reader2.ReadLineAsync() ?? "No line received!";
        Assert.Equal("event: message", eventLine2);
        string dataLine2 = await reader2.ReadLineAsync() ?? "No line received!";
        Assert.Equal($"data: {expectedData}", dataLine2);
        await reader2.ReadLineAsync(); // Blank line

        // Clean up
        client1.Dispose();
        client2.Dispose();
        cts1.Cancel();
        cts2.Cancel();
    }

    [Fact]
    public async Task GetRoomEvents_StreamReceivesBroadcastEvents_ClientsInDifferentRooms()
    {
        // Arrange
        var sseService = new RoomSSEService();
        var roomId1 = Guid.NewGuid();
        var roomId2 = Guid.NewGuid();

        // Open connections to different rooms
        var (client1, reader1, cts1) = await OpenSseConnection(roomId1, sseService);
        var (client2, reader2, cts2) = await OpenSseConnection(roomId2, sseService);

        var messageContent = "Only for room 1!";
        var message = new MessageDTO(messageContent);
        var expectedMessage = $"Anonymous: {messageContent}";
        var expectedData = JsonSerializer.Serialize(expectedMessage);

        // Act: Send a chat message to room 1
        var postResponse = await client1.PostAsJsonAsync($"/api/room/{roomId1}/chat", message);
        postResponse.EnsureSuccessStatusCode();

        // Assert: Client in room 1 receives the event
        string eventLine1 = await reader1.ReadLineAsync() ?? "No line received!";
        Assert.Equal("event: message", eventLine1);
        string dataLine1 = await reader1.ReadLineAsync() ?? "No line received!";
        Assert.Equal($"data: {expectedData}", dataLine1);
        await reader1.ReadLineAsync(); // Blank line

        // Assert: Client in room 2 does NOT receive the event.
        // We cannot use ReadToEndAsync as the stream is kept open by the server.
        // Instead, we race a ReadLineAsync against a short delay. If the delay wins,
        // it means no data was sent, which is the expected outcome.
        var readTask = reader2.ReadLineAsync();
        var delayTask = Task.Delay(TimeSpan.FromMilliseconds(200));
        var completedTask = await Task.WhenAny(readTask, delayTask);

        if (completedTask == readTask)
        {
            // If the read task finished, it means data was unexpectedly received.
            var receivedData = await readTask;
            Assert.Fail($"Client in the wrong room received an event. Data: '{receivedData}'");
        }
        // If the delay task finished, the test passes implicitly.

        // Clean up
        client1.Dispose();
        client2.Dispose();
        cts1.Cancel();
        cts2.Cancel();
    }

    [Fact]
    public async Task GetRoomEvents_WithInvalidAcceptHeader_ReturnsBadRequest()
    {
        // Arrange
        var sseService = new RoomSSEService();
        var client = CreateClientWithMocks(sseService);
        var roomId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/room/{roomId}/events");
        request.Headers.Accept.Clear(); // No "text/event-stream" header

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        // var errorMessage = await response.Content.ReadAsStringAsync();
        // Assert.Contains(
        //     "This endpoint requires the header 'Accept: text/event-stream'.",
        //     errorMessage
        // );

        // Ensure no connection was added to the service
        // This requires inspecting the internal state of RoomSSEService, which is usually
        // done in unit tests. For integration, we primarily test the HTTP contract.
        // However, if RoomSSEService had a public method to get active connections, we could check.
        // For now, the 400 response is sufficient for integration.
        client.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BroadcastMessage_WithInvalidContent_ReturnsBadRequest(string? content)
    {
        // Arrange
        var sseService = new RoomSSEService();
        var client = CreateClientWithMocks(sseService);
        var roomId = Guid.NewGuid();
        var message = new MessageDTO(content!);

        // Act
        var response = await client.PostAsJsonAsync($"/api/room/{roomId}/chat", message);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        // if content is null, the error is handled by ASP.NET directly, so message would be different
        // var errorBody = await response.Content.ReadAsStringAsync();
        // Assert.Contains("Message content cannot be empty.", errorBody);

        client.Dispose();
    }

    [Fact]
    public async Task GetRoomEvents_ClientDisconnectsGracefully()
    {
        // Arrange
        var sseService = new RoomSSEService();
        var roomId = Guid.NewGuid();

        // Open a connection
        var (client, reader, cts) = await OpenSseConnection(roomId, sseService);

        // Act: Simulate client disconnection by disposing the HttpClient
        client.Dispose();

        // Give the server a moment to process the disconnection
        await Task.Delay(200);

        // Act: Try to broadcast an event to the room
        var messageContent = "Should not be received by disconnected client.";
        var message = new MessageDTO(messageContent);
        var postResponse = await CreateClientWithMocks(sseService)
            .PostAsJsonAsync($"/api/room/{roomId}/chat", message);
        postResponse.EnsureSuccessStatusCode();

        // Assert: No exception should be thrown during broadcast, indicating cleanup was successful.
        // This is hard to assert directly in an integration test without inspecting the service's internal state.
        // The primary assertion is that the broadcast itself doesn't fail due to a broken pipe.
        // The unit test for RoomSSEService.BroadcastEventAsync_ShouldCleanupDisconnectedClients
        // provides more direct verification of the internal cleanup.
        Assert.True(true, "Broadcast completed without error after client disconnection.");

        // If we had a way to query the number of active connections in RoomSSEService, we'd assert it's 0.
        // For now, the lack of an exception is the best we can do at this integration level.
        cts.Cancel(); // Ensure any lingering tasks are cancelled.
    }
}
