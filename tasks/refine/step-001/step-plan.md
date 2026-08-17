---
status: done (pending audit)
type: step-plan
task: refine
step: 001
corrects: null
title: "Options-Erweiterung, Override-Kette und IMcpObservabilityService"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T22:45:24+02:00
related_to: []
---

# Step 001: Options-Erweiterung, Override-Kette und IMcpObservabilityService

## Bezug

- **Task:** `refine`
- **Epic:** `EPIC-01` aus `roadmap.md` — Options-Erweiterung (`ServerName`,
  `ServerVersion`, `FeedbackConfirmationMessage`, `AdditionalSensitiveKeys`)
  + Override-Kette im `ObservabilityContext` + neues public-Interface
  `IMcpObservabilityService` + DI-Doppelregistrierung + ein Integration-Test
  für den ServerName-Override.
- **Konzept-Referenz:** `Konzept.md` §„Muss-Haben" → Bullet
  „Optionen-Erweiterung" + „Diagnostik-Service". Schema-Invariante §5
  wird in diesem Step **nicht** berührt (Argument-Sanitizer ändert sich
  erst in EPIC-02).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Quellcodes vorgefunden — beeinflusst den Plan:

- **`McpObservabilityOptions`** (`src/.../McpObservabilityOptions.cs:8-35`):
  vier vorhandene `public`-Properties (`Enabled`, `EnableToolCallLogging`,
  `EnableFeedbackTool`, `LogDirectory`), alle mit `{ get; set; }`,
  Datei-scoped Namespace `RalfHuesing.Mcp.Observability`. Kein
  `public const` heute — der `LogDirectory`-Default ist nur in der
  XML-Doc als Literal beschrieben, nicht als discoverable Konstante.
- **`ObservabilityContext`** (`src/.../Internal/ObservabilityContext.cs:11-37`):
  `internal sealed class`, Properties (`ServerName`, `ServerVersion`,
  `ProcessId`, `InstanceId`, `Options`) heute explizit `internal get`.
  Aktuelle Auflösung in zwei Stufen:
  1. `McpServerOptions.ServerInfo` (falls `Name` nicht leer) → `info.Name`/`info.Version`
  2. sonst `EntryAssembly`-Fallback (sonst `UnknownServerName`).
  Konstruktor nimmt `(McpObservabilityOptions options, IOptions<McpServerOptions>? serverOptions = null)`.
- **`ObservabilityConstants`** (`src/.../Internal/ObservabilityConstants.cs`):
  `internal static class` mit `internal const string DefaultFeedbackResponse = "Feedback recorded. Thank you."`
  und `internal const string UnknownServerName = "UnknownServer"`. Beide
  bleiben in diesem Step `internal` — keine Aufweichung der
  `internal`-Trennung für Magic-Werte.
- **`JsonlLogWriter`** (`src/.../Internal/JsonlLogWriter.cs:15-33`):
  berechnet seinen `FilePath` aktuell **selbst** im Konstruktor aus
  `context.ServerName`/`ProcessId`/`InstanceId`/`Options.LogDirectory` —
  der `ObservabilityContext` kennt seinen Log-Pfad heute nicht. Die
  geplante `IMcpObservabilityService.CurrentLogFilePath` muss aber
  diesen Pfad liefern. → **Designentscheidung unten in „Notes"**:
  Pfad-Berechnung wandert in den `ObservabilityContext` (Single Source
  of Truth), der `JsonlLogWriter` nutzt ihn.
- **`McpObservabilityExtensions.WithObservability`**
  (`src/.../McpObservabilityExtensions.cs:24-54`): registriert
  `McpObservabilityOptions` und `ObservabilityContext` als Singleton, dann
  bedingt `JsonlLogWriter`. Die zusätzliche
  `IMcpObservabilityService`-Doppelregistrierung passt direkt unter
  `services.AddSingleton<ObservabilityContext>()`.
