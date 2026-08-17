---
status: active
task: refine
derived_from: konzept.md
created_at: 2026-08-17T22:42:30+02:00
last_updated: 2026-08-17T22:42:30+02:00
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: refine — Robustheit, Kompatibilität, Diagnostik (v1.0.1)

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

Alle Epics bedienen Muss-Haven-Punkte aus
[`konzept.md`](Konzept.md) für Zielversion `1.0.1`. Finale
Versionsnummer setzt `scripts/create-release.ps1`.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build --configuration Release`
  (aus `task-state.md` Config, gilt für src + tests + samples)
- **Test-Command:** `dotnet test --configuration Release --verbosity normal`
  (aus `task-state.md` Config)
- **Lint-Command:** AiNetLinter `safeguard` + `get_violations` (residenter
  MCP-Server) — Pflicht vor jedem Commit (Richtlinie §4.4).
  Regelsatz: `tests/.../AiNetLinter/rules/RalfHuesing.Mcp.Observability.rules.json`.
- **Code-Style-Kurzfassung:**
  - C# 14 / .NET 10, `#nullable enable` in jeder `.cs`.
  - `sealed` für konkrete Klassen, ≤60 Zeilen/Method prod (≤120 in
    `*.Tests`), Cyclomatic ≤10, Cognitive ≤12, MaxLineCount 300.
  - XML-Doc auf `public`-API (Richtlinie §8).
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` +
    `latest-Recommended` (siehe `Directory.Build.props`).
  - Kein `dynamic`, `out` nur in `Try*`, kein leeres `catch`,
    kein `async void`, keine Blocking-Task-Zugriffe (Linter
    agent-resilience-Regeln).
  - Namespace-Mapping: `RalfHuesing.Mcp.Observability` für public,
    `RalfHuesing.Mcp.Observability.Internal` für intern.
- **Commit-Konventionen:** Conventional Commits, deutsch, imperativ,
  Subject ≤72 Zeichen, Task-Suffix `[refine]` (Richtlinie §10).
  Beispiel: `feat(observability): response-feld in tool_call-records [refine]`.

## Regel-Index

- **`.agents/rules/McpObservabilityRichtlinien.mdc`** — Zentrale
  Architektur- und Workflow-Richtlinie (Design-Philosophie, Architektur-
  Verbote, JSONL-Schema-Invarianten §5, Public-API §6, Tests §7,
  Doku/Qualität §8, Versionierung §9, Commit §10). **§6 wird in
  EPIC-03 bewusst gelockert** (siehe Konzept „Bewusste Richtlinien-Änderung").
- **`.agents/rules/AiNetLinter.mdc`** — Auto-generierte
  Codequalitäts-Grenzwerte aus `RalfHuesing.Mcp.Observability.rules.json`
  (Zeilen-/Methoden-/Parameter-Limits, Cyclomatic/Cognitive Complexity,
  `EnforceSealedClasses`, `EnforceNoSilentCatch`, `BanAsyncVoid`,
  `BanBlockingTaskAccess`, Namespace-Mapping, `EnforceNullableEnable`).
  Coder muss diese Limits im Code-Output direkt einhalten.

## Epics

- [ ] **EPIC-01: Options-Erweiterung & Diagnostic-Service** _(in Arbeit → step-001)_ — `McpObservabilityOptions`
      um `ServerName`, `ServerVersion`, `FeedbackConfirmationMessage`,
      `AdditionalSensitiveKeys` erweitern; in `ObservabilityContext` die
      Override-Kette (Options → `McpServerOptions.ServerInfo` → EntryAssembly
      → `UnknownServer`) implementieren. Neues public-Interface
      `IMcpObservabilityService` mit Properties für `IsEnabled`, `ServerName`,
      `ServerVersion`, `CurrentLogFilePath`, `ProcessId`, `InstanceId`;
      `ObservabilityContext` implementiert das Interface und wird im DI als
      Singleton unter beiden Typen registriert. Tests: `McpOptionsServerNameOverrideTests`
      (Integration). Konzept §„Optionen-Erweiterung" + §„Diagnostik-Service".

- [ ] **EPIC-02: Sanitizer-Generalisierung, LogRecord-Type-Wechsel & Response-Logging** —
      `ArgumentSanitizer` auf `Sanitize(object? rawArguments, IEnumerable<string>? additionalKeys = null)`
      generalisieren (akzeptiert `IReadOnlyDictionary<string, JsonElement>`,
      `IReadOnlyDictionary<string, object?>`, `JsonObject`, `IDictionary<string, object?>`);
      zusätzliche Methode `Sanitize(string? rawText, IEnumerable<string>?)` für
      Response-Strings. `ToolCallRecord.Arguments` intern auf
      `IReadOnlyDictionary<string, object?>?` umstellen (JSON-Output bleibt
      byte-identisch durch `JsonElement`-Serialisierung). Neue additive Felder
      im `ToolCallRecord`: `Response`, `ResponseLength`, `ResponseLines`,
      `ResponseTruncated`, `NonTextContentBlocks`. In `ToolCallLoggingHandler`
      Response aus `CallToolResult.Content` extrahieren (nur `TextContent`,
      `\n`-konkateniert, non-text als Zähler), Sanitizer anwenden, Truncation
      bei `MaxResponseLength > 0`. `McpObservabilityOptions` um
      `EnableResponseLogging` (default `true`) und `MaxResponseLength`
      (default `0` = unbegrenzt) erweitern. Mitdenken-Fund „JsonNode.Parse-
      Elimination": Optimierung in derselben Code-Änderung mitziehen.
      Tests: erweiterte `ArgumentSanitizerTests` (Dict/JsonObject),
      `ToolCallRecordSchemaStabilityTests`, `ResponseLoggingTests`,
      `RequestFullLoggingTests`. Konzept §„ArgumentSanitizer generalisieren"
      + §„Response-Logging" + §„JSONL-Schema-Stabilität".

- [ ] **EPIC-03: Public Feedback-Tool-API & Tool-Schatten-Fix** — Neue public
      static class `McpObservabilityTools` mit `CreateFeedbackTool(IServiceProvider)`
      (semantisch identisch zu `FeedbackTools.ReportFeedback`, aber via
      `McpServerTool.Create` statt Reflection) und Extension
      `AddFeedbackTool(this McpServerPrimitiveCollection<McpServerTool> tools, IServiceProvider)`
      (idempotent per `ProtocolTool.Name`-Vergleich). `McpObservabilityExtensions.WithObservability`
      registriert zusätzlich `IPostConfigureOptions<McpServerOptions>`, das nach
      allen Konfigurationen prüft, ob `McpServerOptions.ToolCollection` gesetzt
      und `EnableFeedbackTool = true` ist, und das Feedback-Tool nachträglich
      anhängt. **Lockerung §6** der Richtlinie in derselben Datei
      dokumentieren (Datum, Begründung, Alternativen). Tests:
      `McpServerOptionsToolCollectionTests` (Integration). Konzept
      §„Manueller ToolCollection-Support" + §„Tool-Schatten-Fix" +
      §„Richtlinien-Update §6".

- [ ] **EPIC-04: Writer-Lifecycle (`IAsyncDisposable` + `FlushAsync`)** —
      `JsonlLogWriter` zusätzlich `IAsyncDisposable` (`DisposeAsync` ruft
      `FlushAsync` und schließt den `StreamWriter` sauber); neue Methode
      `Task FlushAsync(CancellationToken ct = default)`. Lock-Verhalten
      konsistent zur bestehenden `WriteRecord`-Implementierung. Tests:
      `JsonlLogWriterFlushTests` (Unit, temporäres Verzeichnis). Konzept
      §„Writer-Lifecycle". **Hinweis:** Der README-Abschnitt zum
      `FileShare.ReadWrite`-Live-Reader-Hinweis ist Teil von EPIC-05 (Doku).

- [ ] **EPIC-05: Doku, Sample & Release-Vorbereitung** — `README.md`
      erweitern: Options-Tabelle um 6 neue Properties, neue Sektion
      „Manual ToolCollection" mit Copy-Paste-Beispiel für `AddFeedbackTool`,
      neue Sektion „Response Logging" (Default: vollständig/unbegrenzt,
      `appsettings.json`-Override-Beispiel), neuer Hinweis-Block
      „Reading logs while the server is running" mit `FileShare.ReadWrite`-
      Codebeispiel. Neues `CHANGELOG.md` (Keep-a-Changelog, `## [Unreleased]`
      mit Datum 2026-08-17 — finale Version setzt das Release-Skript).
      Neues Sample `samples/ManualToolCollectionServer/` mit eigenem
      `.csproj` und `Program.cs`, das `McpServerOptions.ToolCollection`
      manuell befüllt + `WithObservability` + `AddFeedbackTool` nutzt
      (lauffähig via `dotnet run`). Konzept §„Dokumentation" +
      §„Wo im Projekt" (Doku-/Sample-Eintrag).
