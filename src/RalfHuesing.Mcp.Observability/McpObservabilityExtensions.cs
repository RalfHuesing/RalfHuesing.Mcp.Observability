using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Provides the <see cref="WithObservability"/> extension method for integrating
/// observability into an MCP server with a single line of code.
/// </summary>
public static class McpObservabilityExtensions
{
    /// <summary>
    /// Activates tool-call logging and the feedback tool for this MCP server.
    /// When <paramref name="options"/> is <c>null</c>, all defaults are used (everything enabled,
    /// log directory <c>%LOCALAPPDATA%\RalfHuesing\McpObservability\</c>).
    /// When <see cref="McpObservabilityOptions.Enabled"/> is <c>false</c>, no logging occurs and the
    /// feedback tool is not registered, but a disabled <see cref="IMcpObservabilityService"/> is
    /// registered in DI for safe consumption.
    /// </summary>
    /// <param name="builder">The MCP server builder to extend.</param>
    /// <param name="options">
    /// Optional configuration. Pass <c>null</c> to use all defaults.
    /// </param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IMcpServerBuilder WithObservability(
        this IMcpServerBuilder builder,
        McpObservabilityOptions? options = null)
    {
        var resolvedOptions = options ?? new McpObservabilityOptions();

        if (!resolvedOptions.Enabled)
        {
            builder.Services.AddSingleton(resolvedOptions);
            builder.Services.AddSingleton<IMcpObservabilityService, DisabledObservabilityService>();
            return builder;
        }

        builder.Services.AddSingleton(resolvedOptions);
        builder.Services.AddSingleton<ObservabilityContext>();
        builder.Services.AddSingleton<IMcpObservabilityService>(
            sp => sp.GetRequiredService<ObservabilityContext>());

        if (resolvedOptions.EnableToolCallLogging || resolvedOptions.EnableFeedbackTool)
        {
            builder.Services.AddSingleton<JsonlLogWriter>();
        }

        if (resolvedOptions.EnableToolCallLogging)
        {
            ToolCallLoggingHandler.Register(builder);
        }

        if (resolvedOptions.EnableFeedbackTool)
        {
            builder.WithTools<FeedbackTools>();
            builder.Services.AddSingleton<IPostConfigureOptions<McpServerOptions>,
                ObservabilityPostConfigureOptions>();
        }

        return builder;
    }
}
