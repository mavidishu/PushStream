using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushStream.Core.Abstractions;
using PushStream.Redis.Options;
using StackExchange.Redis;

namespace PushStream.Redis.Storage;

/// <summary>
/// Redis-based implementation of <see cref="IConnectionStore"/> for horizontal scaling.
/// Stores connection metadata in Redis while maintaining local references for writing.
/// </summary>
/// <remarks>
/// Each server instance maintains its own local dictionary of connections it owns.
/// Redis is used to:
/// - Track connection metadata across all servers
/// - Provide accurate global connection counts
/// - Enable connection cleanup via TTL expiration
/// 
/// Note: This store does NOT enable cross-server event publishing.
/// For that, use the Message Backplane ( Future Feature )
/// </remarks>
public sealed class RedisConnectionStore: IConnectionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisConnectionStoreOptions _options;
    private readonly ILogger<RedisConnectionStore> _logger;
    private readonly string _serverId;

    /// <summary>
    /// Local connections owned by this server instance.
    /// Only this server can write to these connections.
    /// </summary>
    private readonly ConcurrentDictionary<string, IClientConnection> _localConnections = new();

    /// <summary>
    /// Gets the unique indentifier for this server instance.
    /// </summary>
    public string ServerId => _serverId;

    /// <summary>
    /// Gets the connection IDs of all local connections.
    /// Used by the TTL refresh service.
    /// </summary>
    public IEnumerable<string> LocalConnectionIds => _localConnections.Keys;

    /// <summary>
    /// Creates a new instance of <see cref="RedisConnectionStore"/>.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RedisConnectionStore(
        IConnectionMultiplexr redis,
        IOption<RedisConnectionStoreOptions> options,
        ILogger<RedisConnectionStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serverId = Guid.NewGuid().ToString("N"); 

        _logger.LogInformation("RedisConnectionStore initialized. ServerId: {ServerId}, KeyPrefix: {KeyPrefix}, _serverId, _options.KeyPrefix");
    }

    //<inheritdoc />
    public async Task AddAsync(IClientConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var db = _redis.GetDatabase();
        var connectionKey = GetConnectionKey(connection.ConnectionId);
        var serverSetkey = GetServerConnectionKey();
        var statsKey = GetStatsKey();

        try
        {
            var transaction = db.CreateTransaction();

            // Store connection metadata in Redis hash
            _ = transaction.HashSetAsync(connectionKey, new HashEntry[]
            {
                new("serverId", _serverId),
                new("clientId", connection.ClientId),
                new("connectionId", connection.ConnectionId),
                new("connectedAt", DateTimeOffset.UtcNow.ToString("O")),
                new("lastEventId", connection.LastEventId ?? string.Empty)
            });

            // Set TTL on connection
            _ = transaction.KeyExpireAsync(connectionKey, _options.ConnectionTtl);

            // Add to server's connection set
            _ = transaction.SetAddAsync(serverSetkey, connection.ConnectionId);
            _ = transaction.KeyExpireAsync(serverSetkey, _options.ConnectionTtl);

            // Increment global connection count
            _ = transaction.StringIncrementAsync(statsKey);

            var success = await transaction.ExecuteAsync();
            if (success)
            {
                // Store locally for writing
                _localConnections.TryAdd(connection.ConnectionId, connection);

                _logger.LogDebug(
                    "Connection added to Redis. ConnectionId: {ConnectionId}, ClientId: {ClientId}, ServerId: {ServerId}",
                    connection.ConnectionId, connection.ClientId, _serverId);
            }
            else
            {
                _logger.LogWarning(
                    "Redis transaction failed when adding connection {ConnectionId}",
                    connection.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error adding connection {ConnectionId} to Redis",
                connection.ConnectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        var db = _redis.GetDatabase();
        var connectionKey = GetConnectionKey(connectionId);
        var serverSetKey = GetServerConnectionKey();
        var statsKey = GetStatsKey();

        try
        {
            var transaction = db.CreateTransaction();

            // Delete connection metadata
            _ = transaction.KeyDeleteAsync(connectionKey);
            // Remove from server's connection set
            _ = transaction.SetRemoveAsync(serverSetKey, connectionId);
            // Decrement global connection count (with floor at 0)
            _ = transaction.StringDecrementAsync(statsKey);

            await transaction.ExecuteAsync();

            // Remove from local dictionary
            _localConnections.TryRemove(connectionId, out var removed);

            _logger.LogDebug(
                "Connection removed from Redis. ConnectionId: {ConnectionId}, ServerId: {ServerId}",
                connectionId, _serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error removing connection {ConnectionId} from Redis",
                connectionId);
            
            // Still try to remove locally even if Redis fails
            _localConnections.TryRemove(connectionId, out _);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns only local connections owned by this server instance.
    /// Each server can only write to its own connections.
    /// </remarks>
    public Task<IEnumerable<IClientConnection>> GetAllAsync()
    {
        // Return snapshot of local connections
        var connections = _localConnections.Values.ToList();
        return Task.FromResult<IEnumerable<IClientConnection>>(connections);
    }


    /// <inheritdoc />
    /// <remarks>
    /// Returns only local connections matching the client ID.
    /// </remarks>
    public Task<IEnumerable<IClientConnection>> GetByClientIdAsync(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        var connections = _localConnections.Values
            .Where(c => c.ClientId == clientId)
            .ToList();

        return Task.FromResult<IEnumerable<IClientConnection>>(connections);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the global connection count from Redis, which is accurate
    /// across all server instances.
    /// </remarks>
    public async Task<int> GetCountAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var statsKey = GetStatsKey();
            var count = await db.StringGetAsync(statsKey);

            if (count.TryParse(out long value))
            {
                // Ensure non-negative (could be negative if decrements happened without increments)
                return (int)Math.Max(0, value);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection count from Redis");
            
            // Fall back to local count on Redis error
            return _localConnections.Count;
        }
    }

    /// <summary>
    /// Refreshes the TTL for all local connections.
    /// Called by the TTL refresh background service.
    /// </summary>
    public async Task RefreshTtlsAsync()
    {
        if (_localConnections.IsEmpty)
        {
            return;
        }

        var db = _redis.GetDatabase();
        var serverSetKey = GetServerConnectionsKey();
        var tasks = new List<Task>();

        // Refresh TTL for each local connection
        foreach (var connectionId in _localConnections.Keys)
        {
            var connectionKey = GetConnectionKey(connectionId);
            tasks.Add(db.KeyExpireAsync(connectionKey, _options.ConnectionTtl));
        }

        // Refresh TTL for server's connection set
        tasks.Add(db.KeyExpireAsync(serverSetKey, _options.ConnectionTtl));

        try
        {
            await Task.WhenAll(tasks);

            _logger.LogTrace(
                "Refreshed TTL for {ConnectionCount} connections on server {ServerId}",
                _localConnections.Count, _serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error refreshing TTLs for connections on server {ServerId}",
                _serverId);
        }
    }

    /// <summary>
    /// Gets the local connection count for this server instance.
    /// </summary>
    public int GetLocalCount() => _localConnections.Count;

    #region Key Generation

    private string GetConnectionKey(string connectionId) =>
        $"{_options.KeyPrefix}:connections:{connectionId}";

    private string GetServerConnectionsKey() =>
        $"{_options.KeyPrefix}:servers:{_serverId}:connections";

    private string GetStatsKey() =>
        $"{_options.KeyPrefix}:stats:total_connections";

    #endregion
}