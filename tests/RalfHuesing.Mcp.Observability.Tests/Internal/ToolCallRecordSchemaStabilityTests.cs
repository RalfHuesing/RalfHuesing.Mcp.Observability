using System.Text.Json;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class ToolCallRecordSchemaStabilityTests
{
    [Fact]
    public void ToolCallRecord_AlwaysContainsResponseFields()
    {
        // @covers ToolCallRecord
        var rawArguments = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement("hello world"),
            ["password"] = JsonSerializer.SerializeToElement("secret123")
        };

        var sanitizedArguments = ArgumentSanitizer.Sanitize(rawArguments);

        var record = new ToolCallRecord(
            SchemaVersion: 1,
            Timestamp: "2026-08-17T17:22:01.1230000Z",
            RecordType: "tool_call",
            ServerName: "TestServer",
            ServerVersion: "1.2.3",
            ProcessId: 12345,
            InstanceId: "abc123def456",
            ToolName: "echo",
            Arguments: sanitizedArguments,
            DurationMs: 42,
            Success: true,
            IsErrorResult: false,
            ErrorMessage: null,
            Response: null,
            ResponseLength: 0,
            ResponseLines: 0,
            ResponseTruncated: false,
            NonTextContentBlocks: 0);

        var actual = JsonSerializer.Serialize(record, JsonlSerializerOptions.Default);

        // Canonical greenfield schema: all tool-call response fields are
        // explicit, even when response content logging is disabled.
        const string baseline =
            "{\"schemaVersion\":1," +
            "\"timestamp\":\"2026-08-17T17:22:01.1230000Z\"," +
            "\"recordType\":\"tool_call\"," +
            "\"serverName\":\"TestServer\"," +
            "\"serverVersion\":\"1.2.3\"," +
            "\"processId\":12345," +
            "\"instanceId\":\"abc123def456\"," +
            "\"toolName\":\"echo\"," +
            "\"arguments\":{\"text\":\"hello world\",\"password\":\"***REDACTED***\"}," +
            "\"durationMs\":42," +
            "\"success\":true," +
            "\"isErrorResult\":false," +
            "\"errorMessage\":null," +
            "\"response\":null," +
            "\"responseLength\":0," +
            "\"responseLines\":0," +
            "\"responseTruncated\":false," +
            "\"nonTextContentBlocks\":0}";

        Assert.Equal(baseline, actual);
    }

    [Fact]
    public void ToolCallRecord_WithResponseContent_ContainsResponseFields()
    {
        // @covers ToolCallRecord
        var rawArguments = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement("hello world")
        };
        var sanitizedArguments = ArgumentSanitizer.Sanitize(rawArguments);

        var record = new ToolCallRecord(
            SchemaVersion: 1,
            Timestamp: "2026-08-17T17:22:01.1230000Z",
            RecordType: "tool_call",
            ServerName: "TestServer",
            ServerVersion: "1.2.3",
            ProcessId: 12345,
            InstanceId: "abc123def456",
            ToolName: "echo",
            Arguments: sanitizedArguments,
            DurationMs: 42,
            Success: true,
            IsErrorResult: false,
            ErrorMessage: null,
            Response: "echo:hello",
            ResponseLength: 11,
            ResponseLines: 1,
            ResponseTruncated: true,
            NonTextContentBlocks: 2);

        var json = JsonSerializer.Serialize(record, JsonlSerializerOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("echo:hello", root.GetProperty("response").GetString());
        Assert.Equal(11, root.GetProperty("responseLength").GetInt32());
        Assert.Equal(1, root.GetProperty("responseLines").GetInt32());
        Assert.True(root.GetProperty("responseTruncated").GetBoolean());
        Assert.Equal(2, root.GetProperty("nonTextContentBlocks").GetInt32());
    }
}
