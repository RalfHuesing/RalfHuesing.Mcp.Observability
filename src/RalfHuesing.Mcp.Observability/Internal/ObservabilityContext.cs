using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Holds process-scoped metadata shared by all observability components.
/// Resolved once at startup; safe to consume as a singleton. Implements
/// <see cref="IMcpObservabilityService"/> for read-only diagnostic access.
/// </summary>
internal sealed class ObservabilityContext : IMcpObservabilityService
{
    private readonly IServiceProvider? _services;

    public string ServerName { get; }
    public string ServerVersion { get; }
    public int ProcessId { get; }
    public string InstanceId { get; }

    /// <summary>
    /// Eagerly computed absolute path to the tool-call JSONL log file for this process.
    /// Gating happens in <see cref="CurrentLogFilePath"/>.
    /// </summary>
    internal string LogFilePath { get; }

    /// <summary>
    /// Eagerly computed absolute path to the feedback JSONL log file for this process.
    /// Gating happens in <see cref="CurrentFeedbackLogFilePath"/>.
    /// </summary>
    internal string FeedbackLogFilePath { get; }

    internal McpObservabilityOptions Options { get; }

    public ObservabilityContext(
        McpObservabilityOptions options,
        IOptions<McpServerOptions>? serverOptions = null,
        IServiceProvider? services = null)
    {
        Options = options;
        _services = services;
        ProcessId = Environment.ProcessId;
        InstanceId = Guid.NewGuid().ToString("N");

        var info = serverOptions?.Value?.ServerInfo;
        var hasInfoName = info is not null && !string.IsNullOrWhiteSpace(info.Name);

        ServerName = ServerIdentityResolver.ResolveServerName(options, info, hasInfoName);
        ServerVersion = ServerIdentityResolver.ResolveServerVersion(options, info, hasInfoName);
        var (logPath, feedbackPath) = ResolveLogFilePaths(options, ServerName, ProcessId, InstanceId);
        LogFilePath = logPath;
        FeedbackLogFilePath = feedbackPath;
    }

    /// <inheritdoc />
    public bool IsEnabled => Options.Enabled;

    /// <inheritdoc />
    public string? CurrentLogFilePath =>
        (Options.Enabled && Options.EnableToolCallLogging) ? LogFilePath : null;

    /// <inheritdoc />
    public string? CurrentFeedbackLogFilePath =>
        (Options.Enabled && Options.EnableFeedbackTool) ? FeedbackLogFilePath : null;

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var writer = _services?.GetService<JsonlLogWriter>();
        if (writer is not null)
        {
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var feedbackWriter = _services?.GetService<FeedbackJsonlLogWriter>();
        if (feedbackWriter is not null)
        {
            await feedbackWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static (string LogFilePath, string FeedbackLogFilePath) ResolveLogFilePaths(
        McpObservabilityOptions options,
        string serverName,
        int processId,
        string instanceId)
    {
        var root = string.IsNullOrWhiteSpace(options.LogDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ObservabilityConstants.DefaultCompanyName,
                ObservabilityConstants.DefaultProductName)
            : options.LogDirectory;

        var dateFolder = DateTime.UtcNow.ToString(
            ObservabilityConstants.DateFormat,
            CultureInfo.InvariantCulture);
        var dir = Path.Combine(root, serverName, dateFolder);

        var logFileName = $"{serverName}_{processId}_{instanceId}.jsonl";
        var feedbackFileName = $"{serverName}_{processId}_{instanceId}.feedback.jsonl";

        return (Path.Combine(dir, logFileName), Path.Combine(dir, feedbackFileName));
    }
}
