---
status: open
type: step-plan
task: initial
step: step-002
corrects: null
title: "MCP Middleware, Feedback-Tool Registrierung und Integrationstests"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.7 Flash
created_by_model_knowledge_cutoff: "2026-01"
created_at: "2026-08-17T20:30:30+02:00"
related_to: [step-001]
---

# Step step-002: MCP Middleware, Feedback-Tool Registrierung und Integrationstests

## Bezug

- **Task:** `initial`
- **Epic:** `EPIC-02` aus `roadmap.md` — MCP Integration & Feedback Channel (Interception, FeedbackTools, WithObservability)
- **Konzept-Referenz:** `Konzept.md` §6 (Feedback-Tool), §7 (Konfiguration), §8 (Öffentliche Integrations-API), §9 (Interne Architektur), §11 (Tests)

## Aktueller Projektzustand (JIT-Kontext)

In Step-001 wurden die Core Logging Engine (`ArgumentSanitizer`, `JsonlLogWriter`) erfolgreich validiert und getestet. `McpObservabilityExtensions.cs`, `ToolCallLoggingHandler.cs` und `FeedbackTools.cs` sind als Entwurf vorhanden, jedoch noch ohne vollständige DI-Absicherung für unabhängige Flag-Kombinationen (`EnableToolCallLogging = false` bei `EnableFeedbackTool = true`) und noch ohne End-to-End Integrationstests gegen das MCP SDK.

## Intention

Die Extension-Methode `WithObservability` und den Interceptor `ToolCallLoggingHandler` sowie das Tool `report_observability_feedback` vollständig mit dem `ModelContextProtocol` Server-Pipeline-Lifecycle verzahnen. Erstellung einer robusten Integrationstest-Suite, die reale MCP-Server-Pipelines aufbaut, Tool-Aufrufe und Feedback-Meldungen ausführt und die resultierenden JSONL-Logs sowie Options-Flags verifiziert.

## Konkrete Änderungen

### Datei 1: `src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs`
- **Was:** `JsonlLogWriter` als Singleton registrieren, wenn Logging oder Feedback aktiviert ist. Vollständige XML-Dokumentation gemäß Richtlinie §8.
- **Warum:** Sicherstellen, dass das Feedback-Tool auch dann loggen kann, wenn das allgemeine Tool-Call-Logging deaktiviert ist.

### Datei 2: `src/RalfHuesing.Mcp.Observability/Internal/FeedbackTools.cs`
- **Was:** DI-Auflösung von `JsonlLogWriter` und `ObservabilityContext` absichern und exakten Rückgabetext `"Feedback recorded. Thank you."` sowie Parameterbeschreibungen gemäß Konzept §6 sicherstellen.
- **Warum:** Richtlinien- und Konzeptkonformität für das LLM-Feedback-Tool.

### Datei 3: `src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs`
- **Was:** Absicherung der `CallToolFilter`-Middleware zur Protokollierung aller Tool-Aufrufe inklusive Dauer, Fehlerzustand (`IsErrorResult`, Exception), sanitizierten Argumenten und Schreiben in den `JsonlLogWriter`.
- **Warum:** Kernversprechen des automatischen und transparenten Tool-Call-Loggings.

### Datei 4: `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpObservabilityIntegrationTests.cs` (Neu)
- **Was:** Integrationstest mit echtem In-Process MCP Server (`AddMcpServer().WithObservability()`), Aufruf eines Beispiel-Tools (`echo`) und Verifikation des generierten `tool_call`-Records in der JSONL-Datei.
- **Warum:** Erfüllt Mindestanforderung aus Konzept §11 und Richtlinie §7.

### Datei 5: `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpFeedbackIntegrationTests.cs` (Neu)
- **Was:** Integrationstest für den Aufruf von `report_observability_feedback`, Prüfung des erzeugten `feedback`-Records in der JSONL-Datei auf alle Pflicht- und optionalen Felder.
- **Warum:** Erfüllt Mindestanforderung aus Konzept §11 und Richtlinie §7.

### Datei 6: `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsFlagsTests.cs` (Neu)
- **Was:** Tests für `Enabled = false` (weder Logging noch Feedback-Tool registriert), `EnableToolCallLogging = false` (nur Feedback-Records), `EnableFeedbackTool = false` (Feedback-Tool nicht in Tool-Liste).
- **Warum:** Erfüllt Mindestanforderung aus Konzept §11 und Richtlinie §7.

## Tests

- [ ] `McpObservabilityIntegrationTests`: `ToolCall_WritesToolCallRecordToJsonl`
- [ ] `McpFeedbackIntegrationTests`: `ReportFeedback_WritesFeedbackRecordToJsonl`
- [ ] `McpOptionsFlagsTests`: `WhenEnabledIsFalse_DoesNotLogAndDoesNotRegisterFeedbackTool`
- [ ] `McpOptionsFlagsTests`: `WhenToolCallLoggingDisabled_OnlyFeedbackIsLogged`
- [ ] `McpOptionsFlagsTests`: `WhenFeedbackToolDisabled_FeedbackToolIsNotRegistered`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) fehlerfrei (Zero-Warning)
- [ ] Test-Command (`dotnet test`) grün
- [ ] AiNetLinter `safeguard` Score >= 8.0 und keine Warnings
- [ ] Code-Commit auf aktuellem Branch
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` aktualisiert

## Rules-Refs

- `.agents/rules/McpObservabilityRichtlinien.mdc#1.-Design-Philosophie` — Minimaler Integrationsaufwand, Single Line Extension.
- `.agents/rules/McpObservabilityRichtlinien.mdc#6.-Öffentliche-API-Stabilität` — Nur `McpObservabilityOptions` und `WithObservability` sind public.
- `.agents/rules/McpObservabilityRichtlinien.mdc#7.-Tests-(verbindlich)` — Isolierte Temp-Pfade für alle I/O-Integrationstests.

## Notes

- Alle Integrationstests nutzen ein temporäres Verzeichnis in `options.LogDirectory`, das in `Dispose()` sauber gelöscht wird.
