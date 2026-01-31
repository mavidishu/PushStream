using System.Text;
using System.Text.Json;

namespace PushStream.Core.Formatting;

/// <summary>
/// Default implementation of <see cref="ISseFormatter"/> that formats events
/// according to the Server-Sent Events specification.
/// </summary>
public sealed class SseFormatter : ISseFormatter
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new instance of <see cref="SseFormatter"/> with default JSON options.
    /// </summary>
    public SseFormatter() : this(DefaultJsonOptions)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SseFormatter"/> with custom JSON options.
    /// </summary>
    /// <param name="jsonOptions">Custom JSON serializer options.</param>
    public SseFormatter(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions ?? DefaultJsonOptions;
    }

    /// <inheritdoc />
    public string FormatEvent<T>(string eventName, T payload, string? eventId = null)
    {
        ArgumentNullException.ThrowIfNull(eventName);

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        
        var builder = new StringBuilder();
        
        // Event ID (must come before event/data per SSE spec)
        if (!string.IsNullOrEmpty(eventId))
        {
            builder.Append("id: ");
            builder.Append(eventId);
            builder.Append('\n');
        }
        
        // Event name
        builder.Append("event: ");
        builder.Append(eventName);
        builder.Append('\n');

        // Handle multi-line JSON payloads
        // Each line must be prefixed with "data: "
        var lines = json.Split('\n');
        foreach (var line in lines)
        {
            builder.Append("data: ");
            builder.Append(line);
            builder.Append('\n');
        }

        // Empty line to terminate the event
        builder.Append('\n');

        return builder.ToString();
    }

    /// <inheritdoc />
    public string FormatHeartbeat()
    {
        // SSE comments start with a colon and are ignored by clients
        return ": heartbeat\n\n";
    }
}