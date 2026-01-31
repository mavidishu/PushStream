using Microsoft.Extensions.Logging.Abstractions;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;
using PushStream.Core.Publishing;
using PushStream.Core.Storage;
using PushStream.Core.Tests.Mocks;
using Xunit;

namespace PushStream.Core.Tests;

/// <summary>
/// Tests for <see cref="EventPublisher"/>.
/// Covers AC-1 through AC-3, AC-9 through AC-11.
/// </summary>
public class EventPublisherTests
{
    private readonly InMemoryConnectionStore _store;
    private readonly SseFormatter _formatter;
    private readonly EventPublisher _publisher;

    public EventPublisherTests()
    {
        _store = new InMemoryConnectionStore(NullLogger<InMemoryConnectionStore>.Instance);
        _formatter = new SseFormatter();
        _publisher = new EventPublisher(_store, _formatter, NullLogger<EventPublisher>.Instance);
    }

    #region AC-1: Broadcast Publishing

    [Fact]
    public async Task PublishAsync_BroadcastsToAllConnections()
    {
        // Arrange
        var conn1 = new MockClientConnection("conn-1", "user-1");
        var conn2 = new MockClientConnection("conn-2", "user-2");
        var conn3 = new MockClientConnection("conn-3", "user-3");
        await _store.AddAsync(conn1);
        await _store.AddAsync(conn2);
        await _store.AddAsync(conn3);

        // Act
        await _publisher.PublishAsync("test.event", new { message = "hello" });

        // Assert
        Assert.Single(conn1.WrittenData);
        Assert.Single(conn2.WrittenData);
        Assert.Single(conn3.WrittenData);
    }

