namespace PushStream.Core.Abstractions;

/// <summary>
/// Publishes events to connected SSE clients.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to all connected clients (broadcast).
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="eventName">The event name (e.g., "task.progress").</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishAsync<T>(string eventName, T payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with an ID to all connected clients (broadcast).
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="eventName">The event name (e.g., "task.progress").</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="eventId">Optional event ID for client reconnection support.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishAsync<T>(string eventName, T payload, string? eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to clients matching a specific client identifier.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="clientId">The target client identifier.</param>
    /// <param name="eventName">The event name (e.g., "task.progress").</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishToAsync<T>(string clientId, string eventName, T payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with an ID to clients matching a specific client identifier.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="clientId">The target client identifier.</param>
    /// <param name="eventName">The event name (e.g., "task.progress").</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="eventId">Optional event ID for client reconnection support.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishToAsync<T>(string clientId, string eventName, T payload, string? eventId, CancellationToken cancellationToken = default);
}