using System.Collections.Concurrent;
using System.Text.Json;
using Project.Api.Models.Games;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Enums;
using Project.Api.Utilities.Extensions;

namespace Project.Api.Services;

public class RoomSSEService(ILogger<RoomSSEService> logger) : IRoomSSEService
{
    private readonly ILogger<RoomSSEService> _logger = logger;

    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<string, StreamWriter>
    > _connections = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // use js convention instead of C#
    };

    public async Task AddConnectionAsync(Guid roomId, HttpResponse response)
    {
        response.Headers.Append("Content-Type", "text/event-stream");
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");

        string connectionId = Guid.CreateVersion7().ToString(); // assign unique connection id
        StreamWriter writer = new(response.Body);

        // add connection to room
        ConcurrentDictionary<string, StreamWriter> connections = _connections.GetOrAdd(
            roomId,
            _ => new()
        );
        connections.TryAdd(connectionId, writer);

        try
        {
            // confirm connection
            await writer.WriteLineAsync(": connected");
            await writer.FlushAsync();

            // wait for client to close connection (abort request)
            await Task.Delay(Timeout.Infinite, response.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // connection closed
            // expected case, do not throw
        }
        finally
        {
            // clean up connection and remove from room
            if (connections.TryRemove(connectionId, out StreamWriter? removedWriter))
            {
                await removedWriter.DisposeAsync();
            }
        }
    }

    public async Task BroadcastEventAsync(Guid roomId, RoomEventType eventType, IRoomEventData data)
    {
        // check if room exists in connections
        if (
            !_connections.TryGetValue(
                roomId,
                out ConcurrentDictionary<string, StreamWriter>? connections
            )
        )
        {
            return;
        }

        string serializedData = JsonSerializer.Serialize(data, data.GetType(), _jsonOptions);
        string eventName = eventType.ToString().ToSnakeCase(); // use snake_case event type

        _logger.LogInformation(
            "[SSE] Broadcasting to room {roomId}: event={eventName}, data length={serializedData.Length}",
            roomId,
            eventName,
            serializedData.Length
        );
        _logger.LogInformation(
            "[SSE] Data preview: {serializedData}",
            serializedData[..Math.Min(256, serializedData.Length)]
        );

        string eventPayload = $"event: {eventName}\ndata: {serializedData}\n\n";

        List<string> closedConnections = [];
        foreach ((string connectionId, StreamWriter writer) in connections)
        {
            try
            {
                await writer.WriteAsync(eventPayload); // assume payload already includes terminating \n\n
                await writer.FlushAsync();
            }
            catch (OperationCanceledException)
            {
                // operation was canceled
                closedConnections.Add(connectionId);
            }
            catch (IOException)
            {
                // broken pipe
                closedConnections.Add(connectionId);
            }
            catch (ObjectDisposedException)
            {
                // writer was disposed
                closedConnections.Add(connectionId);
            }
        }

        // clean up any closed connections
        foreach (string connectionId in closedConnections)
        {
            if (connections.TryRemove(connectionId, out StreamWriter? removedWriter))
            {
                await removedWriter.DisposeAsync();
            }
        }
    }

    public async Task CloseAllConnectionsAsync()
    {
        Console.WriteLine("[SSE] Closing all SSE connections for graceful shutdown...");

        foreach (var roomConnections in _connections.Values)
        {
            foreach (var writer in roomConnections.Values)
            {
                try
                {
                    await writer.DisposeAsync();
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            }
            roomConnections.Clear();
        }
        _connections.Clear();

        Console.WriteLine("[SSE] All SSE connections closed.");
    }

    public void Dispose()
    {
        CloseAllConnectionsAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
