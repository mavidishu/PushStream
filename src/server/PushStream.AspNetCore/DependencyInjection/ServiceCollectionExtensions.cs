using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PushStream.AspNetCore.Options;
using PushStream.AspNetCore.Services;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;
using PushStream.Core.Publishing;
using PushStream.Core.Storage;

namespace PushStream.AspNetCore.DependencyInjection;

/// <summary>
/// Extension methods for registering PushStream services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PushStream SSE services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPushStream(
        this IServiceCollection services,
        Action<PushStreamOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register options
        var optionsBuilder = services.AddOptions<PushStreamOptions>();
        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        // Register core services as singletons (shared state)
        // Using TryAdd to prevent duplicate registrations
        services.TryAddSingleton<IConnectionStore, InMemoryConnectionStore>();
        services.TryAddSingleton<ISseFormatter, SseFormatter>();
        services.TryAddSingleton<IEventPublisher, EventPublisher>();

        // Register heartbeat background service
        services.AddHostedService<HeartbeatBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds PushStream SSE services with custom connection store.
    /// </summary>
    /// <typeparam name="TConnectionStore">The connection store implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPushStream<TConnectionStore>(
        this IServiceCollection services,
        Action<PushStreamOptions>? configure = null)
        where TConnectionStore : class, IConnectionStore
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register options
        var optionsBuilder = services.AddOptions<PushStreamOptions>();
        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        // Register custom connection store
        services.TryAddSingleton<IConnectionStore, TConnectionStore>();
        services.TryAddSingleton<ISseFormatter, SseFormatter>();
        services.TryAddSingleton<IEventPublisher, EventPublisher>();

        // Register heartbeat background service
        services.AddHostedService<HeartbeatBackgroundService>();

        return services;
    }
}