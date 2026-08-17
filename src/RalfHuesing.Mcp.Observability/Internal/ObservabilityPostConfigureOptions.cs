using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Runs after all <c>McpServerOptions</c> configuration (including consumer
/// callbacks and the SDK's own setup). When the consumer manages tools via a
/// manually assigned <see cref="McpServerOptions.ToolCollection"/>, the
/// feedback tool registered through attribute discovery would be shadowed —
/// this post-configure step appends it to the collection instead.
/// </summary>
internal sealed class ObservabilityPostConfigureOptions(
    McpObservabilityOptions options,
    IServiceProvider services) : IPostConfigureOptions<McpServerOptions>
{
    public void PostConfigure(string? name, McpServerOptions serverOptions)
    {
        if (options.EnableFeedbackTool && serverOptions.ToolCollection is { } collection)
        {
            collection.AddFeedbackTool(services);
        }
    }
}
