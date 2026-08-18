namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Read-only diagnostic view of an MCP server's observability state.
/// Exposed by <c>WithObservability</c> so consumers (health endpoints,
/// admin tools, integration tests) can inspect the active server identity
/// and log file without depending on the internal
/// <c>ObservabilityContext</c> type.
/// </summary>
public interface IMcpObservabilityService
{
    /// <summary>
    /// Mirrors <see cref="McpObservabilityOptions.Enabled"/>.
    /// When <c>false</c>, the package performs no logging and does not
    /// register the feedback tool.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Resolved server name. Resolution order:
    /// <list type="number">
    ///   <item><description><see cref="McpObservabilityOptions.ServerName"/> when set and not whitespace.</description></item>
    ///   <item><description><c>McpServerOptions.ServerInfo.Name</c> when set and not whitespace.</description></item>
    ///   <item><description>The entry assembly's simple name.</description></item>
    ///   <item><description><c>"UnknownServer"</c> as a last-resort fallback.</description></item>
    /// </list>
    /// </summary>
    string ServerName { get; }

    /// <summary>
    /// Resolved server version. Resolution order:
    /// <list type="number">
    ///   <item><description><see cref="McpObservabilityOptions.ServerVersion"/> when set and not whitespace.</description></item>
    ///   <item><description><c>McpServerOptions.ServerInfo.Version</c> when <c>ServerInfo.Name</c> is set.</description></item>
    ///   <item><description>The entry assembly's version, or <see cref="string.Empty"/>.</description></item>
    /// </list>
    /// </summary>
    string ServerVersion { get; }

    /// <summary>
    /// Absolute path to the JSONL file the current process is writing to.
    /// <c>null</c> when <c>EnableToolCallLogging</c> is false or observability is disabled.
    /// </summary>
    string? CurrentLogFilePath { get; }

    /// <summary>
    /// Absolute path to the feedback JSONL file (<c>*.feedback.jsonl</c>) the current process writes feedback reports to.
    /// <c>null</c> when <c>EnableFeedbackTool</c> is false or observability is disabled.
    /// Note: The file itself is created lazily on the first feedback report.
    /// </summary>
    string? CurrentFeedbackLogFilePath { get; }

    /// <summary>
    /// Operating-system process identifier of the current process. Stable
    /// for the lifetime of the process; identical to the <c>processId</c>
    /// field on every JSONL record.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Hex-formatted GUID (no hyphens) generated once at startup. Identical
    /// to the <c>instanceId</c> field on every JSONL record; never reused
    /// within a process.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Asynchronously flushes any pending writes in the underlying log writer to disk.
    /// Returns a completed task immediately when observability is disabled or no log writer is active.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the flush operation.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
