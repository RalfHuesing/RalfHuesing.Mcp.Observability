# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - 2026-08-18

### Added
- **Dedicated Feedback Log File**: Feedback reports (`report_observability_feedback`) are written to a separate `{ServerName}_{PID}_{InstanceId}.feedback.jsonl` file in the same date folder. The file is created lazily on the first feedback report (no file if no feedback was reported).
- **`IMcpObservabilityService.CurrentFeedbackLogFilePath`**: Public property exposing the path to the dedicated feedback log file.
- **Clean Tool Call Logs**: Invocations of `report_observability_feedback` are excluded from the main tool-call log, keeping tool logs purely focused on functional server operations.
- **`IMcpObservabilityService`**: Public interface registered in DI providing read-only access to runtime observability metadata (`IsEnabled`, `ServerName`, `ServerVersion`, `CurrentLogFilePath`, `CurrentFeedbackLogFilePath`, `ProcessId`, `InstanceId`) and explicit `FlushAsync()`.
- **Null-Object Pattern for Disabled State**: When `McpObservabilityOptions.Enabled` is `false`, `IMcpObservabilityService` is registered in DI as a safe disabled null-object (`IsEnabled == false`, `CurrentLogFilePath == null`, `CurrentFeedbackLogFilePath == null`, `FlushAsync` completed immediately), preventing DI resolution errors.
- **`McpObservabilityTools.FeedbackToolName`**: Public constant exposing the default feedback tool name (`"report_observability_feedback"`) for instructions, prompts, and tool filters.
- **`McpObservabilityTools`**: Public helper class with `CreateFeedbackTool(IServiceProvider? = null)` and `AddFeedbackTool(this McpServerPrimitiveCollection<McpServerTool>, IServiceProvider? = null)` with optional services parameter for servers that manually manage `McpServerOptions.ToolCollection` without reflection on internal types.
- **Tool-Shadowing Fix**: Registered `IPostConfigureOptions<McpServerOptions>` that automatically appends the feedback tool to manually configured `McpServerOptions.ToolCollection` instances.
- **Tool-Call Response Logging**: Every `tool_call` record includes `response` (concatenated text content blocks or `null`), `responseLength` (original text length), `responseLines` (line count), `responseTruncated` (boolean flag), and `nonTextContentBlocks` (count of image, audio, and embedded resource blocks).
- **Options Extension**: Added `ServerName`, `ServerVersion`, `FeedbackConfirmationMessage`, `AdditionalSensitiveKeys`, `EnableResponseLogging`, and `MaxResponseLength` to `McpObservabilityOptions`.
- **Writer Lifecycle & Lazy Streams**: `JsonlLogWriter` now opens file streams lazily on the first write, implements `IAsyncDisposable` with `DisposeAsync()`, and exposes `FlushAsync(CancellationToken)`.
- **Sample Project**: Added `samples/ManualToolCollectionServer/` demonstrating integration with manually populated tool collections.

### Changed
- **`ArgumentSanitizer`**: Generalized `Sanitize` method to accept `IReadOnlyDictionary<string, JsonElement>`, `IDictionary<string, object?>`, and `JsonObject`; added `Sanitize(string?, ...)` overload for responses; replaced `JsonNode.Parse` round-trips with direct `JsonElement` traversal.
- **`ToolCallRecord`**: `Arguments` internal type changed to `IReadOnlyDictionary<string, object?>?`.
- **`ToolCallRecord` schema**: Defined a complete greenfield `tool_call` contract in which all response fields are serialized, including zero and `null` values.
- **Architecture Guidelines §6**: Relaxed public API boundary to include `IMcpObservabilityService` and `McpObservabilityTools` alongside `McpObservabilityOptions` and `McpObservabilityExtensions`.

## [1.0.0] - 2026-08-17

### Added
- Initial release of `RalfHuesing.Mcp.Observability`.
- Single-line integration `.WithObservability()`.
- Thread-safe JSONL tool-call logging with multi-process isolation.
- Automatic argument sanitization for sensitive keys.
- Structured LLM feedback tool `report_observability_feedback`.
