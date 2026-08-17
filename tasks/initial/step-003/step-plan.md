---
status: done (pending audit)
type: step-plan
task: initial
step: step-003
epic: EPIC-04
step_type: single
planned_by: planer
planned_by_model: Gemini 3.7 Flash
planned_by_model_knowledge_cutoff: "2026-01"
planned_at: "2026-08-17T20:46:30+02:00"
---

# Step Plan: step-003: Dokumentation, Samples & Package-Verifikation

## Ziel

Validierung und Finalisierung des gesamten Repositories: Korrektur des NuGet-Pack-Pfads für `README.md`, Verifikation des Sample-Servers `MinimalMcpServerWithObservability`, vollständiger `dotnet pack -c Release`-Lauf und finale AiNetLinter-Prüfung aller Regeln und Metriken.

## Betroffene Module & Dateien

- `src/RalfHuesing.Mcp.Observability/RalfHuesing.Mcp.Observability.csproj` — Pfad zu `README.md` im NuGet-Pack korrigieren (`../../README.md`).
- `samples/MinimalMcpServerWithObservability/` — Build- und Integrationsfähigkeit des Minimal-Samples verifizieren.
- `README.md` — Dokumentation gegen Implementierung gegenprüfen.

## Geplante Änderungen

1. `RalfHuesing.Mcp.Observability.csproj`: `<None Include="../../README.md" Pack="true" PackagePath="/" />` korrigieren.
2. `dotnet build` für die gesamte Solution (src, tests, samples) ausführen.
3. `dotnet test` für alle 24 Tests ausführen.
4. `dotnet pack -c Release` erfolgreich durchführen.
5. AiNetLinter MCP-Tools (`safeguard`, `get_violations`) für 10/10 Score ausführen.

## Verifikation

- `dotnet build` ohne Fehler und ohne Warnungen.
- `dotnet test` alle 24 Tests bestanden.
- `dotnet pack -c Release` erzeugt `RalfHuesing.Mcp.Observability.1.0.0.nupkg`.
- `safeguard` liefert 10.00/10 PASS.
