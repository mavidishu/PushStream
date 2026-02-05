using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PushStream.Core.Abstractions;
using PushStream.Redis.Options;
using PushStream.Redis.Storage;
using StackExchange.Redis;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace PushStream.Redis.Tests;

/// <summary>
/// Unit tests for <see cref="RedisConnectionStore"/>.
/// </summary>
public class RedisConnectionStoreTests
{
    private readonly IConnectionMultiplexer _mockRedis;
    private readonly IDatabase _mockDatabase;
    private readonly ITransaction _mockTransaction;
    private readonly RedisConnectionStoreOptions _options;
    private readonly RedisConnectionStore _store;

    public RedisConnectionStoreTests()
    {
        _mockRedis = Substitute.For<IConnectionMultiplexer>();
        _mockDatabase = Substitute.For<IDatabase>();
        _mockTransaction = Substitute.For<ITransaction>();

        _mockRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_mockDatabase);
        _mockDatabase.CreateTransaction(Arg.Any<object>()).Returns(_mockTransaction);
        _mockTransaction.ExecuteAsync(Arg.Any<CommandFlags>()).Returns(true);

        _options = new RedisConnectionStoreOptions
        {
            KeyPrefix = "test",
            ConnectionTtl = TimeSpan.FromSeconds(60),
            TtlRefreshInterval = TimeSpan.FromSeconds(30)
        };

        _store = new RedisConnectionStore(
            _mockRedis,
            MsOptions.Create(_options),
            NullLogger<RedisConnectionStore>.Instance);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesServerId()
    {
        // Assert
        Assert.NotNull(_store.ServerId);
        Assert.NotEmpty(_store.ServerId);
    }

    [Fact]
    public void Constructor_ThrowsOnNullRedis()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisConnectionStore(
            null!,
            MsOptions.Create(_options),
            NullLogger<RedisConnectionStore>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisConnectionStore(
            _mockRedis,
            null!,
            NullLogger<RedisConnectionStore>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisConnectionStore(
            _mockRedis,
            MsOptions.Create(_options),
            null!));
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_StoresConnectionLocally()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        var connections = await _store.GetAllAsync();
        Assert.Single(connections);
        Assert.Equal("conn_1", connections.First().ConnectionId);
    }

