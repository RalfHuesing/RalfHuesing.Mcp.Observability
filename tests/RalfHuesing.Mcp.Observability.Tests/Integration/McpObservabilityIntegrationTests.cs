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
internal sealed class EchoTool
{
    [McpServerTool(Name = "echo")]
    [Description("Echoes input text.")]
    internal static string Echo([Description("Text to echo")] string text) => $"echo:{text}";
}

public sealed class McpObservabilityIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public McpObservabilityIntegrationTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "McpObsIntegration_" + Guid.NewGuid().ToString("N"));
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
    public async Task ToolCall_WritesToolCallRecordToJsonl()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
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
            serverOptions.ServerInfo = new() { Name = "TestServer", Version = "1.2.3" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<EchoTool>()
        .WithObservability(options);

        var host = builder.Build();
        await host.StartAsync(ct);

        var clientTransport = new StreamClientTransport(clientWrite, clientRead);
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var callParams = new CallToolRequestParams
        {
            Name = "echo",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("hello world"),
                ["password"] = JsonSerializer.SerializeToElement("secret123")
            }
        };

        var result = await client.CallToolAsync(callParams, cancellationToken: ct);
        Assert.NotNull(result);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        Assert.True(File.Exists(writer.FilePath), $"Expected file {writer.FilePath} does not exist.");

        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("tool_call", root.GetProperty("recordType").GetString());
        Assert.Equal("TestServer", root.GetProperty("serverName").GetString());
        Assert.Equal("1.2.3", root.GetProperty("serverVersion").GetString());
        Assert.Equal("echo", root.GetProperty("toolName").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("isErrorResult").GetBoolean());

        var args = root.GetProperty("arguments");
        Assert.Equal("hello world", args.GetProperty("text").GetString());
        Assert.Equal("***REDACTED***", args.GetProperty("password").GetString());

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
