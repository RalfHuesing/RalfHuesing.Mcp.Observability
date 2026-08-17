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

// Define tools programmatically
var customTool = McpServerTool.Create(
    (Func<string, string>)SampleManualTools.Reverse,
    new McpServerToolCreateOptions
    {
        Name = "reverse_text",
        Description = "Reverses the provided text string."
    });

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "ManualToolCollectionSampleServer", Version = "1.0.0" };
        // Manual ToolCollection assignment: WithObservability will automatically append
        // the feedback tool via IPostConfigureOptions<McpServerOptions>
        options.ToolCollection = [customTool];
    })
    .WithStdioServerTransport()
    .WithObservability(obsOptions);

await builder.Build().RunAsync();

[McpServerToolType]
internal sealed class SampleManualTools
{
    [McpServerTool(Name = "reverse_text")]
    [Description("Reverses the provided text string.")]
    internal static string Reverse([Description("The input text to reverse.")] string text)
    {
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
}
