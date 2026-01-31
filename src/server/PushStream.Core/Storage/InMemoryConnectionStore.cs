using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PushStream.Core.Abstractions;

namespace PushStream.Core.Storage;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IConnectionStore"/>.
/// Suitable for single-server deployments.
/// </summary>
public sealed class InMemoryConnectionStore : IConnectionStore
{
    private readonly ConcurrentDictionary<string, IClientConnection> _connections = new();
    private readonly ILogger<InMemoryConnectionStore> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="InMemoryConnectionStore"/>.
    /// </summary>
    /// <param name="logger">The logger for diagnostic output.</param>
    public InMemoryConnectionStore(ILogger<InMemoryConnectionStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task AddAsync(IClientConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        
        if (_connections.TryAdd(connection.ConnectionId, connection))
        {
            _logger.LogDebug(
                "Connection added. ConnectionId: {ConnectionId}, ClientId: {ClientId}, Total: {TotalConnections}",
                connection.ConnectionId,
                connection.ClientId,
                _connections.Count);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        
        if (_connections.TryRemove(connectionId, out var connection))
        {
            _logger.LogDebug(
                "Connection removed. ConnectionId: {ConnectionId}, ClientId: {ClientId}, Total: {TotalConnections}",
                connectionId,
                connection.ClientId,
                _connections.Count);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<IClientConnection>> GetAllAsync()
    {
        // Return a snapshot to avoid enumeration issues during concurrent modifications
        var connections = _connections.Values.ToList();
        return Task.FromResult<IEnumerable<IClientConnection>>(connections);
    }

    /// <inheritdoc />
    public Task<IEnumerable<IClientConnection>> GetByClientIdAsync(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        
        var connections = _connections.Values
            .Where(c => c.ClientId == clientId)
            .ToList();
        
        return Task.FromResult<IEnumerable<IClientConnection>>(connections);
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync()
    {
        return Task.FromResult(_connections.Count);
    }
}