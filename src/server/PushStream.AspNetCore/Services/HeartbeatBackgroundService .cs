using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushStream.AspNetCore.Options;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;

namespace PushStream.AspNetCore.Services;

/// <summary>
/// Background service that sends periodic heartbeats to all connected clients.
/// Heartbeats keep connections alive and help detect disconnected clients.
/// </summary>
public sealed class HeartbeatBackgroundService : BackgroundService
{
    private readonly IConnectionStore _connectionStore;
    private readonly ISseFormatter _formatter;
    private readonly IOptions<PushStreamOptions> _options;
    private readonly ILogger<HeartbeatBackgroundService> _logger;

    /// <summary>
    /// Creates a new heartbeat background service.
    /// </summary>
    public HeartbeatBackgroundService(
        IConnectionStore connectionStore,
        ISseFormatter formatter,
        IOptions<PushStreamOptions> options,
        ILogger<HeartbeatBackgroundService> logger)
    {
        _connectionStore = connectionStore ?? throw new ArgumentNullException(nameof(connectionStore));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Heartbeat service started with interval {Interval}",
            _options.Value.HeartbeatInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Value.HeartbeatInterval, stoppingToken);
                await SendHeartbeatsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown, expected
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in heartbeat loop");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Heartbeat service stopped");
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        var connections = await _connectionStore.GetAllAsync();
        var heartbeat = _formatter.FormatHeartbeat();
        var deadConnections = new List<string>();

        foreach (var connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!connection.IsConnected)
                {
                    deadConnections.Add(connection.ConnectionId);
                    continue;
                }

                await connection.WriteAsync(heartbeat, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Connection was cancelled, mark for removal
                deadConnections.Add(connection.ConnectionId);
            }
            catch (Exception ex)
            {
                // Connection is dead, mark for removal
                _logger.LogDebug(
                    ex,
                    "Failed to send heartbeat to connection {ConnectionId}, marking for removal",
                    connection.ConnectionId);
                deadConnections.Add(connection.ConnectionId);
            }
        }

        // Clean up dead connections
        foreach (var connectionId in deadConnections)
        {
            try
            {
                await _connectionStore.RemoveAsync(connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove dead connection {ConnectionId}", connectionId);
            }
        }

        if (deadConnections.Count > 0)
        {
            _logger.LogDebug("Removed {Count} dead connections during heartbeat", deadConnections.Count);
        }
    }
}