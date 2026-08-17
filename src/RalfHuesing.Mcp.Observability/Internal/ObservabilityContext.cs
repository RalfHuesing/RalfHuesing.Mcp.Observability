using System.Reflection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Holds process-scoped metadata shared by all observability components.
/// Resolved once at startup; safe to consume as a singleton.
/// </summary>
internal sealed class ObservabilityContext
{
    internal string ServerName { get; }
    internal string ServerVersion { get; }
    internal int ProcessId { get; }
    internal string InstanceId { get; }
    internal McpObservabilityOptions Options { get; }

    public ObservabilityContext(McpObservabilityOptions options, IOptions<McpServerOptions>? serverOptions = null)
    {
        Options = options;
        ProcessId = Environment.ProcessId;
        InstanceId = Guid.NewGuid().ToString("N");

        var info = serverOptions?.Value?.ServerInfo;
        if (info is not null && !string.IsNullOrWhiteSpace(info.Name))
        {
            ServerName = info.Name;
            ServerVersion = info.Version ?? string.Empty;
        }
        else
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            ServerName = entryAssembly?.GetName().Name ?? ObservabilityConstants.UnknownServerName;
            ServerVersion = entryAssembly?.GetName().Version?.ToString() ?? string.Empty;
        }
    }
}
