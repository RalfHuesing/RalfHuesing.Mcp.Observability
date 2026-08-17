using System.Text.Json;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class ToolCallRecordSchemaStabilityTests
{
    [Fact]
    public void ToolCallRecord_WithResponseLoggingDisabled_IsByteIdenticalToV1_0_0()
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
            ErrorMessage: null);

        var actual = JsonSerializer.Serialize(record, JsonlSerializerOptions.Default);

        // Baseline: v1.0.0 output, 14 fields in camelCase, no response fields
        // (the 5 new fields are all at default values, omitted via
        // JsonIgnoreCondition.WhenWritingDefault/WhenWritingNull).
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
            "\"errorMessage\":null}";

        Assert.Equal(baseline, actual);
    }

    [Fact]
    public void ToolCallRecord_WithResponseLoggingEnabled_ContainsResponseFields()
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
