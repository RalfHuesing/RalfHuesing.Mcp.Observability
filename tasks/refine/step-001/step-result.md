---
status: done
type: step-result
task: refine
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: minimax-m3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-17T22:59:12+02:00
code_commit_hash: e318710e22a2f42334b560ed672619eedc8dd0c9
status_after: done
blocker_category: n/a
---

# Result Step 001: Options-Erweiterung, Override-Kette und IMcpObservabilityService

<**Wer das liest:** der Kritiker (prüft dich gegen den Plan) und der
Planer beim nächsten Step. Entscheidungsrelevant sind vor allem
„Abweichungen vom Plan", „Beobachtungen" und „Bekannte Unschärfen" —
dort lieber konkret als knapp. Alles andere: knapp halten, nichts aus
dem Step-Plan wiederholen, was unverändert umgesetzt wurde.>

## Zusammenfassung

Sechs Dateien gemäß Plan umgesetzt: vier neue `McpObservabilityOptions`-
Properties + ein `public const`, neues public-Interface
`IMcpObservabilityService`, Override-Kette + Eager-`LogFilePath` +
Implizite Interface-Implementierung im `ObservabilityContext`,
Pfad-Berechnung in den Context verlagert (Single Source of Truth für
`JsonlLogWriter.FilePath` und `IMcpObservabilityService.CurrentLogFilePath`),
Factory-Forwarding-Doppelregistrierung in `WithObservability`, und vier
neue Integration-Tests in `McpOptionsServerNameOverrideTests`. Build
und 28/28 Tests grün, AiNetLinter-Check clean.

## Geänderte Dateien

- `src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs` — vier
  neue `public`-Properties (`ServerName`, `ServerVersion`,
  `FeedbackConfirmationMessage`, `AdditionalSensitiveKeys`) plus
  `public const string DefaultFeedbackConfirmationMessage`; alle
  XML-Doc dokumentiert die Override-Priorität bzw. Inert-Status.
- `src/RalfHuesing.Mcp.Observability/IMcpObservabilityService.cs` (neu)
  — public-Interface mit sechs Read-only-Properties
  (`IsEnabled`, `ServerName`, `ServerVersion`, `CurrentLogFilePath`,
  `ProcessId`, `InstanceId`); XML-Doc dokumentiert die
  Auflösungsreihenfolge je Property.
- `src/RalfHuesing.Mcp.Observability/Internal/ObservabilityContext.cs` —
  `internal sealed class ... : IMcpObservabilityService`,
  Sichtbarkeit der fünf existierenden Properties auf `public` (implizite
  Interface-Implementierung), `Options`/`LogFilePath` bleiben
  `internal`; neue Property `CurrentLogFilePath` (computed); Konstruktor
  ruft private statische Helper `ResolveServerName`,
  `ResolveServerVersion`, `ResolveLogFilePath` auf.
- `src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs` —
  Konstruktor reduziert auf `Directory.CreateDirectory(dir)` +
  `FileStream` (Pfad-Berechnung kommt jetzt aus
  `context.LogFilePath`).
- `src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs` —
  zusätzliche Zeile `AddSingleton<IMcpObservabilityService>(sp =>
  sp.GetRequiredService<ObservabilityContext>())` direkt nach der
  bestehenden `ObservabilityContext`-Registrierung.
- `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsServerNameOverrideTests.cs` (neu)
  — vier Integration-Tests gegen die Override-Kette, Pattern 1:1 aus
  `McpObservabilityIntegrationTests` (Echo-Tool, Duplex-Pipes, leerer
  Host-Builder, `IntegrationTestBase` für isoliertes `TempDirectory`).

## Commit

- **Code-Commit-Hash:** `e318710e22a2f42334b560ed672619eedc8dd0c9`
- **Message:**
  ```
  feat(observability): servername-override und diagnostic-service [refine]

  - McpObservabilityOptions: ServerName, ServerVersion,
    FeedbackConfirmationMessage, AdditionalSensitiveKeys +
    DefaultFeedbackConfirmationMessage const
  - IMcpObservabilityService (neu, public): read-only
    Diagnostik-Properties (ServerName, ServerVersion,
    CurrentLogFilePath, ProcessId, InstanceId, IsEnabled)
  - ObservabilityContext: Override-Kette
    (Options -> ServerInfo -> EntryAssembly -> UnknownServer),
    Eager-LogFilePath, implementiert IMcpObservabilityService
  - JsonlLogWriter: Pfad-Berechnung in ObservabilityContext verlagert
    (Single Source of Truth fuer CurrentLogFilePath + FilePath)
  - WithObservability: DI-Doppelregistrierung via Factory-Forwarding
    (sp -> sp.GetRequiredService<ObservabilityContext>())
  - McpOptionsServerNameOverrideTests (neu): 4 Integration-Cases fuer
    die Override-Kette

  Refs: tasks/refine/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build --configuration Release → grün (0 Warnungen, 0 Fehler)
dotnet test  --configuration Release --verbosity normal → grün (28 Tests, 0 Fehler, 0 übersprungen, 13,1 s)
```

AiNetLinter `RunLinterShouldBeClean` lief im selben Testlauf mit und
war clean (`tests/.../AiNetLinter/output/linter-report.md` zeigt
`Validation Exit Code: 0`).

