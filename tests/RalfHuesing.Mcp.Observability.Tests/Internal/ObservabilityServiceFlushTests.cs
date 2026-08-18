using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class ObservabilityServiceFlushTests : TempDirectoryTestBase
{
    [Fact]
    public async Task FlushAsync_WhenEnabled_FlushesPendingRecordsToFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions
        {
            LogDirectory = TempDirectory,
            ServerName = "FlushTestServer"
        };

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer()
            .WithObservability(options);

        var host = builder.Build();
        var obsService = host.Services.GetRequiredService<IMcpObservabilityService>();
        var writer = host.Services.GetRequiredService<JsonlLogWriter>();

        writer.WriteRecord(new { test = "flush-me" });

        await obsService.FlushAsync(ct);

        Assert.NotNull(obsService.CurrentLogFilePath);
        Assert.True(File.Exists(obsService.CurrentLogFilePath));

        using var stream = new FileStream(obsService.CurrentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);
        Assert.Contains("flush-me", content);

        await host.StopAsync(ct);
        host.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WhenDisabled_CompletesSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new McpObservabilityOptions
        {
            Enabled = false,
            LogDirectory = TempDirectory
        };

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMcpServer()
            .WithObservability(options);

        var host = builder.Build();
        var obsService = host.Services.GetRequiredService<IMcpObservabilityService>();

        Assert.False(obsService.IsEnabled);
        Assert.Null(obsService.CurrentLogFilePath);

        await obsService.FlushAsync(ct);

        await host.StopAsync(ct);
        host.Dispose();
    }
}
