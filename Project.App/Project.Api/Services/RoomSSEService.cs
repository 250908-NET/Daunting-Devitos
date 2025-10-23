using System.Collections.Concurrent;
using System.Text.Json;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

public class RoomSSEService : IRoomSSEService
{
    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<string, StreamWriter>
    > _connections = new();

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

    public async Task BroadcastEventAsync(Guid roomId, string eventName, object data)
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

        // Use camelCase naming to match ASP.NET Core controller responses
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        string serializedData = JsonSerializer.Serialize(data, options);
        Console.WriteLine(
            $"[SSE] Broadcasting to room {roomId}: event={eventName}, data length={serializedData.Length}"
        );
        Console.WriteLine(
            $"[SSE] Data preview: {serializedData.Substring(0, Math.Min(200, serializedData.Length))}..."
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
    }
}
