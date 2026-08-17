using System.Text.Json;

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
    string? Response,
    int ResponseLength,
    int ResponseLines,
    bool ResponseTruncated,
    int NonTextContentBlocks);

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
