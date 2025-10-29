using Project.Api.Services.Interface;

namespace Project.Api.Services;

/// <summary>
/// Background service that ensures SSE connections are closed gracefully on application shutdown.
/// </summary>
public class SSEShutdownService : IHostedService
{
    private readonly IRoomSSEService _roomSSEService;
    private readonly ILogger<SSEShutdownService> _logger;

    public SSEShutdownService(IRoomSSEService roomSSEService, ILogger<SSEShutdownService> logger)
    {
        _roomSSEService = roomSSEService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SSE Shutdown Service started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SSE Shutdown Service stopping - closing all SSE connections");
        await _roomSSEService.CloseAllConnectionsAsync();
        _logger.LogInformation("SSE Shutdown Service stopped");
        return;
    }
}
