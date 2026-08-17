# RalfHuesing.Mcp.Observability

[![NuGet Version](https://img.shields.io/nuget/v/RalfHuesing.Mcp.Observability.svg?style=flat-square)](https://www.nuget.org/packages/RalfHuesing.Mcp.Observability)
[![Build & Publish](https://img.shields.io/github/actions/workflow/status/RalfHuesing/RalfHuesing.Mcp.Observability/build.yml?branch=main&style=flat-square)](https://github.com/RalfHuesing/RalfHuesing.Mcp.Observability/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

A NuGet package that adds unified JSONL tool-call logging and a structured
agent feedback channel to any MCP server built on the official
[`ModelContextProtocol`](https://github.com/modelcontextprotocol/csharp-sdk) SDK.

## Why

MCP servers run as stdio processes — they have no built-in logging that
survives a binary update. When an LLM agent misbehaves or a tool returns
unexpected results, there is usually no trace of what happened.

This package solves that with a single call:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithObservability();          // ← one line
```

From that point on:

- Every `tools/call` is logged to a JSONL file that survives server
  reinstalls.
- Agents can report issues and feature requests via
  `report_observability_feedback`.

## What it does

| Feature | Detail |
|---------|--------|
| Tool-call logging | Every MCP tool invocation is written as a `tool_call` record (tool name, arguments, duration, success, error, response content). |
| Response logging | Tool response content is captured, sanitized against secret leakage, and optionally truncated to a configurable maximum length. |
| Argument sanitizing | Sensitive keys (`password`, `token`, `apiKey`, `secret`, …) are replaced with `"***REDACTED***"` before writing. Custom keys can be added. |
| Feedback tool | One registered MCP tool (`report_observability_feedback`) lets agents report bugs and feature requests without interrupting their workflow. |
| Manual ToolCollection support | Works seamlessly with both automatic discovery (`WithToolsFromAssembly`) and manual collections (`McpServerOptions.ToolCollection`). |
| Diagnostics Service | `IMcpObservabilityService` is registered in DI for read-only access to log paths, instance ID, and server metadata. |
| Multi-process safe | Each process instance writes its own file (`{ServerName}_{PID}_{InstanceId}.jsonl`) — no concurrent-write issues. |
| Survive reinstalls | Logs are written to `%LOCALAPPDATA%\RalfHuesing\McpObservability\`, outside the server release directory. |

## Requirements

- .NET 10 or later
- `ModelContextProtocol` SDK 2.x (stable)

## Installation

```
dotnet add package RalfHuesing.Mcp.Observability
```

## Quick Start

### Standard Integration (Attribute-Based Tool Discovery)

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Optional: read options from appsettings.json
var obsOptions = builder.Configuration
    .GetSection("McpObservability")
    .Get<McpObservabilityOptions>()
    ?? new McpObservabilityOptions();   // all defaults = enabled

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "MyServer", Version = "1.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithObservability(obsOptions);

await builder.Build().RunAsync();
```

### Manual `ToolCollection` Integration

If your MCP server creates tools programmatically via `McpServerOptions.ToolCollection`,
observability works out-of-the-box without manual reflection on internal tools:

```csharp
var myTool = McpServerTool.Create(
    (Func<string, string>)MyTools.Echo,
    new McpServerToolCreateOptions { Name = "echo" });

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "MyServer", Version = "1.0.0" };
        options.ToolCollection = [myTool];
    })
    .WithStdioServerTransport()
    .WithObservability(); // automatically appends report_observability_feedback via post-configure
```

You can also explicitly attach the feedback tool directly to any `McpServerPrimitiveCollection<McpServerTool>`:

```csharp
toolsCollection.AddFeedbackTool(serviceProvider);
```

## Configuration

All settings are optional. Without configuration, everything is enabled
and logs go to `%LOCALAPPDATA%\RalfHuesing\McpObservability\`.

```json
{
  "McpObservability": {
    "Enabled": true,
    "EnableToolCallLogging": true,
    "EnableFeedbackTool": true,
    "EnableResponseLogging": true,
    "MaxResponseLength": 0,
    "ServerName": "CustomServerName",
    "ServerVersion": "1.2.0"
    // "LogDirectory": "D:\\Logs\\Mcp"
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Master switch. When `false`, no logging and no feedback tool. |
| `EnableToolCallLogging` | `bool` | `true` | Logs every tool invocation as a `tool_call` record. |
| `EnableFeedbackTool` | `bool` | `true` | Registers the `report_observability_feedback` MCP tool. |
| `EnableResponseLogging` | `bool` | `true` | Captures tool response content and metrics in `tool_call` records. |
| `MaxResponseLength` | `int` | `0` | Maximum character length for response strings before truncation (`0` = unconstrained). |
| `ServerName` | `string?` | `null` | Overrides the server name in log records (falls back to `ServerInfo.Name`, entry assembly, or `"UnknownServer"`). |
| `ServerVersion` | `string?` | `null` | Overrides the server version in log records. |
| `FeedbackConfirmationMessage` | `string` | `"Feedback recorded. Thank you."` | Confirmation message returned by the feedback tool. |
| `AdditionalSensitiveKeys` | `HashSet<string>` | `[]` | Additional argument / response keys to redact (case-insensitive). |
| `LogDirectory` | `string?` | `null` | Override log root. `null` = `%LOCALAPPDATA%\RalfHuesing\McpObservability\`. |

## Diagnostics Service

Inject `IMcpObservabilityService` anywhere in your application to read current observability state:

```csharp
public class StatusEndpoint(IMcpObservabilityService observability)
{
    public object GetStatus() => new
    {
        observability.IsEnabled,
        observability.ServerName,
        observability.ServerVersion,
        observability.CurrentLogFilePath,
        observability.ProcessId,
        observability.InstanceId
    };
}
```

## Reading logs while the server is running

Log files are opened with `FileShare.ReadWrite`. When reading log files while the MCP server is actively writing, open the file with read-write sharing to avoid file-lock exceptions:

```csharp
using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var reader = new StreamReader(stream, Encoding.UTF8);

while (await reader.ReadLineAsync() is { } line)
{
    // process JSONL record
}
```

## Log file location

```
%LOCALAPPDATA%\RalfHuesing\McpObservability\
└── {ServerName}\
    └── {yyyy-MM-dd}\
        └── {ServerName}_{ProcessId}_{InstanceId}.jsonl
```

Example: `AiNetLinter\2026-08-17\AiNetLinter_18432_a1b2c3d4e5f67890.jsonl`

## JSONL record format

Each line is a self-contained JSON object. All records share these fields:

```json
{
  "schemaVersion": 1,
  "timestamp": "2026-08-17T17:22:01.123Z",
  "recordType": "tool_call",
  "serverName": "AiNetLinter",
  "serverVersion": "1.4.2",
  "processId": 18432,
  "instanceId": "a1b2c3d4e5f67890"
}
```

### `tool_call` additional fields

```json
{
  "toolName": "analyze_code",
  "arguments": { "filePath": "src/Foo.cs" },
  "durationMs": 142,
  "success": true,
  "isErrorResult": false,
  "errorMessage": null,
  "response": "Analysis clean. 0 violations found.",
  "responseLength": 36,
  "responseLines": 1,
  "responseTruncated": false,
  "nonTextContentBlocks": 0
}
```

### `feedback` additional fields

```json
{
  "feedbackType": "issue",
  "title": "False positive on nullable reference",
  "description": "When analyzing … the tool reported …",
  "relatedTool": "analyze_code",
  "severity": "medium",
  "expectedBehavior": "…",
  "actualBehavior": "…",
  "additionalContext": "…"
}
```

## The feedback tool

`report_observability_feedback` is automatically registered when
`EnableFeedbackTool = true`. Agents see it as a regular MCP tool.

> Report an issue or a feature request about this MCP server.
> Use this tool whenever something is wrong (bugs, false positives,
> unexpected results, confusing output) or when a needed capability is
> missing. After reporting, continue with the best available workaround.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `feedbackType` | `"issue"` \| `"feature_request"` | yes | Kind of feedback |
| `title` | `string` | yes | Short, clear title (max 120 chars) |
| `description` | `string` | yes | What happened or what is missing |
| `relatedTool` | `string?` | no | Name of the affected tool, if known |
| `severity` | `"low"` \| `"medium"` \| `"high"` | no | Default: `"medium"` |
| `expectedBehavior` | `string?` | no | What the agent expected |
| `actualBehavior` | `string?` | no | What actually happened |
| `additionalContext` | `string?` | no | Free-form additional information |

## License

MIT — see [LICENSE](LICENSE).
