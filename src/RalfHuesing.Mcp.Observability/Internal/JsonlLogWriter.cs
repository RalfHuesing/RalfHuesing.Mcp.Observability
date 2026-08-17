using System.Text;
using System.Text.Json;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Thread-safe JSONL writer for the current process instance file.
/// One file per process: {ServerName}_{PID}_{InstanceId}.jsonl
/// opened in append mode for the lifetime of the process.
/// Supports both synchronous and asynchronous disposal and explicit flushing.
/// </summary>
internal sealed class JsonlLogWriter : IDisposable, IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly Lock _lock = new();

    public JsonlLogWriter(ObservabilityContext context)
    {
        var dir = Path.GetDirectoryName(context.LogFilePath)!;
        Directory.CreateDirectory(dir);

        FilePath = context.LogFilePath;
        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    internal string FilePath { get; }

    internal void WriteRecord(object record)
    {
        var json = JsonSerializer.Serialize(record, JsonlSerializerOptions.Default);
        lock (_lock)
        {
            _writer.WriteLine(json);
        }
    }

    internal async Task FlushAsync(CancellationToken ct = default)
    {
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync().ConfigureAwait(false);
    }
}
