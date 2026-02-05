namespace PushStream.Redis.Options;

/// <summary>
/// Configuration options for the Redis connectionn store.
/// </summary>
public class RedisConnectionStoreOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    /// <example> localhost:6379 </example>
    /// <example> redis-server:6379,password=secret,ssl=true </example>
    public string ConnectionString {get; set;} = string.Empty;

    /// <summary>
    /// Gets or sets the key prefix for all PushStream keys in Redis.
    /// Use this to namespace your keys when sharing a Redis instance.
    /// Default : "pushstream"
    /// </summary>
    public string KeyPrefix { get; set; } = "pushstream";

    /// <summary>
    /// Gets or sets the TTL (time-to-live) for connection in Redis.
    /// Connections that don't refresh within this time are considered dead
    /// and will be automatically removed by Redis.
    /// Default : 60 seconds
    /// </summary>
    /// <remarks>
    /// This should be greater than <see cref="TtlRefreshInterval"/> to prevent
    /// premature connection expiration.
    /// </remarks>
    public TimeSpan ConnectionTtl {get; set;} = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how often to refresh connection TTL in Redis.
    /// This should be less than <see cref="ConnectionTtl"/> to prevent expiration.
    /// Default : 30 seconds
    /// </summary>
    public TimeSpan TtlRefreshInterval {get; set;} = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
    public void Validates()
    {
        if(string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("Redis connection string is required.", nameof(ConnectionString));
        }

        if(string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new ArgumentException("Key prefix cannot be empty.", nameof(KeyPrefix));
        }

        if(ConnectionTtl <= TimeSpan.Zero)
        {
            throw new ArgumentException("Connection TTL must be positive.", nameof(ConnectionTtl));
        }

        if(TtlRefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("TTL refresh interval must be positive.", nameof(TtlRefreshInterval));
        }

        if(TtlRefreshInterval >= ConnectionTtl)
        {
            throw new ArgumentException("TTL refresh interval must be less than connection TTL to prevent expiration.", nameof(TtlRefreshInterval));
        }
    }
}