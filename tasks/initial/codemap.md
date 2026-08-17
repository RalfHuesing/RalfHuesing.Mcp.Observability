---
task: initial
type: codemap
maintained_by: planer, coder, kritiker
last_updated: "2026-08-17T20:26:00+02:00"
---

# CodeMap: initial

Task-scoped Landkarte — existiert nur für diesen Task, wird mit `tasks/initial` gelöscht.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem Grobüberblick.
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich angelegte oder geänderte Module, vor dem Doku-Commit.
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen, ergänzt neue Bereiche.
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff entspricht.

## Anti-Loop-Nutzen

Gleicht geplante Änderungen gegen bestehende Architekturentscheidungen ab, um inkrementelle Widersprüche und Schleifen zu vermeiden.

## Karte

- **`src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs`** — Öffentliche Konfigurationsklasse für Logging-, Feedback- und Verzeichnisoptionen. (zuletzt: initial)
- **`src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs`** — Öffentliche Extension-Methode `WithObservability` für `IMcpServerBuilder`. (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/ObservabilityContext.cs`** — Singleton-Kontext für Server-Metadaten, Instanz-ID und Writer-Lebenszyklus. (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs`** — Interceptor zur Protokollierung aller eingehenden `tools/call`-Aufrufe. (zuletzt: step-002)
- **`src/RalfHuesing.Mcp.Observability/Internal/FeedbackTools.cs`** — MCP-Tool `report_observability_feedback` für LLM-Agenten-Rückmeldungen. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpObservabilityIntegrationTests.cs`** — Integrationstests für Tool-Call-Logging über Duplex-Stream-Pipes. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpFeedbackIntegrationTests.cs`** — Integrationstests für das Feedback-Tool und Schema-Verifikation. (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsFlagsTests.cs`** — Integrationstests für Konfigurations-Flags (Enabled, EnableToolCallLogging, EnableFeedbackTool). (zuletzt: step-002)
- **`tests/RalfHuesing.Mcp.Observability.Tests/AiNetLinter/AiNetLinterTests.cs`** — Test-Suite zur Validierung der AiNetLinter-Regelwerke und Metriken. (zuletzt: initial)
- **`samples/MinimalMcpServerWithObservability/Program.cs`** — Minimaler Beispiel-MCP-Server mit `.WithObservability()`. (zuletzt: initial)
