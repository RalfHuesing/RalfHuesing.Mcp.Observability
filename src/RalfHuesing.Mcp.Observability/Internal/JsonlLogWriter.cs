namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Thread-safe JSONL writer for tool-call records.
/// One file per process: {ServerName}_{PID}_{InstanceId}.jsonl
/// opened lazily in append mode upon the first tool-call write.
/// </summary>
internal sealed class JsonlLogWriter : JsonlLogWriterBase
{
    public JsonlLogWriter(ObservabilityContext context)
        : base(context.LogFilePath)
    {
    }

    internal JsonlLogWriter(string filePath)
        : base(filePath)
    {
    }
}
