using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PushStream.Core.Abstractions;
using PushStream.Redis.Backplane;
using PushStream.Redis.Options;
using StackExchange.Redis;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace PushStream.Redis.Tests;

/// <summary>
/// Unit tests for <see cref="RedisBackplane"/>.
/// </summary>
public class RedisBackplaneTests
{
    private readonly IConnectionMultiplexer _mockRedis;
    private readonly ISubscriber _mockSubscriber;
    private readonly RedisBackplaneOptions _options;
    private readonly RedisBackplane _backplane;

    public RedisBackplaneTests()
    {
        _mockRedis = Substitute.For<IConnectionMultiplexer>();
        _mockSubscriber = Substitute.For<ISubscriber>();
        _mockRedis.GetSubscriber(Arg.Any<object>()).Returns(_mockSubscriber);

        _options = new RedisBackplaneOptions
        {
            ChannelPrefix = "pushstream:backplane",
            SerializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }
        };

        _backplane = new RedisBackplane(
            _mockRedis,
            MsOptions.Create(_options),
            NullLogger<RedisBackplane>.Instance,
            connectionStore: null);
    }

    [Fact]
    public void ServerId_IsNotNullOrEmpty()
    {
        Assert.NotNull(_backplane.ServerId);
        Assert.NotEmpty(_backplane.ServerId);
    }

    [Fact]
    public async Task PublishAsync_SerializesAndPublishesToBroadcastChannel()
    {
        _mockSubscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(1);

        var message = new BackplaneMessage
        {
            SourceServerId = _backplane.ServerId,
            EventName = "test.event",
            Payload = "{\"x\":1}"
        };

        await _backplane.PublishAsync(message);

        await _mockSubscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == "pushstream:backplane:broadcast"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task PublishToServerAsync_PublishesToTargetServerChannel()
    {
        _mockSubscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(1);

        var message = new BackplaneMessage
        {
            SourceServerId = _backplane.ServerId,
            EventName = "targeted.event",
            Payload = "{}"
        };

        await _backplane.PublishToServerAsync("other-server-id", message);

        await _mockSubscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c.ToString() == "pushstream:backplane:server:other-server-id"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task PublishToServerAsync_ThrowsOnNullServerId()
    {
        var message = new BackplaneMessage
        {
            SourceServerId = "s1",
            EventName = "e",
            Payload = "{}"
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _backplane.PublishToServerAsync(null!, message));
    }

    [Fact]
    public async Task SubscribeAsync_SubscribesToBroadcastAndTargetedChannels()
    {
        await _backplane.SubscribeAsync(_ => Task.CompletedTask);

        _mockSubscriber.Received(1).Subscribe(
            Arg.Is<RedisChannel>(c => c.ToString() == "pushstream:backplane:broadcast"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
        _mockSubscriber.Received(1).Subscribe(
            Arg.Is<RedisChannel>(c => c.ToString().StartsWith("pushstream:backplane:server:")),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SubscribeAsync_ThrowsOnNullHandler()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _backplane.SubscribeAsync(null!));
    }
}
