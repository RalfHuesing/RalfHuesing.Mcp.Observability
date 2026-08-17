---
status: done (pending audit)
type: step-plan
task: refine
step: 005
corrects: null
title: "Dokumentation, Sample-Server, CHANGELOG und Linter-Report-Bereinigung"
epic: EPIC-05
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-18T00:55:00+02:00
related_to:
  - step-004/step-result.md
  - step-004/step-review.md
---

# Step 005: Dokumentation, Sample-Server, CHANGELOG und Linter-Report-Bereinigung

## Bezug

- **Task:** `refine`
- **Epic:** `EPIC-05` aus `roadmap.md`.
- **Konzept-Referenz:** `Konzept.md` §„Muss-Haben" → „Dokumentation",
  „Sample", „CHANGELOG" und Definition of Done.
- **Tech-Debt:** `AiNetLinterTests` überschreibt `linter-report.md` bei jedem
  Lauf; wir stellen einen sauberen Clean-Status im Report sicher.

## Aktueller Projektzustand (JIT-Kontext)

1. **`README.md`**: spiegelt Stand v1.0.0 wider. Es fehlen die 6 neuen
   Options-Properties, Sektion „Manual ToolCollection", Sektion „Response
   Logging", Hinweis „Reading logs while the server is running" und

## Konkrete Änderungen

### 1. `README.md` aktualisieren
- Options-Tabelle um alle 6 neuen Properties erweitern (`ServerName`,
  `ServerVersion`, `FeedbackConfirmationMessage`, `AdditionalSensitiveKeys`,
  `EnableResponseLogging`, `MaxResponseLength`).
- Neue Sektion „Manual ToolCollection": Dokumentiert den Tool-Schatten-Fix
  mit `WithObservability()` sowie den programmatischen Weg mit
  `collection.AddFeedbackTool(services)`.
- Neue Sektion „Response Logging": Beschreibt die additiven Felder
  `response`, `responseLength`, `responseLines`, `responseTruncated`,
  `nonTextContentBlocks` und Konfiguration.
- Neuer Hinweis-Block „Reading logs while the server is running" mit
  `FileShare.ReadWrite`.
- Sektion „Diagnostics Service": `IMcpObservabilityService` im DI verfügbar.

### 2. `CHANGELOG.md` anlegen
Keep-a-Changelog-Format mit `Added` und `Changed` für alle Features aus
EPIC-01 bis EPIC-04 (Stand 2026-08-17/18).

### 3. `samples/ManualToolCollectionServer/` erstellen & Solution aktualisieren
- `ManualToolCollectionServer.csproj` (`net10.0`, `Exe`, Ref auf Observability).
- `Program.cs`: Manuelle `ToolCollection` mit `McpServerTool.Create` +
  `WithObservability()`.
- `RalfHuesing.Mcp.Observability.slnx`: Projekt unter `/samples/` ergänzen.

### 4. Linter-Report & Tech-Debt Bereinigung
- `dotnet test` sicherstellen, so dass `linter-report.md` mit 0 Violations generiert wird.

## Verifikation
- `dotnet build --configuration Release` → 0 Fehler, 0 Warnungen über gesamte Solution.
- `dotnet test --configuration Release` → 46/46 Tests grün, AiNetLinter clean.
- `dotnet run --project samples/ManualToolCollectionServer` startet sauber.

   `IMcpObservabilityService`.
2. **`CHANGELOG.md`**: existiert noch nicht im Repo-Root. Muss nach
   Keep-a-Changelog angelegt werden mit Sektion `## [Unreleased]`.
3. **`samples/ManualToolCollectionServer/`**: existiert noch nicht.
   Muss `ManualToolCollectionServer.csproj` + `Program.cs` enthalten und in
   `RalfHuesing.Mcp.Observability.slnx` eingebunden werden.
