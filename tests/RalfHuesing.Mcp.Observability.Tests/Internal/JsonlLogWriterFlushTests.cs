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
    public async Task FlushAsync_WhenNoWritesOccurred_DoesNotCreateFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var context = new ObservabilityContext(options);

        using var writer = new JsonlLogWriter(context);
        await writer.FlushAsync(ct);

        Assert.False(File.Exists(writer.FilePath));
    }

    [Fact]
    public async Task DisposeAsync_WhenNoWritesOccurred_DoesNotCreateFile()
    {
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var context = new ObservabilityContext(options);
        string logPath;

        await using (var writer = new JsonlLogWriter(context))
        {
            logPath = writer.FilePath;
        }

        Assert.False(File.Exists(logPath));
    }

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

    [Fact]
    public async Task FlushAsync_ConcurrentWithWrites_ProducesValidJsonLines()
    {
        var ct = TestContext.Current.CancellationToken;
        var writer = CreateWriter();
        const int recordCount = 100;

        var writes = Enumerable.Range(0, recordCount)
            .Select(index => Task.Run(() => writer.WriteRecord(new { index }), ct));
        var flushes = Enumerable.Range(0, 10)
            .Select(_ => writer.FlushAsync(ct));

        await Task.WhenAll(writes.Concat(flushes));
        await writer.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(writer.FilePath, ct);
        Assert.Equal(recordCount, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.True(document.RootElement.TryGetProperty("index", out _));
        }
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentWithWrites_IsIdempotentAndWritesWholeLines()
    {
        var ct = TestContext.Current.CancellationToken;
        var writer = CreateWriter();
        writer.WriteRecord(new { index = -1 });
        var writes = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() => writer.WriteRecord(new { index }), ct));

        var disposal = writer.DisposeAsync().AsTask();
        await Task.WhenAll(writes.Append(disposal));
        writer.Dispose();
        await writer.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(writer.FilePath, ct);
        Assert.Contains(lines, line => line.Contains("\"index\":-1", StringComparison.Ordinal));
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.True(document.RootElement.TryGetProperty("index", out _));
        }
    }

    private JsonlLogWriter CreateWriter()
    {
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        return new JsonlLogWriter(new ObservabilityContext(options));
    }
}

