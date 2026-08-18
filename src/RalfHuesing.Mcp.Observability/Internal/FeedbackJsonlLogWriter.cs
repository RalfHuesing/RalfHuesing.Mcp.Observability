namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Thread-safe JSONL writer for the current process instance feedback file.
/// One file per process: {ServerName}_{PID}_{InstanceId}.feedback.jsonl
/// opened lazily in append mode upon the first feedback report.
/// </summary>
internal sealed class FeedbackJsonlLogWriter : JsonlLogWriterBase
{
    public FeedbackJsonlLogWriter(ObservabilityContext context)
        : base(context.FeedbackLogFilePath)
    {
    }

    internal FeedbackJsonlLogWriter(string filePath)
        : base(filePath)
    {
    }
}
