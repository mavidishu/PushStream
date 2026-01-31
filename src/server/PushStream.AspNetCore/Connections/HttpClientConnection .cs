using System.Text;
using Microsoft.AspNetCore.Http;
using PushStream.Core.Abstractions;

namespace PushStream.AspNetCore.Connections;

/// <summary>
/// Adapts an HTTP response stream to the <see cref="IClientConnection"/> interface.
/// Provides thread-safe writes to the SSE stream.
/// </summary>
public sealed class HttpClientConnection : IClientConnection, IAsyncDisposable
{
    private readonly HttpResponse _response;
    private readonly CancellationToken _requestAborted;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _isDisposed;

    /// <summary>
    /// Creates a new HTTP client connection adapter.
    /// </summary>
    /// <param name="connectionId">Unique identifier for this connection.</param>
    /// <param name="clientId">Logical client identifier for targeting.</param>
    /// <param name="response">The HTTP response to write to.</param>
    /// <param name="requestAborted">Cancellation token that fires when the request is aborted.</param>
    /// <param name="lastEventId">The Last-Event-ID header value from the client request, if provided.</param>
    public HttpClientConnection(
        string connectionId,
        string clientId,
        HttpResponse response,
        CancellationToken requestAborted,
        string? lastEventId = null)
    {
        ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _requestAborted = requestAborted;
        LastEventId = lastEventId;
    }

    /// <inheritdoc />
    public string ConnectionId { get; }

    /// <inheritdoc />
    public string ClientId { get; }

    /// <inheritdoc />
    public bool IsConnected => !_isDisposed && !_requestAborted.IsCancellationRequested;

    /// <inheritdoc />
    public string? LastEventId { get; }

    /// <inheritdoc />
    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(HttpClientConnection));
        }

        // Combine with request aborted token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _requestAborted);

        await _writeLock.WaitAsync(linkedCts.Token);
        try
        {
            if (!IsConnected)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(data);
            await _response.Body.WriteAsync(bytes, linkedCts.Token);
            await _response.Body.FlushAsync(linkedCts.Token);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Disposes the connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Wait for any pending writes to complete
        await _writeLock.WaitAsync();
        try
        {
            _writeLock.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}