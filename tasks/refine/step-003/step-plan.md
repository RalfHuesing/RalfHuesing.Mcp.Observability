---
status: planned
type: step-plan
task: refine
step: 003
corrects: null
title: "Public Feedback-Tool-API, Tool-Schatten-Fix und Richtlinien-Lockerung §6"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-18T00:05:00+02:00
related_to:
  - step-002/step-result.md
  - step-002/step-review.md
---

# Step 003: Public Feedback-Tool-API, Tool-Schatten-Fix und Richtlinien-Lockerung §6

## Bezug

- **Task:** `refine`
- **Epic:** `EPIC-03` aus `roadmap.md`.
- **Konzept-Referenz:** `Konzept.md` §„Muss-Haben" →
  „Manueller ToolCollection-Support", „Tool-Schatten-Fix",
  „Richtlinien-Update" sowie Tests Punkt 2 (`McpServerOptionsToolCollectionTests`).

## Aktueller Projektzustand (JIT-Kontext)

Geprüft gegen SDK `ModelContextProtocol.Core 2.2.0` (XML-Docs im NuGet-Cache):

- **`McpServerTool.Create(Delegate, McpServerToolCreateOptions)`** existiert.
  Eine **Method-Group** auf eine statische Methode bewahrt Parameternamen,
  `[Description]`-Attribute und Default-Werte (Schema-Inferenz via
  `Delegate.Method`). `McpServerToolCreateOptions` hat `Name`, `Description`,
  `Services` u. a.
- **`McpServerPrimitiveCollection<T>`** ist name-keyed: `Add(T)`,
  `TryGetPrimitive(string, out T)`, `Contains`. `Add` auf vorhandenem Namen
  würde werfen → Idempotenz zwingend vorher prüfen.
- **`McpServerOptions.ToolCollection`** ist setzbar
  (`McpServerPrimitiveCollection<McpServerTool>?`). `IPostConfigureOptions<McpServerOptions>`
  läuft garantiert **nach** allen `IConfigureOptions`, also nach Consumer-
  Konfiguration und dem SDK-eigenen `McpServerOptionsSetup`.
- **`FeedbackTools.ReportFeedback`** (`src/.../Internal/FeedbackTools.cs`):
  `internal static string ReportFeedback(IServiceProvider? services, ...)`.
  `IServiceProvider?` wird vom SDK aus dem Request-Kontext gebunden; Methode
  toleriert `null`. Tool-Name `"report_observability_feedback"` ist aktuell

## Konkrete Änderungen

### 1. `src/.../Internal/ObservabilityConstants.cs` — Tool-Name-Konstante

- Neu: `internal const string FeedbackToolName = "report_observability_feedback";`
- In `FeedbackTools.cs` das Attribut auf
  `[McpServerTool(Name = ObservabilityConstants.FeedbackToolName)]` umstellen
  (Attribut-Argumente müssen const sein — erfüllt).

### 2. `src/.../McpObservabilityTools.cs` (NEU, public)

Namespace `RalfHuesing.Mcp.Observability` (öffentliche API, §6 nach Lockerung):

```csharp
public static class McpObservabilityTools
{
    public static McpServerTool CreateFeedbackTool(IServiceProvider services)
        => McpServerTool.Create(
            (Func<IServiceProvider?, string, string, string, string?, string,
                   string?, string?, string?, string>)
            FeedbackTools.ReportFeedback,
            new McpServerToolCreateOptions
            {
                Name = ObservabilityConstants.FeedbackToolName,
                Services = services,
            });

    public static void AddFeedbackTool(
        this McpServerPrimitiveCollection<McpServerTool> tools,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(tools);
        if (tools.TryGetPrimitive(ObservabilityConstants.FeedbackToolName, out _))
        {
            return; // idempotent
        }
        tools.Add(CreateFeedbackTool(services));
    }
}
```

- Method-Group auf die interne Implementierung: behält Parameternamen,
  `[Description]`-Attribute und Default-Werte für die Schema-Inferenz.
- XML-Doc auf beiden Membern (public API, Richtlinie §8).
- `FeedbackTools` bleibt `internal` — die Method-Group ist innerhalb derselben
  Assembly zulässig, keine Sichtbarkeits-Änderung nötig.

### 3. `src/.../Internal/ObservabilityPostConfigureOptions.cs` (NEU, internal)

