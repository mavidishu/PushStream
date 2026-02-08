using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushStream.Core.Abstractions;
using PushStream.Redis.Options;
using PushStream.Redis.Storage;
using StackExchange.Redis;

namespace PushStream.Redis.Backplane;

/// <summary>
/// Redis pub/sub implementation of <see cref="IBackplane"/> for cross-server message distribution.
/// </summary>
public sealed class RedisBackplane : IBackplane
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    private readonly RedisBackplaneOptions _options;
    private readonly ILogger<RedisBackplane> _logger;

    /// <inheritdoc />
    public string ServerId { get; }

    private string BroadcastChannel => $"{_options.ChannelPrefix}:broadcast";
    private string TargetedChannelPrefix => $"{_options.ChannelPrefix}:server:";

    /// <summary>
    /// Creates a new instance of <see cref="RedisBackplane"/>.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="options">Backplane configuration options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="connectionStore">Optional Redis connection store to reuse ServerId when both are used.</param>
    public RedisBackplane(
        IConnectionMultiplexer redis,
        IOptions<RedisBackplaneOptions> options,
        ILogger<RedisBackplane> logger,
        RedisConnectionStore? connectionStore = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriber = redis.GetSubscriber();
        ServerId = connectionStore?.ServerId ?? Guid.NewGuid().ToString("N");
    }

    /// <inheritdoc />
    public async Task PublishAsync(BackplaneMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, _options.SerializerOptions);
            await _subscriber.PublishAsync(RedisChannel.Literal(BroadcastChannel), json).WaitAsync(cancellationToken);

            _logger.LogTrace("Published backplane message: {EventName}", message.EventName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to backplane: {EventName}", message.EventName);
            // Don't throw - local delivery already succeeded (AC-4)
        }
    }

    /// <inheritdoc />
    public async Task PublishToServerAsync(string serverId, BackplaneMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverId);

        try
        {
            var json = JsonSerializer.Serialize(message, _options.SerializerOptions);
            var channel = $"{TargetedChannelPrefix}{serverId}";
            await _subscriber.PublishAsync(RedisChannel.Literal(channel), json).WaitAsync(cancellationToken);

            _logger.LogTrace(
                "Published backplane message to server {ServerId}: {EventName}",
                serverId,
                message.EventName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish to backplane for server {ServerId}: {EventName}",
                serverId,
                message.EventName);
        }
    }

    /// <inheritdoc />
    public Task SubscribeAsync(Func<BackplaneMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        // Subscribe to broadcast channel
        _subscriber.Subscribe(RedisChannel.Literal(BroadcastChannel), (channel, value) =>
        {
            _ = HandleMessageAsync(value, handler);
        });

        // Subscribe to this server's targeted channel
        var targetedChannel = $"{TargetedChannelPrefix}{ServerId}";
        _subscriber.Subscribe(RedisChannel.Literal(targetedChannel), (channel, value) =>
        {
            _ = HandleMessageAsync(value, handler);
        });

        _logger.LogInformation("Subscribed to backplane. ServerId: {ServerId}", ServerId);

        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(RedisValue value, Func<BackplaneMessage, Task> handler)
    {
        try
        {
            var message = JsonSerializer.Deserialize<BackplaneMessage>(value.ToString(), _options.SerializerOptions);

            if (message == null)
            {
                return;
            }

            // Skip messages from self (already delivered locally) - AC-5
            if (string.Equals(message.SourceServerId, ServerId, StringComparison.Ordinal))
            {
                _logger.LogTrace("Ignoring own backplane message");
                return;
            }

            await handler(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling backplane message");
        }
    }
}
