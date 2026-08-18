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
    /// Eagerly computed absolute path to the JSONL log file for this process.
    /// Independent of whether <c>EnableToolCallLogging</c> or
    /// <c>EnableFeedbackTool</c> is enabled; the gating happens in
    /// <see cref="CurrentLogFilePath"/>.
    /// </summary>
    internal string LogFilePath { get; }

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

        ServerName = ResolveServerName(options, info, hasInfoName);
        ServerVersion = ResolveServerVersion(options, info, hasInfoName);
        LogFilePath = ResolveLogFilePath(options, ServerName, ProcessId, InstanceId);
    }

    /// <inheritdoc />
    public bool IsEnabled => Options.Enabled;

    /// <inheritdoc />
    public string? CurrentLogFilePath =>
        (Options.Enabled && (Options.EnableToolCallLogging || Options.EnableFeedbackTool)) ? LogFilePath : null;

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var writer = _services?.GetService<JsonlLogWriter>();
        if (writer is not null)
        {
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ResolveServerName(
        McpObservabilityOptions options,
        Implementation? info,
        bool hasInfoName)
    {
        if (!string.IsNullOrWhiteSpace(options.ServerName))
        {
            return options.ServerName;
        }

        if (hasInfoName)
        {
            return info!.Name;
        }

        return Assembly.GetEntryAssembly()?.GetName().Name
            ?? ObservabilityConstants.UnknownServerName;
    }

    private static string ResolveServerVersion(
        McpObservabilityOptions options,
        Implementation? info,
        bool hasInfoName)
    {
        if (!string.IsNullOrWhiteSpace(options.ServerVersion))
        {
            return options.ServerVersion;
        }

        if (hasInfoName)
        {
            return info!.Version ?? string.Empty;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? string.Empty;
    }

    private static string ResolveLogFilePath(
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

        var fileName = $"{serverName}_{processId}_{instanceId}.jsonl";
        return Path.Combine(dir, fileName);
    }
}