- **Bestehende Tests, die den aktuellen Pfad absichern:**
  - `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`
    baut `McpServerOptions.ServerInfo = { Name = "TestServer", Version = "1.2.3" }`
    und prüft genau diese Werte im JSONL-Record. **Bleibt grün**, weil
    die Override-Kette bei `options.ServerName == null` weiterhin auf
    `McpServerOptions.ServerInfo` zurückfällt.
  - `McpOptionsFlagsTests` (3 Cases): prüfen `Enabled = false` /
    `EnableToolCallLogging = false` / `EnableFeedbackTool = false`. **Bleibt
    grün** — die neuen Properties ändern diese Pfade nicht.
- **CodeMap-Status:** Karte ist aktuell (alle relevanten Dateien
  verzeichnet, EPIC-01-Annotationen sind drin). Keine Lücken oder
  Widersprüche zum aktuellen Code.

## Intention

Dieser Step rüstet `RalfHuesing.Mcp.Observability` mit den vier
Audit-Properties aus und macht den `ObservabilityContext` über ein neues
public-Interface `IMcpObservabilityService` für Diagnostik-Endpunkte
konsumierbar. Die Override-Kette priorisiert explizit die
`McpObservabilityOptions`-Properties über `McpServerOptions.ServerInfo`,
damit Konsumenten den Server-Namen ohne SDK-Umwege überschreiben können.
Die Log-Datei-Pfad-Berechnung wird in den `ObservabilityContext` verlagert,
damit das neue Interface `CurrentLogFilePath` anbieten kann, ohne den
`JsonlLogWriter` mutable zu machen.

## Konkrete Änderungen

### Datei 1: `src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs` (Zeile 8-35)

