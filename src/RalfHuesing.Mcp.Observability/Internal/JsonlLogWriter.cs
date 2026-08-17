using System.Text.Json;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Thread-safe JSONL writer for the current process instance file.
/// One file per process: {ServerName}_{PID}_{InstanceId}.jsonl
/// opened in append mode for the lifetime of the process.
/// </summary>
internal sealed class JsonlLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly Lock _lock = new();

    internal JsonlLogWriter(ObservabilityContext context)
    {
        var root = string.IsNullOrWhiteSpace(context.Options.LogDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RalfHuesing",
                "McpObservability")
            : context.Options.LogDirectory;

        var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var dir = Path.Combine(root, context.ServerName, dateFolder);
        Directory.CreateDirectory(dir);

        var fileName = $"{context.ServerName}_{context.ProcessId}_{context.InstanceId}.jsonl";
        var filePath = Path.Combine(dir, fileName);

        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };
    }

    internal void WriteRecord(object record)
    {
        var json = JsonSerializer.Serialize(record, JsonlSerializerOptions.Default);
        lock (_lock)
        {
            _writer.WriteLine(json);
        }
    }

    public void Dispose() => _writer.Dispose();
}
