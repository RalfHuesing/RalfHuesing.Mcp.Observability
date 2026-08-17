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

## Remaining finding: writer lifecycle synchronization

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

### Required implementation

Replace the mixed synchronization model with one primitive that safely covers
all writer operations. A `SemaphoreSlim` is suitable because it can be awaited
by `FlushAsync` and `DisposeAsync`; `WriteRecord` can acquire it synchronously.
The implementation must also make disposal idempotent and reject or safely
ignore writes after disposal.

### Required tests

Add isolated temporary-directory tests that:

1. run writes concurrently with `FlushAsync` and confirm valid JSONL lines;
2. race an in-flight write with `DisposeAsync` and confirm no unhandled
   `ObjectDisposedException` or corrupted partial line;
3. invoke synchronous and asynchronous disposal repeatedly to verify
   idempotence.

The implementation must pass `dotnet build --configuration Release` and
`dotnet test --configuration Release` with zero warnings.

## Remaining finding: default response logging conflicts with byte compatibility

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

### Decision required

Choose and document one compatible contract before changing code:

1. **Compatibility-first:** set `EnableResponseLogging` to `false` by default,
   retaining byte-identical records until consumers opt in.
2. **Feature-first:** keep the default at `true`, state that records gain
   additive fields, and release with an appropriate SemVer and migration note.
3. **Versioned schema:** retain the default at `true` but introduce a new
   schema version; this is a breaking schema change and requires a major
   release.

After the decision, add an integration test which invokes a real tool using
the chosen default and compares the serialized record with the selected
compatibility contract.