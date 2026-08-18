using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Integration;

[McpServerToolType]
internal sealed class SampleFeedbackTestTools
{
    [McpServerTool(Name = "sample_tool")]
    [Description("Sample tool for testing.")]
    internal static string Run() => "ok";
}

public sealed class McpFeedbackIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ReportFeedback_WritesFeedbackRecordToJsonl()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory,
            FeedbackConfirmationMessage = "Feedback saved for review."
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "FeedbackServer", Version = "2.0.0" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<SampleFeedbackTestTools>()
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        Assert.Contains(tools, t => t.Name == "report_observability_feedback");

        var callParams = new CallToolRequestParams
        {
            Name = "report_observability_feedback",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["feedbackType"] = JsonSerializer.SerializeToElement("issue"),
                ["title"] = JsonSerializer.SerializeToElement("Null reference in parser"),
                ["description"] = JsonSerializer.SerializeToElement("When analyzing nullable syntax an exception occurred."),
                ["relatedTool"] = JsonSerializer.SerializeToElement("sample_tool"),
                ["severity"] = JsonSerializer.SerializeToElement("high"),
                ["expectedBehavior"] = JsonSerializer.SerializeToElement("Parser returns warnings"),
                ["actualBehavior"] = JsonSerializer.SerializeToElement("Parser threw exception"),
                ["additionalContext"] = JsonSerializer.SerializeToElement("Happened with C# 14 syntax")
            }
        };

        var result = await client.CallToolAsync(callParams, cancellationToken: ct);
        Assert.NotNull(result);
        Assert.False(result.IsError ?? false);

        var textContent = result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        Assert.Equal(options.FeedbackConfirmationMessage, textContent);

        var obsService = host.Services.GetRequiredService<IMcpObservabilityService>();
        Assert.NotNull(obsService.CurrentFeedbackLogFilePath);
        Assert.True(File.Exists(obsService.CurrentFeedbackLogFilePath));

        // Tool log should not exist because only feedback was reported
        if (obsService.CurrentLogFilePath is not null)
        {
            Assert.False(File.Exists(obsService.CurrentLogFilePath));
        }

        var lines = await ReadAllLinesSharedAsync(obsService.CurrentFeedbackLogFilePath, ct);
        Assert.Single(lines);

        var feedbackLine = JsonDocument.Parse(lines[0]);

        var root = feedbackLine.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("feedback", root.GetProperty("recordType").GetString());
        Assert.Equal("FeedbackServer", root.GetProperty("serverName").GetString());
        Assert.Equal("2.0.0", root.GetProperty("serverVersion").GetString());
        Assert.Equal("issue", root.GetProperty("feedbackType").GetString());
        Assert.Equal("Null reference in parser", root.GetProperty("title").GetString());
        Assert.Equal("When analyzing nullable syntax an exception occurred.", root.GetProperty("description").GetString());
        Assert.Equal("sample_tool", root.GetProperty("relatedTool").GetString());
        Assert.Equal("high", root.GetProperty("severity").GetString());
        Assert.Equal("Parser returns warnings", root.GetProperty("expectedBehavior").GetString());
        Assert.Equal("Parser threw exception", root.GetProperty("actualBehavior").GetString());
        Assert.Equal("Happened with C# 14 syntax", root.GetProperty("additionalContext").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }
}
