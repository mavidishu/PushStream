using System.Text.Json;
using PushStream.Core.Abstractions;
using Xunit;

namespace PushStream.Redis.Tests;

/// <summary>
/// Unit tests for <see cref="BackplaneMessage"/> serialization and deserialization.
/// </summary>
public class BackplaneMessageTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void RoundTrip_SerializesAndDeserializes()
    {
        var message = new BackplaneMessage
        {
            SourceServerId = "server-1",
            TargetClientId = "user_123",
            EventName = "order.updated",
            Payload = "{\"id\":1,\"status\":\"shipped\"}",
            EventId = "evt-42",
            Timestamp = new DateTimeOffset(2026, 1, 29, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(message, CamelCaseOptions);
        var deserialized = JsonSerializer.Deserialize<BackplaneMessage>(json, CamelCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(message.SourceServerId, deserialized.SourceServerId);
        Assert.Equal(message.TargetClientId, deserialized.TargetClientId);
        Assert.Equal(message.EventName, deserialized.EventName);
        Assert.Equal(message.Payload, deserialized.Payload);
        Assert.Equal(message.EventId, deserialized.EventId);
        Assert.Equal(message.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void RoundTrip_BroadcastMessage_NoTargetClientId()
    {
        var message = new BackplaneMessage
        {
            SourceServerId = "server-a",
            TargetClientId = null,
            EventName = "broadcast.event",
            Payload = "{}"
        };

        var json = JsonSerializer.Serialize(message, CamelCaseOptions);
        var deserialized = JsonSerializer.Deserialize<BackplaneMessage>(json, CamelCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.TargetClientId);
        Assert.Equal("broadcast.event", deserialized.EventName);
    }

    [Fact]
    public void Deserialize_WithMissingOptionalFields_SetsRequiredOnly()
    {
        var json = """{"sourceServerId":"s1","eventName":"e","payload":"{}"}""";
        var deserialized = JsonSerializer.Deserialize<BackplaneMessage>(json, CamelCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("s1", deserialized.SourceServerId);
        Assert.Null(deserialized.TargetClientId);
        Assert.Null(deserialized.EventId);
        // Timestamp when omitted from JSON may be default or unset depending on deserializer behavior
        Assert.Equal("e", deserialized.EventName);
        Assert.Equal("{}", deserialized.Payload);
    }
}
