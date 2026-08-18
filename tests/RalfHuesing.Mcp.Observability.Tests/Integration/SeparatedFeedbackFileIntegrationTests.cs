using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace RalfHuesing.Mcp.Observability.Tests.Integration;

[McpServerToolType]
internal sealed class SeparatedTestTools
{
    [McpServerTool(Name = "work_tool")]
    [Description("Executes work.")]
    internal static string DoWork([Description("Input param")] string input) => $"done:{input}";
}

public sealed class SeparatedFeedbackFileIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ToolCallsAndFeedback_AreSeparatedIntoDedicatedFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory,
            ServerName = "SeparatedServer",
            ServerVersion = "3.1.0"
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer()
            .WithStreamServerTransport(serverRead, serverWrite)
            .WithTools<SeparatedTestTools>()
            .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var obsService = host.Services.GetRequiredService<IMcpObservabilityService>();
        Assert.NotNull(obsService.CurrentLogFilePath);
        Assert.NotNull(obsService.CurrentFeedbackLogFilePath);
        Assert.EndsWith(".jsonl", obsService.CurrentLogFilePath, StringComparison.Ordinal);
        Assert.EndsWith(".feedback.jsonl", obsService.CurrentFeedbackLogFilePath, StringComparison.Ordinal);

        // Before any tool calls: neither file exists (lazy creation)
        Assert.False(File.Exists(obsService.CurrentLogFilePath));
        Assert.False(File.Exists(obsService.CurrentFeedbackLogFilePath));

        // 1. Call normal tool
        var workResult = await client.CallToolAsync(new CallToolRequestParams
        {
            Name = "work_tool",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["input"] = JsonSerializer.SerializeToElement("task-alpha")
            }
        }, cancellationToken: ct);

        Assert.NotNull(workResult);
        Assert.False(workResult.IsError ?? false);

        // Tool log now exists, but feedback file still MUST NOT exist
        Assert.True(File.Exists(obsService.CurrentLogFilePath));
        Assert.False(File.Exists(obsService.CurrentFeedbackLogFilePath));

        var toolLines = await ReadAllLinesSharedAsync(obsService.CurrentLogFilePath, ct);
        Assert.Single(toolLines);
        using (var doc = JsonDocument.Parse(toolLines[0]))
        {
            Assert.Equal("tool_call", doc.RootElement.GetProperty("recordType").GetString());
            Assert.Equal("work_tool", doc.RootElement.GetProperty("toolName").GetString());
        }

        // 2. Call feedback tool
        var feedbackResult = await client.CallToolAsync(new CallToolRequestParams
        {
            Name = "report_observability_feedback",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["feedbackType"] = JsonSerializer.SerializeToElement("issue"),
                ["title"] = JsonSerializer.SerializeToElement("Bug in work_tool"),
                ["description"] = JsonSerializer.SerializeToElement("Detailed issue description.")
            }
        }, cancellationToken: ct);

        Assert.NotNull(feedbackResult);
        Assert.False(feedbackResult.IsError ?? false);

        // Feedback file now exists!
        Assert.True(File.Exists(obsService.CurrentFeedbackLogFilePath));

        var feedbackLines = await ReadAllLinesSharedAsync(obsService.CurrentFeedbackLogFilePath, ct);
        Assert.Single(feedbackLines);
        using (var doc = JsonDocument.Parse(feedbackLines[0]))
        {
            Assert.Equal("feedback", doc.RootElement.GetProperty("recordType").GetString());
            Assert.Equal("issue", doc.RootElement.GetProperty("feedbackType").GetString());
            Assert.Equal("Bug in work_tool", doc.RootElement.GetProperty("title").GetString());
        }

        // The regular tool log MUST NOT contain the feedback call
        var updatedToolLines = await ReadAllLinesSharedAsync(obsService.CurrentLogFilePath, ct);
        Assert.Single(updatedToolLines); // still only 1 record (work_tool)

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }
}
