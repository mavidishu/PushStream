namespace PushStream.Core.Abstractions;

/// <summary>
/// Represents a connected client that can receive SSE events.
/// </summary>
public interface IClientConnection
{
    /// <summary>
    /// Gets the unique identifier for this specific connection instance.
    /// Each browser tab or connection gets a unique ConnectionId.
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Gets the logical client identifier (e.g., user ID, session ID).
    /// Multiple connections can share the same ClientId (e.g., multiple tabs).
    /// Used for targeted event publishing.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// Gets a value indicating whether the connection is currently open.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Writes raw SSE-formatted data to the client stream.
    /// </summary>
    /// <param name="data">The SSE-formatted string to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async write operation.</returns>
    Task WriteAsync(string data, CancellationToken cancellationToken = default);
}

