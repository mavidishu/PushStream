using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushStream.Redis.Options;
using PushStream.Redis.Storage;

namespace PushStream.Redis.Services;

/// <summary>
/// Background service that periodically refreshes TTL for all local connections in Redis.
/// This prevents active connections from being expired by Redis.
/// </summary>
/// <remarks>
/// The service runs at intervals defined by <see cref="RedisConnectionStoreOptions.TtlRefreshInterval"/>.
/// Each refresh updates the TTL for all connections owned by this server instance.
/// </remarks>
public sealed class RedisConnectionTtlService : BackgroundService
{
    private readonly RedisConnectionStore _store;
    private readonly RedisConnectionStoreOptions _options;
    private readonly ILogger<RedisConnectionTtlService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="RedisConnectionTtlService"/>.
    /// </summary>
    /// <param name="store">The Redis connection store to refresh TTLs for.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RedisConnectionTtlService(
        RedisConnectionStore store,
        IOptions<RedisConnectionStoreOptions> options,
        ILogger<RedisConnectionTtlService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Redis TTL refresh service started. RefreshInterval: {RefreshInterval}, ConnectionTtl: {ConnectionTtl}",
            _options.TtlRefreshInterval, _options.ConnectionTtl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.TtlRefreshInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await RefreshTtlsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error in TTL refresh cycle. Will retry in {RefreshInterval}",
                    _options.TtlRefreshInterval);
            }
        }

        _logger.LogInformation("Redis TTL refresh service stopped.");
    }

    private async Task RefreshTtlsAsync(CancellationToken cancellationToken)
    {
        var connectionCount = _store.LocalConnectionIds.Count();

        if (connectionCount == 0)
        {
            _logger.LogTrace("No local connections to refresh TTL for.");
            return;
        }

        _logger.LogTrace(
            "Refreshing TTL for {ConnectionCount} connections on server {ServerId}",
            connectionCount, _store.ServerId);

        await _store.RefreshTtlsAsync();
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Redis TTL refresh service stopping. ServerId: {ServerId}",
            _store.ServerId);

        await base.StopAsync(cancellationToken);
    }
}
