using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PushStream.Core.Abstractions;
using PushStream.Redis.Options;
using PushStream.Redis.Services;
using PushStream.Redis.Storage;
using StackExchange.Redis;

namespace PushStream.Redis.DependencyInjection;

/// <summary>
/// Extension methods for registering PushStream Redis services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis-backed connection store to PushStream.
    /// This replaces the default in-memory connection store with a Redis implementation
    /// that enables horizontal scaling across multiple server instances.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure Redis options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method should be called AFTER <c>AddPushStream()</c>.
    /// 
    /// Example usage:
    /// <code>
    /// builder.Services.AddPushStream();
    /// builder.Services.AddPushStreamRedis(options =>
    /// {
    ///     options.ConnectionString = "localhost:6379";
    ///     options.KeyPrefix = "myapp:pushstream";
    /// });
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if configure is null.</exception>
    public static IServiceCollection AddPushStreamRedis(
        this IServiceCollection services,
        Action<RedisConnectionStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Configure options
        services.Configure(configure);

        // Validate options on startup
        services.AddSingleton<IValidateOptions<RedisConnectionStoreOptions>, 
            RedisConnectionStoreOptionsValidator>();

        // Register Redis connection multiplexer as singleton
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisConnectionStoreOptions>>().Value;
            
            var configOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configOptions.AbortOnConnectFail = false; // Allow retry on connection failure
            
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Register RedisConnectionStore as concrete type (needed by TTL service)
        services.AddSingleton<RedisConnectionStore>();

        // Replace IConnectionStore with Redis implementation
        // Using Replace to override the InMemoryConnectionStore registered by AddPushStream()
        services.Replace(ServiceDescriptor.Singleton<IConnectionStore>(sp =>
            sp.GetRequiredService<RedisConnectionStore>()));

        // Register TTL refresh background service
        services.AddHostedService<RedisConnectionTtlService>();

        return services;
    }

    /// <summary>
    /// Adds Redis-backed connection store with a pre-configured connection multiplexer.
    /// Use this overload when you already have a Redis connection to reuse.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionMultiplexer">The existing Redis connection multiplexer.</param>
    /// <param name="configure">Optional action to configure Redis options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPushStreamRedis(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        Action<RedisConnectionStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        // Configure options with defaults if no configure action provided
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<RedisConnectionStoreOptions>(_ => { });
        }

        // Use the provided connection multiplexer
        services.AddSingleton(connectionMultiplexer);

        // Register RedisConnectionStore as concrete type (needed by TTL service)
        services.AddSingleton<RedisConnectionStore>();

        // Replace IConnectionStore with Redis implementation
        services.Replace(ServiceDescriptor.Singleton<IConnectionStore>(sp =>
            sp.GetRequiredService<RedisConnectionStore>()));

        // Register TTL refresh background service
        services.AddHostedService<RedisConnectionTtlService>();

        return services;
    }
}

/// <summary>
/// Validates <see cref="RedisConnectionStoreOptions"/> on application startup.
/// </summary>
internal sealed class RedisConnectionStoreOptionsValidator 
    : IValidateOptions<RedisConnectionStoreOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisConnectionStoreOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
