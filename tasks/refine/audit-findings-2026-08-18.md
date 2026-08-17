# Refine Implementation Audit Findings

Date: 2026-08-18

## Scope and verification

The audit compared the implemented source code, executable tests, package
configuration, and compiled public surface with the requirements in
`tasks/refine/Konzept.md` and `.agents/rules/McpObservabilityRichtlinien.mdc`.
Task documentation was treated as the specification only; claims in task
documents were not used as evidence of implementation.

The following small findings were fixed during the audit and covered by new or
updated regression tests:

- `FeedbackConfirmationMessage` is now returned by the feedback tool instead
  of being ignored in favor of a hard-coded string.
- `ArgumentSanitizer` now recursively sanitizes nested .NET dictionaries and
  collections in `Dictionary<string, object?>` inputs.

## Resolved finding: writer lifecycle synchronization

**Priority:** medium

**Affected code:**
`src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs`

### Evidence

- `WriteRecord` writes while holding the private `Lock`.
- `FlushAsync` invokes `StreamWriter.FlushAsync` without acquiring the same
  synchronization primitive.
- `DisposeAsync` invokes `StreamWriter.DisposeAsync` without acquiring it
  either.

Consequently, a concurrent tool call may write while the host is flushing or
disposing the singleton writer. This violates the refinement requirement that
the new lifecycle operations use behavior consistent with the existing
thread-safe write path. It can result in a race between writing and closing the
underlying stream during server shutdown.

### Implemented resolution

`JsonlLogWriter` now owns a single `SemaphoreSlim` for every write, flush, and
dispose operation. Disposal is atomically started once, waits for an active
operation, closes the writer, and is idempotent across synchronous and
asynchronous disposal. Writes and flushes requested after disposal starts are
safe no-ops.

### Verification

Isolated temporary-directory tests cover concurrent writes with `FlushAsync`,
racing writes with `DisposeAsync`, valid JSONL line integrity, and repeated
mixed synchronous/asynchronous disposal.

## Resolved finding: response schema contract

**Priority:** medium

**Affected code:**
`src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs`,
`src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs`, and
`src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs`

### Evidence

- `EnableResponseLogging` defaults to `true`.
- For a normal tool result containing text, `ToolCallLoggingHandler` emits the
  additive `response`, `responseLength`, and `responseLines` fields.
- `Konzept.md` requires byte-identical JSON output for existing v1.0.0
  consumers, while simultaneously requiring response logging to be enabled by
  default.

An actual default tool call therefore does not produce a byte-identical v1.0.0
record. The existing schema-stability test only constructs a record with all
response fields at default values; it does not exercise the default request
path and cannot demonstrate the stated compatibility guarantee.

### Decision and implemented resolution

This greenfield project adopts a complete, feature-first schema contract:
every `tool_call` record serializes `response`, `responseLength`,
`responseLines`, `responseTruncated`, and `nonTextContentBlocks`. The default
continues to capture response content. When response content logging is
disabled, `response` is `null` while the remaining metrics describe the
unlogged result. Schema tests assert the complete canonical JSON, and an
integration test verifies response fields from a real default tool call.