using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

var builder = Host.CreateApplicationBuilder(args);

var obsOptions = builder.Configuration
    .GetSection("McpObservability")
    .Get<McpObservabilityOptions>()
    ?? new McpObservabilityOptions();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "MinimalSampleServer", Version = "1.0.0" };
    })
    .WithStdioServerTransport()
    .WithTools<SampleTools>()
    .WithObservability(obsOptions);

await builder.Build().RunAsync();

[McpServerToolType]
internal sealed class SampleTools
{
    [McpServerTool(Name = "echo")]
    [Description("Echoes the provided message back to the caller.")]
    internal static string Echo([Description("The message to echo.")] string message) => message;
}
