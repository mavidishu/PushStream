using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PushStream.Core.Abstractions;
using PushStream.Core.Formatting;
using PushStream.Core.Publishing;
using PushStream.Redis.Backplane;
using Xunit;

namespace PushStream.Redis.Tests;

/// <summary>
/// Unit tests for <see cref="BackplaneEventPublisher"/>.
/// </summary>
public class BackplaneEventPublisherTests
{
    private readonly EventPublisher _localPublisher;
    private readonly IBackplane _mockBackplane;
    private readonly BackplaneEventPublisher _decorator;

    public BackplaneEventPublisherTests()
    {
        var connectionStore = Substitute.For<IConnectionStore>();
        connectionStore.GetAllAsync().Returns(Array.Empty<IClientConnection>());
        connectionStore.GetByClientIdAsync(Arg.Any<string>()).Returns(Array.Empty<IClientConnection>());

        var formatter = Substitute.For<ISseFormatter>();
        formatter.FormatEvent(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string?>()).Returns("data: {}");

        _localPublisher = new EventPublisher(
            connectionStore,
            formatter,
            NullLogger<EventPublisher>.Instance);

        _mockBackplane = Substitute.For<IBackplane>();
        _mockBackplane.ServerId.Returns("test-server-id");

        _decorator = new BackplaneEventPublisher(
            _localPublisher,
            _mockBackplane,
            NullLogger<BackplaneEventPublisher>.Instance,
            serializerOptions: null);
    }

    [Fact]
    public async Task PublishAsync_CallsBackplaneWithCorrectMessage()
    {
        var payload = new { OrderId = 42, Status = "shipped" };

        await _decorator.PublishAsync("order.updated", payload);

        await _mockBackplane.Received(1).PublishAsync(
            Arg.Is<BackplaneMessage>(m =>
                m.SourceServerId == "test-server-id" &&
                m.EventName == "order.updated" &&
                m.TargetClientId == null &&
                m.Payload.Contains("42") &&
                m.Payload.Contains("shipped")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithEventId_PassesEventIdToBackplane()
    {
        await _decorator.PublishAsync("e", new { x = 1 }, eventId: "evt-123");

        await _mockBackplane.Received(1).PublishAsync(
            Arg.Is<BackplaneMessage>(m => m.EventId == "evt-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishToAsync_CallsBackplaneWithTargetClientId()
    {
        await _decorator.PublishToAsync("user_123", "notification", new { text = "hi" });

        await _mockBackplane.Received(1).PublishAsync(
            Arg.Is<BackplaneMessage>(m =>
                m.TargetClientId == "user_123" &&
                m.EventName == "notification" &&
                m.Payload.Contains("hi")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WhenBackplaneThrows_DoesNotThrow()
    {
        _mockBackplane.PublishAsync(Arg.Any<BackplaneMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Redis down"));

        await _decorator.PublishAsync("e", new { x = 1 });
        // Should not throw (local delivery succeeded, backplane failure is logged only)
    }

    [Fact]
    public void Constructor_ThrowsOnNullLocalPublisher()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackplaneEventPublisher(
                null!,
                _mockBackplane,
                NullLogger<BackplaneEventPublisher>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullBackplane()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackplaneEventPublisher(
                _localPublisher,
                null!,
                NullLogger<BackplaneEventPublisher>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackplaneEventPublisher(
                _localPublisher,
                _mockBackplane,
                null!));
    }
}
