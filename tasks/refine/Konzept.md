---
status: draft  # draft | ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-17T22:15:00Z
open_questions:
  - Keine — bereit für User-Bestätigung (siehe Schritt 6)
---

# Konzept: RalfHuesing.Mcp.Observability v1.1 — Robustheit, Kompatibilität, Diagnostik

## Ziel (Was)

`RalfHuesing.Mcp.Observability` v1.1.0 behebt die vier in der praktischen
Integration mit `AiNetLinter` identifizierten Hürden (Tool-Schatten-Effekt
bei manueller `ToolCollection`, fragiles Argument-Casting im Sanitizer,
fehlende `ServerName`/`ServerVersion`-Overrides, zu enge `internal`-
Sichtbarkeit) und ergänzt zwei neue öffentliche API-Typen
(`IMcpObservabilityService` für Diagnostik, `McpObservabilityTools` für
manuelle Tool-Registrierung), einen zweiten Sample-Server, vollständige
Test-Coverage für die neuen Pfade sowie eine CHANGELOG-Datei. Alle
Änderungen sind additiv — bestehende v1.0.0-Konsumenten bleiben
vollständig kompatibel, der Bump ist Minor (`1.0.0` → `1.1.0`).

## Warum / Kontext

**Hintergrund.** Das Paket ist seit v1.0.0 auf nuget.org veröffentlicht
und wird produktiv in `AiNetLinter` (Roslyn-basierter MCP-Server mit 22
Tools) eingesetzt. Bei dieser Integration traten vier Reibungspunkte auf,
die im Audit-Report vom 17.08.2026 dokumentiert sind und Workarounds im
konsumierenden Code nötig machten (Reflection auf `FeedbackTools`,
stilles `null` in `tool_call`-Records, `UnknownServer`-Fallback, ignoriertes
Feedback-Tool bei manueller `ToolCollection`).

**Zielgruppe.** Konsumenten von `RalfHuesing.Mcp.Observability`, die
entweder (a) MCP-Server mit manueller `ToolCollection` bauen, (b) ihre
Tools in Tests/CLI-Servern ohne `Host.CreateApplicationBuilder`
betreiben, oder (c) Diagnostik-/Health-Endpunkte auf Basis des
Observability-Zustands anbieten wollen.

**Constraints (aus Richtlinien, nicht verhandelbar).**
- `TreatWarningsAsErrors=true` (Zero-Warning-Direktive, §8).
- JSONL-Schema-Invariante: `schemaVersion`, `timestamp`, `recordType`,
  `instanceId` sind Pflichtfelder; `recordType` bleibt hart enumeriert
  auf `"tool_call" | "feedback"` (§5).
- Bestehende JSONL-Konsumenten dürfen nicht gebrochen werden — der
  JSON-Output muss byte-identisch zu v1.0.0 bleiben.
- xUnit v3, `net10.0`, File-I/O-Tests in temporären Verzeichnissen (§7).

