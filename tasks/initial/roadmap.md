---
status: completed
task: initial
derived_from: Konzept.md
created_at: "2026-08-17T20:26:00+02:00"
last_updated: "2026-08-17T20:26:00+02:00"
created_by_model: Gemini 3.7 Flash
created_by_model_knowledge_cutoff: "2026-01"
---

# Roadmap: initial

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build`
- **Test-Command:** `dotnet test`
- **Lint-Command:** `dotnet test` & AiNetLinter MCP-Server (`safeguard`, `get_violations`)
- **Code-Style-Kurzfassung:** .NET 10 / C# 14, `#nullable enable`, `sealed` für Klassen, max. 300 LoC/Datei, max. 60 LoC/Methode, XML-Docs auf öffentlichen Typen (`McpObservabilityOptions`, `McpObservabilityExtensions`), interne Typen in `Internal`-Namespace, keine externen Dependencies außer den im Konzept freigegebenen.
- **Commit-Konventionen:** Conventional Commits, Deutsch, imperativ, Subject <= 72 Zeichen inkl. Suffix `[initial]`, Body mit `Refs: tasks/initial/step-NNN`.

## Regel-Index

- `.agents/rules/McpObservabilityRichtlinien.mdc` — Architektur- & Workflow-Richtlinien, JSONL-Schema-Invarianten, API-Einschränkungen und Windows-Umgebungskonventionen.
- `.agents/rules/AiNetLinter.mdc` — Roslyn-basierte C#-Codequalitätsrichtlinien, Komplexitätsgrenzen, NoSilentCatch und Sentinel-Regeln.

## Epics

- [x] EPIC-01: Core Logging & Sanitizing Engine — Datenmodelle, JsonlLogWriter mit Verzeichnisstruktur/Lebenszyklus und rekursiver ArgumentSanitizer (→ step-001)
- [x] EPIC-02: MCP Integration & Feedback Channel — ToolCallLoggingHandler-Interzeption, FeedbackTools (`report_observability_feedback`) und WithObservability-Extension (→ step-002)
- [x] EPIC-03: Umfassende Test-Suite — Unit-Tests für Sanitizer & Writer sowie Integrationstests für Tool-Aufrufe, Feedback-Aufrufe und Options-Schalter mit Temp-Verzeichnis-Isolation (→ step-001, step-002)
- [ ] EPIC-04: Dokumentation, Samples & Package-Verifikation — Sample-Überprüfung, README-Dokumentation mit Schema & Integrationsanleitung sowie Pack-/Release-Prüfung (→ step-003)
