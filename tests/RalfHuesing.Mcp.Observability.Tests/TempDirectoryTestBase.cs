using System.IO.Pipelines;
using System.Text;

namespace RalfHuesing.Mcp.Observability.Tests;

/// <summary>
/// Base class for tests requiring an isolated temporary directory lifecycle.
/// </summary>
public abstract class TempDirectoryTestBase : IDisposable
{
    protected string TempDirectory { get; }

    protected TempDirectoryTestBase()
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
}
