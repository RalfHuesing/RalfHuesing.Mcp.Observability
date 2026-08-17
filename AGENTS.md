# AGENTS.md — RalfHuesing.Mcp.Observability

## Was ist dieses Projekt?

`RalfHuesing.Mcp.Observability` ist ein NuGet-Paket, das MCP-Servern
(basierend auf dem offiziellen `ModelContextProtocol`-SDK) einheitliches
JSONL-Logging aller Tool-Aufrufe und einen strukturierten Feedback-Kanal
für LLM-Agenten gibt — mit minimalem Integrationsaufwand.

Kernversprechen: **Eine Zeile Code** (`.WithObservability(...)`) genügt,
um einen bestehenden MCP-Server mit Tool-Call-Logging und dem Feedback-
Tool `report_observability_feedback` auszustatten.

## Projektstruktur

```
RalfHuesing.Mcp.Observability/
├── src/
│   └── RalfHuesing.Mcp.Observability/   # Das NuGet-Paket
│       ├── McpObservabilityOptions.cs
│       ├── McpObservabilityExtensions.cs
│       └── Internal/
│           ├── ObservabilityContext.cs
│           ├── JsonlLogWriter.cs
│           ├── ArgumentSanitizer.cs
│           ├── ToolCallLoggingHandler.cs
│           └── FeedbackTools.cs
├── tests/
│   └── RalfHuesing.Mcp.Observability.Tests/
├── samples/
│   └── MinimalMcpServerWithObservability/
├── .agents/
│   ├── rules/
│   │   └── McpObservabilityRichtlinien.mdc   # Verbindliche Richtlinien
│   └── Agent-Scaffolding/                    # git subtree — Workflow-Templates
├── tasks/
│   └── initial/
│       └── Konzept.md                        # Vollständiges Implementierungskonzept
├── AGENTS.md                                 # Diese Datei
└── README.md
```

## Verbindliche Richtlinien

**Vor jeder Code-Änderung lesen:**
[`.agents/rules/McpObservabilityRichtlinien.mdc`](.agents/rules/McpObservabilityRichtlinien.mdc)

Die wichtigsten Punkte:

- **Öffentliche API:** Nur `McpObservabilityOptions` und
  `McpObservabilityExtensions.WithObservability` sind `public`.
  Alle internen Klassen bleiben `internal`.
- **JSONL-Schema-Invarianten:** `schemaVersion`, `timestamp` (UTC),
  `recordType` (`"tool_call"` | `"feedback"`) und `instanceId` sind
  Pflichtfelder. Änderungen am Schema sind Breaking Changes.
- **Argument-Sanitizing:** `ArgumentSanitizer` läuft immer vor dem
  Schreiben. Sensible Keys werden durch `"***REDACTED***"` ersetzt.
- **Zero-Warning:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  in allen Projekten.
- **Tests:** xUnit v3. File-I/O-Tests nutzen temporäre, isolierte
  Verzeichnisse — nie das echte `%LOCALAPPDATA%`-Zielverzeichnis.

## Implementierungskonzept

Das vollständige Konzept (Ziele, Schema, API, Architektur, Tests,
Definition of Done) liegt in:
[`tasks/initial/Konzept.md`](tasks/initial/Konzept.md)

## Entwicklungs-Workflows (Agent-Scaffolding)

Orchestrierte Multi-Step-Workflows liegen unter:
`.agents/Agent-Scaffolding/dev-loop/`

- **Konzept → Umsetzung:**
  `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md tasks/initial`

## Windows-Umgebung

Shell: PowerShell 7 (`pwsh`). Git immer mit `--no-pager`.
Details: `.agents/rules/McpObservabilityRichtlinien.mdc` §3.

## Sprache

- **Code & Dokumentation (README, AGENTS.md, Docs):** Englisch.
- **Kommunikation Agent ↔ Nutzer:** Deutsch.
- **Richtlinien-Dateien (`.mdc`, Konzept):** Deutsch.
