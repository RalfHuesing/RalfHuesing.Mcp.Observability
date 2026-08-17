---
task: refine
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-18T01:05:00+02:00
---

# CodeMap: refine — Robustheit, Kompatibilität, Diagnostik (v1.0.1)

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(`../spec.md` §6). Zwischen einem Coder-Update und dem nächsten Lesezugriff
kann sich am Code strukturell nichts geändert haben, was hier nicht auch
eingetragen wurde. Die Map ist also, solange sie gepflegt wird, tatsächlich
aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im Step-Modus des
Planers („tatsächlichen Projektzustand lesen", `../spec.md` §7.2) bleibt
trotzdem Pflicht** — die Map sagt *wo* nachschauen, ersetzt nie das
Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (`../skills/planer/SKILL.md` Roadmap-Modus
  Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. Step-234 würde zurückdrehen, was Step-123
laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

### Public API (Namespace `RalfHuesing.Mcp.Observability`)

- **`src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs`** — `public sealed class`
      mit `Enabled`, `EnableToolCallLogging`, `EnableFeedbackTool`, `LogDirectory`,
      `ServerName`, `ServerVersion`, `FeedbackConfirmationMessage`,
      `AdditionalSensitiveKeys`, `EnableResponseLogging`, `MaxResponseLength`,
      plus `public const string DefaultFeedbackConfirmationMessage`.
      (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/IMcpObservabilityService.cs`** — `public interface`
      (neu in EPIC-01, erste bewusste Lockerung von Richtlinie §6), sechs
      Read-only-Properties für Diagnostik (`IsEnabled`, `ServerName`,
      `ServerVersion`, `CurrentLogFilePath`, `ProcessId`, `InstanceId`).
      (zuletzt: step-001)
- **`src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs`** — `public static class`
      mit der `WithObservability(IMcpServerBuilder, McpObservabilityOptions?)`-Extension,
      die Singleton-Registrierungen vornimmt (`McpObservabilityOptions` +
      `ObservabilityContext` + Factory-Forwarding `IMcpObservabilityService`),
      bedingt `JsonlLogWriter`, ruft `builder.WithTools<FeedbackTools>()` auf
      und registriert `IPostConfigureOptions<McpServerOptions>`
      (`ObservabilityPostConfigureOptions`, Tool-Schatten-Fix).
      (zuletzt: step-003)
- **`src/RalfHuesing.Mcp.Observability/McpObservabilityTools.cs`** — `public static class`
      (neu in EPIC-03, zweite Lockerung von Richtlinie §6): `CreateFeedbackTool(IServiceProvider)`
      erzeugt das Feedback-Tool via `McpServerTool.Create` mit Method-Group auf
      `internal FeedbackTools.ReportFeedback` (Parameternamen/Descriptions/Defaults
      bleiben erhalten); `AddFeedbackTool`-Extension auf
      `McpServerPrimitiveCollection<McpServerTool>`, idempotent per
      `TryGetPrimitive`. (zuletzt: step-003)

### Internal (Namespace `RalfHuesing.Mcp.Observability.Internal`)

- **`src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs`** — `internal sealed record`
      `ToolCallRecord` (Arguments ist `IReadOnlyDictionary<string, object?>?`
      + 5 additive Response-Felder mit JsonIgnore WhenWritingNull/Default)
      + `FeedbackRecord` (unverändert). Schema-Invariante §5 (Richtlinie)
      bleibt — bei `EnableResponseLogging = false` ist der JSON-Output
      byte-identisch zu v1.0.0. (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs`** — `internal static class`,
      `Sanitize(object?, IEnumerable<string>?)` akzeptiert
      `IReadOnlyDictionary<string, JsonElement>`, `Dict<string, object?>`,
      `JsonObject`, `IDictionary<string, object?>`; `Sanitize(string?, …)`-
      Overload für Response-Strings (zwei Regex-Patterns pro Key mit
      Word-Boundary). `JsonNode.Parse`-Round-Trip eliminiert durch direkte
      `JsonElement`-Traversierung. `JsonValueKind.Null` → echtes `null`.
      (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs`** — `internal static class`,
      registriert `WithRequestFilters(AddCallToolFilter)` und baut den
      `ToolCallRecord`; `ExtractResponse` (internal static) extrahiert
      TextContent-Blocks (ImageContentBlock/AudioContentBlock/EmbeddedResourceBlock
      werden gezählt), Sanitizer läuft auf Response, Truncation bei
      `MaxResponseLength > 0`. `ResponseExtraction` ist top-level
      `internal readonly record struct` (AiNetLinter `BanPublicNestedTypes`).
      (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs`** — `internal sealed class : IDisposable, IAsyncDisposable`,
      FileStream im Append-Mode mit `FileShare.ReadWrite`, thread-safe via
      `Lock`. Pfad kommt aus `ObservabilityContext.LogFilePath` (Single
      Source of Truth). `FlushAsync(CancellationToken)` und `DisposeAsync()`
      für sauberen asynchronen Lifecycle. (zuletzt: step-004)
- **`src/RalfHuesing.Mcp.Observability/Internal/ObservabilityContext.cs`** — `internal sealed class : IMcpObservabilityService`,
      process-scoped Singleton mit `ServerName`/`ServerVersion`/`ProcessId`/
      `InstanceId`/`Options` + `internal string LogFilePath` (eager im
      Konstruktor). Auflösung in Override-Kette:
      `options.ServerName` → `McpServerOptions.ServerInfo.Name` → EntryAssembly
      → `UnknownServer` (analog für `ServerVersion` mit `string.Empty` als
      unterste Stufe). Implementiert `IMcpObservabilityService` implizit
      (public-Properties, `Options`/`LogFilePath` bleiben internal).
      (zuletzt: step-001)
- **`src/RalfHuesing.Mcp.Observability/Internal/FeedbackTools.cs`** — `internal sealed class`
      mit `[McpServerToolType]` + `ReportFeedback(...)` (statische Methode).
      Tool-Name kommt seit step-003 aus `ObservabilityConstants.FeedbackToolName`;
      die public `McpObservabilityTools`-Variante nutzt dieselbe Methode per
      Method-Group (Single Source). (zuletzt: step-003)
- **`src/RalfHuesing.Mcp.Observability/Internal/ObservabilityPostConfigureOptions.cs`** —
      `internal sealed class : IPostConfigureOptions<McpServerOptions>` (neu,
      EPIC-03): hängt das Feedback-Tool nach allen Konfigurationen an eine
      manuell gesetzte `McpServerOptions.ToolCollection` an (idempotent).
      (zuletzt: step-003)
- **`src/RalfHuesing.Mcp.Observability/Internal/ObservabilityConstants.cs`** — `internal static class`,
      alle Magic-Werte: `SchemaVersion=1`, `ToolCallRecordType="tool_call"`,
      `FeedbackRecordType="feedback"`, `RedactedMarker="***REDACTED***"`,
      Default-Severity, Default-Feedback-Response, `UnknownServerName`,
      `FeedbackToolName="report_observability_feedback"` (seit step-003).
      (zuletzt: step-003)
- **`src/RalfHuesing.Mcp.Observability/Internal/JsonlSerializerOptions.cs`** — `internal static class`,
      `JsonSerializerOptions.Default` mit `CamelCase`-Naming, `WriteIndented=false`.

### Tests

- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/ArgumentSanitizerTests.cs`** — bestehende
      Cases für Null/Empty, Case-Insensitive, nested objects/arrays (an
      neue `IReadOnlyDictionary<string, object?>?`-Rückgabe angepasst) +
      Cases für `Dictionary<string, object?>`-Round-Trip, `JsonObject`-Input
      und `Sanitize(string?, …)`-Overload. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/ToolCallRecordSchemaStabilityTests.cs`** (neu)
      — byte-Identität gegen hartkodiertes v1.0.0-Baseline-JSON bei
      `EnableResponseLogging = false` + Round-Trip-Check der Response-Felder
      bei Non-Default-Werten. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/ResponseLoggingTests.cs`** (neu)
      — 5 Cases (EnableResponseLogging true/false, MaxResponseLength 0/100,
      IsErrorResult, nonTextContentBlocks) gegen `ExtractResponse` direkt
      (via `InternalsVisibleTo`). (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/RequestFullLoggingTests.cs`** (neu)
      — 3 Cases (Top-Level-Keys inkl. null, komplexe Typen DateTime/Guid/Array,
      AdditionalSensitiveKeys) gegen `ArgumentSanitizer` direkt. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterTests.cs`** — bestehende
      Cases für Datei-Pfad, Append mehrerer Records (mechanisch um 5
      additive positional args erweitert), Concurrent-Writes.
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterFlushTests.cs`** — neu
      (EPIC-04, step-004), 2 Cases: `FlushAsync` schreibt Payload lesbar in
      die Datei, `DisposeAsync` flasht und schließt den FileStream sauber.
      (zuletzt: step-004)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/IntegrationTestBase.cs`** — `abstract`
      Basisklasse mit isoliertem `TempDirectory` (per `Guid`), `CreateDuplexPipes()`
      und `ReadAllLinesSharedAsync` (mit `FileShare.ReadWrite`). Alle neuen
      Integration-Tests erben davon.
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpObservabilityIntegrationTests.cs`** —
      bestehender Happy-Path (Echo-Tool + Tool-Call-Record inkl. Password-Redaction).
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpFeedbackIntegrationTests.cs`** —
      bestehender Test, der das Feedback-Tool aufruft und den `feedback`-Record liest.
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsFlagsTests.cs`** — bestehende
      Tests für `Enabled=false`, `EnableToolCallLogging=false`,
      `EnableFeedbackTool=false`.
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsServerNameOverrideTests.cs`** — neu
      (EPIC-01, step-001), 4 Integration-Cases für die
      `McpObservabilityOptions.ServerName`/`ServerVersion`-Override-Kette
      gegen `McpServerOptions.ServerInfo`. (zuletzt: step-001)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpServerOptionsToolCollectionTests.cs`** — neu
      (EPIC-03, step-003), 3 Integration-Cases: manuelle `ToolCollection` +
      `WithObservability` listet Feedback-Tool (Tool-Schatten-Fix), Pre-Add
      bleibt idempotent (genau ein Eintrag), Aufruf schreibt korrekten
      `feedback`-Record. `ManualSampleTools` als top-level `internal` Klasse
      (AiNetLinter `BanPublicNestedTypes`). (zuletzt: step-003)

### Konfiguration

- **`Directory.Build.props`** — `net10.0`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
      `latest-Recommended` Analysis-Level. Unverändert in diesem Task.
- **`Directory.Packages.props`** — Central Package Management.
      `ModelContextProtocol 2.2.0`, `Microsoft.Extensions.* 10.0.10`,
      `xunit.v3 3.2.2`, `Microsoft.NET.Test.Sdk 18.7.0`.
      Unverändert in diesem Task (Konzept: keine neuen NuGet-Deps).

### Sample

- **`samples/MinimalMcpServerWithObservability/Program.cs`** — bestehender
      Minimal-Server mit `WithTools<SampleTools>()` + `WithObservability()`.
      Repräsentiert den „happy path" (DI-Tools). Bleibt in v1.0.1 unverändert.
- **`samples/ManualToolCollectionServer/Program.cs`** — neu (EPIC-05,
      step-005): Sample-Server mit manuell befüllter `ToolCollection` +
      `WithObservability()`. Zeigt Tool-Schatten-Fix in Aktion. (zuletzt: step-005)

### Dokumentation

- **`README.md`** — User-facing Doku mit „Why", „Quick Start", Options-Tabelle
      (10 Properties), „Manual ToolCollection"-Sektion, „Diagnostics Service"-
      Sektion, „Reading logs while the server is running"-Hinweis-Block mit
      `FileShare.ReadWrite`-Codebeispiel. (zuletzt: step-005)
- **`CHANGELOG.md`** — neu (EPIC-05, step-005): Keep-a-Changelog-Format,
      Sektion `## [Unreleased]` (Stand 2026-08-18) und `## [1.0.0]`. (zuletzt: step-005)

### Konzept & Planung

- **`tasks/refine/Konzept.md`** — Status `ready`, primary Input für diesen
      Task. Enthält Muss-Haven-Cluster, Non-Goals, Definition of Done.
- **`tasks/refine/roadmap.md`** — diese Roadmap, 5 Epics.
- **`tasks/refine/task-state.md`** — Loop-Status (Steps-Tabelle, Config).
- **`tasks/refine/tech-debt.md`** — vom Kritiker befüllt, vom Planer
      im Step-Modus gelesen (nicht im Roadmap-Modus).