- **Was:** Vier neue `public`-Properties anhängen, plus ein
  `public const` für den `FeedbackConfirmationMessage`-Default.
  - `public const string DefaultFeedbackConfirmationMessage = "Feedback recorded. Thank you.";`
  - `public string? ServerName { get; set; }` (XML-Doc: erklärt
    Override-Priorität gegenüber `McpServerOptions.ServerInfo.Name`).
  - `public string? ServerVersion { get; set; }` (analog, mit
    Hinweis auf `EntryAssembly`-Fallback und leeren String als
    unterste Stufe).
  - `public string FeedbackConfirmationMessage { get; set; } = DefaultFeedbackConfirmationMessage;`
    (XML-Doc: verweist auf das `report_observability_feedback`-Tool,
    das diesen Text zurückgibt; Konzept erwähnt, dass EPIC-03 den
    Konsumenten des Werts baut).
  - `public HashSet<string> AdditionalSensitiveKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);`
    (XML-Doc: „wird ab EPIC-02 vom `ArgumentSanitizer` zusätzlich zur
    hartkodierten Default-Liste berücksichtigt; in v1.0.1 selbst
    inert — Property + Default reichen für den Test der
    Override-Kette").
- **Warum:** Konzept §„Optionen-Erweiterung" verlangt genau diese
  vier Properties mit den dokumentierten Defaults. `const` auf der
  Options-Klasse hält `ObservabilityConstants` weiterhin `internal`
  (Richtlinie §6) und gibt Konsumenten trotzdem einen discoverable
  Default-Wert. `OrdinalIgnoreCase` am HashSet ist die
  Schema-Vorgabe aus §5 („case-insensitive").

### Datei 2: `src/RalfHuesing.Mcp.Observability/IMcpObservabilityService.cs` (neu)

- **Was:** Komplett neue Datei. Inhalt:
  - File-scoped `namespace RalfHuesing.Mcp.Observability;`
  - `#nullable enable`
  - `public interface IMcpObservabilityService` mit sechs
    `get`-only-Properties, jeweils mit XML-Doc:
    - `bool IsEnabled { get; }` — „Spiegelt `McpObservabilityOptions.Enabled`."
    - `string ServerName { get; }` — XML-Doc dokumentiert die
      Auflösungsreihenfolge (Options → `McpServerOptions.ServerInfo.Name` →
      EntryAssembly → `UnknownServer`).
    - `string ServerVersion { get; }` — analog, mit leerer Zeichenkette
      als unterster Stufe.
    - `string? CurrentLogFilePath { get; }` — „Absoluter Pfad zur
      aktuell geöffneten JSONL-Datei, oder `null`, wenn weder
      Tool-Call- noch Feedback-Logging aktiviert ist."
    - `int ProcessId { get; }` — XML-Doc: stabil über die Prozesslebensdauer.
    - `string InstanceId { get; }` — XML-Doc: hex-formatierte GUID, identisch
      mit dem `instanceId`-Feld in jedem JSONL-Record.
  - Keine Methoden, keine Defaults, keine statischen Member.
- **Warum:** Konzept §„Diagnostik-Service" verlangt genau diese
  Oberfläche. Bewusste Lockerung von Richtlinie §6 — die
  Lockerung **selbst** wird in EPIC-03 dokumentiert (siehe Konzept
  §„Richtlinien-Update §6"). Für step-001 nur: Datei sauber als
  `public interface` anlegen.

### Datei 3: `src/RalfHuesing.Mcp.Observability/Internal/ObservabilityContext.cs` (Zeile 11-37, ~komplette Neufassung)

- **Was:**
  - Klassen-Deklaration: `internal sealed class ObservabilityContext : IMcpObservabilityService`.
  - Sichtbarkeit der fünf existierenden Properties (`ServerName`,
    `ServerVersion`, `ProcessId`, `InstanceId`, `Options`) auf `public`
    ändern. **Begründung:** die Klasse ist `internal`, also bleibt
    der public Surface nach außen ausschließlich das Interface; eine
    implizite Interface-Implementierung ist so am lesbarsten und
    spart explizite `IMcpObservabilityService.X => X`-Boilerplate.
  - `Options` bleibt `internal` (nicht Teil des Interface).
  - Neue Property: `internal string LogFilePath { get; }` — eagerly
    im Konstruktor berechnet (siehe Designentscheidung in „Notes").
  - Neue Property: `public string? CurrentLogFilePath { get; }` —
    Computed: `return (Options.EnableToolCallLogging || Options.EnableFeedbackTool) ? LogFilePath : null;`.
  - Konstruktor-Logik für `ServerName` und `ServerVersion` umbauen
    auf die Override-Kette:
    1. `if (!string.IsNullOrWhiteSpace(options.ServerName)) ServerName = options.ServerName;`
    2. `else if (serverInfo-Name nicht leer) ServerName = info.Name;`
    3. `else ServerName = EntryAssembly?.GetName().Name ?? ObservabilityConstants.UnknownServerName;`
       (identische Reihenfolge für `ServerVersion`, unterste Stufe
       dort `string.Empty` statt `UnknownServerName`).
  - Anschließend `LogFilePath = ResolveLogFilePath(options, ServerName, ProcessId, InstanceId);`.
  - Privater statischer Helper `ResolveLogFilePath(...)` enthält
    die bisherige `JsonlLogWriter`-Pfad-Logik verbatim (Root =
    `LogDirectory` oder `%LOCALAPPDATA%\RalfHuesing\McpObservability\`,
    `DateFormat`-Unterordner, Dateiname
    `{ServerName}_{ProcessId}_{InstanceId}.jsonl`).
- **Warum:** Konzept §„Diagnostik-Service" + §„Override-Kette".
  Die Eager-Berechnung von `LogFilePath` ist die Voraussetzung dafür,
  dass `IMcpObservabilityService.CurrentLogFilePath` ohne mutable
  `set` im Context auskommt.

### Datei 4: `src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs` (Zeile 15-33, Konstruktor)

- **Was:** Konstruktor umbauen — keine eigene Pfad-Berechnung mehr.
  Statt dessen:
  ```
  var dir = Path.GetDirectoryName(context.LogFilePath)!;
  Directory.CreateDirectory(dir);
  FilePath = context.LogFilePath;
  var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
  _writer = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };
  ```
  `internal string FilePath { get; }` bleibt unverändert (wird von den
  bestehenden Integration-Tests benutzt).
- **Warum:** Single Source of Truth. `ObservabilityContext` berechnet
  den Pfad einmalig, der `JsonlLogWriter` nutzt ihn. Damit hat
  `IMcpObservabilityService.CurrentLogFilePath` denselben String wie
  `JsonlLogWriter.FilePath` — keine Drift möglich. Die Logik
  (Verzeichnis anlegen, Append-Mode, `FileShare.ReadWrite`) bleibt
  1:1 erhalten, nur ihr Ursprung wechselt.

### Datei 5: `src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs` (Zeile 35-36, in `WithObservability`)

- **Was:** Direkt nach `builder.Services.AddSingleton<ObservabilityContext>();`
  eine Zeile ergänzen:
  ```
  builder.Services.AddSingleton<IMcpObservabilityService>(sp => sp.GetRequiredService<ObservabilityContext>());
  ```
- **Warum:** Konzept §„Diagnostik-Service" — exakt diese
  Factory-Registrierung. Singleton-Forwarding statt
  `AddSingleton<ObservabilityContext, IMcpObservabilityService>`
  vermeidet zweite Instanz.

### Datei 6: `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsServerNameOverrideTests.cs` (neu)

- **Was:** Neue Datei, Pattern aus `McpObservabilityIntegrationTests.cs`
  übernehmen:
  - `[Fact] ServerName_OptionOverridesServerInfo_Name`:
    `McpServerOptions.ServerInfo.Name = "SdkName"`,
    `options.ServerName = "CustomName"`,
    `options.ServerVersion = null`. Erwartung: JSONL-Record hat
    `serverName: "CustomName"`, `serverVersion` ist die SDK-Version
    (weil `options.ServerVersion` null → Fallback `serverInfo.Version`).
  - `[Fact] ServerVersion_OptionOverridesServerInfo_Version`:
    spiegelbildlich — `options.ServerVersion = "9.9.9"`,
    `options.ServerName = null`, SDK-Name im Record.
  - `[Fact] BothOptionsSet_BothAppearInRecord`:
    `options.ServerName = "X"`, `options.ServerVersion = "Y"`,
    SDK-Info mit anderen Werten. Erwartung: Record trägt `X`/`Y`.
  - `[Fact] BothOptionsNull_FallsBackToServerInfo`:
    `options.ServerName = null`, `options.ServerVersion = null`,
    SDK-Info gesetzt. Erwartung: Record trägt die SDK-Werte
    (entspricht der bestehenden Erwartung in
    `McpObservabilityIntegrationTests`).
  - Test-Klasse erbt von `IntegrationTestBase`, nutzt
    `CreateDuplexPipes()`, `Host.CreateEmptyApplicationBuilder(null)`,
    `EchoTool`-Pattern (lokales `[McpServerToolType]` mit einem
    `echo`/`noop`-Tool) — siehe `McpObservabilityIntegrationTests.cs:13-19`.
  - `LogDirectory = TempDirectory` bei jedem Test, damit
    `CurrentLogFilePath` und `JsonlLogWriter.FilePath` ins Test-Temp
    zeigen.
- **Warum:** Konzept §„Tests" Punkt 3 + Definition-of-Done §„Funktional"
  „`McpObservabilityOptions.ServerName = "X"` schlägt sich in jedem
  geschriebenen JSONL-Record als `serverName: "X"` nieder — verifiziert
  per Integration-Test."

## Tests

- [ ] `McpOptionsServerNameOverrideTests.ServerName_OptionOverridesServerInfo_Name`
- [ ] `McpOptionsServerNameOverrideTests.ServerVersion_OptionOverridesServerInfo_Version`
- [ ] `McpOptionsServerNameOverrideTests.BothOptionsSet_BothAppearInRecord`
- [ ] `McpOptionsServerNameOverrideTests.BothOptionsNull_FallsBackToServerInfo`
- [ ] **Regression:** `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl` weiterhin grün
- [ ] **Regression:** `McpOptionsFlagsTests` (3 Cases) weiterhin grün
- [ ] **Regression:** `McpFeedbackIntegrationTests` weiterhin grün
- [ ] **Build:** `dotnet build --configuration Release` (Tech-Stack-Notiz `roadmap.md`) ohne Warnings unter `TreatWarningsAsErrors`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (6 Dateien)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün — bestehende + 4 neue Tests
- [ ] AiNetLinter `safeguard` (Richtlinie §4.4) — Mindest-Score 8.0, neue `public`-Member mit XML-Doc (Richtlinie §8)
- [ ] Commit auf aktuellem Branch (Conventional Commit, deutsch, imperativ, Subject ≤72, Suffix `[refine]`). Beispiel: `feat(observability): options um servername-override und diagnostic-service erweitern [refine]`
- [ ] `step-001/step-result.md` geschrieben (Coder-Pflicht, nicht Inhalt dieses Plans)
- [ ] `status` in `step-plan.md` (dieser Datei) von `open` auf `done (pending audit)` gesetzt
- [ ] `task-state.md`-Steps-Tabelle um `step-001` ergänzt
- [ ] `roadmap.md`-Notiz „(in Arbeit → step-001)" an EPIC-01 bestehen lassen — wird in `step-002` (oder beim Abschluss von EPIC-01) auf „done" umgestellt

## Rules-Refs

- `.agents/rules/McpObservabilityRichtlinien.mdc#6 Öffentliche API-Stabilität` —
  `IMcpObservabilityService` ist die **erste** bewusste Lockerung dieser
  Regel. In step-001 **nicht** die Richtlinie selbst ändern (EPIC-03).
  Aber: die neue Datei muss als `public interface` mit XML-Doc
  angelegt sein, damit EPIC-03 nur die Richtlinie nachzuziehen braucht.
- `.agents/rules/McpObservabilityRichtlinien.mdc#5 Datenformat-Invarianten` —
  `recordType`-Enumeration bleibt unberührt; `serverName`/`serverVersion`
  sind keine Schema- sondern Options-Properties. JSONL-Schema ändert
  sich in step-001 **nicht**.
- `.agents/rules/McpObservabilityRichtlinien.mdc#7 Tests` — Integration-Test
  nutzt `IntegrationTestBase` (Temp-Dir pro Test, `CreateDuplexPipes`,
  `ReadAllLinesSharedAsync`). Kein Schreiben in echte
  `%LOCALAPPDATA%`-Pfade.
- `.agents/rules/McpObservabilityRichtlinien.mdc#8 Dokumentation & Qualität` —
  Zero-Warning-Direktive (alle neuen public-Member mit XML-Doc).
  README-Sync ist in EPIC-05 (Doku), nicht in step-001 — die
  dokumentationspflichtigen API-Änderungen werden in EPIC-05
  nachgezogen.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil + agent-resilience + general` —
  `sealed` auf Klassen (Interface ausgenommen), `≤60` Zeilen
  Produktionsmethode, `≤4` Konstruktor-Deps (Klasse hat heute
  2 — bleibt unter dem Limit, auch nach Interface-Vererbung), kein
  `dynamic`, kein leeres `catch`, kein `async void`.
- `.agents/rules/AiNetLinter.mdc#test-coverage` — `EnableTestSentinel`
  gilt für die neue `McpOptionsServerNameOverrideTests`-Klasse; die
  Tool-Definitions-Klasse im Test braucht keinen eigenen Sentinel
  (Pattern wie in `McpObservabilityIntegrationTests.cs:14`).
- `.agents/rules/McpObservabilityRichtlinien.mdc#10 Commit-Pflicht` —
  Conventional Commits deutsch imperativ, Suffix `[refine]`,
  Subject ≤72 Zeichen.

## Bekannte Ausnahmen

- `McpObservabilityOptions.AdditionalSensitiveKeys` ist in step-001
  bewusst **inert**: Die Property + der `OrdinalIgnoreCase`-Default
  stehen, aber kein Konsument wertet sie aus. Der Sanitizer wird
  in EPIC-02 generalisiert und liest die Property dort. Begründung:
  Konzept §„ArgumentSanitizer generalisieren" weist `additionalKeys`
  erst der neuen `Sanitize(object?, IEnumerable<string>?)`-Signatur
  zu — das ist EPIC-02-Scope. step-001 liefert nur die Properties +
  Test, dass sie existieren und gesetzt werden können (implizit über
  den Options-Builder).
- **Schritt 1 der Konzept-„Wie"-Liste** nennt die Properties zusammen
  mit der Override-Kette. step-001 bedient **beides** in einem Schritt
  (statt zwei), weil die Properties ohne den Override-Mechanismus
  nicht sinnvoll testbar wären und vice versa. Begründung: die
  Schritte-Größen-Heuristik „in einem Commit committbar, in einer
  Review-Runde prüfbar" ist mit dem 6-Dateien-Cluster (~80-120
  Diff-Zeilen) erfüllt, ohne dass eine künstliche Trennung echten
  Mehrwert brächte.

## Code-Skizze (optional)

```csharp
// McpObservabilityOptions.cs — neue Properties
public const string DefaultFeedbackConfirmationMessage = "Feedback recorded. Thank you.";

public string? ServerName { get; set; }
public string? ServerVersion { get; set; }
public string FeedbackConfirmationMessage { get; set; } = DefaultFeedbackConfirmationMessage;
public HashSet<string> AdditionalSensitiveKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

// ObservabilityContext.cs — Override-Kette + Eager-Path
public string? CurrentLogFilePath =>
    (Options.EnableToolCallLogging || Options.EnableFeedbackTool) ? LogFilePath : null;

internal string LogFilePath { get; }

public ObservabilityContext(McpObservabilityOptions options, IOptions<McpServerOptions>? serverOptions = null)
{
    Options = options;
    ProcessId = Environment.ProcessId;
    InstanceId = Guid.NewGuid().ToString("N");

    ServerName    = ResolveServerName(options, serverOptions?.Value);
    ServerVersion = ResolveServerVersion(options, serverOptions?.Value);
    LogFilePath   = ResolveLogFilePath(options, ServerName, ProcessId, InstanceId);
}

// McpObservabilityExtensions.cs — Interface-Doppelregistrierung
builder.Services.AddSingleton<ObservabilityContext>();
builder.Services.AddSingleton<IMcpObservabilityService>(
    sp => sp.GetRequiredService<ObservabilityContext>());
```

## Notes

### Designentscheidung 1 — `CurrentLogFilePath`-Ownership

`JsonlLogWriter.FilePath` wurde bisher vom Writer selbst berechnet; der
`ObservabilityContext` kannte den Pfad nicht. Für
`IMcpObservabilityService.CurrentLogFilePath` brauchen wir ihn im
Context. Zwei saubere Optionen:

- **(A) Eager-Berechnung im Context (gewählt):** der Context berechnet
  den Pfad im Konstruktor, der Writer bekommt ihn via
  `context.LogFilePath` und nutzt ihn unverändert. Vorteile: Single
  Source of Truth, kein mutabler State, kein DI-Order-Trick
  („Writer zuerst registrieren, dann Context mit Pfad befuellen").
  Der Pfad wird ohnehin nur einmal pro Prozess benutzt.
- **(B) Spätere Propagation via `internal set`:** der Writer setzt
  `context.CurrentLogFilePath` nach dem Öffnen. Vorteil: weniger
  Logik-Wanderung. Nachteil: mutable State auf einer
  „process-scoped metadata"-Klasse, die bisher strikt immutable ist
  (`{ get; }` ohne Setter).

**Gewählt: A.** Die `ResolveLogFilePath`-Methode wandert verbatim aus
dem `JsonlLogWriter`-Konstruktor in den Context; der Writer
reduziert sich auf „Verzeichnis anlegen + Stream öffnen". Der
bisherige `JsonlLogWriter.FilePath` bleibt als Property erhalten,
damit die bestehenden Integration-Tests
(`McpObservabilityIntegrationTests`, `McpOptionsFlagsTests`) ohne
Änderung weiterlaufen — der String ist identisch.

### Designentscheidung 2 — Sichtbarkeit der `ObservabilityContext`-Properties

Vorher: `internal string ServerName { get; }` etc. Mit der
public-Interface-Implementierung gibt es zwei saubere Wege:

- **Implizit (gewählt):** Modifier auf `public` ändern. Da die Klasse
  `internal` ist, bleibt der externe Surface ausschließlich das
  Interface. Lesbar, kein expliziter
  `string IMcpObservabilityService.ServerName => ServerName;`-Boilerplate.
- **Explizit:** `internal`-Modifier beibehalten, mit
  `string IMcpObservabilityService.ServerName => ServerName;` für
  jeden Member. Vorteil: striktere Kapselung, Nachteil: 6 zusätzliche
  Zeilen Boilerplate ohne echten Mehrwert (die Klasse ist intern).

**Gewählt: implizit.** Die `Options`-Property bleibt `internal` (nicht
im Interface), demonstriert die Kapselung da, wo sie zählt.

### Designentscheidung 3 — `FeedbackConfirmationMessage`-Default-Quelle

`ObservabilityConstants.DefaultFeedbackResponse` ist `internal const`
— ein direkter Default in `McpObservabilityOptions` kann darauf nicht
zugreifen, ohne `ObservabilityConstants` selbst `public` zu machen
(Verstoß gegen §6, oder zumindest Aufweichung der
„interne Magic-Werte"-Disziplin). Optionen:

- **(A) Public const auf `McpObservabilityOptions` (gewählt):**
  `public const string DefaultFeedbackConfirmationMessage = "Feedback recorded. Thank you.";`
  Konsumenten finden den Default über den Typ, den sie eh schon
  nutzen; `ObservabilityConstants` bleibt `internal`.
- **(B) String-Literal im Property-Initializer:** kompakter, aber
  kein discoverable Default für Konsumenten, die zurücksetzen wollen.
- **(C) `ObservabilityConstants` teilweise public machen:** würde die
  interne-vs-public-Trennung aufweichen, ohne klaren Gewinn.

**Gewählt: A.** `ObservabilityConstants.DefaultFeedbackResponse` bleibt
für internen Gebrauch (EPIC-03 liest den Wert im `FeedbackTools`-Pfad
aus den Options); der public-Default steht sauber auf der
Options-Klasse.

### Wiederverwendete Strukturen (kein Anti-Loop-Verstoß)

- `IntegrationTestBase` wird in `McpOptionsServerNameOverrideTests`
  1:1 wiederverwendet (gleicher Host-Aufbau, gleiche Duplex-Pipe,
  gleiche `ReadAllLinesSharedAsync`-Lese-Sequenz). Kein
  bestehender Test wird umgangen oder dupliziert.
- `EchoTool`-Pattern (`[McpServerToolType]` mit einer
  `[McpServerTool(Name = "echo")]`-Methode) wird aus
  `McpObservabilityIntegrationTests.cs:13-19` übernommen — eine
  private Echo-Tool-Klasse pro Test-Datei, kein projektweiter
  Helper (würde die Tests koppeln).
- `JsonlLogWriter.FilePath` bleibt die Quelle für Test-Asserts; nur
  der Ursprung der Berechnung wechselt (vom Writer in den Context).
