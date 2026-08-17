---
status: done
type: step-result
task: refine
step: 003
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: minimax-m3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-18T00:20:00+02:00
code_commit_hash: ef83d6e
status_after: done
blocker_category: n/a
---

# Result Step 003: Public Feedback-Tool-API, Tool-Schatten-Fix und Richtlinien-Lockerung §6

## Zusammenfassung

Alle 6 Plan-Punkte umgesetzt: `FeedbackToolName`-Konstante, public
`McpObservabilityTools` (`CreateFeedbackTool` + idempotente `AddFeedbackTool`-
Extension), `ObservabilityPostConfigureOptions` als Tool-Schatten-Fix in
`WithObservability`, Richtlinie §6 mit Datum/Begründung/Alternativen gelockert,
3 neue Integration-Tests. Build grün (0 Warnungen unter
`TreatWarningsAsErrors`), 44/44 Tests grün, AiNetLinter
`RunLinterShouldBeClean` clean nach Extraktion von `ManualSampleTools` auf
Top-Level.

## Geänderte Dateien

- `src/.../Internal/ObservabilityConstants.cs` — `FeedbackToolName`-Konstante.
- `src/.../Internal/FeedbackTools.cs` — Attribut nutzt die Konstante.
- `src/.../McpObservabilityTools.cs` (neu, public) — `CreateFeedbackTool` via
  `McpServerTool.Create` mit Method-Group auf `FeedbackTools.ReportFeedback`
  (Schema-Inferenz behält Parameternamen, `[Description]` und Defaults);
  `AddFeedbackTool`-Extension, idempotent per `TryGetPrimitive`.
- `src/.../Internal/ObservabilityPostConfigureOptions.cs` (neu) — hängt das
  Feedback-Tool nach allen Konfigurationen an manuelle `ToolCollection` an.
- `src/.../McpObservabilityExtensions.cs` — registriert
  `IPostConfigureOptions<McpServerOptions>` im `EnableFeedbackTool`-Zweig.
- `.agents/rules/McpObservabilityRichtlinien.mdc` §6 — Lockerung mit Datum,
  Begründung und verworfenen Alternativen dokumentiert.
- `tests/.../Integration/McpServerOptionsToolCollectionTests.cs` (neu) —
  3 Cases (Listing, Idempotenz, Feedback-Record).

## Verifikation

```
dotnet build --configuration Release → grün, 0 Warnungen
dotnet test --configuration Release  → grün, 44/44 (41 alt + 3 neu)
AiNetLinter RunLinterShouldBeClean   → clean
```

## Abweichungen vom Plan

- **`ManualSampleTools` als top-level Klasse statt nested** in der Test-Klasse
  (AiNetLinter `BanPublicNestedTypes` schlug im ersten Lauf an) — analog zum
  `ResponseExtraction`-Präzedenzfall aus step-002. Semantik unverändert.
- **`CreateFeedbackTool` setzt zusätzlich `McpServerToolCreateOptions.Services`**
  auf den übergebenen Provider (Fallback-Bindung des `IServiceProvider`-
  Parameters, wenn der Request-Kontext keinen liefert). Plan-Skizze enthielt
  das Feld nicht explizit, Konzept-Signatur `CreateFeedbackTool(IServiceProvider)`
  impliziert die Verdrahtung aber.

## Beobachtungen

- **SDK-Verhalten bei manueller `ToolCollection` verifiziert:** der Consumer-
  Configure-Callback ersetzt die vom SDK-Setup befüllte Collection; das
  DI-registrierte `WithTools<FeedbackTools>()`-Tool wird dadurch beschattet —
  genau der im Konzept beschriebene Effekt. Der Post-Configure-Hook läuft
  danach und hängt das Tool wieder an. Idempotenz greift, wenn der Consumer
  es selbst vorab angehängt hat (Test 2: genau ein Eintrag in `tools/list`).
- **Method-Group auf `internal` Methode funktioniert** mit
  `McpServerTool.Create(Delegate)` — kein `IsPublic`-Zwang im SDK 2.2.0,
  Invocation über den gebundenen Delegate. Aufruf-Test (Test 3) schreibt den
  `feedback`-Record byte-gleich zum Reflection-Weg.
- **Kein AiNetLinter.mdc-Side-Effect** in diesem Lauf (Tool-Version
  unverändert).

## Bekannte Unschärfen

- **`CreateFeedbackTool(IServiceProvider)` verlangt non-null Provider.**
  Konsumenten ohne DI-Container (reine CLI-Server) müssen
  `new ServiceCollection().BuildServiceProvider()` o. ä. übergeben;
  `ReportFeedback` selbst toleriert weiterhin `null`-Services zur
  Aufrufzeit. Bewusst so belassen (Fail-fast an der API-Grenze).
- **Doku-Commit** (step-result, codemap, task-state) steht noch aus;
  Code-Commit `ef83d6e` ist self-contained.

## Falls Status `blocked`

Nicht zutreffend.
