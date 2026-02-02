namespace PushStream.Core.Formatting;

/// <summary>
/// Formats events and messages according to the Server-Sent Events specification.
/// </summary>
public interface ISseFormatter
{
    /// <summary>
    /// Formats an event with a name and payload into SSE protocol format.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="eventName">The event name (e.g., "task.progress").</param>
    /// <param name="payload">The event payload to serialize as JSON.</param>
    /// <param name="eventId">Optional event ID for client reconnection support.</param>
    /// <returns>SSE-formatted string ready to write to the stream.</returns>
    string FormatEvent<T>(string eventName, T payload, string? eventId = null);

    /// <summary>
    /// Formats a heartbeat comment to keep the connection alive.
    /// </summary>
    /// <returns>SSE comment string for heartbeat.</returns>
    string FormatHeartbeat();

    /// <summary>
    /// Formats a retry interval directive for client reconnection.
    /// The retry value tells browsers how long to wait before reconnecting.
    /// </summary>
    /// <param name="interval">The retry interval to send to clients.</param>
    /// <returns>SSE retry directive string.</returns>
    string FormatRetry(TimeSpan interval);
}



