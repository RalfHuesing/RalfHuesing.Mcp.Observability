using System.IO.Pipelines;
using System.Text;

namespace RalfHuesing.Mcp.Observability.Tests.Integration;

/// <summary>
/// Base class for MCP Observability integration tests providing isolated temporary
/// directory lifecycle management, duplex pipe transport streams, and shared log file reader.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected string TempDirectory { get; }

    protected IntegrationTestBase()
    {
        TempDirectory = Path.Combine(
            Path.GetTempPath(),
            "McpObsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectory);
    }

    public virtual void Dispose()
    {
        try
        {
            if (Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignored on test cleanup
        }

        GC.SuppressFinalize(this);
    }

    protected static (Stream ClientRead, Stream ClientWrite, Stream ServerRead, Stream ServerWrite) CreateDuplexPipes()
    {
        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientRead = serverPipe.Reader.AsStream();
        var clientWrite = clientPipe.Writer.AsStream();
        var serverRead = clientPipe.Reader.AsStream();
        var serverWrite = serverPipe.Writer.AsStream();

        return (clientRead, clientWrite, serverRead, serverWrite);
    }

    protected static async Task<string[]> ReadAllLinesSharedAsync(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lines.Add(line);
        }
        return lines.ToArray();
    }
}
