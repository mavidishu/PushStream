using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PushStream.AspNetCore.Options;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;

namespace PushStream.AspNetCore.Connections;

/// <summary>
/// Handles SSE connection lifecycle for incoming HTTP requests.
/// </summary>
internal static class SseConnectionHandler
{
    /// <summary>
    /// Handles an incoming SSE connection request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="connectionStore">The connection store.</param>
    /// <param name="formatter">The SSE formatter.</param>
    /// <param name="options">PushStream options.</param>
    /// <param name="clientIdResolver">Optional per-endpoint client ID resolver.</param>
    public static async Task HandleAsync(
        HttpContext context,
        IConnectionStore connectionStore,
        ISseFormatter formatter,
        IOptions<PushStreamOptions> options,
        Func<HttpContext, string?>? clientIdResolver = null)
    {
        var pushStreamOptions = options.Value;
        
        // Resolve client identifier
        var resolver = clientIdResolver ?? pushStreamOptions.ClientIdResolver;
        var clientId = resolver?.Invoke(context) ?? Guid.NewGuid().ToString();

        // Check if resolver rejected the connection
        if (clientId == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Client identifier is required.");
            return;
        }

        var connectionId = Guid.NewGuid().ToString();

        // Configure SSE response headers
        ConfigureSseResponse(context.Response);

        // Create the connection adapter
        var connection = new HttpClientConnection(
            connectionId,
            clientId,
            context.Response,
            context.RequestAborted);

        await using (connection)
        {
            try
            {
                // Register connection
                await connectionStore.AddAsync(connection);

                // Send initial heartbeat if configured
                if (pushStreamOptions.SendInitialHeartbeat)
                {
                    var heartbeat = formatter.FormatHeartbeat();
                    await connection.WriteAsync(heartbeat, context.RequestAborted);
                }

                // Wait until the connection is closed
                // This keeps the response open for SSE streaming
                await WaitForDisconnectionAsync(context.RequestAborted);
            }
            finally
            {
                // Always remove connection from store
                await connectionStore.RemoveAsync(connectionId);
            }
        }
    }

    /// <summary>
    /// Configures the HTTP response for SSE streaming.
    /// </summary>
    private static void ConfigureSseResponse(HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        
        // Disable buffering for immediate delivery
        response.Headers["X-Accel-Buffering"] = "no"; // nginx
        
        // Disable response compression which can buffer content
        var feature = response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        feature?.DisableBuffering();
    }

    /// <summary>
    /// Waits until the connection is closed by the client or server.
    /// </summary>
    private static async Task WaitForDisconnectionAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var registration = cancellationToken.Register(() => tcs.TrySetResult(null));

        await tcs.Task;
    }
}