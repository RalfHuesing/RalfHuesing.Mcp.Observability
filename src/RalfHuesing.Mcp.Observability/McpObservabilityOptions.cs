namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Configuration options for MCP server observability.
/// All properties default to enabled; set <see cref="Enabled"/> to <c>false</c>
/// to suppress all observability behaviour in a single switch.
/// </summary>
public sealed class McpObservabilityOptions
{
    /// <summary>
    /// Master switch. When <c>false</c>, no logging occurs and the feedback tool is not registered.
    /// Default: <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Writes a <c>tool_call</c> JSONL record for every MCP tool invocation.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableToolCallLogging { get; set; } = true;

    /// <summary>
    /// Registers the <c>report_observability_feedback</c> MCP tool so agents can
    /// report issues and feature requests.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableFeedbackTool { get; set; } = true;

    /// <summary>
    /// Overrides the default log root directory.
    /// When <c>null</c>, logs are written to
    /// <c>%LOCALAPPDATA%\RalfHuesing\McpObservability\</c>.
    /// </summary>
    public string? LogDirectory { get; set; }
}
