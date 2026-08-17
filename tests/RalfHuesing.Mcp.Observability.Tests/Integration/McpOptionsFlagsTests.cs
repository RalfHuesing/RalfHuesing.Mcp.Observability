using System.ComponentModel;
using System.IO.Pipelines;
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
internal sealed class FlagsTestTools
{
    [McpServerTool(Name = "ping")]
    [Description("Simple ping tool.")]
    internal static string Ping() => "pong";
}

public sealed class McpOptionsFlagsTests : IDisposable
{
    private readonly string _tempDirectory;

    public McpOptionsFlagsTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "McpFlagsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignored on cleanup
        }
    }

    [Fact]
    public async Task WhenEnabledIsFalse_DoesNotLogAndDoesNotRegisterFeedbackTool()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            Enabled = false,
            LogDirectory = _tempDirectory
        };

        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientRead = serverPipe.Reader.AsStream();
        var clientWrite = clientPipe.Writer.AsStream();
        var serverRead = clientPipe.Reader.AsStream();
        var serverWrite = serverPipe.Writer.AsStream();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "DisabledServer", Version = "1.0.0" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<FlagsTestTools>()
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        Assert.DoesNotContain(tools, t => t.Name == "report_observability_feedback");
        Assert.Contains(tools, t => t.Name == "ping");

        var callParams = new CallToolRequestParams
        {
            Name = "ping"
        };
        var result = await client.CallToolAsync(callParams, cancellationToken: ct);
        Assert.NotNull(result);

        // No log files should be created
        var jsonlFiles = Directory.GetFiles(_tempDirectory, "*.jsonl", SearchOption.AllDirectories);
        Assert.Empty(jsonlFiles);

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task WhenToolCallLoggingDisabled_OnlyFeedbackIsLogged()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            Enabled = true,
            EnableToolCallLogging = false,
            EnableFeedbackTool = true,
            LogDirectory = _tempDirectory
        };

        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientRead = serverPipe.Reader.AsStream();
        var clientWrite = clientPipe.Writer.AsStream();
        var serverRead = clientPipe.Reader.AsStream();
        var serverWrite = serverPipe.Writer.AsStream();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "FeedbackOnlyServer", Version = "1.0.0" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<FlagsTestTools>()
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        // 1. Call ping (tool call logging is off -> should not be logged)
        var pingParams = new CallToolRequestParams { Name = "ping" };
        await client.CallToolAsync(pingParams, cancellationToken: ct);

        // 2. Call feedback tool (should be logged)
        var feedbackParams = new CallToolRequestParams
        {
            Name = "report_observability_feedback",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["feedbackType"] = JsonSerializer.SerializeToElement("feature_request"),
                ["title"] = JsonSerializer.SerializeToElement("Add streaming support"),
                ["description"] = JsonSerializer.SerializeToElement("Streaming responses would be useful.")
            }
        };
        await client.CallToolAsync(feedbackParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        Assert.True(File.Exists(writer.FilePath));

        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("feedback", doc.RootElement.GetProperty("recordType").GetString());
        Assert.Equal("feature_request", doc.RootElement.GetProperty("feedbackType").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task WhenFeedbackToolDisabled_FeedbackToolIsNotRegistered()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            Enabled = true,
            EnableToolCallLogging = true,
            EnableFeedbackTool = false,
            LogDirectory = _tempDirectory
        };

        var clientPipe = new Pipe();
        var serverPipe = new Pipe();

        var clientRead = serverPipe.Reader.AsStream();
        var clientWrite = clientPipe.Writer.AsStream();
        var serverRead = clientPipe.Reader.AsStream();
        var serverWrite = serverPipe.Writer.AsStream();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "NoFeedbackToolServer", Version = "1.0.0" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<FlagsTestTools>()
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        Assert.DoesNotContain(tools, t => t.Name == "report_observability_feedback");
        Assert.Contains(tools, t => t.Name == "ping");

        var pingParams = new CallToolRequestParams { Name = "ping" };
        await client.CallToolAsync(pingParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        Assert.True(File.Exists(writer.FilePath));

        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("tool_call", doc.RootElement.GetProperty("recordType").GetString());
        Assert.Equal("ping", doc.RootElement.GetProperty("toolName").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    private static async Task<string[]> ReadAllLinesSharedAsync(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var lines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lines.Add(line);
        }
        return lines.ToArray();
    }
}
