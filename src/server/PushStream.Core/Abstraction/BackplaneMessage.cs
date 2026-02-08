namespace PushStream.Core.Abstractions;

/// <summary>
/// Message envelope for cross-server distribution via the backplane.
/// </summary>
public class BackplaneMessage
{
    /// <summary>
    /// Server that originated the message.
    /// </summary>
    public required string SourceServerId { get; init; }

    /// <summary>
    /// Target client ID (null for broadcast).
    /// </summary>
    public string? TargetClientId { get; init; }

    /// <summary>
    /// SSE event name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Serialized payload (JSON).
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Optional event ID.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// Message timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
