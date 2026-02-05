using PushStream.Redis.Options;
using Xunit;

namespace PushStream.Redis.Tests;

/// <summary>
/// Unit tests for <see cref="RedisConnectionStoreOptions"/>.
/// </summary>
public class RedisConnectionStoreOptionsTests
{
    #region Default Values Tests

    [Fact]
    public void DefaultValues_KeyPrefixIsPushStream()
    {
        // Arrange & Act
        var options = new RedisConnectionStoreOptions();

        // Assert
        Assert.Equal("pushstream", options.KeyPrefix);
    }

    [Fact]
    public void DefaultValues_ConnectionTtlIs60Seconds()
    {
        // Arrange & Act
        var options = new RedisConnectionStoreOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(60), options.ConnectionTtl);
    }

    [Fact]
    public void DefaultValues_TtlRefreshIntervalIs30Seconds()
    {
        // Arrange & Act
        var options = new RedisConnectionStoreOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), options.TtlRefreshInterval);
    }

    [Fact]
    public void DefaultValues_ConnectionStringIsEmpty()
    {
        // Arrange & Act
        var options = new RedisConnectionStoreOptions();

        // Assert
        Assert.Equal(string.Empty, options.ConnectionString);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ThrowsOnEmptyConnectionString()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = ""
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("connection string", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ThrowsOnWhitespaceConnectionString()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "   "
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnEmptyKeyPrefix()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = ""
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("prefix", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ThrowsOnZeroConnectionTtl()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            ConnectionTtl = TimeSpan.Zero
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("TTL", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsOnNegativeConnectionTtl()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            ConnectionTtl = TimeSpan.FromSeconds(-1)
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnZeroTtlRefreshInterval()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            TtlRefreshInterval = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_ThrowsWhenRefreshIntervalExceedsTtl()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            ConnectionTtl = TimeSpan.FromSeconds(30),
            TtlRefreshInterval = TimeSpan.FromSeconds(60)
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("less than", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ThrowsWhenRefreshIntervalEqualsTtl()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            ConnectionTtl = TimeSpan.FromSeconds(30),
            TtlRefreshInterval = TimeSpan.FromSeconds(30)
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_SucceedsWithValidOptions()
    {
        // Arrange
        var options = new RedisConnectionStoreOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "myapp",
            ConnectionTtl = TimeSpan.FromSeconds(60),
            TtlRefreshInterval = TimeSpan.FromSeconds(30)
        };

        // Act & Assert - no exception
        options.Validate();
    }

    #endregion
}