```csharp
internal sealed class ObservabilityPostConfigureOptions(
    McpObservabilityOptions options,
    IServiceProvider services) : IPostConfigureOptions<McpServerOptions>
{
    public void PostConfigure(string? name, McpServerOptions serverOptions)
    {
        if (options.EnableFeedbackTool && serverOptions.ToolCollection is { } collection)
        {
            collection.AddFeedbackTool(services);
        }
    }
}
```

### 4. `src/.../McpObservabilityExtensions.cs` — Registrierung des Post-Configure

Im `EnableFeedbackTool`-Zweig zusätzlich:

```csharp
builder.Services.AddSingleton<IPostConfigureOptions<McpServerOptions>,
    ObservabilityPostConfigureOptions>();
```

Damit funktioniert `WithObservability()` sowohl mit `builder.WithTools()`
(Reflection-Weg, unverändert) als auch mit manuell gesetzter
`McpServerOptions.ToolCollection` (Tool-Schatten-Fix). Idempotenz verhindert
Doppel-Registrierung, wenn der Consumer das Tool selbst schon angehängt hat.

### 5. `.agents/rules/McpObservabilityRichtlinien.mdc` §6 — Lockerung dokumentieren

§6 erweitern: öffentliche Typen sind jetzt `McpObservabilityOptions`,
`McpObservabilityExtensions`, `IMcpObservabilityService` und
`McpObservabilityTools`. Mit Datum (2026-08-18), Begründung
(AiNetLinter-Integration: Reflection-Workarounds auf `internal FeedbackTools`
auflösen, manuelle `ToolCollection`-Szenarien ohne Hack unterstützen) und
geprüften Alternativen (`InternalsVisibleTo` für Konsumenten — verworfen,
koppelt an Interna; Duplikation der Tool-Implementierung als public Kopie —
verworfen; `IMcpObservabilityService.LogCustomRecord` — bewusstes Non-Goal,
Schema-Invariante §5).

### 6. `tests/.../Integration/McpServerOptionsToolCollectionTests.cs` (NEU)

Erbt von `IntegrationTestBase`. Drei Cases:

1. **`ManualToolCollection_WithObservability_FeedbackToolIsListed`** —
   Server mit manuell gesetzter `ToolCollection` (ein Sample-Tool via
   `McpServerTool.Create`) + `WithObservability(options)`; `tools/list` enthält
   `report_observability_feedback` **und** das Sample-Tool.
2. **`ManualToolCollection_PreAddedFeedbackTool_StaysIdempotent`** —
   Consumer hängt das Feedback-Tool selbst per `collection.AddFeedbackTool(sp)`
   an + `WithObservability` → kein Duplikat/keine Exception; `tools/list`
   enthält genau einen Eintrag mit dem Namen.
3. **`ManualToolCollection_FeedbackToolCall_WritesFeedbackRecord`** —
   Feedback-Tool über die manuelle Collection aufrufen → `feedback`-Record
   korrekt in der JSONL (semantisch identisch zum Reflection-Weg).

Aufbau analog `McpFeedbackIntegrationTests`: `Host.CreateEmptyApplicationBuilder`,
`AddMcpServer(o => o.ToolCollection = [sampleTool])`, Duplex-Pipes, TempDirectory.

## Verifikation

- `dotnet build --configuration Release` → 0 Warnungen (TreatWarningsAsErrors).
- `dotnet test --configuration Release` → alle bisherigen 41 + 3 neue Tests grün.
- AiNetLinter-Regeln: `McpObservabilityTools` ist `static`, XML-Doc auf public,
  ≤60 Zeilen/Methode, kein `dynamic`, Namespace-Mapping korrekt.

## Nicht-Ziele dieses Steps

- README/CHANGELOG/Sample (EPIC-05).
- `JsonlLogWriter`-Lifecycle (EPIC-04).

  nur im `[McpServerTool(Name = ...)]`-Attribut hartkodiert — keine Konstante.
- **`McpObservabilityExtensions.WithObservability`**: registriert Options,
  Context, `IMcpObservabilityService`, `JsonlLogWriter`, `ToolCallLoggingHandler`
  und `builder.WithTools<FeedbackTools>()`. Kein `IPostConfigureOptions`.
- **Richtlinie §6** (`McpObservabilityRichtlinien.mdc:94-98`): erlaubt aktuell
  nur `McpObservabilityOptions` + `McpObservabilityExtensions.WithObservability`
  als public; `IMcpObservabilityService` ist seit step-001 public, aber §6 noch
  nicht angepasst (für EPIC-03 geplant, Konzept §„Bewusste Richtlinien-Änderung").