    [Fact]
    public async Task PublishAsync_EventDataIsCorrectlyFormatted()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishAsync("task.progress", new { taskId = "123", percentage = 50 });

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.Contains("event: task.progress", data);
        Assert.Contains("\"taskId\":\"123\"", data);
        Assert.Contains("\"percentage\":50", data);
    }

    [Fact]
    public async Task PublishAsync_CompletesAsynchronously()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act & Assert - Should complete without error
        await _publisher.PublishAsync("test", new { x = 1 });
        
        // Verify event was received
        Assert.Single(conn.WrittenData);
    }

    #endregion

    #region AC-2: Targeted Publishing

    [Fact]
    public async Task PublishToAsync_OnlyTargetedClientReceives()
    {
        // Arrange
        var user1Conn = new MockClientConnection("conn-1", "user-1");
        var user2Conn = new MockClientConnection("conn-2", "user-2");
        await _store.AddAsync(user1Conn);
        await _store.AddAsync(user2Conn);

        // Act
        await _publisher.PublishToAsync("user-1", "private.message", new { text = "secret" });

        // Assert
        Assert.Single(user1Conn.WrittenData);
        Assert.Empty(user2Conn.WrittenData);
    }

    [Fact]
    public async Task PublishToAsync_MultipleConnectionsSameClient_AllReceive()
    {
        // Arrange - same user, multiple tabs
        var tab1 = new MockClientConnection("conn-1", "user-1");
        var tab2 = new MockClientConnection("conn-2", "user-1");
        var tab3 = new MockClientConnection("conn-3", "user-1");
        var otherUser = new MockClientConnection("conn-4", "user-2");
        await _store.AddAsync(tab1);
        await _store.AddAsync(tab2);
        await _store.AddAsync(tab3);
        await _store.AddAsync(otherUser);

        // Act
        await _publisher.PublishToAsync("user-1", "notification", new { count = 5 });

        // Assert
        Assert.Single(tab1.WrittenData);
        Assert.Single(tab2.WrittenData);
        Assert.Single(tab3.WrittenData);
        Assert.Empty(otherUser.WrittenData);
    }

    [Fact]
    public async Task PublishToAsync_NonExistentClient_NoError()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act & Assert - Should not throw
        await _publisher.PublishToAsync("non-existent-user", "test", new { });

        // And the existing user should not receive anything
        Assert.Empty(conn.WrittenData);
    }

    #endregion

    #region AC-3: No Active Connections

    [Fact]
    public async Task PublishAsync_NoConnections_CompletesSuccessfully()
    {
        // Act & Assert - Should not throw
        await _publisher.PublishAsync("test.event", new { message = "hello" });
    }

    [Fact]
    public async Task PublishToAsync_NoConnections_CompletesSuccessfully()
    {
        // Act & Assert - Should not throw
        await _publisher.PublishToAsync("any-user", "test.event", new { message = "hello" });
    }

    #endregion

    #region AC-9: Write Event (via IClientConnection)

    [Fact]
    public async Task WriteAsync_DataWrittenToConnection()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishAsync("test", new { value = 42 });

        // Assert
        Assert.NotEmpty(conn.WrittenData);
        Assert.Contains("42", conn.LastWrittenData!);
    }

    #endregion

    #region AC-10: Connection State

    [Fact]
    public async Task PublishAsync_SkipsDisconnectedClients()
    {
        // Arrange
        var activeConn = new MockClientConnection("conn-1", "user-1");
        var closedConn = new MockClientConnection("conn-2", "user-2");
        closedConn.Close(); // Simulate disconnect
        
        await _store.AddAsync(activeConn);
        await _store.AddAsync(closedConn);

        // Act
        await _publisher.PublishAsync("test", new { x = 1 });

        // Assert
        Assert.Single(activeConn.WrittenData);
        Assert.Empty(closedConn.WrittenData);
    }

    [Fact]
    public async Task PublishToAsync_SkipsDisconnectedClients()
    {
        // Arrange - Same user, one closed connection
        var activeTab = new MockClientConnection("conn-1", "user-1");
        var closedTab = new MockClientConnection("conn-2", "user-1");
        closedTab.Close();
        
        await _store.AddAsync(activeTab);
        await _store.AddAsync(closedTab);

        // Act
        await _publisher.PublishToAsync("user-1", "test", new { x = 1 });

        // Assert
        Assert.Single(activeTab.WrittenData);
        Assert.Empty(closedTab.WrittenData);
    }

    #endregion

    #region AC-11: Client Identifier

    [Fact]
    public void ClientConnection_HasCorrectIdentifier()
    {
        // Arrange
        var conn = new MockClientConnection("conn-123", "user-456");

        // Assert
        Assert.Equal("conn-123", conn.ConnectionId);
        Assert.Equal("user-456", conn.ClientId);
    }

    [Fact]
    public async Task TargetedPublishing_UsesClientId_NotConnectionId()
    {
        // Arrange
        var conn = new MockClientConnection("conn-xyz", "user-abc");
        await _store.AddAsync(conn);

        // Act - Target by ClientId
        await _publisher.PublishToAsync("user-abc", "test", new { });

        // Assert
        Assert.Single(conn.WrittenData);

        // Act - Target by ConnectionId should not work
        conn.ClearWrittenData();
        await _publisher.PublishToAsync("conn-xyz", "test", new { });

        // Assert
        Assert.Empty(conn.WrittenData);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task PublishAsync_OneConnectionFails_OthersStillReceive()
    {
        // Arrange
        var goodConn1 = new MockClientConnection("conn-1", "user-1");
        var badConn = new MockClientConnection("conn-2", "user-2");
        var goodConn2 = new MockClientConnection("conn-3", "user-3");
        
        // Close badConn but leave in store (simulates race condition)
        badConn.Close();
        
        await _store.AddAsync(goodConn1);
        await _store.AddAsync(badConn);
        await _store.AddAsync(goodConn2);

        // Act - Should not throw despite badConn being closed
        await _publisher.PublishAsync("test", new { x = 1 });

        // Assert
        Assert.Single(goodConn1.WrittenData);
        Assert.Single(goodConn2.WrittenData);
    }

    [Fact]
    public async Task PublishAsync_NullEventName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _publisher.PublishAsync(null!, new { }));
    }

    [Fact]
    public async Task PublishToAsync_NullClientId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _publisher.PublishToAsync(null!, "test", new { }));
    }

    [Fact]
    public async Task PublishToAsync_NullEventName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _publisher.PublishToAsync("user", null!, new { }));
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullConnectionStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new EventPublisher(null!, _formatter, NullLogger<EventPublisher>.Instance));
    }

    [Fact]
    public void Constructor_NullFormatter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new EventPublisher(_store, null!, NullLogger<EventPublisher>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new EventPublisher(_store, _formatter, null!));
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task PublishAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _publisher.PublishAsync("test", new { }, cts.Token));
    }

    #endregion

    #region Event ID Support

    [Fact]
    public async Task PublishAsync_WithEventId_FormatsCorrectly()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishAsync("task.progress", new { taskId = "123" }, "evt_abc");

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.Contains("id: evt_abc", data);
        Assert.Contains("event: task.progress", data);
    }

    [Fact]
    public async Task PublishAsync_WithNullEventId_OmitsIdField()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishAsync("task.progress", new { taskId = "123" }, eventId: null);

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.DoesNotContain("id:", data);
    }

    [Fact]
    public async Task PublishToAsync_WithEventId_FormatsCorrectly()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishToAsync("user-1", "notification", new { count = 5 }, "evt_xyz");

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.Contains("id: evt_xyz", data);
        Assert.Contains("event: notification", data);
    }

    [Fact]
    public async Task PublishToAsync_WithNullEventId_OmitsIdField()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act
        await _publisher.PublishToAsync("user-1", "notification", new { count = 5 }, eventId: null);

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.DoesNotContain("id:", data);
    }

    [Fact]
    public async Task PublishAsync_WithoutEventIdOverload_BackwardCompatible()
    {
        // Arrange
        var conn = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(conn);

        // Act - Using the original overload without eventId
        await _publisher.PublishAsync("test.event", new { value = 42 });

        // Assert
        var data = conn.LastWrittenData;
        Assert.NotNull(data);
        Assert.Contains("event: test.event", data);
        Assert.DoesNotContain("id:", data);
    }

    #endregion
}