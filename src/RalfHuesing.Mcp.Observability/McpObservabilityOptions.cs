namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Configuration options for MCP server observability.
/// All properties default to enabled; set <see cref="Enabled"/> to <c>false</c>
/// to suppress all observability behaviour in a single switch.
/// </summary>
public sealed class McpObservabilityOptions
{
    /// <summary>
    /// Default message returned by the <c>report_observability_feedback</c> tool
    /// after a successful write. Exposed as a discoverable constant for consumers
    /// who want to restore the default after overriding
    /// <see cref="FeedbackConfirmationMessage"/>.
    /// </summary>
    public const string DefaultFeedbackConfirmationMessage = "Feedback recorded. Thank you.";

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

    /// <summary>
    /// Overrides the server name written to every JSONL record
    /// (<c>serverName</c> field). When <c>null</c> or whitespace, the
    /// value falls back to <c>McpServerOptions.ServerInfo.Name</c>, then
    /// to the entry assembly name, then to <c>"UnknownServer"</c>.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Overrides the server version written to every JSONL record
    /// (<c>serverVersion</c> field). When <c>null</c> or whitespace, the
    /// value falls back to <c>McpServerOptions.ServerInfo.Version</c>, then
    /// to the entry assembly version, then to <see cref="string.Empty"/>.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Message returned by the <c>report_observability_feedback</c> tool after a
    /// successful write. Defaults to
    /// <see cref="DefaultFeedbackConfirmationMessage"/>. The tool itself consumes
    /// this value (wiring in a later step); setting it here is inert until then.
    /// </summary>
    public string FeedbackConfirmationMessage { get; set; } = DefaultFeedbackConfirmationMessage;

    /// <summary>
    /// Master switch for the response content field in the <c>tool_call</c>
    /// record. When <c>false</c>, <c>Response</c> and <c>ResponseTruncated</c>
    /// are omitted from the JSONL output, and
    /// <c>ResponseLength</c>/<c>ResponseLines</c>/<c>NonTextContentBlocks</c>
    /// are omitted when they hold their default value. Consumer-specific
    /// activation via <c>appsettings.json</c> (no global default value here).
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableResponseLogging { get; set; } = true;

    /// <summary>
    /// Hard character limit for the <c>response</c> string. When <c>&gt; 0</c>
    /// and the response length exceeds the limit, the text is truncated and a
    /// <c>... [truncated at N chars]</c> marker is appended. When <c>0</c>
    /// (default) no truncation is applied. Effective only when
    /// <see cref="EnableResponseLogging"/> is <c>true</c>. Set in the consumer
    /// server's <c>appsettings.json</c> (no global default).
    /// </summary>
    public int MaxResponseLength { get; set; }

    /// <summary>
    /// Additional argument keys that <see cref="Internal.ArgumentSanitizer"/>
    /// treats as sensitive (in addition to the built-in list). Comparison is
    /// case-insensitive (<see cref="StringComparer.OrdinalIgnoreCase"/>).
    /// The sanitizer merges this set with its built-in defaults since EPIC-02.
    /// </summary>
    public HashSet<string> AdditionalSensitiveKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
