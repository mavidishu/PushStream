using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;

namespace PushStream.Core.Publishing;

/// <summary>
/// Default implementation of <see cref="IEventPublisher"/> that publishes
/// events to connected clients via the connection store.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IConnectionStore _connectionStore;
    private readonly ISseFormatter _formatter;

    /// <summary>
    /// Creates a new instance of <see cref="EventPublisher"/>.
    /// </summary>
    /// <param name="connectionStore">The connection store to retrieve clients from.</param>
    /// <param name="formatter">The SSE formatter for event serialization.</param>
    public EventPublisher(IConnectionStore connectionStore, ISseFormatter formatter)
    {
        _connectionStore = connectionStore ?? throw new ArgumentNullException(nameof(connectionStore));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(string eventName, T payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventName);

        var formattedEvent = _formatter.FormatEvent(eventName, payload);
        var connections = await _connectionStore.GetAllAsync();

        await WriteToConnectionsAsync(connections, formattedEvent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishToAsync<T>(string clientId, string eventName, T payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(eventName);

        var formattedEvent = _formatter.FormatEvent(eventName, payload);
        var connections = await _connectionStore.GetByClientIdAsync(clientId);

        await WriteToConnectionsAsync(connections, formattedEvent, cancellationToken);
    }

    private static async Task WriteToConnectionsAsync(
        IEnumerable<IClientConnection> connections,
        string data,
        CancellationToken cancellationToken)
    {
        // Write to all connections concurrently
        var writeTasks = new List<Task>();

        foreach (var connection in connections)
        {
            // Skip disconnected clients
            if (!connection.IsConnected)
            {
                continue;
            }

            writeTasks.Add(WriteToConnectionSafeAsync(connection, data, cancellationToken));
        }

        // Wait for all writes to complete
        // We use WhenAll to ensure all writes are attempted even if some fail
        if (writeTasks.Count > 0)
        {
            await Task.WhenAll(writeTasks);
        }
    }

    private static async Task WriteToConnectionSafeAsync(
        IClientConnection connection,
        string data,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(data, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, rethrow
            throw;
        }
        catch
        {
            // Swallow other exceptions from individual connections
            // The connection is likely already disconnected
            // Cleanup will happen via the connection store
        }
    }
}