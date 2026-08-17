using System.Text.Json;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

/// <summary>
/// Verifies the asynchronous writer lifecycle (<see cref="IAsyncDisposable"/>
/// and <see cref="JsonlLogWriter.FlushAsync"/>).
/// </summary>
public sealed class JsonlLogWriterFlushTests : TempDirectoryTestBase
{
    [Fact]
    public async Task FlushAsync_FlushesPendingWritesToFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var context = new ObservabilityContext(options);

        using var writer = new JsonlLogWriter(context);
        writer.WriteRecord(new { message = "flushed-payload" });

        await writer.FlushAsync(ct);

        Assert.True(File.Exists(writer.FilePath));
        using var stream = new FileStream(writer.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        Assert.Contains("flushed-payload", content);
    }

    [Fact]
    public async Task DisposeAsync_FlushesAndClosesStreamProperly()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var context = new ObservabilityContext(options);
        string logPath;

        await using (var writer = new JsonlLogWriter(context))
        {
            logPath = writer.FilePath;
            writer.WriteRecord(new { message = "async-disposed-payload" });
        }

        Assert.True(File.Exists(logPath));
        var lines = await File.ReadAllLinesAsync(logPath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("async-disposed-payload", doc.RootElement.GetProperty("message").GetString());
    }
}

