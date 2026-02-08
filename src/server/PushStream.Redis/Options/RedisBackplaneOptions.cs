using System.Text.Json;

namespace PushStream.Redis.Options;

/// <summary>
/// Configuration options for the Redis message backplane.
/// </summary>
public class RedisBackplaneOptions
{
    /// <summary>
    /// Gets or sets the channel prefix for backplane pub/sub channels.
    /// Default: "pushstream:backplane"
    /// </summary>
    /// <remarks>
    /// Channels used: {ChannelPrefix}:broadcast and {ChannelPrefix}:server:{{serverId}}
    /// </remarks>
    public string ChannelPrefix { get; set; } = "pushstream:backplane";

    /// <summary>
    /// Gets or sets the JSON serializer options for backplane message serialization.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
