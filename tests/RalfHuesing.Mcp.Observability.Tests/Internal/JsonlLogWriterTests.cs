using System.Text.Json;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class JsonlLogWriterTests : TempDirectoryTestBase
{
    [Fact]
    public void WriteRecord_CreatesFileInSpecifiedDirectoryWithCorrectNaming()
    {
        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory
        };
        var context = new ObservabilityContext(options);

        using (var writer = new JsonlLogWriter(context))
        {
            writer.WriteRecord(new { message = "test" });
        }

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var expectedDir = Path.Combine(TempDirectory, context.ServerName, today);
        Assert.True(Directory.Exists(expectedDir));

        var expectedFile = Path.Combine(expectedDir, $"{context.ServerName}_{context.ProcessId}_{context.InstanceId}.jsonl");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public void WriteRecord_AppendsValidJsonLines()
    {
        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory
        };
        var context = new ObservabilityContext(options);

        var toolCallRecord = new ToolCallRecord(
            SchemaVersion: 1,
            Timestamp: DateTime.UtcNow.ToString("O"),
            RecordType: "tool_call",
            ServerName: context.ServerName,
            ServerVersion: context.ServerVersion,
            ProcessId: context.ProcessId,
            InstanceId: context.InstanceId,
            ToolName: "test_tool",
            Arguments: null,
            DurationMs: 42,
            Success: true,
            IsErrorResult: false,
            ErrorMessage: null,
            Response: null,
            ResponseLength: 0,
            ResponseLines: 0,
            ResponseTruncated: false,
            NonTextContentBlocks: 0);

        var feedbackRecord = new FeedbackRecord(
            SchemaVersion: 1,
            Timestamp: DateTime.UtcNow.ToString("O"),
            RecordType: "feedback",
            ServerName: context.ServerName,
            ServerVersion: context.ServerVersion,
            ProcessId: context.ProcessId,
            InstanceId: context.InstanceId,
            FeedbackType: "issue",
            Title: "Bug report",
            Description: "Something failed",
            RelatedTool: "test_tool",
            Severity: "medium",
            ExpectedBehavior: "Success",
            ActualBehavior: "Failure",
            AdditionalContext: null);

        using (var writer = new JsonlLogWriter(context))
        {
            writer.WriteRecord(toolCallRecord);
            writer.WriteRecord(feedbackRecord);
        }

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var filePath = Path.Combine(TempDirectory, context.ServerName, today, $"{context.ServerName}_{context.ProcessId}_{context.InstanceId}.jsonl");
        var lines = File.ReadAllLines(filePath);

        Assert.Equal(2, lines.Length);

        using var doc1 = JsonDocument.Parse(lines[0]);
        Assert.Equal(1, doc1.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("tool_call", doc1.RootElement.GetProperty("recordType").GetString());
        Assert.Equal("test_tool", doc1.RootElement.GetProperty("toolName").GetString());

        using var doc2 = JsonDocument.Parse(lines[1]);
        Assert.Equal(1, doc2.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("feedback", doc2.RootElement.GetProperty("recordType").GetString());
        Assert.Equal("issue", doc2.RootElement.GetProperty("feedbackType").GetString());
    }

    [Fact]
    public async Task WriteRecord_ThreadSafeConcurrentWrites()
    {
        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory
        };
        var context = new ObservabilityContext(options);

        const int recordCount = 50;

        using (var writer = new JsonlLogWriter(context))
        {
            var tasks = Enumerable.Range(0, recordCount).Select(i => Task.Run(() =>
            {
                writer.WriteRecord(new
                {
                    schemaVersion = 1,
                    recordType = "tool_call",
                    index = i
                });
            }));

            await Task.WhenAll(tasks);
        }

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var filePath = Path.Combine(TempDirectory, context.ServerName, today, $"{context.ServerName}_{context.ProcessId}_{context.InstanceId}.jsonl");
        var lines = File.ReadAllLines(filePath);

        Assert.Equal(recordCount, lines.Length);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("tool_call", doc.RootElement.GetProperty("recordType").GetString());
        }
    }
}

