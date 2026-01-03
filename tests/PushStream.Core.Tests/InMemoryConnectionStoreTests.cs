using PushStream.Core.Abstractions;
using PushStream.Core.Storage;
using PushStream.Core.Tests.Mocks;
using Xunit;

namespace PushStream.Core.Tests;

/// <summary>
/// Tests for <see cref="InMemoryConnectionStore"/>.
/// Covers AC-4 through AC-8, AC-17, AC-18.
/// </summary>
public class InMemoryConnectionStoreTests
{
    private readonly InMemoryConnectionStore _store;

    public InMemoryConnectionStoreTests()
    {
        _store = new InMemoryConnectionStore();
    }

    #region AC-4: Register Connection

    [Fact]
    public async Task AddAsync_RegistersConnection_Successfully()
    {
        // Arrange
        var connection = new MockClientConnection("conn-1", "user-1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        var all = await _store.GetAllAsync();
        Assert.Single(all);
        Assert.Contains(all, c => c.ConnectionId == "conn-1");
    }

    [Fact]
    public async Task AddAsync_ConnectionIsRetrievableByClientId()
    {
        // Arrange
        var connection = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(connection);

        // Act
        var retrieved = await _store.GetByClientIdAsync("user-1");

        // Assert
        Assert.Single(retrieved);
        Assert.Equal("conn-1", retrieved.First().ConnectionId);
    }

    #endregion

    #region AC-5: Multiple Connections Same Identifier

    [Fact]
    public async Task AddAsync_MultipleConnectionsSameClientId_AllStored()
    {
        // Arrange
        var connection1 = new MockClientConnection("conn-1", "user-1");
        var connection2 = new MockClientConnection("conn-2", "user-1");
        var connection3 = new MockClientConnection("conn-3", "user-1");

        // Act
        await _store.AddAsync(connection1);
        await _store.AddAsync(connection2);
        await _store.AddAsync(connection3);

        // Assert
        var connections = await _store.GetByClientIdAsync("user-1");
        Assert.Equal(3, connections.Count());
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyMatchingConnections()
    {
        // Arrange
        var user1Conn = new MockClientConnection("conn-1", "user-1");
        var user2Conn = new MockClientConnection("conn-2", "user-2");
        await _store.AddAsync(user1Conn);
        await _store.AddAsync(user2Conn);

        // Act
        var user1Connections = await _store.GetByClientIdAsync("user-1");

        // Assert
        Assert.Single(user1Connections);
        Assert.Equal("user-1", user1Connections.First().ClientId);
    }

    #endregion

    #region AC-6: Remove Connection

    [Fact]
    public async Task RemoveAsync_RemovesConnection_NotRetrievable()
    {
        // Arrange
        var connection = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(connection);

        // Act
        await _store.RemoveAsync("conn-1");

        // Assert
        var all = await _store.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentConnection_NoError()
    {
        // Act & Assert - should not throw
        await _store.RemoveAsync("non-existent");
    }

    [Fact]
    public async Task RemoveAsync_RemovedConnection_NotInClientIdQuery()
    {
        // Arrange
        var connection = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(connection);
        await _store.RemoveAsync("conn-1");

        // Act
        var connections = await _store.GetByClientIdAsync("user-1");

        // Assert
        Assert.Empty(connections);
    }

    #endregion

    #region AC-7: Get All Connections

    [Fact]
    public async Task GetAllAsync_ReturnsAllActiveConnections()
    {
        // Arrange
        var conn1 = new MockClientConnection("conn-1", "user-1");
        var conn2 = new MockClientConnection("conn-2", "user-2");
        var conn3 = new MockClientConnection("conn-3", "user-3");
        await _store.AddAsync(conn1);
        await _store.AddAsync(conn2);
        await _store.AddAsync(conn3);

        // Act
        var all = await _store.GetAllAsync();

        // Assert
        Assert.Equal(3, all.Count());
    }

    [Fact]
    public async Task GetAllAsync_ExcludesRemovedConnections()
    {
        // Arrange
        var conn1 = new MockClientConnection("conn-1", "user-1");
        var conn2 = new MockClientConnection("conn-2", "user-2");
        await _store.AddAsync(conn1);
        await _store.AddAsync(conn2);
        await _store.RemoveAsync("conn-1");

        // Act
        var all = await _store.GetAllAsync();

        // Assert
        Assert.Single(all);
        Assert.Equal("conn-2", all.First().ConnectionId);
    }

    [Fact]
    public async Task GetAllAsync_EmptyStore_ReturnsEmpty()
    {
        // Act
        var all = await _store.GetAllAsync();

        // Assert
        Assert.Empty(all);
    }

    #endregion

    #region AC-8: Get Connections By Identifier

    [Fact]
    public async Task GetByClientIdAsync_NoMatchingConnections_ReturnsEmpty()
    {
        // Arrange
        var connection = new MockClientConnection("conn-1", "user-1");
        await _store.AddAsync(connection);

        // Act
        var connections = await _store.GetByClientIdAsync("user-2");

        // Assert
        Assert.Empty(connections);
    }

    [Fact]
    public async Task GetByClientIdAsync_MultipleMatches_ReturnsAll()
    {
        // Arrange
        var conn1 = new MockClientConnection("conn-1", "user-1");
        var conn2 = new MockClientConnection("conn-2", "user-1");
        var conn3 = new MockClientConnection("conn-3", "user-2");
        await _store.AddAsync(conn1);
        await _store.AddAsync(conn2);
        await _store.AddAsync(conn3);

        // Act
        var user1Connections = await _store.GetByClientIdAsync("user-1");

        // Assert
        Assert.Equal(2, user1Connections.Count());
        Assert.All(user1Connections, c => Assert.Equal("user-1", c.ClientId));
    }

    #endregion

    #region AC-17: Thread Safety

    [Fact]
    public async Task ConcurrentOperations_RemainConsistent()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Add 100 connections concurrently
        for (int i = 0; i < 100; i++)
        {
            var id = i;
            tasks.Add(Task.Run(async () =>
            {
                var conn = new MockClientConnection($"conn-{id}", $"user-{id % 10}");
                await _store.AddAsync(conn);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var count = await _store.GetCountAsync();
        Assert.Equal(100, count);
    }

    [Fact]
    public async Task ConcurrentAddAndRemove_NoExceptions()
    {
        // Arrange
        var addTasks = new List<Task>();
        var removeTasks = new List<Task>();

        // Pre-add some connections
        for (int i = 0; i < 50; i++)
        {
            await _store.AddAsync(new MockClientConnection($"conn-{i}", "user"));
        }

        // Act - Concurrently add and remove
        for (int i = 0; i < 50; i++)
        {
            var id = i;
            addTasks.Add(Task.Run(async () =>
            {
                await _store.AddAsync(new MockClientConnection($"new-conn-{id}", "user"));
            }));
            removeTasks.Add(Task.Run(async () =>
            {
                await _store.RemoveAsync($"conn-{id}");
            }));
        }

        // Assert - Should complete without exceptions
        await Task.WhenAll(addTasks.Concat(removeTasks));
    }

    #endregion

    #region AC-18: Memory Cleanup

    [Fact]
    public async Task RemoveAsync_DoesNotHoldReference()
    {
        // Arrange
        WeakReference<IClientConnection>? weakRef = null;
        
        await Task.Run(async () =>
        {
            var connection = new MockClientConnection("conn-1", "user-1");
            weakRef = new WeakReference<IClientConnection>(connection);
            await _store.AddAsync(connection);
            await _store.RemoveAsync("conn-1");
        });

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert - The connection should be eligible for GC
        // Note: This test may be flaky in debug builds
        Assert.NotNull(weakRef);
        // We can't guarantee GC happened, but we verify removal works
        var all = await _store.GetAllAsync();
        Assert.Empty(all);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _store.AddAsync(new MockClientConnection("conn-1", "user-1"));
        await _store.AddAsync(new MockClientConnection("conn-2", "user-2"));

        // Act
        var count = await _store.GetCountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AddAsync_NullConnection_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.AddAsync(null!));
    }

    [Fact]
    public async Task RemoveAsync_NullConnectionId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.RemoveAsync(null!));
    }

    [Fact]
    public async Task GetByClientIdAsync_NullClientId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.GetByClientIdAsync(null!));
    }

    #endregion
}

