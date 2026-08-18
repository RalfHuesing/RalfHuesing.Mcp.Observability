using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace RalfHuesing.Mcp.Observability.Tests.Integration;

public sealed class McpObservabilityToolsTests : IntegrationTestBase
{
    [Fact]
    public void FeedbackToolName_ConstantMatchesExpectedValue()
    {
        Assert.Equal("report_observability_feedback", McpObservabilityTools.FeedbackToolName);
    }

    [Fact]
    public void CreateFeedbackTool_WithoutServices_CreatesValidTool()
    {
        var tool = McpObservabilityTools.CreateFeedbackTool();

        Assert.NotNull(tool);
        Assert.Equal(McpObservabilityTools.FeedbackToolName, tool.ProtocolTool.Name);
        Assert.NotNull(tool.ProtocolTool.Description);
        Assert.NotEmpty(tool.ProtocolTool.Description);
    }

    [Fact]
    public void AddFeedbackTool_WithoutServices_AddsToolToCollection()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();

        collection.AddFeedbackTool();

        Assert.Single(collection);
        Assert.True(collection.TryGetPrimitive(McpObservabilityTools.FeedbackToolName, out var tool));
        Assert.NotNull(tool);
        Assert.Equal(McpObservabilityTools.FeedbackToolName, tool.ProtocolTool.Name);
    }

    [Fact]
    public void AddFeedbackTool_WhenAlreadyPresent_IsIdempotent()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();

        collection.AddFeedbackTool();
        collection.AddFeedbackTool();

        Assert.Single(collection);
    }

    [Fact]
    public async Task CreateFeedbackTool_InvokedViaClientWithoutServices_ReturnsDefaultConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        collection.AddFeedbackTool(); // no services passed

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "NoServicesServer", Version = "1.0.0" };
            serverOptions.ToolCollection = collection;
        })
        .WithStreamServerTransport(serverRead, serverWrite);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var callParams = new CallToolRequestParams
        {
            Name = McpObservabilityTools.FeedbackToolName,
            Arguments = new Dictionary<string, JsonElement>
            {
                ["feedbackType"] = JsonSerializer.SerializeToElement("issue"),
                ["title"] = JsonSerializer.SerializeToElement("Test issue"),
                ["description"] = JsonSerializer.SerializeToElement("Description of issue")
            }
        };

        var result = await client.CallToolAsync(callParams, cancellationToken: ct);

        Assert.NotNull(result);
        Assert.False(result.IsError ?? false);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content);

        var textContent = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Equal(McpObservabilityOptions.DefaultFeedbackConfirmationMessage, textContent.Text);

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }
}
