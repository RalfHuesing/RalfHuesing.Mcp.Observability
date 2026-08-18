using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Integration;

[McpServerToolType]
internal sealed class ManualSampleTools
{
    [McpServerTool(Name = "manual_echo")]
    [Description("Echo tool for manual collection tests.")]
    internal static string Echo(string input) => input;
}

/// <summary>
/// Integration tests for servers that manage their tools via a manually assigned
/// <see cref="McpServerOptions.ToolCollection"/> instead of attribute discovery.
/// Verifies the tool-shadow fix: <c>WithObservability</c> appends the feedback
/// tool to manual collections (idempotently), without reflection on internals.
/// </summary>
public sealed class McpServerOptionsToolCollectionTests : IntegrationTestBase
{
    [Fact]
    public async Task ManualToolCollection_WithObservability_FeedbackToolIsListed()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "ManualServer", Version = "1.0.0" };
            serverOptions.ToolCollection = [CreateSampleTool()];
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        await using var client = await CreateClientAsync(clientWrite, clientRead, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);

        Assert.Contains(tools, t => t.Name == "report_observability_feedback");
        Assert.Contains(tools, t => t.Name == "manual_echo");

        await host.StopAsync(ct);
    }

    [Fact]
    public async Task ManualToolCollection_PreAddedFeedbackTool_StaysIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "ManualServer", Version = "1.0.0" };
            serverOptions.ToolCollection = collection;
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithObservability(options);

        var host = builder.Build();

        // Consumer attaches the feedback tool up-front; post-configure must not duplicate it.
        collection.AddFeedbackTool(host.Services);

        await host.StartAsync(ct);

        await using var client = await CreateClientAsync(clientWrite, clientRead, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);

        Assert.Single(tools, t => t.Name == "report_observability_feedback");

        await host.StopAsync(ct);
    }

    [Fact]
    public async Task ManualToolCollection_FeedbackToolCall_WritesFeedbackRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions { LogDirectory = TempDirectory };
        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "ManualServer", Version = "1.0.0" };
            serverOptions.ToolCollection = [CreateSampleTool()];
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        await using var client = await CreateClientAsync(clientWrite, clientRead, ct);

        var result = await client.CallToolAsync(new CallToolRequestParams
        {
            Name = "report_observability_feedback",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["feedbackType"] = JsonSerializer.SerializeToElement("issue"),
                ["title"] = JsonSerializer.SerializeToElement("Manual collection feedback"),
                ["description"] = JsonSerializer.SerializeToElement("Reported via manually assigned ToolCollection."),
            }
        }, cancellationToken: ct);

        Assert.False(result.IsError ?? false);
        var text = result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        Assert.Equal(McpObservabilityOptions.DefaultFeedbackConfirmationMessage, text);

        var writer = host.Services.GetRequiredService<FeedbackJsonlLogWriter>();
        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        var feedback = lines.Select(l => JsonDocument.Parse(l))
            .First(d => d.RootElement.GetProperty("recordType").GetString() == "feedback");

        var root = feedback.RootElement;
        Assert.Equal("ManualServer", root.GetProperty("serverName").GetString());
        Assert.Equal("issue", root.GetProperty("feedbackType").GetString());
        Assert.Equal("Manual collection feedback", root.GetProperty("title").GetString());

        await host.StopAsync(ct);
    }

    private static McpServerTool CreateSampleTool()
        => McpServerTool.Create(
            (Func<string, string>)ManualSampleTools.Echo,
            new McpServerToolCreateOptions { Name = "manual_echo" });

    private static async Task<McpClient> CreateClientAsync(
        Stream clientWrite, Stream clientRead, CancellationToken ct)
    {
        var transport = new StreamClientTransport(clientWrite, clientRead);
        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}

