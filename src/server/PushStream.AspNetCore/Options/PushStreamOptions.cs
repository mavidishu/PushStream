using Microsoft.AspNetCore.Http;

namespace PushStream.AspNetCore.Options;

/// <summary>
/// Configuration options for PushStream SSE services.
/// </summary>
public class PushStreamOptions
{
    /// <summary>
    /// Gets or sets the interval between heartbeat messages.
    /// Heartbeats keep connections alive and help detect disconnected clients.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the default client identifier resolver.
    /// This resolver is used when no endpoint-specific resolver is provided.
    /// If null, a unique connection ID is used as the client identifier.
    /// </summary>
    /// <remarks>
    /// The resolver receives the HttpContext and should return a string identifier
    /// for the client (e.g., user ID, session ID). Return null to reject the connection.
    /// </remarks>
    public Func<HttpContext, string?>? ClientIdResolver { get; set; }

    /// <summary>
    /// Gets or sets whether to send an initial heartbeat immediately upon connection.
    /// This can help verify the connection is working. Default is true.
    /// </summary>
    public bool SendInitialHeartbeat { get; set; } = true;
}