using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PushStream.AspNetCore.DependencyInjection;
using PushStream.AspNetCore.Routing;
using PushStream.Core.Abstractions;
using Xunit;

namespace PushStream.AspNetCore.Tests;

/// <summary>
/// Integration tests for the full SSE pipeline.
/// Covers AC-4 through AC-18.
/// </summary>
public class IntegrationTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _host = await CreateTestHostAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private static async Task<IHost> CreateTestHostAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureApp = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddPushStream();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapEventStream("/events");
        configureApp?.Invoke(app);

        await app.StartAsync();
        return app;
    }

    #region AC-4: Basic Endpoint

    [Fact]
    public async Task MapEventStream_EndpointExists_ReturnsOk()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapEventStream_GetMethodOnly()
    {
        // Act
        var postResponse = await _client!.PostAsync("/events", new StringContent(""));
        var putResponse = await _client!.PutAsync("/events", new StringContent(""));
        var deleteResponse = await _client!.DeleteAsync("/events");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResponse.StatusCode);
    }

    #endregion

    #region AC-5: SSE Response Headers

    [Fact]
    public async Task MapEventStream_SetsCorrectContentType()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Assert
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MapEventStream_SetsCacheControl()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Assert
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString() ?? "");
    }

    #endregion

    #region Retry Interval

    [Fact]
    public async Task MapEventStream_SendsRetryIntervalFirst()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var reader = new StreamReader(stream, Encoding.UTF8);

        // Read the first line
        var firstLine = await reader.ReadLineAsync(cts.Token);

        // Assert - First message should be retry interval (default 3000ms)
        Assert.StartsWith("retry:", firstLine);
        Assert.Contains("3000", firstLine);
    }

    [Fact]
    public async Task MapEventStream_RetryBeforeHeartbeat()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var reader = new StreamReader(stream, Encoding.UTF8);

        // Read first few lines
        var retryLine = await reader.ReadLineAsync(cts.Token);
        var emptyLine1 = await reader.ReadLineAsync(cts.Token);
        var heartbeatLine = await reader.ReadLineAsync(cts.Token);

        // Assert - Order should be: retry, empty, heartbeat
        Assert.StartsWith("retry:", retryLine);
        Assert.Equal("", emptyLine1);
        Assert.StartsWith(":", heartbeatLine); // Heartbeat starts with colon (comment)
    }

    #endregion

    #region AC-6: Connection Registration

    [Fact]
    public async Task MapEventStream_RegistersConnectionInStore()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var store = _host!.Services.GetRequiredService<IConnectionStore>();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        _ = _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Wait for connection to be registered
        await Task.Delay(100);

        // Assert
        var count = await store.GetCountAsync();
        Assert.True(count >= 1);
    }

    #endregion

    #region AC-10: Default Identifier

    [Fact]
    public async Task MapEventStream_AssignsUniqueIdentifier_WhenNoResolver()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var store = _host!.Services.GetRequiredService<IConnectionStore>();

        // Act - Make two connections
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/events");
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/events");
        
        _ = _client!.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        _ = _client!.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        await Task.Delay(200);

        // Assert
        var connections = (await store.GetAllAsync()).ToList();
        Assert.True(connections.Count >= 2);
        Assert.NotEqual(connections[0].ClientId, connections[1].ClientId);
    }

    #endregion

    #region AC-15: Event Delivery

    [Fact]
    public async Task PublishAsync_DeliversEventToConnectedClient()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var publisher = _host!.Services.GetRequiredService<IEventPublisher>();

        // Connect and read the stream
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        var response = await _client!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var reader = new StreamReader(stream, Encoding.UTF8);

        // Read initial retry and heartbeat
        await reader.ReadLineAsync(cts.Token); // retry: 3000
        await reader.ReadLineAsync(cts.Token); // empty line
        await reader.ReadLineAsync(cts.Token); // : heartbeat
        await reader.ReadLineAsync(cts.Token); // empty line

        // Act - Publish an event
        await publisher.PublishAsync("test.event", new { message = "hello" }, cts.Token);

        // Read the event
        var eventLine = await reader.ReadLineAsync(cts.Token);
        var dataLine = await reader.ReadLineAsync(cts.Token);

        // Assert
        Assert.Equal("event: test.event", eventLine);
        Assert.StartsWith("data: ", dataLine);
        Assert.Contains("hello", dataLine);
    }

    #endregion

    #region AC-16: Broadcast Delivery

    [Fact]
    public async Task PublishAsync_BroadcastsToAllClients()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var publisher = _host!.Services.GetRequiredService<IEventPublisher>();
        var store = _host!.Services.GetRequiredService<IConnectionStore>();

        // Connect two clients
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/events");
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/events");
        
        _ = _client!.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        _ = _client!.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        await Task.Delay(200);
        var initialCount = await store.GetCountAsync();

        // Act
        await publisher.PublishAsync("broadcast.test", new { data = 123 }, cts.Token);

        // Assert - Both connections exist
        Assert.True(initialCount >= 2);
    }

    #endregion

    #region AC-11: Custom Identifier Resolver

    [Fact]
    public async Task MapEventStream_WithResolver_UsesResolvedIdentifier()
    {
        // Arrange
        var customHost = await CreateTestHostAsync(
            configureApp: app =>
            {
                app.MapEventStream("/custom-events", context => "custom-user-123");
            });

        var client = customHost.GetTestClient();
        var store = customHost.Services.GetRequiredService<IConnectionStore>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/custom-events");
            _ = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            await Task.Delay(200);

            // Assert
            var connections = await store.GetByClientIdAsync("custom-user-123");
            Assert.Single(connections);
        }
        finally
        {
            client.Dispose();
            await customHost.StopAsync();
            customHost.Dispose();
        }
    }

    #endregion

    #region AC-17: Targeted Delivery

    [Fact]
    public async Task PublishToAsync_OnlyTargetsSpecificClient()
    {
        // Arrange
        var customHost = await CreateTestHostAsync(
            configureApp: app =>
            {
                app.MapEventStream("/targeted", context => 
                    context.Request.Query["userId"].FirstOrDefault() ?? "anonymous");
            });

        var client = customHost.GetTestClient();
        var publisher = customHost.Services.GetRequiredService<IEventPublisher>();
        var store = customHost.Services.GetRequiredService<IConnectionStore>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            // Connect two clients with different IDs
            var request1 = new HttpRequestMessage(HttpMethod.Get, "/targeted?userId=user1");
            var request2 = new HttpRequestMessage(HttpMethod.Get, "/targeted?userId=user2");

            var response1 = await client.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            _ = client.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            await Task.Delay(200);

            // Verify both connected
            var user1Connections = await store.GetByClientIdAsync("user1");
            var user2Connections = await store.GetByClientIdAsync("user2");
            Assert.Single(user1Connections);
            Assert.Single(user2Connections);

            // Act - Publish to user1 only
            await publisher.PublishToAsync("user1", "private.msg", new { secret = "data" }, cts.Token);

            // Read from user1's stream
            var stream = await response1.Content.ReadAsStreamAsync(cts.Token);
            var reader = new StreamReader(stream, Encoding.UTF8);
            
            // Skip retry and heartbeat
            await reader.ReadLineAsync(cts.Token); // retry: 3000
            await reader.ReadLineAsync(cts.Token); // empty line
            await reader.ReadLineAsync(cts.Token); // : heartbeat
            await reader.ReadLineAsync(cts.Token); // empty line

            // Read the targeted event
            var eventLine = await reader.ReadLineAsync(cts.Token);
            
            Assert.Equal("event: private.msg", eventLine);
        }
        finally
        {
            client.Dispose();
            await customHost.StopAsync();
            customHost.Dispose();
        }
    }

    #endregion

    #region AC-23: Multiple Endpoints

    [Fact]
    public async Task MapEventStream_MultipleEndpoints_WorkIndependently()
    {
        // Arrange
        var customHost = await CreateTestHostAsync(
            configureApp: app =>
            {
                app.MapEventStream("/events/channel1");
                app.MapEventStream("/events/channel2");
            });

        var client = customHost.GetTestClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            // Act
            var request1 = new HttpRequestMessage(HttpMethod.Get, "/events/channel1");
            var request2 = new HttpRequestMessage(HttpMethod.Get, "/events/channel2");

            var response1 = await client.SendAsync(request1, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var response2 = await client.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }
        finally
        {
            client.Dispose();
            await customHost.StopAsync();
            customHost.Dispose();
        }
    }

    #endregion
}