---
status: done
type: step-review
task: refine
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: minimax-m3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-17T23:03:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 001: Options-Erweiterung, Override-Kette und IMcpObservabilityService

## Verdict

- [x] **approved** - alle vier Prüfebenen ok
- [ ] **issues** - Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** - Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle 6 im Plan genannten Datei-Änderungen umgesetzt (Options-Properties + `public const`, neue `IMcpObservabilityService`-Datei, Override-Kette im `ObservabilityContext` mit Eager-`LogFilePath`, `JsonlLogWriter` nutzt `context.LogFilePath`, Factory-Forwarding-Doppelregistrierung in `WithObservability`, 4 neue Integration-Tests in `McpOptionsServerNameOverrideTests`); `step-plan.md`-Status auf `done (pending audit)` gesetzt; `codemap.md` aktualisiert (Pointer auf `IMcpObservabilityService`, `McpOptionsServerNameOverrideTests`, aktualisierte Beschreibungen von Options/Extensions/Context/Writer); `task-state.md` mit `done (pending audit)` für step-001 nachgezogen.

### Rules-Konformität

§5 (Schema-Invariante) unangetastet - keine Felder in `LogRecords` verändert, JSONL bleibt byte-identisch. §6-Lockerung minimal und wie geplant: nur `IMcpObservabilityService` neu public, `McpObservabilityOptions` um vier weitere public-Properties + ein `public const` erweitert; alle internen Klassen (`ObservabilityContext`, `JsonlLogWriter`, `ArgumentSanitizer`, `ToolCallLoggingHandler`, `FeedbackTools`) bleiben `internal` (zweites `internal`-Klassen-Beispiel jetzt mit `public`-Properties via impliziter Interface-Implementierung - explizit so im Plan Designentscheidung 2 gewählt und im XML-Doc am Klassenkommentar dokumentiert). §7 (Tests): `IntegrationTestBase` korrekt wiederverwendet, `TempDirectory` per `Guid`, kein Schreiben in echte `%LOCALAPPDATA%`-Pfade. §8: alle 6 neuen public-Member der `IMcpObservabilityService` haben XML-Doc, alle 4 neuen `McpObservabilityOptions`-Members + das `public const` haben XML-Doc, `ObservabilityContext.IsEnabled`/`CurrentLogFilePath` nutzen `<inheritdoc />` (kanonisch für Interface-Member). §10: Conventional-Commit-Message mit deutschem Imperativ, Suffix `[refine]`, Subject ≤72 Zeichen (`servername-override und diagnostic-service [refine]` = 56 Zeichen). AiNetLinter `RunLinterShouldBeClean` grün - alle im Plan referenzierten Regeln (sealed, ≤60 Zeilen, ≤4 Constructor-Deps, keine leeren Catches im neuen Code) gehalten.

### Logische Korrektheit

Override-Kette semantisch korrekt: `options.ServerName` schlägt `McpServerOptions.ServerInfo.Name` schlägt EntryAssembly schlägt `"UnknownServer"` (analog für `ServerVersion` mit `string.Empty` als unterster Stufe). Identität `JsonlLogWriter.FilePath` ↔ `IMcpObservabilityService.CurrentLogFilePath` (via `ObservabilityContext.LogFilePath` als Single Source of Truth) per Code-Inspektion verifiziert - der Writer übernimmt den Pfad-String unverändert aus dem Context, der `CurrentLogFilePath`-Getter gibt bei aktiviertem Logging denselben String zurück. Test `BothOptionsSet_BothAppearInRecord` setzt beide Optionen (`"X"`/`"Y"`) gegen SDK-Info (`"SdkName"`/`"1.2.3"`) und assertet beide Werte - testet also nachweislich, dass beide Optionen unabhängig voneinander greifen. Test 1 (`ServerName_OptionOverridesServerInfo_Name`) prüft zusätzlich die Version-Fallback-Kette (Options-Version null → SDK-Version `"1.2.3"`); Test 2 spiegelbildlich für Version. Test 4 (`BothOptionsNull_FallsBackToServerInfo`) sichert den unveränderten Default-Pfad ab (Regression-Schutz für `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`, das auch mit SDK-Info und ohne Options-Override arbeitet). `CurrentLogFilePath` ist korrekt `string?` und gibt `null` zurück, wenn weder Tool-Call- noch Feedback-Logging aktiv ist. DI-Reihenfolge in `WithObservability` (`AddSingleton<ObservabilityContext>()` **vor** `AddSingleton<IMcpObservabilityService>(sp => sp.GetRequiredService<ObservabilityContext>())`) ist eingehalten; Factory-Forwarding vermeidet zweite Instanz. `AdditionalSensitiveKeys` ist wie geplant inert (kein Konsument, Property + Default stehen) - `grep` über das Repo bestätigt: einzige Treffer in `McpObservabilityOptions.cs:75` (Definition) und in Doku-Dateien (Konzept, Roadmap, CodeMap, Step-Plan/Result).

### Konzept-Treue (Ebene 4)

Konzept §„Optionen-Erweiterung" vollständig abgedeckt: `ServerName` (string?), `ServerVersion` (string?), `FeedbackConfirmationMessage` (default = `ObservabilityConstants.DefaultFeedbackResponse` = `"Feedback recorded. Thank you."` - in der `McpObservabilityOptions.DefaultFeedbackConfirmationMessage` const wiedergespiegelt, vgl. Designentscheidung 3 im Plan), `AdditionalSensitiveKeys` (`HashSet<string>`, `OrdinalIgnoreCase`). Konzept §„Diagnostik-Service" vollständig abgedeckt: `IMcpObservabilityService` mit den sechs verlangten Properties, `ObservabilityContext` implementiert das Interface, Registrierung im DI-Container als Singleton über die exakte Factory-Forwarding-Form `services.AddSingleton<IMcpObservabilityService>(sp => sp.GetRequiredService<ObservabilityContext>())`. Auflösungsreihenfolge wie im Konzept vorgegeben. Keine Non-Goals verletzt (kein `LogCustomRecord`, kein OTel, keine DB, keine Log-Rotation, keine Major-Bump-Absicht, kein Auto-Discovery). Konzept §„Tests" Punkt 3 abgehakt: `McpOptionsServerNameOverrideTests` als Integration-Test existiert, prüft `McpObservabilityOptions.ServerName = "X"` schlägt sich als `serverName: "X"` nieder. Konzept §„Richtlinien-Update §6" wurde korrekterweise **nicht** in step-001 geändert (im Plan als „in EPIC-03 dokumentieren" festgelegt) - die Datei selbst ist als `public interface` mit vollständigem XML-Doc sauber angelegt, damit EPIC-03 nur die Richtlinie nachziehen muss. Kein Scope-Creep: keine Sample-Datei, keine Schema-Änderung, keine ToolCollection-Erweiterung, keine `IAsyncDisposable`-Erweiterung des Writers - alles korrekt auf EPIC-04/05 verschoben.

### Build-/Test-Status

```
dotnet build --configuration Release                        → grün (0 Warnungen, 0 Fehler, 2,98 s)
dotnet test  --configuration Release --verbosity normal     → grün (28 Tests, 0 Fehler, 13,1 s; AiNetLinter RunLinterShouldBeClean inklusive)
```
