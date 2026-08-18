using System.Reflection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Null-object implementation of <see cref="IMcpObservabilityService"/> used when
/// <see cref="McpObservabilityOptions.Enabled"/> is <c>false</c>. Ensures that consumers
/// injecting <see cref="IMcpObservabilityService"/> can safely query properties without
/// null checks or DI activation failures.
/// </summary>
internal sealed class DisabledObservabilityService : IMcpObservabilityService
{
    public bool IsEnabled => false;
    public string ServerName { get; }
    public string ServerVersion { get; }
    public string? CurrentLogFilePath => null;
    public string? CurrentFeedbackLogFilePath => null;
    public int ProcessId { get; }
    public string InstanceId => string.Empty;

    public DisabledObservabilityService(
        McpObservabilityOptions options,
        IOptions<McpServerOptions>? serverOptions = null)
    {
        ProcessId = Environment.ProcessId;

        var info = serverOptions?.Value?.ServerInfo;
        var hasInfoName = info is not null && !string.IsNullOrWhiteSpace(info.Name);

        ServerName = ServerIdentityResolver.ResolveServerName(options, info, hasInfoName);
        ServerVersion = ServerIdentityResolver.ResolveServerVersion(options, info, hasInfoName);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
