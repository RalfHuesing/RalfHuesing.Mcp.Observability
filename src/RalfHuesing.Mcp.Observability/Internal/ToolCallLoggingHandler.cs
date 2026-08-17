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
        var additionalKeys = ctx.Options.AdditionalSensitiveKeys;
        var sanitized = ArgumentSanitizer.Sanitize(request.Params?.Arguments, additionalKeys);
        var isErrorResult = result?.IsError ?? false;
        var extracted = ExtractResponse(result, additionalKeys, ctx.Options);

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
            ErrorMessage: ExtractErrorMessage(result, exception, isErrorResult),
            Response: extracted.Response,
            ResponseLength: extracted.Length,
            ResponseLines: extracted.Lines,
            ResponseTruncated: extracted.Truncated,
            NonTextContentBlocks: extracted.NonTextCount);
    }

    internal static ResponseExtraction ExtractResponse(
        CallToolResult? result,
        HashSet<string> additionalKeys,
        McpObservabilityOptions options)
    {
        if (result?.Content is null || result.Content.Count == 0)
        {
            return ResponseExtraction.Empty;
        }

        var (rawText, nonTextCount) = ConcatResponseContent(result.Content);
        var length = rawText.Length;
        var lines = length == 0 ? 0 : rawText.Count(c => c == '\n') + 1;

        if (!options.EnableResponseLogging)
        {
            return new ResponseExtraction(null, length, lines, false, nonTextCount);
        }

        var (response, truncated) = BuildResponseText(rawText, additionalKeys, options);
        return new ResponseExtraction(response, length, lines, truncated, nonTextCount);
    }

    private static (string RawText, int NonTextCount) ConcatResponseContent(
        IList<ContentBlock> content)
    {
        var builder = new System.Text.StringBuilder();
        var nonTextCount = 0;
        foreach (var block in content)
        {
            switch (block)
            {
                case TextContentBlock text:
                    AppendTextBlock(builder, text.Text);
                    break;
                default:
                    nonTextCount++;
                    break;
            }
        }

        return (builder.ToString(), nonTextCount);
    }

    private static void AppendTextBlock(System.Text.StringBuilder builder, string text)
    {
        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(text);
    }

    private static (string Response, bool Truncated) BuildResponseText(
        string rawText,
        HashSet<string> additionalKeys,
        McpObservabilityOptions options)
    {
        var sanitized = ArgumentSanitizer.Sanitize(rawText, additionalKeys) ?? string.Empty;
        if (options.MaxResponseLength <= 0 || sanitized.Length <= options.MaxResponseLength)
        {
            return (sanitized, false);
        }

        var truncated = sanitized.Substring(0, options.MaxResponseLength) +
            "... [truncated at " + options.MaxResponseLength + " chars]";
        return (truncated, true);
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

/// <summary>
/// Result of <see cref="ToolCallLoggingHandler.ExtractResponse"/>: the
/// sanitized response text plus metadata captured before sanitization or
/// truncation, so consumers can keep accurate statistics regardless of
/// redaction or length limits.
/// </summary>
internal readonly record struct ResponseExtraction(
    string? Response,
    int Length,
    int Lines,
    bool Truncated,
    int NonTextCount)
{
    public static readonly ResponseExtraction Empty = new(null, 0, 0, false, 0);
}
