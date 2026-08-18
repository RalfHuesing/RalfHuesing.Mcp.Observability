namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Thread-safe JSONL writer for the current process instance feedback file.
/// One file per process: {ServerName}_{PID}_{InstanceId}.feedback.jsonl
/// opened lazily in append mode upon the first feedback report.
/// </summary>
internal sealed class FeedbackJsonlLogWriter : IDisposable, IAsyncDisposable
{
    private readonly JsonlLogWriter _innerWriter;

    public FeedbackJsonlLogWriter(ObservabilityContext context)
        : this(context.FeedbackLogFilePath)
    {
    }

    internal FeedbackJsonlLogWriter(string filePath)
    {
        _innerWriter = new JsonlLogWriter(filePath);
    }

    internal string FilePath => _innerWriter.FilePath;

    internal void WriteRecord(object record) => _innerWriter.WriteRecord(record);

    internal Task FlushAsync(CancellationToken ct = default) => _innerWriter.FlushAsync(ct);

    public void Dispose() => _innerWriter.Dispose();

    public ValueTask DisposeAsync() => _innerWriter.DisposeAsync();
}
