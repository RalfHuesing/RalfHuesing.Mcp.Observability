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
internal sealed class ServerNameOverrideEchoTool
{
    [McpServerTool(Name = "echo")]
    [Description("Echoes input text.")]
    internal static string Echo([Description("Text to echo")] string text) => $"echo:{text}";
}

public sealed class McpOptionsServerNameOverrideTests : IntegrationTestBase
{
    [Fact]
    public async Task ServerName_OptionOverridesServerInfo_Name()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            ServerName = "CustomName",
            LogDirectory = TempDirectory
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "SdkName", Version = "1.2.3" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<ServerNameOverrideEchoTool>()
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
                ["text"] = JsonSerializer.SerializeToElement("hello")
            }
        };
        await client.CallToolAsync(callParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("CustomName", root.GetProperty("serverName").GetString());
        Assert.Equal("1.2.3", root.GetProperty("serverVersion").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task ServerVersion_OptionOverridesServerInfo_Version()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            ServerVersion = "9.9.9",
            LogDirectory = TempDirectory
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "SdkName", Version = "1.2.3" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<ServerNameOverrideEchoTool>()
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
                ["text"] = JsonSerializer.SerializeToElement("hello")
            }
        };
        await client.CallToolAsync(callParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("SdkName", root.GetProperty("serverName").GetString());
        Assert.Equal("9.9.9", root.GetProperty("serverVersion").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task BothOptionsSet_BothAppearInRecord()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            ServerName = "X",
            ServerVersion = "Y",
            LogDirectory = TempDirectory
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "SdkName", Version = "1.2.3" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<ServerNameOverrideEchoTool>()
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
                ["text"] = JsonSerializer.SerializeToElement("hello")
            }
        };
        await client.CallToolAsync(callParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("X", root.GetProperty("serverName").GetString());
        Assert.Equal("Y", root.GetProperty("serverVersion").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task BothOptionsNull_FallsBackToServerInfo()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory
        };

        var (clientRead, clientWrite, serverRead, serverWrite) = CreateDuplexPipes();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer(serverOptions =>
        {
            serverOptions.ServerInfo = new() { Name = "SdkName", Version = "1.2.3" };
        })
        .WithStreamServerTransport(serverRead, serverWrite)
        .WithTools<ServerNameOverrideEchoTool>()
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
                ["text"] = JsonSerializer.SerializeToElement("hello")
            }
        };
        await client.CallToolAsync(callParams, cancellationToken: ct);

        var writer = host.Services.GetRequiredService<JsonlLogWriter>();
        var lines = await ReadAllLinesSharedAsync(writer.FilePath, ct);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("SdkName", root.GetProperty("serverName").GetString());
        Assert.Equal("1.2.3", root.GetProperty("serverVersion").GetString());

        await client.DisposeAsync();
        await host.StopAsync(ct);
        host.Dispose();
    }
}
