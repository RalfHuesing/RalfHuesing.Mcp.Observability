using System.Text.Json;
using System.Text.Json.Serialization;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Shared record schema for all JSONL entries.
/// All fields match the schema defined in Konzept.md §5.
/// </summary>
internal sealed record ToolCallRecord(
    int SchemaVersion,
    string Timestamp,
    string RecordType,
    string ServerName,
    string ServerVersion,
    int ProcessId,
    string InstanceId,
    string ToolName,
    IReadOnlyDictionary<string, object?>? Arguments,
    long DurationMs,
    bool Success,
    bool IsErrorResult,
    string? ErrorMessage,
    [property: System.Text.Json.Serialization.JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Response = null,
    [property: System.Text.Json.Serialization.JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ResponseLength = 0,
    [property: System.Text.Json.Serialization.JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ResponseLines = 0,
    [property: System.Text.Json.Serialization.JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ResponseTruncated = false,
    [property: System.Text.Json.Serialization.JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NonTextContentBlocks = 0);

internal sealed record FeedbackRecord(
    int SchemaVersion,
    string Timestamp,
    string RecordType,
    string ServerName,
    string ServerVersion,
    int ProcessId,
    string InstanceId,
    string FeedbackType,
    string Title,
    string Description,
    string? RelatedTool,
    string Severity,
    string? ExpectedBehavior,
    string? ActualBehavior,
    string? AdditionalContext);