**Bewusste Richtlinien-Änderung für v1.1.** §6 ("Nur `McpObservabilityOptions`
und `McpObservabilityExtensions.WithObservability` sind public") wird
**gelockert**: `IMcpObservabilityService` und `McpObservabilityTools`
werden ergänzend public. Die Lockerung wird in `McpObservabilityRichtlinien.mdc`
§6 mit Datum, Begründung und geprüften Alternativen dokumentiert.

## Scope

### Muss-Haben

**Optionen-Erweiterung** (`McpObservabilityOptions.cs`):
- Neue additive Properties: `ServerName` (string?), `ServerVersion`
  (string?), `FeedbackConfirmationMessage` (string, default aus
  `ObservabilityConstants.DefaultFeedbackResponse`),
  `AdditionalSensitiveKeys` (`HashSet<string>`, default leer,
  `OrdinalIgnoreCase`).
- Reihenfolge der Auflösung in `ObservabilityContext`:
  `options.ServerName` → `McpServerOptions.ServerInfo.Name` → EntryAssembly
  → `"UnknownServer"`. Analog für `ServerVersion`.

**ArgumentSanitizer generalisieren** (`Internal/ArgumentSanitizer.cs`):
- `Sanitize(object? rawArguments, IEnumerable<string>? additionalKeys = null)`
  akzeptiert `object?` und normalisiert intern aus:
  `IReadOnlyDictionary<string, JsonElement>`, `IReadOnlyDictionary<string, object?>`,
  `JsonObject`, beliebiges `IDictionary<string, object?>`. Rückgabe:
  `IReadOnlyDictionary<string, object?>?` (Werte als `JsonElement` für
  stabile JSON-Serialisierung).
- `SensitiveKeys` wird aus zwei Quellen gemerged: hartkodierte
  Default-Liste + `additionalKeys`.
- `ToolCallLoggingHandler.CreateRecord` reicht `request.Params?.Arguments`
  ohne Cast direkt an `Sanitize` weiter.

**JSONL-Schema-Stabilität** (`Internal/LogRecords.cs`):
- `ToolCallRecord.Arguments` wechselt intern von
  `IReadOnlyDictionary<string, JsonElement>?` auf
  `IReadOnlyDictionary<string, object?>?`. Der serialisierte JSON-Output
  bleibt byte-identisch (alle Werte landen als `JsonElement` im JSON).
- Test sichert die JSON-Output-Invariante explizit ab.

**Manueller ToolCollection-Support** (`McpObservabilityTools.cs`, neu,
public):
- `static McpServerTool CreateFeedbackTool(IServiceProvider services)`
  erzeugt das `report_observability_feedback`-Tool als `McpServerTool`
  via `McpServerTool.Create`. Implementiert das Tool semantisch identisch
  zu `FeedbackTools.ReportFeedback`, aber über ein `Delegate` (statt
  Reflection auf `internal`).
- `static void AddFeedbackTool(this McpServerPrimitiveCollection<McpServerTool> tools, IServiceProvider services)`
  hängt das Tool an eine bestehende Collection an, falls noch nicht
  vorhanden (idempotent per `ProtocolTool.Name`-Vergleich).

**Tool-Schatten-Fix** (`McpObservabilityExtensions.cs`):
- Registriert zusätzlich `IPostConfigureOptions<McpServerOptions>`, das
  nach Abschluss aller Konfigurationen prüft, ob
  `McpServerOptions.ToolCollection` gesetzt und `EnableFeedbackTool` true
  ist; falls ja, ruft `AddFeedbackTool` auf der Collection auf, sofern
  noch nicht vorhanden. Dadurch funktioniert `WithObservability()` sowohl
  mit `builder.WithTools()` als auch mit manueller `ToolCollection`.

**Diagnostik-Service** (`IMcpObservabilityService.cs`, neu, public):
- Properties: `IsEnabled`, `ServerName`, `ServerVersion`, `CurrentLogFilePath`,
  `ProcessId`, `InstanceId`.
- `ObservabilityContext` (internal) implementiert das Interface
  (`public sealed class ObservabilityContext : IMcpObservabilityService`).
- Registrierung im DI-Container als Singleton über
  `services.AddSingleton<IMcpObservabilityService>(sp => sp.GetRequiredService<ObservabilityContext>())`.

**Writer-Lifecycle** (`Internal/JsonlLogWriter.cs`):
- `JsonlLogWriter` implementiert zusätzlich `IAsyncDisposable`
  (`DisposeAsync` schließt den `StreamWriter` sauber mit `FlushAsync`).
- Neue Methode `Task FlushAsync(CancellationToken ct = default)`.
- Doku-Hinweis im README: Live-Reader müssen `FileShare.ReadWrite` (oder
  `FileShare.Read | FileShare.Write`) nutzen, nicht `File.ReadAllLines`.

**Tests** (`tests/.../Internal/` + `tests/.../Integration/`):
1. `ArgumentSanitizerTests`: zusätzliche Cases für
   `Dictionary<string, object?>` und `JsonObject`-Inputs.
2. `McpServerOptionsToolCollectionTests`: Integration-Test, der einen
   MCP-Server mit manuell gesetzter `ToolCollection` + `WithObservability`
   aufbaut und verifiziert, dass `tools/list` das
   `report_observability_feedback`-Tool enthält.
3. `McpOptionsServerNameOverrideTests`: Integration-Test, der
   `McpObservabilityOptions.ServerName = "CustomName"` setzt und prüft,
   dass die geschriebenen JSONL-Records den Custom-Namen tragen.
4. `JsonlLogWriterFlushTests`: Unit-Test, der `FlushAsync` und
   `DisposeAsync` verifiziert (Schreiben → FlushAsync → Datei lesbar).
5. **JSONL-Schema-Invariante:** Neuer Test
   `ToolCallRecordSchemaStabilityTests`, der das JSON-Output-Schema vor
   und nach dem Type-Wechsel auf `IReadOnlyDictionary<string, object?>`
   byte-genau vergleicht.

**Dokumentation**:
- `README.md` — neue Sektion "Manual ToolCollection" mit Copy-Paste-Beispiel
  für den Reflection-freien manuellen Weg. Tabelle der Options-Properties
  um `ServerName`, `ServerVersion`, `FeedbackConfirmationMessage`,
  `AdditionalSensitiveKeys` erweitern. Neuer Hinweis-Block "Reading logs
  while the server is running" mit `FileShare.ReadWrite`-Beispiel.
- `CHANGELOG.md` (neu, Keep-a-Changelog-Format, Sektion `## [1.1.0]`)
  listet die hinzugefügten Features, die gelockerte §6-Richtlinie, die
  Migrations-Hinweise und das Datum 2026-08-17.
- `samples/ManualToolCollectionServer/` (neu) — minimaler MCP-Server, der
  seine Tools manuell in `McpServerOptions.ToolCollection` registriert
  und `WithObservability` nutzt. Lauffähig per `dotnet run`.

**Richtlinien-Update**:
- `McpObservabilityRichtlinien.mdc` §6: Klarstellung, dass
  `McpObservabilityOptions`, `McpObservabilityExtensions`,
  `IMcpObservabilityService` und `McpObservabilityTools` die einzigen
  öffentlichen Typen sind. Datum, Begründung (AiNetLinter-Workarounds
  ohne Reflection auflösen) und geprüfte Alternativen dokumentieren.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

Leer. Alle Audit-Punkte und vom Nutzer explizit gewünschten Features
sind in Muss-Haven hochgestuft.

### Non-Goals (bewusst NICHT Teil davon)

- **`IMcpObservabilityService.LogCustomRecord(...)`** — vom Audit
  vorgeschlagen, aber **bewusst weggelassen**: würde die JSONL-
  Schema-Invariante §5 aufweichen (`recordType` wäre nicht mehr hart
  enumeriert). Konsumenten, die eigene Records schreiben wollen,
  verwenden stattdessen `tool_call`-Records mit passendem `toolName`
  (z. B. `"custom.startup"`).
- **OpenTelemetry / Metrics / Traces** — explizit durch §2 verboten.
- **Log-Rotation / Cleanup** — bleibt wie in v1.0.0: eine Datei pro
  Prozess, keine Rotation. Konsumenten rotation selbst (oder via
  externes Tool).
- **Major-Bump v2.0** — alle Änderungen sind additiv, daher v1.1.0.
- **Log-Reader-API** (Query, HTTP-Endpoint) — §2 verbietet es.
- **Auto-Discovery von `IMcpObservabilityService` durch das SDK** — wäre
  eine SDK-Erweiterung, nicht im Scope dieses Pakets.

## Zielplattformen / Technischer Rahmen

- **Laufzeit:** .NET 10, C# 14 (wie v1.0.0).
- **MCP-SDK:** `ModelContextProtocol` 2.x stable (wie v1.0.0).
- **DI:** `Microsoft.Extensions.Options` (für
  `IPostConfigureOptions<McpServerOptions>`).
- **JSON:** `System.Text.Json` (bereits verwendet, keine neue Dep).
- **Tests:** xUnit v3 (wie v1.0.0). Integration-Tests via
  `McpServerBuilder` (existierende `IntegrationTestBase.cs` wiederverwenden,
  Pattern-Reuse).
- **Keine neuen NuGet-Abhängigkeiten** — alle Erweiterungen nutzen
  bereits referenzierte Pakete.

## Verworfene Alternativen

- **Reflection-Helper im Konsumenten (AiNetLinter-Workaround):** Verworfen
  für die Konzept-Zukunft — manuelle Reflection bricht beim ersten
  Refactoring in `RalfHuesing.Mcp.Observability` und macht
  AiNetLinter-Code unnötig kompliziert. Wird durch die public
  `McpObservabilityTools.AddFeedbackTool`-Extension ersetzt.
- **§6 strikt halten, Tool-Schatten nur intern fixen:** Verworfen — die
  Diagnostik-Fähigkeit (`IMcpObservabilityService`) ist ein eigenständiges
  Feature mit echtem Konsumenten-Mehrwert (z. B. für
  `get_server_health`-Tools), nicht nur ein Implementierungsdetail.
- **`IMcpObservabilityService.LogCustomRecord` mit beliebigem
  `recordType`:** Verworfen — verletzt §5-Schema-Invariante. Stattdessen
  Konsumenten auf `tool_call`-Records mit eigenem `toolName` verweisen.
- **Breaking Change v2.0:** Verworfen — keine der Änderungen bricht
  bestehende Konsumenten (alle Erweiterungen sind additiv oder rein
  intern). Major-Bump wäre Image-Schaden ohne Mehrwert.
- **Sample im selben Projekt statt `samples/ManualToolCollectionServer/`:**
  Verworfen — bestehende Konvention in v1.0.0 ist ein Sample pro
  Integrationsmuster. Konsistenz vor Bequemlichkeit.
- **CHANGELOG aus Git-Commits generieren (`git-cliff` o. ä.):** Verworfen
  — bringt Tool-Chain-Komplexität ohne spürbaren Mehrwert für ein
  Paket mit <20 Commits. Manuelle CHANGELOG-Pflege reicht.

## Wo im Projekt

- `src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs` —
  erweitern (4 neue Properties, XML-Docs).
- `src/RalfHuesing.Mcp.Observability/McpObservabilityExtensions.cs` —
  `IPostConfigureOptions<McpServerOptions>` registrieren.
- `src/RalfHuesing.Mcp.Observability/IMcpObservabilityService.cs` — **neu**,
  public interface.
- `src/RalfHuesing.Mcp.Observability/McpObservabilityTools.cs` — **neu**,
  public static class.
- `src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs` —
  Signatur ändern, alle Dict-Typen normalisieren.
- `src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs` —
  Cast entfernen, `Sanitize(object?)` aufrufen.
- `src/RalfHuesing.Mcp.Observability/Internal/ObservabilityContext.cs` —
  `IMcpObservabilityService` implementieren, ServerName-Override-Logik.
- `src/RalfHuesing.Mcp.Observability/Internal/JsonlLogWriter.cs` —
  `IAsyncDisposable` + `FlushAsync`.
- `src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs` —
  `ToolCallRecord.Arguments` Typ-Wechsel, JSON-Output invariant.
- `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ArgumentSanitizerTests.cs` —
  erweitern (Dictionary, JsonObject).
- `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpServerOptionsToolCollectionTests.cs` — **neu**.
- `tests/RalfHuesing.Mcp.Observability.Tests/Integration/McpOptionsServerNameOverrideTests.cs` — **neu**.
- `tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterFlushTests.cs` — **neu**.
- `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ToolCallRecordSchemaStabilityTests.cs` — **neu**.
- `samples/ManualToolCollectionServer/` — **neu**, parallel zu
  `samples/MinimalMcpServerWithObservability/`.
- `README.md` — Options-Tabelle, Dual-Use-Sektion, File-Locking-Hinweis.
- `CHANGELOG.md` — **neu**.
- `.agents/rules/McpObservabilityRichtlinien.mdc` §6 — lockern, Begründung dokumentieren.
- `Directory.Build.props` / `RalfHuesing.Mcp.Observability.csproj` — keine Änderung
  erwartet (Zero-Warning + bestehende Pakete reichen).

## Entdeckte Mängel/Redundanzen

- **§6 verbietet die geplanten public-Typen**
  - **Gefunden:** `McpObservabilityRichtlinien.mdc` §6 schreibt fest:
    "Nur `McpObservabilityOptions` und `McpObservabilityExtensions` sind
    public."
  - **Bezug:** Direkter Verstoß durch die geplanten
    `IMcpObservabilityService` + `McpObservabilityTools`.
  - **Vorschlag:** §6 in der Richtlinie explizit für v1.1 lockern, Datum
    + Begründung + geprüfte Alternativen dokumentieren.
  - **Entscheidung:** übernommen (User-Bestätigung 2026-08-17).

- **`ToolCallRecord.Arguments` ist hart auf `IReadOnlyDictionary<string, JsonElement>` getypt**
  - **Gefunden:** `src/.../Internal/LogRecords.cs:18` (C#-Typ), genutzt
    von `ToolCallLoggingHandler.cs:62` (Cast-Stelle).
  - **Bezug:** Audit-Punkt 2.3 + §5-Schema-Invariante (JSON muss stabil
    bleiben).
  - **Vorschlag:** Interner Typ-Wechsel auf
    `IReadOnlyDictionary<string, object?>?`; Serialisierung erzwingt
    `JsonElement`-Werte → identisches JSON. Schema-Invariante bleibt
    gewahrt, Schema-Version unverändert bei `1`.
  - **Entscheidung:** übernommen (siehe Muss-Haven "JSONL-Schema-Stabilität").

- **`JsonlLogWriter` blockiert Standard-Reader**
  - **Gefunden:** `src/.../Internal/JsonlLogWriter.cs:31` öffnet mit
    `FileShare.ReadWrite`. `File.ReadAllLines` schlägt fehl.
  - **Bezug:** Audit-Punkt 2.4; §8 (Doku-Objektivität, kein erwähntes
    Verhalten ohne Doku).
  - **Vorschlag:** README-Hinweis hinzufügen + `IAsyncDisposable` +
    `FlushAsync` für sauberen Lifecycle.
  - **Entscheidung:** übernommen (User-Bestätigung 2026-08-17).

- **`ArgumentSanitizer.SanitizeElement` ist ineffizient** (Mitdenken-Fund)
  - **Gefunden:** `src/.../Internal/ArgumentSanitizer.cs:50-64` nutzt
    `JsonNode.Parse(element.GetRawText())` + `JsonSerializer.SerializeToElement(node)`
    für verschachtelte Elemente.
  - **Bezug:** Kein konkreter Regel-Verstoß, aber unnötige
    Allokation/Round-Trip-Serialisierung.
  - **Vorschlag:** Bei der Generalisierung auf `object?` einen direkten
    Pfad ohne `JsonNode.Parse` anstreben (Traversierung direkt auf
    `JsonElement`).
  - **Entscheidung:** übernommen, aber nur als Optimierung im selben
    Step — kein eigener Step nötig.

## Wie (grober Ansatz)

Drift-Loop-Planer wird daraus ~6-8 Steps ableiten. Reihenfolge hier nur
als Anker, nicht bindend:

1. **Options erweitern + ObservabilityContext-Override-Logik** —
   `ServerName`/`ServerVersion`/`FeedbackConfirmationMessage`/
   `AdditionalSensitiveKeys` Properties; Auflösungsreihenfolge
   dokumentiert.
2. **ArgumentSanitizer generalisieren + LogRecord-Type-Wechsel** —
   `Sanitize(object?)`, alle Dict-Typen; `ToolCallRecord.Arguments` auf
   `IReadOnlyDictionary<string, object?>?`; JSON-Output-Invariante per
   Test sichern.
3. **IMcpObservabilityService + ObservabilityContext-Implementierung** —
   public Interface, DI-Registrierung.
4. **McpObservabilityTools public** — `CreateFeedbackTool` +
   `AddFeedbackTool`-Extension.
5. **Tool-Schatten-Fix via IPostConfigureOptions** — in
   `WithObservability` registrieren; prüft nachträglich gesetzte
   `McpServerOptions.ToolCollection`.
6. **JsonlLogWriter IAsyncDisposable + FlushAsync** — neue Methoden,
   Tests.
7. **Integration-Tests + Sample** — ToolCollection-Test,
   ServerName-Override-Test, FlushTest,
   `samples/ManualToolCollectionServer/`.
8. **README + CHANGELOG + Richtlinien-Update** — Doku-Sync, §6 lockern.

Jeder Step endet mit Coder-Commit + Doku-Commit (sofern Doku betroffen)
+ Kritiker-Audit. Convention: Conventional Commits, deutsch, imperativ
(siehe Richtlinie §10).

## Definition of Done / Erfolgskriterien

**Funktional:**
- `WithObservability()` registriert das Feedback-Tool auch dann, wenn
  der Konsument `McpServerOptions.ToolCollection` manuell befüllt —
  verifiziert per Integration-Test.
- `ArgumentSanitizer.Sanitize` verarbeitet `JsonObject`,
  `Dictionary<string, object?>`, `IReadOnlyDictionary<string, JsonElement>`
  und liefert in allen drei Fällen denselben JSONL-`arguments`-Output —
  verifiziert per Unit-Test.
- `McpObservabilityOptions.ServerName = "X"` schlägt sich in jedem
  geschriebenen JSONL-Record als `serverName: "X"` nieder — verifiziert
  per Integration-Test.
- `IMcpObservabilityService` ist im DI-Container als Singleton
  auflösbar; `CurrentLogFilePath` zeigt auf die tatsächlich geöffnete
  Datei.
- `JsonlLogWriter.FlushAsync()` schreibt ausstehende Zeilen synchron;
  `DisposeAsync()` schließt sauber.
- Konsumenten können das Feedback-Tool **ohne Reflection** in eine
  manuelle `ToolCollection` einhängen via
  `collection.AddFeedbackTool(services)`.

**Schema-Stabilität:**
- `ToolCallRecord`-JSONL-Output vor und nach v1.1.0 ist byte-identisch
  (gleiche Felder, gleiche Reihenfolge, gleiche Wert-Repräsentation).
  Verifiziert per `ToolCallRecordSchemaStabilityTests`.
- `schemaVersion` bleibt `1`. `recordType`-Enum bleibt
  `"tool_call" | "feedback"`.

**Qualität:**
- `dotnet build` mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  ist grün.
- `dotnet test` ist grün: alle bestehenden Tests + 4 neue Tests.
- `AiNetLinter` (Referenz-Integration) kann auf die neuen public APIs
  umgestellt werden und entfernt seine Reflection-Workarounds — Smoke-
  Test dokumentiert in `task-summary.md`.

**Dokumentation:**
- `README.md` dokumentiert beide Integrationswege (Auto-DI und manuelle
  `ToolCollection`) mit Copy-Paste-Codebeispielen.
- `README.md` Options-Tabelle enthält die 4 neuen Properties.
- `README.md` enthält den `FileShare.ReadWrite`-Hinweis für Live-Reader.
- `CHANGELOG.md` mit Sektion `## [1.1.0] - 2026-08-17` ist vorhanden.
- `.agents/rules/McpObservabilityRichtlinien.mdc` §6 ist gelockert und
  enthält Begründung + Datum.

**Versionierung & Veröffentlichung:**
- Versionsnummer in `RalfHuesing.Mcp.Observability.csproj` auf `1.1.0`.
- Kein v2.0-Bump, da alle Änderungen additiv sind.
- Git-Tag `v1.1.0` nach Drift-Loop-Abschluss (vom User manuell oder
  via bestehendem `scripts/create-release.ps1`).

## Offene Punkte

Keine. Konzept ist bereit für die Übergabe an
`../drift-loop/orchestrator.md`.
