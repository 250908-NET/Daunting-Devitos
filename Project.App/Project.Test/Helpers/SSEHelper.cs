using System.Text;

namespace Project.Test.Helpers;

public static class SSEHelper
{
    /// <summary>
    /// Helper to open an SSE connection and return the StreamReader.
    /// </summary>
    public static async Task<(StreamReader reader, CancellationTokenSource cts)> OpenSseConnection(
        this HttpClient client,
        Guid roomId
    )
    {
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
        return (reader, cts);
    }
}