    [Fact]
    public async Task AddAsync_ExecutesRedisTransaction()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        await _mockTransaction.Received(1).ExecuteAsync(Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task AddAsync_SetsConnectionHash()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        _ = _mockTransaction.Received(1).HashSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("connections:conn_1")),
            Arg.Any<HashEntry[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task AddAsync_SetsTtl()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        _ = _mockTransaction.Received().KeyExpireAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("connections:conn_1")),
            Arg.Is<TimeSpan>(t => t == _options.ConnectionTtl),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task AddAsync_AddsToServerSet()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        _ = _mockTransaction.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("servers:") && k.ToString().Contains(":connections")),
            Arg.Is<RedisValue>(v => v.ToString() == "conn_1"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task AddAsync_IncrementsCounter()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");

        // Act
        await _store.AddAsync(connection);

        // Assert
        _ = _mockTransaction.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("stats:total_connections")),
            Arg.Any<long>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task AddAsync_ThrowsOnNullConnection()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.AddAsync(null!));
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_RemovesConnectionLocally()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");
        await _store.AddAsync(connection);

        // Act
        await _store.RemoveAsync("conn_1");

        // Assert
        var connections = await _store.GetAllAsync();
        Assert.Empty(connections);
    }

    [Fact]
    public async Task RemoveAsync_DeletesFromRedis()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");
        await _store.AddAsync(connection);

        // Act
        await _store.RemoveAsync("conn_1");

        // Assert
        _ = _mockTransaction.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("connections:conn_1")),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromServerSet()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");
        await _store.AddAsync(connection);

        // Act
        await _store.RemoveAsync("conn_1");

        // Assert
        _ = _mockTransaction.Received().SetRemoveAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("servers:") && k.ToString().Contains(":connections")),
            Arg.Is<RedisValue>(v => v.ToString() == "conn_1"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RemoveAsync_DecrementsCounter()
    {
        // Arrange
        var connection = CreateMockConnection("conn_1", "client_1");
        await _store.AddAsync(connection);

        // Act
        await _store.RemoveAsync("conn_1");

        // Assert
        _ = _mockTransaction.Received().StringDecrementAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("stats:total_connections")),
            Arg.Any<long>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RemoveAsync_ThrowsOnNullConnectionId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.RemoveAsync(null!));
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyWhenNoConnections()
    {
        // Act
        var connections = await _store.GetAllAsync();

        // Assert
        Assert.Empty(connections);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllLocalConnections()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));
        await _store.AddAsync(CreateMockConnection("conn_3", "client_1"));

        // Act
        var connections = await _store.GetAllAsync();

        // Assert
        Assert.Equal(3, connections.Count());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSnapshot()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        var snapshot = await _store.GetAllAsync();

        // Act - add more after snapshot
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));

        // Assert - snapshot should not include new connection
        Assert.Single(snapshot);
    }

    #endregion

    #region GetByClientIdAsync Tests

    [Fact]
    public async Task GetByClientIdAsync_ReturnsMatchingConnections()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));
        await _store.AddAsync(CreateMockConnection("conn_3", "client_1"));

        // Act
        var connections = await _store.GetByClientIdAsync("client_1");

        // Assert
        Assert.Equal(2, connections.Count());
        Assert.All(connections, c => Assert.Equal("client_1", c.ClientId));
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsEmptyWhenNoMatches()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));

        // Act
        var connections = await _store.GetByClientIdAsync("client_nonexistent");

        // Assert
        Assert.Empty(connections);
    }

    [Fact]
    public async Task GetByClientIdAsync_ThrowsOnNullClientId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.GetByClientIdAsync(null!));
    }

    #endregion

    #region GetCountAsync Tests

    [Fact]
    public async Task GetCountAsync_ReturnsCountFromRedis()
    {
        // Arrange
        _mockDatabase.StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains("stats:total_connections")),
            Arg.Any<CommandFlags>())
            .Returns((RedisValue)"5");

        // Act
        var count = await _store.GetCountAsync();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsZeroWhenKeyMissing()
    {
        // Arrange
        _mockDatabase.StringGetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        // Act
        var count = await _store.GetCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsZeroForNegativeCount()
    {
        // Arrange
        _mockDatabase.StringGetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<CommandFlags>())
            .Returns((RedisValue)"-5");

        // Act
        var count = await _store.GetCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    #endregion

    #region GetLocalCount Tests

    [Fact]
    public async Task GetLocalCount_ReturnsLocalConnectionCount()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));

        // Act
        var count = _store.GetLocalCount();

        // Assert
        Assert.Equal(2, count);
    }

    #endregion

    #region LocalConnectionIds Tests

    [Fact]
    public async Task LocalConnectionIds_ReturnsAllLocalConnectionIds()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));

        // Act
        var ids = _store.LocalConnectionIds.ToList();

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains("conn_1", ids);
        Assert.Contains("conn_2", ids);
    }

    #endregion

    #region RefreshTtlsAsync Tests

    [Fact]
    public async Task RefreshTtlsAsync_RefreshesTtlForAllLocalConnections()
    {
        // Arrange
        await _store.AddAsync(CreateMockConnection("conn_1", "client_1"));
        await _store.AddAsync(CreateMockConnection("conn_2", "client_2"));
        
        _mockDatabase.KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>())
            .Returns(true);

        // Act
        await _store.RefreshTtlsAsync();

        // Assert
        await _mockDatabase.Received(3).KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Is<TimeSpan>(t => t == _options.ConnectionTtl),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RefreshTtlsAsync_DoesNothingWhenNoConnections()
    {
        // Act
        await _store.RefreshTtlsAsync();

        // Assert
        await _mockDatabase.DidNotReceive().KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    #endregion

    #region Helper Methods

    private static IClientConnection CreateMockConnection(string connectionId, string clientId)
    {
        var mock = Substitute.For<IClientConnection>();
        mock.ConnectionId.Returns(connectionId);
        mock.ClientId.Returns(clientId);
        mock.IsConnected.Returns(true);
        mock.LastEventId.Returns((string?)null);
        return mock;
    }

    #endregion
}
