using PushStream.Core.Formatting;
using Xunit;

namespace PushStream.Core.Tests;

/// <summary>
/// Tests for <see cref="SseFormatter"/>.
/// Covers AC-12 through AC-16.
/// </summary>
public class SseFormatterTests
{
    private readonly SseFormatter _formatter;

    public SseFormatterTests()
    {
        _formatter = new SseFormatter();
    }

    #region AC-12: Standard Event Format

    [Fact]
    public void FormatEvent_StandardFormat_IncludesEventLine()
    {
        // Arrange
        var payload = new { message = "hello" };

        // Act
        var result = _formatter.FormatEvent("test.event", payload);

        // Assert
        Assert.StartsWith("event: test.event\n", result);
    }

    [Fact]
    public void FormatEvent_StandardFormat_IncludesDataLine()
    {
        // Arrange
        var payload = new { message = "hello" };

        // Act
        var result = _formatter.FormatEvent("test.event", payload);

        // Assert
        Assert.Contains("data: ", result);
    }

    [Fact]
    public void FormatEvent_StandardFormat_EndsWithDoubleNewline()
    {
        // Arrange
        var payload = new { message = "hello" };

        // Act
        var result = _formatter.FormatEvent("test.event", payload);

        // Assert
        Assert.EndsWith("\n\n", result);
    }

    [Fact]
    public void FormatEvent_CompleteFormat_MatchesSSESpec()
    {
        // Arrange
        var payload = new { taskId = "123", status = "complete" };

        // Act
        var result = _formatter.FormatEvent("task.completed", payload);

        // Assert
        // Should be: "event: task.completed\ndata: {json}\n\n"
        var lines = result.Split('\n');
        Assert.Equal("event: task.completed", lines[0]);
        Assert.StartsWith("data: ", lines[1]);
        Assert.Equal("", lines[2]); // Empty line between data and end
        Assert.Equal("", lines[3]); // Trailing empty from final \n
    }

    #endregion

    #region AC-13: Multi-line Payload

    [Fact]
    public void FormatEvent_MultilineJson_EachLinePrefixedWithData()
    {
        // Arrange - Create a payload that will serialize with newlines when pretty-printed
        // Note: Default System.Text.Json doesn't pretty print, so this tests the single-line case
        var payload = new { line1 = "first", line2 = "second" };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Single line JSON should have one data: line
        var dataLines = result.Split('\n')
            .Where(l => l.StartsWith("data: "))
            .ToList();
        
        Assert.Single(dataLines);
    }

    [Fact]
    public void FormatEvent_PayloadWithEmbeddedNewlines_HandledCorrectly()
    {
        // Arrange - String with newlines becomes escaped in JSON
        var payload = new { text = "line1\nline2\nline3" };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Newlines in strings are escaped in JSON, so single data line
        var dataLines = result.Split('\n')
            .Where(l => l.StartsWith("data: "))
            .ToList();
        
        Assert.Single(dataLines);
        Assert.Contains("\\n", result); // Escaped newlines
    }

    #endregion

    #region AC-14: Heartbeat Format

    [Fact]
    public void FormatHeartbeat_IsComment()
    {
        // Act
        var result = _formatter.FormatHeartbeat();

        // Assert - Comments start with colon
        Assert.StartsWith(":", result);
    }

    [Fact]
    public void FormatHeartbeat_ContainsHeartbeatText()
    {
        // Act
        var result = _formatter.FormatHeartbeat();

        // Assert
        Assert.Contains("heartbeat", result);
    }

    [Fact]
    public void FormatHeartbeat_EndsWithDoubleNewline()
    {
        // Act
        var result = _formatter.FormatHeartbeat();

        // Assert
        Assert.EndsWith("\n\n", result);
    }

    [Fact]
    public void FormatHeartbeat_ExactFormat()
    {
        // Act
        var result = _formatter.FormatHeartbeat();

        // Assert
        Assert.Equal(": heartbeat\n\n", result);
    }

    #endregion

    #region AC-15: JSON Serialization with camelCase

    [Fact]
    public void FormatEvent_UsesCamelCase()
    {
        // Arrange
        var payload = new { TaskId = "123", UserName = "john" };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert
        Assert.Contains("\"taskId\"", result);
        Assert.Contains("\"userName\"", result);
        Assert.DoesNotContain("\"TaskId\"", result);
        Assert.DoesNotContain("\"UserName\"", result);
    }

    [Fact]
    public void FormatEvent_ValidJson()
    {
        // Arrange
        var payload = new { id = 1, name = "test", active = true };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Extract JSON from data line
        var dataLine = result.Split('\n').First(l => l.StartsWith("data: "));
        var json = dataLine.Substring("data: ".Length);
        
        // Verify it's valid JSON by parsing
        var parsed = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, parsed.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("test", parsed.RootElement.GetProperty("name").GetString());
        Assert.True(parsed.RootElement.GetProperty("active").GetBoolean());
    }

    #endregion

    #region AC-16: Null Handling

    [Fact]
    public void FormatEvent_NullProperties_Excluded()
    {
        // Arrange
        var payload = new { id = 1, name = (string?)null };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Null properties should be excluded
        Assert.DoesNotContain("\"name\"", result);
        Assert.Contains("\"id\"", result);
    }

    [Fact]
    public void FormatEvent_AllNullPayload_ValidJson()
    {
        // Arrange
        var payload = new { a = (string?)null, b = (int?)null };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Should produce empty object
        Assert.Contains("data: {}", result);
    }

    [Fact]
    public void FormatEvent_NullPayload_HandledGracefully()
    {
        // Act
        var result = _formatter.FormatEvent("test", (object?)null);

        // Assert
        Assert.Contains("data: null", result);
    }

    #endregion

    #region Additional Tests

    [Fact]
    public void FormatEvent_NullEventName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _formatter.FormatEvent<object>(null!, new { }));
    }

    [Fact]
    public void FormatEvent_EmptyEventName_Allowed()
    {
        // Act
        var result = _formatter.FormatEvent("", new { x = 1 });

        // Assert
        Assert.Contains("event: \n", result);
    }

    [Fact]
    public void FormatEvent_ComplexNestedPayload_ValidJson()
    {
        // Arrange
        var payload = new
        {
            user = new { id = 1, name = "John" },
            items = new[] { "a", "b", "c" },
            metadata = new Dictionary<string, object>
            {
                { "created", "2024-01-01" },
                { "count", 42 }
            }
        };

        // Act
        var result = _formatter.FormatEvent("complex.event", payload);

        // Assert
        Assert.Contains("\"user\"", result);
        Assert.Contains("\"items\"", result);
        Assert.Contains("\"metadata\"", result);
    }

    [Fact]
    public void FormatEvent_SpecialCharactersInPayload_EscapedCorrectly()
    {
        // Arrange
        var payload = new { text = "Hello \"World\"! <script>alert('xss')</script>" };

        // Act
        var result = _formatter.FormatEvent("test", payload);

        // Assert - Quotes should be escaped (System.Text.Json uses \u0022 for quotes)
        Assert.Contains("\\u0022World\\u0022", result);
    }

    #endregion
}

