using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Registers the call-tool filter that writes a <c>tool_call</c> JSONL record
/// for every MCP tool invocation.
/// </summary>
internal static class ToolCallLoggingHandler
{
    internal static void Register(IMcpServerBuilder builder)
    {
        builder.WithRequestFilters(filters =>
            filters.AddCallToolFilter(next => (request, ct) => ExecuteWithLoggingAsync(request, next, ct)));
    }

    private static async ValueTask<CallToolResult> ExecuteWithLoggingAsync(
        RequestContext<CallToolRequestParams> request,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        CancellationToken cancellationToken)
    {
        var services = request.Services ?? request.Server?.Services;
        var logWriter = services?.GetService<JsonlLogWriter>();
        var ctx = services?.GetService<ObservabilityContext>();

        var stopwatch = Stopwatch.StartNew();
        CallToolResult? result = null;
        Exception? exception = null;

        try
        {
            result = await next(request, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (logWriter is not null && ctx is not null)
            {
                var record = CreateRecord(request, ctx, result, exception, stopwatch.ElapsedMilliseconds);
                logWriter.WriteRecord(record);
            }
        }
    }

    private static ToolCallRecord CreateRecord(
        RequestContext<CallToolRequestParams> request,
        ObservabilityContext ctx,
        CallToolResult? result,
        Exception? exception,
        long durationMs)
    {
        var sanitized = ArgumentSanitizer.Sanitize(
            request.Params?.Arguments as IReadOnlyDictionary<string, System.Text.Json.JsonElement>);

        var isErrorResult = result?.IsError ?? false;

        return new ToolCallRecord(
            SchemaVersion: ObservabilityConstants.SchemaVersion,
            Timestamp: DateTime.UtcNow.ToString(ObservabilityConstants.TimestampFormat, System.Globalization.CultureInfo.InvariantCulture),
            RecordType: ObservabilityConstants.ToolCallRecordType,
            ServerName: ctx.ServerName,
            ServerVersion: ctx.ServerVersion,
            ProcessId: ctx.ProcessId,
            InstanceId: ctx.InstanceId,
            ToolName: request.Params?.Name ?? string.Empty,
            Arguments: sanitized,
            DurationMs: durationMs,
            Success: exception is null,
            IsErrorResult: isErrorResult,
            ErrorMessage: ExtractErrorMessage(result, exception, isErrorResult));
    }

    private static string? ExtractErrorMessage(CallToolResult? result, Exception? exception, bool isErrorResult)
    {
        if (exception is not null)
        {
            return exception.Message;
        }

        if (isErrorResult)
        {
            return result?.Content?.FirstOrDefault()?.ToString();
        }

        return null;
    }
}
