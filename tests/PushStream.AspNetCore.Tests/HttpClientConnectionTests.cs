using Microsoft.AspNetCore.Http;
using NSubstitute;
using PushStream.AspNetCore.Connections;
using Xunit;

namespace PushStream.AspNetCore.Tests;

/// <summary>
/// Tests for HttpClientConnection.
/// Covers AC-9, AC-10, AC-11.
/// </summary>
public class HttpClientConnectionTests
{
    #region AC-9: Write Event

    [Fact]
    public async Task WriteAsync_WritesToResponseBody()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var response = CreateMockResponse(memoryStream);
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);

        // Act
        await connection.WriteAsync("event: test\ndata: hello\n\n");

        // Assert
        memoryStream.Position = 0;
        var content = new StreamReader(memoryStream).ReadToEnd();
        Assert.Equal("event: test\ndata: hello\n\n", content);
    }

    [Fact]
    public async Task WriteAsync_FlushesAfterWrite()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var response = CreateMockResponse(memoryStream);
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);

        // Act
        await connection.WriteAsync("data: test\n\n");

        // Assert - Content should be immediately available
        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public async Task WriteAsync_ThreadSafe_ConcurrentWrites()
    {
        // Arrange
        var memoryStream = new MemoryStream();
        var response = CreateMockResponse(memoryStream);
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);
        var tasks = new List<Task>();

        // Act - Write concurrently
        for (int i = 0; i < 10; i++)
        {
            var data = $"event: test{i}\n\n";
            tasks.Add(connection.WriteAsync(data));
        }

        await Task.WhenAll(tasks);

        // Assert - All writes completed without exception
        memoryStream.Position = 0;
        var content = new StreamReader(memoryStream).ReadToEnd();
        Assert.Contains("event:", content);
    }

    #endregion

    #region AC-10: Connection State

    [Fact]
    public void IsConnected_ReturnsTrue_WhenNotCancelled()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);

        // Assert
        Assert.True(connection.IsConnected);
    }

    [Fact]
    public void IsConnected_ReturnsFalse_WhenCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var response = CreateMockResponse(new MemoryStream());
        var connection = new HttpClientConnection("conn-1", "user-1", response, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task IsConnected_ReturnsFalse_WhenDisposed()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);

        // Act
        await connection.DisposeAsync();

        // Assert
        Assert.False(connection.IsConnected);
    }

    #endregion

    #region AC-11: Client Identifier

    [Fact]
    public void ConnectionId_ReturnsAssignedValue()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());
        
        // Act
        var connection = new HttpClientConnection("my-conn-id", "my-client-id", response, CancellationToken.None);

        // Assert
        Assert.Equal("my-conn-id", connection.ConnectionId);
    }

    [Fact]
    public void ClientId_ReturnsAssignedValue()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());
        
        // Act
        var connection = new HttpClientConnection("conn-1", "user-abc-123", response, CancellationToken.None);

        // Assert
        Assert.Equal("user-abc-123", connection.ClientId);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task WriteAsync_ThrowsObjectDisposedException_WhenDisposed()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);
        await connection.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => connection.WriteAsync("data: test\n\n"));
    }

    [Fact]
    public async Task WriteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var response = CreateMockResponse(new MemoryStream());
        var connection = new HttpClientConnection("conn-1", "user-1", response, CancellationToken.None);
        cts.Cancel();

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.WriteAsync("data: test\n\n", cts.Token));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullConnectionId()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new HttpClientConnection(null!, "user-1", response, CancellationToken.None));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullClientId()
    {
        // Arrange
        var response = CreateMockResponse(new MemoryStream());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new HttpClientConnection("conn-1", null!, response, CancellationToken.None));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullResponse()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new HttpClientConnection("conn-1", "user-1", null!, CancellationToken.None));
    }

    #endregion

    #region Helper Methods

    private static HttpResponse CreateMockResponse(Stream bodyStream)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = bodyStream;
        return context.Response;
    }

    #endregion
}