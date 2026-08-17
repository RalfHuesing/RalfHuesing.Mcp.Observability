namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Centralized constants for JSONL schema invariants, default paths, and protocol values.
/// </summary>
internal static class ObservabilityConstants
{
    internal const int SchemaVersion = 1;
    internal const string ToolCallRecordType = "tool_call";
    internal const string FeedbackRecordType = "feedback";
    internal const string DateFormat = "yyyy-MM-dd";
    internal const string TimestampFormat = "O";
    internal const string RedactedMarker = "***REDACTED***";
    internal const string DefaultCompanyName = "RalfHuesing";
    internal const string DefaultProductName = "McpObservability";
    internal const string DefaultFeedbackResponse = "Feedback recorded. Thank you.";
    internal const string DefaultSeverity = "medium";
    internal const string UnknownServerName = "UnknownServer";
}
