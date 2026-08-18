using System.Text;
using System.Text.Json;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Abstract base class for thread-safe JSONL file writers.
/// Opens the file stream lazily on first write in append mode for the lifetime of the process.
/// Supports both synchronous and asynchronous disposal and explicit flushing.
/// </summary>
internal abstract class JsonlLogWriterBase : IDisposable, IAsyncDisposable
{
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _disposalStarted;

    protected JsonlLogWriterBase(string filePath)
    {
        FilePath = filePath;
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

            EnsureWriterInitialized();
            _writer!.WriteLine(json);
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
            if (!IsDisposalStarted && _writer is not null)
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
            _writer?.Dispose();
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
            if (_writer is not null)
            {
                await _writer.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void EnsureWriterInitialized()
    {
        if (_writer is not null)
        {
            return;
        }

        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);

        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    private bool IsDisposalStarted => Volatile.Read(ref _disposalStarted) != 0;

    private bool TryStartDisposal() => Interlocked.Exchange(ref _disposalStarted, 1) == 0;
}
