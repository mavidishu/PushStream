using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PushStream.AspNetCore.Connections;
using PushStream.AspNetCore.Options;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;

namespace PushStream.AspNetCore.Routing;

/// <summary>
/// Extension methods for mapping SSE endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an SSE event stream endpoint at the specified pattern.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern (e.g., "/events").</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// app.MapEventStream("/events");
    /// 
    /// // With authorization
    /// app.MapEventStream("/events").RequireAuthorization();
    /// 
    /// // With custom client ID resolver
    /// app.MapEventStream("/events", context => context.User.FindFirst("sub")?.Value);
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapEventStream(
        this IEndpointRouteBuilder endpoints,
        string pattern)
    {
        return MapEventStream(endpoints, pattern, clientIdResolver: null);
    }

    /// <summary>
    /// Maps an SSE event stream endpoint with a custom client identifier resolver.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern (e.g., "/events").</param>
    /// <param name="clientIdResolver">
    /// A function to resolve the client identifier from the HTTP context.
    /// Return null to reject the connection with a 400 status.
    /// </param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapEventStream(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, string?>? clientIdResolver)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);

        return endpoints.MapGet(pattern, async (HttpContext context) =>
        {
            // Resolve required services
            var connectionStore = context.RequestServices.GetRequiredService<IConnectionStore>();
            var formatter = context.RequestServices.GetRequiredService<ISseFormatter>();
            var options = context.RequestServices.GetRequiredService<IOptions<PushStreamOptions>>();

            // Handle the SSE connection
            await SseConnectionHandler.HandleAsync(
                context,
                connectionStore,
                formatter,
                options,
                clientIdResolver);
        });
    }

    /// <summary>
    /// Maps an SSE event stream endpoint with access to route values in the resolver.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern with parameters (e.g., "/events/{channel}").</param>
    /// <param name="clientIdResolver">
    /// A function to resolve the client identifier from HTTP context and route values.
    /// </param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// app.MapEventStreamWithRouteValues("/events/{channel}", (context, routeValues) => 
    ///     $"{context.User.Identity?.Name}:{routeValues["channel"]}");
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapEventStreamWithRouteValues(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, RouteValueDictionary, string?> clientIdResolver)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(clientIdResolver);

        return endpoints.MapGet(pattern, async (HttpContext context) =>
        {
            // Resolve required services
            var connectionStore = context.RequestServices.GetRequiredService<IConnectionStore>();
            var formatter = context.RequestServices.GetRequiredService<ISseFormatter>();
            var options = context.RequestServices.GetRequiredService<IOptions<PushStreamOptions>>();

            // Create resolver that includes route values
            var routeValues = context.GetRouteData()?.Values ?? new RouteValueDictionary();
            string? ResolveClientId(HttpContext ctx) => clientIdResolver(ctx, routeValues);

            // Handle the SSE connection
            await SseConnectionHandler.HandleAsync(
                context,
                connectionStore,
                formatter,
                options,
                ResolveClientId);
        });
    }
}