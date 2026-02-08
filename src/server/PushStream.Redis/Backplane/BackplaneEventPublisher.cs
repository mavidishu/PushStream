using System.Text.Json;
using Microsoft.Extensions.Logging;
using PushStream.Core.Abstractions;
using PushStream.Core.Publishing;

namespace PushStream.Redis.Backplane;

/// <summary>
/// Wraps the standard EventPublisher to add backplane distribution.
/// Delivers to local connections first, then publishes to the backplane for other servers.
/// </summary>
public sealed class BackplaneEventPublisher : IEventPublisher
{
    private readonly EventPublisher _localPublisher;
    private readonly IBackplane _backplane;
    private readonly ILogger<BackplaneEventPublisher> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Creates a new instance of <see cref="BackplaneEventPublisher"/>.
    /// </summary>
    /// <param name="localPublisher">The local event publisher (delivers to this server's connections only).</param>
    /// <param name="backplane">The backplane for cross-server distribution.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="serializerOptions">Optional JSON options for payload serialization; uses default camelCase if null.</param>
    public BackplaneEventPublisher(
        EventPublisher localPublisher,
        IBackplane backplane,
        ILogger<BackplaneEventPublisher> logger,
        JsonSerializerOptions? serializerOptions = null)
    {
        _localPublisher = localPublisher ?? throw new ArgumentNullException(nameof(localPublisher));
        _backplane = backplane ?? throw new ArgumentNullException(nameof(backplane));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    /// <inheritdoc />
    public Task PublishAsync<T>(string eventName, T payload, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventName, payload, eventId: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(string eventName, T payload, string? eventId, CancellationToken cancellationToken = default)
    {
        // 1. Local delivery first (immediate)
        await _localPublisher.PublishAsync(eventName, payload, eventId, cancellationToken);

        // 2. Backplane for other servers (fire-and-forget with error logging)
        await PublishToBackplaneAsync(eventName, payload, eventId, targetClientId: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishToAsync<T>(string clientId, string eventName, T payload, CancellationToken cancellationToken = default)
    {
        return PublishToAsync(clientId, eventName, payload, eventId: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishToAsync<T>(string clientId, string eventName, T payload, string? eventId, CancellationToken cancellationToken = default)
    {
        // 1. Try local delivery first
        await _localPublisher.PublishToAsync(clientId, eventName, payload, eventId, cancellationToken);

        // 2. Publish to backplane (other servers will check if they have this client)
        await PublishToBackplaneAsync(eventName, payload, eventId, targetClientId: clientId, cancellationToken);
    }

    private async Task PublishToBackplaneAsync<T>(
        string eventName,
        T payload,
        string? eventId,
        string? targetClientId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload, _serializerOptions);
            var message = new BackplaneMessage
            {
                SourceServerId = _backplane.ServerId,
                TargetClientId = targetClientId,
                EventName = eventName,
                Payload = payloadJson,
                EventId = eventId,
                Timestamp = DateTimeOffset.UtcNow
            };

            await _backplane.PublishAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventName} to backplane", eventName);
            // Don't throw - local delivery already succeeded
        }
    }
}
