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
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _disposalStarted;

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
        _operationGate.Wait();
        try
        {
            if (IsDisposalStarted)
            {
                return;
            }

            _writer.WriteLine(json);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    internal async Task FlushAsync(CancellationToken ct = default)
    {
        await _operationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsDisposalStarted)
            {
                await _writer.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (!TryStartDisposal())
        {
            return;
        }

        _operationGate.Wait();
        try
        {
            _writer.Dispose();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!TryStartDisposal())
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private bool IsDisposalStarted => Volatile.Read(ref _disposalStarted) != 0;

    private bool TryStartDisposal() => Interlocked.Exchange(ref _disposalStarted, 1) == 0;
}
