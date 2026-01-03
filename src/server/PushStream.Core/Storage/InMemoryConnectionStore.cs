using System.Collections.Concurrent;
using PushStream.Core.Abstractions;

namespace PushStream.Core.Storage;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IConnectionStore"/>.
/// Suitable for single-server deployments.
/// </summary>
public sealed class InMemoryConnectionStore : IConnectionStore
{
    private readonly ConcurrentDictionary<string, IClientConnection> _connections = new();

    /// <inheritdoc />
    public Task AddAsync(IClientConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connections.TryAdd(connection.ConnectionId, connection);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        _connections.TryRemove(connectionId, out _);
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

