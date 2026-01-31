using Microsoft.Extensions.Logging;
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
    private readonly ILogger<EventPublisher> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="EventPublisher"/>.
    /// </summary>
    /// <param name="connectionStore">The connection store to retrieve clients from.</param>
    /// <param name="formatter">The SSE formatter for event serialization.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public EventPublisher(
        IConnectionStore connectionStore, 
        ISseFormatter formatter,
        ILogger<EventPublisher> logger)
    {
        _connectionStore = connectionStore ?? throw new ArgumentNullException(nameof(connectionStore));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task PublishAsync<T>(string eventName, T payload, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventName, payload, eventId: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(string eventName, T payload, string? eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventName);

        var formattedEvent = _formatter.FormatEvent(eventName, payload, eventId);
        var connections = await _connectionStore.GetAllAsync();
        var connectionList = connections.ToList();

        _logger.LogDebug(
            "Publishing event '{EventName}' to {ConnectionCount} connections",
            eventName,
            connectionList.Count);

        await WriteToConnectionsAsync(connectionList, formattedEvent, eventName, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishToAsync<T>(string clientId, string eventName, T payload, CancellationToken cancellationToken = default)
    {
        return PublishToAsync(clientId, eventName, payload, eventId: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishToAsync<T>(string clientId, string eventName, T payload, string? eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(eventName);

        var formattedEvent = _formatter.FormatEvent(eventName, payload, eventId);
        var connections = await _connectionStore.GetByClientIdAsync(clientId);
        var connectionList = connections.ToList();

        _logger.LogDebug(
            "Publishing event '{EventName}' to client '{ClientId}' ({ConnectionCount} connections)",
            eventName,
            clientId,
            connectionList.Count);

        await WriteToConnectionsAsync(connectionList, formattedEvent, eventName, cancellationToken);
    }

    private async Task WriteToConnectionsAsync(
        IEnumerable<IClientConnection> connections,
        string data,
        string eventName,
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

            writeTasks.Add(WriteToConnectionSafeAsync(connection, data, eventName, cancellationToken));
        }

        // Wait for all writes to complete
        // We use WhenAll to ensure all writes are attempted even if some fail
        if (writeTasks.Count > 0)
        {
            await Task.WhenAll(writeTasks);
        }
    }

    private async Task WriteToConnectionSafeAsync(
        IClientConnection connection,
        string data,
        string eventName,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(data, cancellationToken);
            
            _logger.LogTrace(
                "Event '{EventName}' written to connection {ConnectionId}",
                eventName,
                connection.ConnectionId);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, rethrow
            throw;
        }
        catch (Exception ex)
        {
            // Log the error but continue - connection is likely already disconnected
            // Cleanup will happen via the connection store
            _logger.LogWarning(
                "Failed to write event '{EventName}' to connection {ConnectionId}: {Error}",
                eventName,
                connection.ConnectionId,
                ex.Message);
        }
    }
}