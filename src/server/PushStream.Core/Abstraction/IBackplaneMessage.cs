namespace PushStream.Core.Abstractions;

/// <summary>
/// Abstraction for cross-server message distribution.
/// </summary>
public interface IBackplane
{
    /// <summary>
    /// Gets this server's unique identifier.
    /// </summary>
    string ServerId { get; }

    /// <summary>
    /// Publishes a message to all server instances.
    /// </summary>
    Task PublishAsync(BackplaneMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to a specific server instance.
    /// </summary>
    Task PublishToServerAsync(string serverId, BackplaneMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to backplane messages.
    /// </summary>
    Task SubscribeAsync(Func<BackplaneMessage, Task> handler, CancellationToken cancellationToken = default);
}
