using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Factory helpers for integrating the <c>report_observability_feedback</c>
/// tool into MCP servers that manage their tools manually via
/// <see cref="McpServerOptions.ToolCollection"/> instead of attribute-based
/// discovery (<c>builder.WithTools&lt;T&gt;()</c>).
/// </summary>
public static class McpObservabilityTools
{
    /// <summary>
    /// The default tool name for the agent feedback reporting tool (<c>"report_observability_feedback"</c>).
    /// </summary>
    public const string FeedbackToolName = "report_observability_feedback";

    /// <summary>
    /// Creates the <c>report_observability_feedback</c> tool as an
    /// <see cref="McpServerTool"/> instance, semantically identical to the
    /// tool registered by <see cref="McpObservabilityExtensions.WithObservability"/>,
    /// but usable without reflection on internal types.
    /// </summary>
    /// <param name="services">
    /// Optional service provider used as fallback for resolving observability services
    /// (<c>JsonlLogWriter</c>, <c>ObservabilityContext</c>) when the invocation
    /// request context does not supply one.
    /// </param>
    /// <returns>The feedback tool, ready to be added to a tool collection.</returns>
    public static McpServerTool CreateFeedbackTool(IServiceProvider? services = null)
    {
        // Method group (not a lambda): Delegate.Method keeps parameter names,
        // [Description] attributes and default values for schema inference.
        return McpServerTool.Create(
            (Func<IServiceProvider?, string, string, string, string?, string,
                string?, string?, string?, string>)FeedbackTools.ReportFeedback,
            new McpServerToolCreateOptions
            {
                Name = FeedbackToolName,
                Services = services,
            });
    }

    /// <summary>
    /// Adds the <c>report_observability_feedback</c> tool to an existing tool
    /// collection. Idempotent: when a tool with the same name is already
    /// present, the collection is left unchanged.
    /// </summary>
    /// <param name="tools">The tool collection to extend.</param>
    /// <param name="services">Optional service provider, see <see cref="CreateFeedbackTool"/>.</param>
    public static void AddFeedbackTool(
        this McpServerPrimitiveCollection<McpServerTool> tools,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.TryGetPrimitive(FeedbackToolName, out _))
        {
            return;
        }

        tools.Add(CreateFeedbackTool(services));
    }
}
