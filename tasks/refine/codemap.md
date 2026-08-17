---
task: refine
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-17T22:42:30+02:00
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
      `AdditionalSensitiveKeys`, plus `public const string DefaultFeedbackConfirmationMessage`.
      Wird in EPIC-02 um 2 weitere Properties (`EnableResponseLogging`, `MaxResponseLength`)
      erweitert. (zuletzt: step-001)
- **`src/RalfHuesing.Mcp.Observability/IMcpObservabilityService.cs`** — `public interface`
      (neu in EPIC-01, erste bewusste Lockerung von Richtlinie §6), sechs
      Read-only-Properties für Diagnostik (`IsEnabled`, `ServerName`,
      `ServerVersion`, `CurrentLogFilePath`, `ProcessId`, `InstanceId`).
      (zuletzt: step-001)
- **`src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs`** — `public static class`
      mit der `WithObservability(IMcpServerBuilder, McpObservabilityOptions?)`-Extension,
      die Singleton-Registrierungen vornimmt (`McpObservabilityOptions` +
      `ObservabilityContext` + Factory-Forwarding `IMcpObservabilityService`),
      bedingt `JsonlLogWriter`, ruft `builder.WithTools<FeedbackTools>()` auf.
      Wird in EPIC-03 um `IPostConfigureOptions<McpServerOptions>` ergänzt.
      (zuletzt: step-001)

### Internal (Namespace `RalfHuesing.Mcp.Observability.Internal`)

- **`src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs`** — `internal sealed record`
      `ToolCallRecord` + `FeedbackRecord`, Felder exakt nach JSONL-Schema §5.
      `ToolCallRecord.Arguments` ist `IReadOnlyDictionary<string, JsonElement>?`
      (heute); in EPIC-02 Wechsel auf `IReadOnlyDictionary<string, object?>?`
      + 5 additive Response-Felder. Schema-Invariante §5 (Richtlinie) bleibt.
- **`src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs`** — `internal static class`,
      rekursive Sanitizer mit hartkodierter `SensitiveKeys`-Liste und
      `Sanitize(IReadOnlyDictionary<string, JsonElement>?)`. EPIC-02: Signatur
      wird zu `Sanitize(object?, IEnumerable<string>?)` generalisiert, neue
      `Sanitize(string?, ...)`-Overload für Response-Strings,
      `JsonNode.Parse`-Round-Trip wird durch direkte `JsonElement`-Traversierung
      ersetzt (Mitdenken-Fund-Optimierung).
- **`src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs`** — `internal static class`,
      registriert `WithRequestFilters(AddCallToolFilter)` und baut den
      `ToolCallRecord`. EPIC-02: Cast auf `request.Params?.Arguments` entfällt
      (durch generischen Sanitizer), Response-Extraktion aus
      `result.Content` mit `TextContent`-Filter + `\n`-Join, Sanitizer auf
      Response anwenden, Truncation bei `MaxResponseLength > 0`.
- **`src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs`** — `internal sealed class : IDisposable`,
      FileStream im Append-Mode mit `FileShare.ReadWrite`, thread-safe via
      `Lock`. Pfad kommt jetzt aus `ObservabilityContext.LogFilePath` (Single
      Source of Truth, verlagert in step-001). EPIC-04: zusätzlich
      `IAsyncDisposable` + `FlushAsync(CancellationToken)`. (zuletzt: step-001)
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
      mit `[McpServerToolType]` + `ReportFeedback(...)` (statische Methode,
      via Reflection von `builder.WithTools<FeedbackTools>()` aufgerufen).
      Bleibt `internal`; EPIC-03 baut die public `McpObservabilityTools`-
      Variante (semantisch identisch, aber via `McpServerTool.Create` Delegate).
- **`src/RalfHuesing.Mcp.Observability/Internal/ObservabilityConstants.cs`** — `internal static class`,
      alle Magic-Werte: `SchemaVersion=1`, `ToolCallRecordType="tool_call"`,
      `FeedbackRecordType="feedback"`, `RedactedMarker="***REDACTED***"`,
      Default-Severity, Default-Feedback-Response, `UnknownServerName`.
- **`src/RalfHuesing.Mcp.Observability/Internal/JsonlSerializerOptions.cs`** — `internal static class`,
      `JsonSerializerOptions.Default` mit `CamelCase`-Naming, `WriteIndented=false`.

### Tests

- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/ArgumentSanitizerTests.cs`** — bestehende
      Cases für Null/Empty, Case-Insensitive, nested objects/arrays. EPIC-02
      ergänzt Cases für `Dictionary<string, object?>` und `JsonObject`-Inputs.
- **`tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterTests.cs`** — bestehende
      Cases für Datei-Pfad, Append mehrerer Records, Concurrent-Writes.
      EPIC-04 ergänzt `JsonlLogWriterFlushTests` (neue Datei) für
      `FlushAsync` + `DisposeAsync`.
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
- **`samples/ManualToolCollectionServer/`** — **geplant, noch nicht
      angelegt** (EPIC-05). Wird `Program.cs` + `.csproj` enthalten, der
      `McpServerOptions.ToolCollection` manuell befüllt + `AddFeedbackTool`
      nutzt.

### Dokumentation

- **`README.md`** — User-facing Doku mit „Why", „Quick Start", Options-Tabelle,
      JSONL-Schema-Beispiel, Feedback-Tool-Tabelle. EPIC-05 erweitert:
      Options-Tabelle (6 neue Properties), „Manual ToolCollection"-Sektion,
      „Response Logging"-Sektion, „Reading logs while the server is running"-
      Hinweis-Block mit `FileShare.ReadWrite`-Codebeispiel.
- **`CHANGELOG.md`** — **geplant, noch nicht angelegt** (EPIC-05).
      Keep-a-Changelog-Format, Sektion `## [Unreleased]` mit Datum 2026-08-17.

### Konzept & Planung

- **`tasks/refine/Konzept.md`** — Status `ready`, primary Input für diesen
      Task. Enthält Muss-Haven-Cluster, Non-Goals, Definition of Done.
- **`tasks/refine/roadmap.md`** — diese Roadmap, 5 Epics.
- **`tasks/refine/task-state.md`** — Loop-Status (Steps-Tabelle, Config).
- **`tasks/refine/tech-debt.md`** — vom Kritiker befüllt, vom Planer
      im Step-Modus gelesen (nicht im Roadmap-Modus).
