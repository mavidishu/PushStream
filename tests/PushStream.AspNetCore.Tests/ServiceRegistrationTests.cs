using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PushStream.AspNetCore.DependencyInjection;
using PushStream.AspNetCore.Options;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;
using Xunit;

namespace PushStream.AspNetCore.Tests;

/// <summary>
/// Tests for service registration.
/// Covers AC-1, AC-2, AC-3.
/// </summary>
public class ServiceRegistrationTests
{
    #region AC-1: Basic Registration

    [Fact]
    public void AddPushStream_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPushStream();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IConnectionStore>());
        Assert.NotNull(provider.GetService<ISseFormatter>());
        Assert.NotNull(provider.GetService<IEventPublisher>());
        Assert.NotNull(provider.GetService<IOptions<PushStreamOptions>>());
    }

    [Fact]
    public void AddPushStream_IEventPublisher_IsResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream();

        // Act
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetService<IEventPublisher>();

        // Assert
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddPushStream_UsesDefaultOptions_WhenNoConfigure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream();

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PushStreamOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), options.HeartbeatInterval);
        Assert.Null(options.ClientIdResolver);
        Assert.True(options.SendInitialHeartbeat);
    }

    [Fact]
    public void AddPushStream_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream(options =>
        {
            options.HeartbeatInterval = TimeSpan.FromSeconds(15);
            options.SendInitialHeartbeat = false;
        });

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PushStreamOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(15), options.HeartbeatInterval);
        Assert.False(options.SendInitialHeartbeat);
    }

    #endregion

    #region AC-2: Service Lifetimes

    [Fact]
    public void AddPushStream_IConnectionStore_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream();
        var provider = services.BuildServiceProvider();

        // Act
        var store1 = provider.GetRequiredService<IConnectionStore>();
        var store2 = provider.GetRequiredService<IConnectionStore>();

        // Assert
        Assert.Same(store1, store2);
    }

    [Fact]
    public void AddPushStream_IEventPublisher_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream();
        var provider = services.BuildServiceProvider();

        // Act
        var publisher1 = provider.GetRequiredService<IEventPublisher>();
        var publisher2 = provider.GetRequiredService<IEventPublisher>();

        // Assert
        Assert.Same(publisher1, publisher2);
    }

    [Fact]
    public void AddPushStream_ISseFormatter_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPushStream();
        var provider = services.BuildServiceProvider();

        // Act
        var formatter1 = provider.GetRequiredService<ISseFormatter>();
        var formatter2 = provider.GetRequiredService<ISseFormatter>();

        // Assert
        Assert.Same(formatter1, formatter2);
    }

    #endregion

    #region AC-3: Multiple Registrations

    [Fact]
    public void AddPushStream_CalledMultipleTimes_DoesNotDuplicateServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPushStream();
        services.AddPushStream();
        services.AddPushStream();

        // Assert - Count singleton registrations
        var storeRegistrations = services.Count(s => s.ServiceType == typeof(IConnectionStore));
        var publisherRegistrations = services.Count(s => s.ServiceType == typeof(IEventPublisher));
        
        Assert.Equal(1, storeRegistrations);
        Assert.Equal(1, publisherRegistrations);
    }

    [Fact]
    public void AddPushStream_CalledMultipleTimes_LastConfigurationWins()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPushStream(o => o.HeartbeatInterval = TimeSpan.FromSeconds(10));
        services.AddPushStream(o => o.HeartbeatInterval = TimeSpan.FromSeconds(20));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PushStreamOptions>>().Value;

        // Assert - Both configurations are applied, but we can verify it works
        // Note: Multiple Configure calls accumulate, so the last value wins
        Assert.Equal(TimeSpan.FromSeconds(20), options.HeartbeatInterval);
    }

    #endregion

    #region Custom Connection Store

    [Fact]
    public void AddPushStream_WithCustomConnectionStore_UsesCustomStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPushStream<CustomConnectionStore>();
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetRequiredService<IConnectionStore>();
        Assert.IsType<CustomConnectionStore>(store);
    }

    private class CustomConnectionStore : IConnectionStore
    {
        public Task AddAsync(IClientConnection connection) => Task.CompletedTask;
        public Task<IEnumerable<IClientConnection>> GetAllAsync() => Task.FromResult<IEnumerable<IClientConnection>>([]);
        public Task<IEnumerable<IClientConnection>> GetByClientIdAsync(string clientId) => Task.FromResult<IEnumerable<IClientConnection>>([]);
        public Task<int> GetCountAsync() => Task.FromResult(0);
        public Task RemoveAsync(string connectionId) => Task.CompletedTask;
    }

    #endregion
}