---
status: done (pending audit)
type: step-plan
task: initial
step: step-001
corrects: null
title: "Core Engine Validierung & Unit-Tests fuer Sanitizer und Writer"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.7 Flash
created_by_model_knowledge_cutoff: "2026-01"
created_at: "2026-08-17T20:27:00+02:00"
related_to: []
---

# Step step-001: Core Engine Validierung & Unit-Tests fuer Sanitizer und Writer

## Bezug

- **Task:** `initial`
- **Epic:** `EPIC-01` aus `roadmap.md` — Core Logging & Sanitizing Engine (Datenmodelle, Writer, Sanitizer)
- **Konzept-Referenz:** `Konzept.md` §4 (Verzeichnis- und Dateistruktur), §5 (Datenschema JSONL-Records), §9.1 (Kernkomponenten)

## Aktueller Projektzustand (JIT-Kontext)

Die Kernklassen `LogRecords.cs`, `ArgumentSanitizer.cs`, `JsonlLogWriter.cs`, `ObservabilityContext.cs` und `JsonlSerializerOptions.cs` existieren bereits als Grundgerüst in `src/RalfHuesing.Mcp.Observability/Internal/`. In `tests/` existiert aktuell nur der `AiNetLinterTests`-Prüfrahmen, jedoch noch keine Unit-Tests für `ArgumentSanitizer` und `JsonlLogWriter`.

## Intention

Sicherstellen, dass die Core Logging Engine exakt den Invarianten aus dem Konzept entspricht (rekursives Sanitizing inklusive case-insensitiver Erkennung und verschachtelter Objekte/Arrays, Schema-Serialisierung mit UTC-Timestamp und schemaVersion 1, Verzeichnisauflösung und thread-sichere Dateierstellung). Zudem Implementierung einer vollständigen Unit-Test-Suite für `ArgumentSanitizer` und `JsonlLogWriter` unter Verwendung isolierter temporärer Testverzeichnisse.

## Konkrete Änderungen

### Datei 1: `src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs`
- **Was:** Verifizieren und Absichern der rekursiven Sanitizing-Logik für `JsonObject`, `JsonArray` und `JsonElement` unter Verwendung der in Konzept §9.1 definierten sensiblen Schlüssel (`password`, `pwd`, `secret`, `token`, `apiKey`, `apikey`, `accessToken`, `authorization`, `connectionString`, `privateKey`).
- **Warum:** Richtlinie §5 verlangt vollständige Redaktion sensibler Parameter vor dem Logging.

### Datei 2: `src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs`
- **Was:** Absichern der Pfadkonstruktion (`%LOCALAPPDATA%\RalfHuesing\McpObservability\{ServerName}\{yyyy-MM-dd}\{ServerName}_{ProcessId}_{InstanceId}.jsonl` bzw. `LogDirectory`), Erstellung fehlender Ordner und `FileShare.Read`-Append-Modus.
- **Warum:** Gewährleistet Multi-Prozess-Sicherheit und Überlebensfähigkeit außerhalb von Release-Ordnern.

### Datei 3: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ArgumentSanitizerTests.cs` (Neu)
- **Was:** Unit-Tests für `ArgumentSanitizer`: Flache Schlüssel, verschachtelte Objekte, Arrays, case-insensitiver Vergleich (`APIKEY`, `Password`, `authorization`), Whitelist-Elemente ohne Redaction, Null-/Empty-Handling.
- **Warum:** Erfüllt Test-Mindestanforderung aus Konzept §11 und Richtlinie §7.

### Datei 4: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterTests.cs` (Neu)
- **Was:** Unit-Tests für `JsonlLogWriter`: Dateierstellung im konfigurierten Temp-Pfad, JSONL-Gültigkeit jeder Zeile, sequenzielle/parallele Schreibvorgänge, sauberes Schließen bei Dispose.
- **Warum:** Erfüllt Test-Mindestanforderung aus Konzept §11 und Richtlinie §7.

## Tests

- [ ] `ArgumentSanitizerTests`: `Sanitize_RedactsKnownSensitiveKeys_CaseInsensitive`
- [ ] `ArgumentSanitizerTests`: `Sanitize_RecursivelyRedactsNestedObjectsAndArrays`
- [ ] `ArgumentSanitizerTests`: `Sanitize_PreservesNonSensitiveValues`
- [ ] `ArgumentSanitizerTests`: `Sanitize_HandlesNullOrEmptyArguments`
- [ ] `JsonlLogWriterTests`: `WriteRecord_CreatesFileInSpecifiedDirectoryWithCorrectNaming`
- [ ] `JsonlLogWriterTests`: `WriteRecord_AppendsValidJsonLines`
- [ ] `JsonlLogWriterTests`: `WriteRecord_ThreadSafeConcurrentWrites`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) fehlerfrei (Zero-Warning)
- [ ] Test-Command (`dotnet test`) grün
- [ ] AiNetLinter `safeguard` Score >= 8.0 und keine Warnings
- [ ] Code-Commit auf aktuellem Branch
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` aktualisiert

## Rules-Refs

- `.agents/rules/McpObservabilityRichtlinien.mdc#5.-Datenformat-Invarianten-(verbindlich)` — SchemaVersion 1, UTC-ISO8601, Argument-Sanitizer Redaction Marker.
- `.agents/rules/McpObservabilityRichtlinien.mdc#7.-Tests-(verbindlich)` — Isolierte Temp-Verzeichnisse, xUnit v3, Mindest-Coverage für Sanitizer & Writer.
- `.agents/rules/AiNetLinter.mdc` — Sealed Classes, Nullable Enable, MaxLineCount, NoSilentCatch.

## Notes

- Für Dateitests `Path.Combine(Path.GetTempPath(), "McpObsTests_" + Guid.NewGuid().ToString("N"))` verwenden und im `Dispose` aufräumen.
- Keine Redundanzprüfungen per Grep, wenn AiNetLinter-Tools vollständige Ergebnisse liefern.
