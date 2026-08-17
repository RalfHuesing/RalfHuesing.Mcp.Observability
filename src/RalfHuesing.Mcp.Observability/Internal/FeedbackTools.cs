using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Provides the <c>report_observability_feedback</c> MCP tool.
/// Registered only when <see cref="McpObservabilityOptions.EnableFeedbackTool"/> is <c>true</c>.
/// </summary>
[McpServerToolType]
internal sealed class FeedbackTools
{
    [McpServerTool(Name = ObservabilityConstants.FeedbackToolName)]
    [Description(
        "Report an issue or a feature request about this MCP server. " +
        "Use this tool whenever something is wrong (bugs, false positives, unexpected results, confusing output) " +
        "or when a needed capability is missing. " +
        "After reporting, continue with the best available workaround.")]
    internal static string ReportFeedback(
        IServiceProvider? services,
        [Description("Type of feedback: 'issue' or 'feature_request'.")] string feedbackType,
        [Description("Short, clear title (max 120 characters).")] string title,
        [Description("Detailed description of what happened or what is missing.")] string description,
        [Description("Name of the affected MCP tool, if known.")] string? relatedTool = null,
        [Description("Severity level: 'low', 'medium', or 'high'. Default: 'medium'.")] string severity = ObservabilityConstants.DefaultSeverity,
        [Description("What the agent expected to happen.")] string? expectedBehavior = null,
        [Description("What actually happened.")] string? actualBehavior = null,
        [Description("Any additional free-form context.")] string? additionalContext = null)
    {
        // services may be null in test scenarios; gracefully fall back
        var logWriter = services?.GetService<JsonlLogWriter>();
        var ctx = services?.GetService<ObservabilityContext>();

        if (logWriter is not null && ctx is not null)
        {
            var record = new FeedbackRecord(
                SchemaVersion: ObservabilityConstants.SchemaVersion,
                Timestamp: DateTime.UtcNow.ToString(ObservabilityConstants.TimestampFormat, System.Globalization.CultureInfo.InvariantCulture),
                RecordType: ObservabilityConstants.FeedbackRecordType,
                ServerName: ctx.ServerName,
                ServerVersion: ctx.ServerVersion,
                ProcessId: ctx.ProcessId,
                InstanceId: ctx.InstanceId,
                FeedbackType: feedbackType,
                Title: title,
                Description: description,
                RelatedTool: relatedTool,
                Severity: severity,
                ExpectedBehavior: expectedBehavior,
                ActualBehavior: actualBehavior,
                AdditionalContext: additionalContext);

            logWriter.WriteRecord(record);
        }

        return ObservabilityConstants.DefaultFeedbackResponse;
    }
}