## Abweichungen vom Plan

- `ResolveServerVersion` wurde im Helper-Signatur symmetrisch zu
  `ResolveServerName` gestaltet (nimmt `options, info, hasInfoName`).
  Im Plan stand nur „identische Reihenfolge" für die innere Logik;
  die Signatur ist eine Folge der Helper-Disziplin. Verhalten
  identisch zum Plan.
- `ResolveLogFilePath` verwendet jetzt `using System.Globalization;`
  statt `System.Globalization.CultureInfo.InvariantCulture`-Vollpfad —
  reine Aufräumung, Verhalten identisch.
- `JsonlLogWriter` braucht jetzt `using System.Text;` explizit für
  `Encoding.UTF8` (vorher stand der Namespace als
  `System.Text.Encoding.UTF8`-Vollpfad). Reines Cleanup.

## Beobachtungen

- **Doppelregistrierung-Reihenfolge nicht deterministisch garantiert:
  `McpObservabilityOptions.Enabled` kann sich nach Registrierung nicht
  mehr ändern, ohne dass der `ObservabilityContext` neu gebaut wird.**
  Heute ist das in Ordnung (`Options.Enabled` ist nur die
  Master-Switch-Spiegelung in `IMcpObservabilityService.IsEnabled`).
  Falls ein Konsument die Optionen post-Configure modifiziert (z. B.
  via `IOptionsMonitor.OnChange`), würde die Interface-Property
  veraltete Werte liefern. Aktuell kein Konsument macht das — Hinweis
  für später, nicht step-001-relevant.
- **`ObservabilityContext` ist `internal`, exponiert aber fünf `public`
  Properties, die außerhalb des Assemblies nur über
  `IMcpObservabilityService` sichtbar sind (implizite
  Interface-Implementierung). Die `public`-Modifier sind trotzdem
  nicht falsch — der Compiler würde bei einer späteren
  `ObservabilityContext`-Beförderung auf `public` Konsistenz
  erleichtern. Steht im Plan (Designentscheidung 2). Kein Handlungs-
  bedarf.**
- **AiNetLinter-Side-Effect während des Test-Runs:** der
  `AiNetLinterTests.RunLinterShouldBeClean` ruft am Ende
  `--sync-agent-rules` auf, was die generierte
  `.agents/rules/AiNetLinter.mdc` von `AiNetLinter 1.0.95` auf
  `1.0.96` zieht. Dieser Diff ist **nicht** Teil des Code-Commits
  (nicht in der gezielten `git add`-Liste); ich übergebe ihn an den
  Doku-Commit.
- **Bei Test 1 (`ServerName_OptionOverridesServerInfo_Name`) prüfe ich
  `serverVersion` mit `"1.2.3"`. Das ist nur korrekt, weil
  `options.ServerVersion = null` und `McpServerOptions.ServerInfo` die
  Version `"1.2.3"` hat — d. h. die Fallback-Logik greift für die
  Version. Der Test wäre sonst rot. Der Plan-Hinweis
  „serverVersion ist die SDK-Version" ist im Test-Code durch
  das `options`-Setup und die ServerInfo-Initialisierung
  sichergestellt — kein zusätzlicher Assert nötig.**

## Bekannte Unschärfen

- **Implizite Interface-Implementierung über `public`-Modifier** statt
  expliziter `string IMcpObservabilityService.X => X`-Boilerplate: der
  Plan hat sich bewusst dafür entschieden (Designentscheidung 2),
  aber strenggenommen ist „Member sichtbar als `public` an einer
  `internal` Klasse" eine C#-Eigenheit, die manche Leser auf den
  ersten Blick falsch verstehen. Kritiker sollte ggf. prüfen, ob die
  XML-Doc am Klassen-XmlDoc-Hinweis auf das Interface den Sachverhalt
  klar genug macht.
- **`CurrentLogFilePath` ist `string?`** — `null`, wenn weder
  `EnableToolCallLogging` noch `EnableFeedbackTool` aktiv ist. Das
  deckt sich mit dem Plan und der Logik aus
  `McpObservabilityExtensions.WithObservability` (dort wird der
  `JsonlLogWriter` unter derselben Bedingung registriert). Allerdings
  wird `JsonlLogWriter` bei `Enabled = false` gar nicht erst gebaut —
  d. h. `CurrentLogFilePath` ist in dem Fall strenggenommen
  undefiniert (es gibt keinen Writer). Da `WithObservability` bei
  `Enabled = false` aber ein No-Op ist und auch kein
  `ObservabilityContext` registriert wird, ist der Service gar nicht
  auflösbar. Trotzdem: der Computed-Getter ist auf der sicheren
  Seite.
- **Aktivierreihenfolge im DI-Container:** die neue Zeile
  `AddSingleton<IMcpObservabilityService>(sp =>
  sp.GetRequiredService<ObservabilityContext>())` muss **nach** der
  `AddSingleton<ObservabilityContext>()`-Zeile stehen, damit die
  Factory den konkreten Typ auflösen kann. Steht so im Diff — aber
  wer den Extensions-Code in EPIC-03 umsortiert, sollte das mitdenken.
  Ein entsprechender Kommentar am Code wurde **nicht** gesetzt (Plan
  hat keinen verlangt; steigert Noise).

## Falls Status `blocked`

Nicht zutreffend.
