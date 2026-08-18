using System.Reflection;
using ModelContextProtocol.Protocol;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Centralized resolution helper for MCP server identity (name and version).
/// Used by both <see cref="ObservabilityContext"/> and <see cref="DisabledObservabilityService"/>.
/// </summary>
internal static class ServerIdentityResolver
{
    internal static string ResolveServerName(
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

    internal static string ResolveServerVersion(
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
}
