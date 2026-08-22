---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P1
rules_dir: .agents/rules
last_updated: 2026-08-22
open_questions:
  - Finaler Befehlsname des dotnet tools (`ToolCommandName`, Vorschlag: `rh-mcpobs`) — externer Vertrag, vor Umsetzung festzulegen?
  - Plattform-Strategie: Windows-first mit zentraler Pfadauflösung und dokumentierter Einschränkung (Empfehlung) oder volle Cross-Platform-Unterstützung ab v1?
entscheidungen:
  - "2026-08-22: Generisches Analyse-CLI als dotnet tool im Paket-Repo (Option B); opt-in MCP-Audit-Tool (C) und Retention-Cleanup sind Non-Goals mit Wiederöffnungsbedingungen; JSONL-Schema wird additiv um optionales `packageVersion` erweitert."
---

# Zielbild: Runtime-Snapshot und generische Call-Log-Auswertung

## Kurzfassung und Entscheidung

Die aktuelle Auswertung in AiNetLinter ist ein sinnvoller, aber bewusst lokaler
Workaround. Das Observability-Paket schreibt die JSONL-Daten bereits zentral,
stellt aber noch keinen wiederverwendbaren Live-Snapshot und keinen öffentlichen
Reader/Aggregator für diese Daten bereit. AiNetLinter muss deshalb für
`get_server_health` und `--analyze-mcp-log` eigene JSONL-Dateien lesen, parsen und
aggregieren.

Diese beiden generischen Fähigkeiten sollten in
`RalfHuesing.Mcp.Observability` entstehen. Dadurch erhalten alle MCP-Server
dieselbe Semantik für Zähler, Fehlerresultate, Tool-Verteilungen,
Unvollständigkeit und laufende Dateien. Der MCP-Server selbst soll weiterhin
für die fachliche Darstellung und serverbezogene Heuristiken verantwortlich
bleiben.

Der jetzige AiNetLinter-Code bleibt bis zur Veröffentlichung einer passenden
Paketversion bestehen. Danach wird er auf eine dünne Adapter-Schicht reduziert;
die lokale Parser-/Aggregator-Implementierung kann dann entfallen.

## Was derzeit in AiNetLinter kompensiert wird

Die Version 1.0.3 des Pakets stellt über `IMcpObservabilityService` derzeit
folgende Informationen bereit:

- Aktivierungsstatus und Servermetadaten
- Pfade der aktuellen Tool-Call- und Feedback-Logdateien
- Prozess-ID und `instanceId`
- `FlushAsync`

Es gibt dort noch keine API für:

- die Anzahl der Tool-Calls oder Fehlerresultate im laufenden Prozess,
- die Verteilung der Aufrufe nach Tool,
- einen unveränderlichen Snapshot für Health-/Diagnose-Tools,
- das Lesen und Aggregieren einer noch geöffneten JSONL-Datei,
- die Erkennung beschädigter oder unvollständiger Zeilen,
- eine deterministische Auswertung mehrerer Prozessdateien.

Genau diese Lücken füllt der aktuelle AiNetLinter-Code lokal. Das ist kein
fachlicher Fehler im Server, sondern eine fehlende gemeinsame Paketabstraktion.
Die Doppelimplementierung ist jedoch langfristig Drift-Risiko: Schemaänderungen
müssten im Paket und im AiNetLinter-Parser synchron angepasst werden.

## Verantwortungsgrenze

| Fähigkeit | Zielverantwortung | Begründung |
| --- | --- | --- |
| JSONL-Schema, Schreiben und Dateinamen | Observability-Paket | Gemeinsamer Vertrag aller MCP-Server |
| Prozess-/Instanzmetadaten | Observability-Paket | Wird beim Schreiben ohnehin zentral erzeugt |
| Laufende Zähler und unveränderlicher Snapshot | Observability-Paket | Muss dieselben Events wie der Writer sehen |
| Generischer JSONL-Reader und Aggregator | Observability-Paket | Wiederverwendbare Offline-Diagnose |
| Reader für noch geöffnete Dateien | Observability-Paket | Die Paketdateien werden mit `FileShare.ReadWrite` geschrieben |
| Behandlung/Anzahl ungültiger JSONL-Zeilen | Observability-Paket | Generische Datenqualität des eigenen Formats |
| Health-Text und `structuredContent` | AiNetLinter bzw. jeweiliger MCP-Server | Server-spezifische Darstellung und API-Vertrag |
| Generisches Analyse-CLI (`analyze`, Discovery, Exit-Codes, Text/JSON-Report) | Observability-Paket (dotnet tool) | Entscheidung 2026-08-22: einmal statt N-mal pro Server; Details siehe Abschnitt „Erweiterung 2026-08-22“ |
| Host-spezifische CLI-Erweiterungen und Heuristiken (`[ERROR]: CODE:` etc.) | AiNetLinter bzw. jeweiliger MCP-Server | Bleiben als dünner Adapter beim Host |
| Loading-/Startup-Marker | AiNetLinter | Heuristik des AiNetLinter-Startups, kein MCP-Standard |
| Feedback- und Tool-Ausschlüsse | Paket liefert Metadaten; Host entscheidet | Das Paket kennt die Records, der Host kennt seine Auswertung |
| MCP-Query-Tool, HTTP-Endpunkt oder Logserver | Nicht Teil dieses Vorhabens | Widerspricht der bewusst kleinen Paketverantwortung |

Wichtig ist die Unterscheidung zwischen **Audit als Bibliotheksfähigkeit** und
**Audit als Produktfunktion**: Das Paket sollte Datenqualität und generische
Aggregationen liefern. Es sollte nicht selbst ein MCP-Audit-Tool oder einen
zentralen Query-Service veröffentlichen. Jeder Server kann daraus seine eigene
Health-/Diagnosefunktion bauen.

## Erweiterung 2026-08-22: Generisches Analyse-CLI im Paket?

### Auslöser

Die Verantwortungsgrenze oben ordnet „CLI-Optionen und Ausgabeformat" dem
jeweiligen Host zu. Bei mehr als einem konsumierenden MCP-Server entsteht
dadurch jedoch derselbe Drift, den diese Aufgabe eigentlich beseitigt — nur
eine Ebene höher: Jeder Host müsste Argument-Parsing, Text/JSON-Formatter,
Exit-Codes und Discovery-Logik (Tagesordner, Glob über `{serverName}_*_*.jsonl`)
duplicieren. Das DRY-Argument, das den Parser ins Paket holt, gilt auch für die
CLI-Grundausstattung.

### Optionen

**Option A — Status quo (nur Daten-API, CLI je Host):**
AiNetLinter behält `--analyze-mcp-log`; jeder weitere Server baut eigenes
CLI-Plumbing auf der gemeinsamen Daten-API. Saubere Verantwortungsgrenze,
aber N-fache Duplikation der Präsentationsgrundlagen.

**Option B — Generisches CLI-Tool im Paket-Repo:**
Separates Projekt (z. B. `src/RalfHuesing.Mcp.Observability.Cli`), verteilt als
dotnet tool. Einmal implementiert, auditiert es die Logs **aller** Server,
weil Dateipfad- und Namenskonvention (`%LOCALAPPDATA%\RalfHuesing\
McpObservability\<serverName>\<yyyy-MM-dd>\{serverName}_{PID}_{instanceId}.jsonl`)
vom Paket standardisiert werden. Der Analyzer kennt dafür bereits Discovery
über den Standard-Root. Server-spezifische Heuristiken (`[ERROR]: CODE:`,
Loading-Marker) wandern auch damit **nicht** ins Paket — AiNetLinter behält
sein dünnes CLI-Command als Adapter mit Heuristiken, delegiert aber Parsing und
Aggregation an das Paket.

**Option C — Opt-in MCP-Audit-Tool im Paket:**
Analog zum bestehenden FeedbackTools-Vorbild könnte das Paket ein opt-in
Diagnose-Tool bereitstellen (z. B. `query_observability_summary`), sodass
Agenten jeden konsumierenden Server ohne CLI selbst auditen können.
Vorteil: null Code pro Server, agentennativ. Risiken: Tool-Proliferation,
potenziell große Antworten (Limits/Truncation nötig), Datenschutz
(Feedback-Records). Konsistent mit der Rule-of-Two-Haltung des Konzepts
(Fehlersemantik, Marker): erst wenn zwei Server denselben Bedarf zeigen.

**Einschätzung (Stand Diskussion):** B ist der natürliche nächste Schritt und
löst das genannte DRY-Problem direkt; A allein lässt das Problem für alle
weiteren Server bestehen; C ist sinnvoll erst nach B und bei nachgewiesenem
Bedarf in mindestens zwei Servern. A und B schließen sich nicht aus — B ersetzt
kein Host-CLI, es macht es überflüssig für den generischen Teil.

**Entscheidung (2026-08-22):** **Option B** wird umgesetzt — ein generisches
Analyse-CLI als separates Projekt (`src/RalfHuesing.Mcp.Observability.Cli`),
verteilt als **dotnet tool** (`dotnet tool install --global`). Eine zusätzlich
eigenständige exe ist ausdrücklich kein Ziel; sie bleibt ein mechanisch
ableitbarer Publish-Schritt für den Bedarfsfall (.NET ist ohnehin Prerequisite
aller konsumierenden Server). Option C (opt-in MCP-Audit-Tool) ist **Non-Goal**
mit Wiederöffnungsbedingung: erst wenn mindestens zwei unabhängige konsumierende
Server denselben Bedarf zeigen. AiNetLinter behält sein serverbezogenes
`--analyze-mcp-log` als dünnen Adapter (Heuristiken wie `[ERROR]: CODE:`,
Formatter), delegiert Parsing, Aggregation und Discovery aber an das Paket und
kann den lokalen Parser dann entfernen.

### Edge Cases und weiterführende Punkte (aus AiNetLinter-Learnings und Review)

- **Version-Spreizung:** Analyzer-Version kann neuer/älter als die Writer-
  Version der gelesenen Dateien sein. **Entschieden:** additives, optionales
  `packageVersion`-Feld im Schema (siehe JSONL-Vertrag); der Report weist die
  vertretenen Versionen aus; fehlt das Feld (Bestandsdateien), steht dort
  `unknown (<letzte bekannte Paketversion vor Einführung)`.
- **Discovery über den Standard-Root:** Der CLI-Eingabepfad sollte optional
  leer bleiben können (= ganzer Root), mit Filter `--server <name>` und ggf.
  `--date <yyyy-MM-dd|today|all>`. Mehrere Server in einem Lauf → Report muss
  serverweise getrennt und deterministisch aggregieren.
- **Reparse Points:** Rekursive Verzeichnis-Scans müssen Junctions/Symlinks
  überspringen (AiNetLinter-Learning aus Commit 106ebe8e: Staleness-Walk mit
  Reparse-Point-Schutz). Gilt für Discovery im Paket und im CLI.
- **Datei wächst während des Lesens:** Geöffnete Dateien mit
  `FileShare.ReadWrite` lesen ist geplant; zusätzlich: Snapshot-Liste der
  Dateien fixieren, nicht „live" neu globben; abschließende Teilzeile zählt
  als malformed (bereits geplant), darf aber nicht als Serverfehler miss-
  interpretiert werden, solange die Datei noch geschrieben wird.
- **Encoding/Grenzfälle:** UTF-8 ohne/statt BOM, CRLF/LF, leere Dateien,
  Datei nur aus einer halben Zeile — alles als definierte Fälle testen.
- **Sehr große Logs:** Streaming-Lesen (kein Komplett-Einlesen), optionale
  Zeitfilter (`--from`/`--to` UTC), `MaxMalformedLineDetails`-Cap ist bereits
  geplant; analoge Caps für Sessions-Liste prüfen (tausende Prozessdateien).
- **Exit-Codes als Vertrag:** CLI braucht stabile Exit-Codes (0 = ok inkl.
  malformed lines, distinct code = keine Dateien gefunden, Fehlercodes für
  IO/Zugriff), damit Agenten das Tool maschinell auswerten können. JSON-Modus
  deterministisch (sortierte Keys, sortierte Dateilisten).
- **Datenschutz:** Feedback-Records default-excluded (bereits geplant);
  Argumente sind durch den Sanitizer redacted, bevor sie geschrieben werden —
  der Analyzer darf niemals Rohargumente nachliefern. Kein Zeileninhalt-Dump
  bei Malformed-Details (bereits geplant).
- **Retention:** Tagesordner wachsen unbegrenzt. Ein `--clean older-than`
  wäre naheliegend, ist aber ein Schreibzugriff und damit eigener Scope —
  **Non-Goal** für diesen Task. Der Report weist stattdessen read-only die
  Gesamtgröße und den ältesten Tagesordner pro Server aus, damit Wachstum
  sichtbar wird; ein Cleanup-Kommando ist möglicher Folgetask.
- **Plattform-Annahme (Audit-Fund 2026-08-22):** Der Default-Log-Root liegt
  unter `%LOCALAPPDATA%` — Windows-spezifisch, während ein dotnet tool
  grundsätzlich plattformübergreifend läuft. Die Pfadauflösung wird deshalb
  zentral gekapselt und das CLI erhält eine explizite `--root`-Option; die
  finale Strategie (Windows-first mit dokumentierter Einschränkung vs. volle
  Cross-Platform-Unterstützung) ist als offene Frage notiert.
- **Berechtigungen (Audit-Fund):** Logs anderer Benutzer-/Dienstkonten sind
  ggf. nicht lesbar. Unlesbare Dateien sind ein eigener, ausgewiesener
  Fehlerfall im Report — niemals stilles Überspringen.
- **Zukünftige Major-`schemaVersion` (Audit-Fund):** Der Analyzer parst
  Records mit höherer Major-Version als seine eigene nicht, zählt sie aber
  als eigenen Ausweis (`unsupportedVersionCount`-artig) statt abzubrechen;
  additive Felder innerhalb derselben Major-Version bleiben voll lesbar.

## Zielarchitektur

Die zentrale Abstraktion ist ein gemeinsamer Abschluss eines Tool-Call-Events:

```text
ToolCallLoggingHandler
        |
        +--> ObservabilityRuntimeState.Record(record)
        |          |
        |          +--> IMcpObservabilityService.Snapshot
        |
        +--> JsonlLogWriter.WriteRecord(record)
        |
        +--> optional: generischer McpLogAnalyzer für bestehende Dateien
```

Der Runtime-Snapshot und der Offline-Analyzer dürfen nicht zwei verschiedene
Semantiken entwickeln. Beide müssen aus demselben `ToolCallRecord`-Vertrag
arbeiten. Der Snapshot ist eine schnelle Prozesssicht; der Analyzer ist eine
nachträgliche Dateisicht und kann zusätzlich mehrere Dateien, beschädigte
Zeilen und getrennte Prozessinstanzen auswerten.

Empfohlene Schichten im Paket:

1. **Runtime collection** – interne, thread-sichere Zähler im selben
   Abschluss-Event wie das JSONL-Schreiben.
2. **Public diagnostics** – unveränderliche, öffentliche Snapshot-Typen über
   `IMcpObservabilityService`.
3. **Offline analysis** – öffentlicher, synchroner Reader/Aggregator für
   einzelne Dateien, Verzeichnisse oder explizite Dateilisten.
4. **Consumer adapter** – Health-Tools, CLI und projektbezogene Heuristiken in
   den jeweiligen MCP-Servern.

## Vorgeschlagene öffentliche API

Die folgenden Typen sind additive API-Erweiterungen. Sie sollten erst mit einer
neuen Minor-Version veröffentlicht werden, wenn die Semantik durch Tests und
Dokumentation festgeschrieben ist. Die bestehenden Einstiegspunkte
`McpObservabilityOptions`, `WithObservability`, `IMcpObservabilityService` und
`McpObservabilityTools` bleiben erhalten.

Die aktuellen Paketregeln begrenzen die öffentliche API bewusst auf diese
Einstiegspunkte. Die hier vorgeschlagenen Snapshot-/Analysis-Typen sind daher
eine explizite, zu begründende Erweiterung dieser Regel und keine stillschweigende
Freigabe interner Implementierungsdetails. Bei der Umsetzung müssen die
Paketregeln, API-Dokumentation und SemVer-Entscheidung gemeinsam aktualisiert
werden. Falls diese Erweiterung nicht gewünscht ist, bleibt der Analyzer intern
und wird stattdessen in ein separates, optionales Analysis-Paket ausgelagert;
für die Ablösung des AiNetLinter-Workarounds ist aber eine konsumierbare
Daten-API erforderlich.

### 1. Live-Snapshot

```csharp
using System.Collections.ObjectModel;
using System.Globalization;

namespace RalfHuesing.Mcp.Observability;

/// <summary>
/// Immutable process-local counters collected by the observability pipeline.
/// </summary>
public sealed record McpObservabilitySnapshot(
    int ToolCallCount,
    int FailedCallCount,
    int ErrorResultCount,
    int ResponseTruncatedCount,
    long TotalDurationMilliseconds,
    IReadOnlyDictionary<string, int> CallsByTool,
    DateTimeOffset? FirstCallUtc,
    DateTimeOffset? LastCallUtc)
{
    public static McpObservabilitySnapshot Empty { get; } = new(
        ToolCallCount: 0,
        FailedCallCount: 0,
        ErrorResultCount: 0,
        ResponseTruncatedCount: 0,
        TotalDurationMilliseconds: 0,
        CallsByTool: new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.Ordinal)),
        FirstCallUtc: null,
        LastCallUtc: null);
}
```

`FailedCallCount` beschreibt `success == false` und deckt damit Exceptions ab.
`ErrorResultCount` beschreibt ein MCP-Ergebnis mit `isError == true`. Die
Trennung ist wichtig, weil ein Server ein fachliches Fehlerresultat liefern
kann, ohne dass die Handler-Ausführung eine Exception wirft.

Die bestehende Schnittstelle sollte additiv erweitert werden:

```csharp
public interface IMcpObservabilityService
{
    // Bestehende Mitglieder bleiben unverändert.
    McpObservabilitySnapshot Snapshot { get; }
}
```

Die Implementierung muss bei jedem Zugriff eine unveränderliche Sicht liefern.
Insbesondere darf ein Consumer weder das interne Dictionary mutieren noch die
Zähler während einer laufenden Aktualisierung beobachten können. Der Snapshot
des deaktivierten Services ist immer `McpObservabilitySnapshot.Empty`.

### 2. Thread-sicherer interner Zustand

Der Zustand sollte nicht aus der aktuellen Datei zurückgelesen werden. Das
würde bei jedem Health-Aufruf I/O verursachen und bei einer gerade geschriebenen
Zeile unnötige Race-Conditions erzeugen. Ein möglicher interner Baustein:

```csharp
internal sealed class ObservabilityRuntimeState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _callsByTool = new(StringComparer.Ordinal);
    private int _toolCallCount;
    private int _failedCallCount;
    private int _errorResultCount;
    private int _responseTruncatedCount;
    private long _totalDurationMilliseconds;
    private DateTimeOffset? _firstCallUtc;
    private DateTimeOffset? _lastCallUtc;

    internal void Record(ToolCallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            _toolCallCount++;
            if (!record.Success)
            {
                _failedCallCount++;
            }

            if (record.IsErrorResult)
            {
                _errorResultCount++;
            }

            if (record.ResponseTruncated)
            {
                _responseTruncatedCount++;
            }

            _totalDurationMilliseconds += record.DurationMs;
            _callsByTool[record.ToolName] =
                _callsByTool.GetValueOrDefault(record.ToolName) + 1;

            var timestamp = DateTimeOffset.Parse(
                record.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            _firstCallUtc ??= timestamp;
            _lastCallUtc = timestamp;
        }
    }

    internal McpObservabilitySnapshot CreateSnapshot()
    {
        lock (_gate)
        {
            return new McpObservabilitySnapshot(
                _toolCallCount,
                _failedCallCount,
                _errorResultCount,
                _responseTruncatedCount,
                _totalDurationMilliseconds,
                new ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int>(_callsByTool, StringComparer.Ordinal)),
                _firstCallUtc,
                _lastCallUtc);
        }
    }
}
```

Der konkrete Code muss an die vorhandenen Record-Typen und die geltenden
Analyzer-Regeln angepasst werden. Die wesentlichen Eigenschaften sind der
gemeinsame Event-Punkt, `StringComparer.Ordinal`, ein konsistenter Lock und
eine Kopie des Dictionaries beim Snapshot.

Der `ToolCallLoggingHandler` sollte nach der Erstellung des Records genau
einmal `runtimeState.Record(record)` auslösen. Die bestehende Filterung des
Feedback-Tools bleibt dabei erhalten, damit der Health-Aufruf nicht durch
seine eigene Auswertung in die Zähler hineinrutscht.

### 3. Wiederverwendbarer Offline-Analyzer

Für die spätere Ablösung des AiNetLinter-Workarounds sollte das Paket außerdem
eine kleine Daten-API anbieten. Sie liefert Daten, keine formatierten
MCP-Antworten und keine CLI:

```csharp
namespace RalfHuesing.Mcp.Observability.Analysis;

public sealed record McpLogAnalysisOptions
{
    public bool Recursive { get; init; } = true;
    public bool IncludeFeedbackRecords { get; init; }
    public bool IgnoreMalformedLines { get; init; } = true;
    public int MaxMalformedLineDetails { get; init; } = 100;
}

public sealed record McpLogSessionSummary(
    string FilePath,
    int ProcessId,
    string InstanceId,
    int ToolCallCount,
    DateTimeOffset? FirstCallUtc,
    DateTimeOffset? LastCallUtc);

public sealed record McpLogAnalysisReport(
    IReadOnlyList<string> InputFiles,
    int ToolCallCount,
    int FailedCallCount,
    int ErrorResultCount,
    int ResponseTruncatedCount,
    int MalformedLineCount,
    int IgnoredRecordCount,
    long TotalDurationMilliseconds,
    IReadOnlyDictionary<string, int> CallsByTool,
    IReadOnlyList<McpLogSessionSummary> Sessions,
    IReadOnlyList<string> MalformedLineDetails);

public static class McpLogAnalyzer
{
    public static McpLogAnalysisReport Analyze(
        IEnumerable<string> inputPaths,
        McpLogAnalysisOptions? options = null);
}
```

Die endgültige Signatur sollte vor der Implementierung noch auf die gewünschte
Datei-/Verzeichnis-/Glob-Semantik festgelegt werden. Eine explizite
`IEnumerable<string>`-Dateiliste ist für andere MCP-Server am stabilsten; eine
separate `McpLogFileDiscovery`-API kann später Komfort für Verzeichnisse und
Globs ergänzen. Der Analyzer muss:

- geöffnete Dateien mit `FileShare.ReadWrite` lesen können,
- Feedbackdateien standardmäßig ausschließen,
- deterministisch sortierte Inputdateien und Toolschlüssel liefern,
- ungültige Zeilen zählen und optional begrenzte Diagnosedetails liefern,
- unbekannte additive Record-Felder ignorieren,
- fehlende Pflichtfelder als ungültigen Record behandeln,
- keine Exceptions durch einzelne unvollständig geschriebene Zeilen erzeugen,
- `schemaVersion`, `recordType`, `processId` und `instanceId` validieren,
- die vorhandenen Prozess-/Instanzgrenzen in `Sessions` erhalten.

`IgnoreMalformedLines` darf nicht dazu führen, dass Fehler unsichtbar werden:
`MalformedLineCount` ist immer Teil des Reports. Wenn die Option `false` ist,
wirft die API eine dokumentierte, typisierte Ausnahme mit Dateipfad und
Zeilennummer. Ein unbeschränkter Dump von Zeileninhalten ist zu vermeiden, da
diese Argumente oder Response-Daten enthalten können.

Der Analyzer soll keine Ausgabeformatierung kennen. Markdown, JSON für die
CLI, Exit-Codes und die konkrete Struktur eines `get_server_health`-Ergebnisses
bleiben beim Consumer.

### 4. Fehlersemantik nicht vorschnell verallgemeinern

Der aktuelle AiNetLinter-Analyzer extrahiert Fehlercodes aus einem Textmuster
wie `[ERROR]: CODE:`. Dieses Muster ist kein Bestandteil des allgemeinen
Observability-Vertrags und sollte nicht in das Paket wandern.

Das Paket sollte zunächst nur die bereits vorhandenen generischen Felder
aggregieren:

- `success` bzw. fehlgeschlagene Ausführung,
- `isErrorResult`,
- optional gekürzte `errorMessage`-Information,
- Response-Truncation.

Wenn mehrere MCP-Server einen strukturierten Fehlercode benötigen, sollte das
JSONL-Schema später additiv um ein optionales `errorCode`-Feld erweitert werden.
Die Erzeugung dieses Feldes muss dann an einen stabilen MCP-/Server-Vertrag
gebunden werden; ein heuristischer Regex im Basispaket wäre die falsche
Abstraktion.

Ebenso bleiben Loading-/Startup-Marker, Vollständigkeitsmarker und
serverbezogene Recovery-Hinweise zunächst außerhalb des Pakets. Falls sich
dieselben Anforderungen in mindestens zwei unabhängigen MCP-Servern wiederholen,
kann dafür eine kleine, explizit konfigurierte Klassifikations-API entworfen
werden. Bis dahin verhindert die Trennung, dass AiNetLinter-Heuristiken in ein
allgemeines Paket durchsickern.

## Konkrete Anpassung am bestehenden Paket

### `ObservabilityContext`

Der Context sollte intern eine Singleton-Instanz von `ObservabilityRuntimeState`
halten und deren Snapshot weiterreichen:

```csharp
internal sealed class ObservabilityContext : IMcpObservabilityService
{
    private readonly ObservabilityRuntimeState _runtimeState;

    public McpObservabilitySnapshot Snapshot => _runtimeState.CreateSnapshot();
}
```

Die Konstruktorverkettung und DI-Registrierung müssen so angepasst werden, dass
pro MCP-Server-Prozess genau ein Zustand verwendet wird. Die Paketdatei bleibt
die Persistenz, der Runtime-State ist nur ein schneller Prozess-Snapshot und
muss beim Start leer sein.

### `ToolCallLoggingHandler`

Die Record-Erstellung bleibt die einzige Quelle für beide Sichten:

```csharp
var record = CreateRecord(...);
runtimeState.Record(record);
logWriter.WriteRecord(record);
```

Die Reihenfolge und das Fehlerverhalten sind mit Tests zu fixieren. Besonders
zu entscheiden ist, ob ein Schreibfehler den MCP-Call weiterhin fehlschlagen
lässt oder nur die Persistenz als Diagnosefehler markiert. Die Entscheidung
muss mit der bestehenden Paketsemantik kompatibel sein und darf nicht durch
einen stillen `catch` verdeckt werden.

### JSONL-Vertrag

Die Record-Eigenschaften sollten für den Analyzer nicht über mehrfach kopierte
String-Literale beschrieben werden. Ein interner Schema-Contract oder
serializerbasierter Zugriff soll die Feldnamen zentralisieren. Dabei bleiben
die bestehenden Regeln gültig:

- `schemaVersion` bleibt `1`, solange nur additive Felder hinzukommen.
- `timestamp` bleibt UTC.
- `recordType` bleibt `tool_call` oder `feedback`.
- additiv neu: optionales `packageVersion` (Paketversion des schreibenden
  Prozesses); fehlendes Feld wird beim Lesen als unbekannte Version
  ausgewiesen — rein additive Erweiterung, daher kein Major-Bump.
- unbekannte additive Felder werden beim Lesen ignoriert.
- eine Breaking Change erfordert eine Major-Version.

Die JSONL-Dateien bleiben weiterhin append-only und menschenlesbar. Es wird
keine Datenbank und kein Server-seitiger Log-Query-Endpunkt eingeführt.

## Migration von AiNetLinter

Die Migration sollte in einem separaten AiNetLinter-Task erfolgen, nachdem eine
Paketversion mit den neuen APIs verfügbar ist:

1. AiNetLinter aktualisiert die Paketreferenz.
2. `GetServerHealthTool` liest `IMcpObservabilityService.Snapshot` für die
   schnelle aktuelle Prozesssicht.
3. `--analyze-mcp-log` verwendet `McpLogAnalyzer` und behält nur
   `McpLogReportFormatter`, CLI-Optionen und AiNetLinter-spezifische Heuristiken.
4. Die lokalen Typen unter `src/AiNetLinter/Observability/` werden schrittweise
   entfernt, sobald keine Referenzen mehr bestehen.
5. Bestehende JSON-/Text-Ausgaben und Health-Tests bleiben als Kompatibilitäts-
   vertrag des AiNetLinter erhalten.

Bis dahin ist der lokale Code absichtlich nicht anzupassen. Er dient als
Referenzimplementierung für die Tests des Pakets und macht die aktuell
fehlenden Anforderungen konkret.

## Testkonzept

### Runtime-Snapshot

- leerer Snapshot direkt nach der Registrierung,
- ein erfolgreicher Tool-Call erhöht `ToolCallCount`, Dauer und Tool-Zähler,
- Exception erhöht `FailedCallCount`,
- `isError == true` erhöht `ErrorResultCount`, aber nicht zwingend
  `FailedCallCount`,
- gekürzte Response erhöht `ResponseTruncatedCount`,
- mehrere Tools werden ordinal und deterministisch aggregiert,
- parallele Calls verlieren keine Inkremente,
- zwei aufeinanderfolgende Snapshots teilen keine mutierbare Collection,
- Feedback-Tools werden nicht als normale Tool-Calls gezählt,
- deaktivierte Observability liefert den leeren Snapshot.

### Offline-Analyzer

- eine gültige Datei mit mehreren Tools,
- mehrere Dateien aus unterschiedlichen Prozessen und `instanceId`s,
- geöffnete Datei während eines Schreibvorgangs,
- abschließende unvollständige JSONL-Zeile,
- ungültige JSON-Zeile zwischen gültigen Records,
- unbekannte additive Felder,
- falsche oder fehlende `schemaVersion`/`recordType`,
- Record mit höherer Major-`schemaVersion` wird gezahlt/ausgewiesen, aber
  nicht geparst; kein Abbruch des Laufs,
- Feedbackdatei standardmäßig ausgeschlossen und optional einschließbar,
- deterministische Reihenfolge der Dateien, Sessions und Toolschlüssel,
- Begrenzung von `MalformedLineDetails`,
- keine Ausgabe sensibler Argumente oder Responses in Fehlermeldungen.

### Analyse-CLI

- stabile Exit-Codes: ok (inkl. malformed lines), keine Dateien gefunden,
  IO-/Zugriffsfehler — jeweils als eigener dokumentierter Code,
- JSON-Modus ist byte-deterministisch bei identischem Input (sortierte Keys,
  sortierte Dateilisten),
- Discovery-Filter (`--server`, `--date`, `--root`) greifen wie dokumentiert,
- Standard-Root-Auflösung ist zentral gekapselt und in Tests überschreibbar,
- unlesbare Dateien (Berechtigungen) erscheinen als eigener Report-Fehlerfall,
  werden nicht still überschrieben/übergangen,
- `--help` dokumentiert alle Optionen und Exit-Codes.

### Integration und Dokumentation

- Minimaler MCP-Server mit zwei erfolgreichen Calls und einem Fehlerresultat,
- Auflösung des öffentlichen `IMcpObservabilityService` aus DI,
- Snapshot und JSONL-Report müssen dieselben generischen Zahlen liefern,
- `dotnet build` ohne Warnungen,
- Fast- und Integrationstests gemäß Repository-Regeln,
- README und öffentliche API-Dokumentation aktualisieren,
- SemVer-Entscheidung und JSONL-Schemaänderungen dokumentieren.

## Rollout in Etappen

### Phase 1: Runtime-Snapshot

`McpObservabilitySnapshot`, interne Runtime-Zähler und die additive Erweiterung
von `IMcpObservabilityService` implementieren. Dies liefert sofort Nutzen für
alle Server, ohne Dateiparsing oder neue Abhängigkeiten.

### Phase 2: Generischer Reader/Aggregator

Reader, Discovery und Aggregation aus dem AiNetLinter-Workaround als
paketinterne bzw. öffentliche Daten-API überführen. Malformed-Line-Diagnostik
und FileShare-Verhalten dabei ausdrücklich testen.

### Phase 2b: Generisches Analyse-CLI (dotnet tool)

Neues Projekt `src/RalfHuesing.Mcp.Observability.Cli`, verteilt als dotnet
tool. Baut vollständig auf dem Analyzer aus Phase 2 auf und enthält ausschließlich
generische Fähigkeiten: Discovery über den Standard-Log-Root (optional mit
`--server`/`--date`-Filtern), deterministischen Text-/JSON-Report inklusive
Versionsausweisung (`packageVersion`) und Retention-Indikatoren, sowie stabile,
dokumentierte Exit-Codes als maschineller Vertrag. Keine serverspezifischen
Heuristiken, keine Schreibzugriffe.

## Auslieferungsmodell und Release (Klärung 2026-08-22)

Es bleiben **zwei getrennte Artefakte** aus einem Repo:

| Artefakt | Typ | Consumer |
| --- | --- | --- |
| `RalfHuesing.Mcp.Observability.<v>.nupkg` | Library (.dll) | MCP-Server via `<PackageReference>` — erhält weiterhin **nur** die .dll, keine .exe, keine Zusatzdateien im Server-Output |
| `RalfHuesing.Mcp.Observability.Cli.<v>.nupkg` | dotnet tool | Pro Maschine via `dotnet tool install --global`; landet in `%USERPROFILE%\.dotnet\tools`, keine Kopie in Server-Verzeichnissen |

Bewusst **nicht** umgesetzt: die Audit-.exe als Content-File ins Lib-Paket.
Das würde die .exe N-fach in jeden Server-`bin`-Ordner duplizieren,
Versions-Chaos bei unterschiedlich gepinnten Server-Paketen erzeugen und
Server aufblähen, die das Audit-Werkzeug nie aufrufen. Das dotnet tool hat
stattdessen genau eine installierte Version pro Maschine, die Logs aller
Server auswertet — ohne dass die Server davon wissen müssen.

Release-Ablauf (ein Git-Tag `v<x.y.z>` = ein Release = zwei Artefakte):

- Beide packbaren Projekte liegen in derselben Solution; das Cli-Projekt
  referenziert die Lib per ProjectReference (kein Drift möglich).
- `build.yml`: Pack-Schritt wird von einem hartcodierten csproj auf eine
  Schleife über beide Projekte erweitert; Push (`*.nupkg --skip-duplicate`)
  und GitHub-Release-Upload (`./artifacts/*`) funktionieren danach unverändert
  für beide Pakete.
- `scripts/create-release.ps1` (Audit-Fund 2026-08-22): muss den zweiten
  Pack-Pfad ebenso erhalten wie `build.yml`; `CHANGELOG.md` bekommt je Release
  Abschnitte für beide Pakete.
- **Lockstep-Versionierung:** Beide Pakete erhalten stets dieselbe
  Versionsnummer. Der Analyzer parst das Schema, das die Lib schreibt —
  gleiche Nummern machen „Tool `<v>` versteht alle Logs von Lib ≤ `<v>`"
  trivial wahr; Restrisiken deckt das `packageVersion`-Feld ab.

### Phase 3: AiNetLinter umstellen

Paketreferenz aktualisieren, lokale Doppelimplementierung entfernen und nur
serverbezogene Ausgabe/Heuristiken behalten. Die Ausgaben müssen vor und nach
der Migration mit Golden-/Integrationstests vergleichbar bleiben.

### Phase 4: Weitere MCP-Server integrieren

Mindestens ein weiterer MCP-Server sollte den Snapshot konsumieren. Erst wenn
mehrere Consumer denselben zusätzlichen Bedarf zeigen, werden weitere
Abstraktionen wie strukturierte Error-Codes oder konfigurierbare Marker
aufgenommen.

## Definition of Done

- Jeder MCP-Server kann ohne Dateilesen einen konsistenten Live-Snapshot
  abfragen.
- Ein generischer Analyzer kann Paket-JSONL-Dateien sicher und deterministisch
  auswerten, auch wenn sie gerade geschrieben werden.
- Das generische Analyse-CLI (dotnet tool) auditiert Logs aller konsumierenden
  Server ohne hostspezifischen Code; Exit-Codes und JSON-Ausgabe sind
  dokumentierte, deterministische Verträge.
- AiNetLinter muss nach der Migration keinen eigenen JSONL-Parser und keinen
  generischen Aggregator mehr pflegen.
- Server-spezifische Health-Darstellung, CLI und Heuristiken bleiben außerhalb
  des Pakets.
- Es gibt keine neue Datenbank, keinen HTTP-/MCP-Logquery-Service und keine
  unnötige Plugin-Abstraktion.
- Schema-, API-, Datenschutz- und SemVer-Dokumentation sind aktualisiert
  (einschließlich `packageVersion` als additive Schemaerweiterung).
- Build, relevante Tests sowie der paketweite Abschlusslauf sind grün und
  warnungsfrei.
- Vor dem Paket-Commit sind Duplicate-, Magic-Value-, Dead-Code- und
  Violation-Audits durchgeführt; echte Befunde sind behoben oder dokumentiert.

## Bewusste Non-Goals dieses Tasks (mit Wiederöffnungsbedingungen)

- **Opt-in MCP-Audit-Tool im Paket (Option C):** Nicht jetzt. Wiederöffnung,
  wenn mindestens zwei unabhängige konsumierende Server denselben Bedarf
  zeigen. Der FeedbackTools-Vorbild-Mechanismus (opt-in Tool-Registrierung)
  bleibt der dafür vorgesehene Weg.
- **Retention/Cleanup-Kommando:** Nicht jetzt — Schreibzugriff ist eigener
  Scope. Der Report macht Wachstum read-only sichtbar; Cleanup bleibt
  möglicher Folgetask.
- **Eigenständige exe-Verteilung:** Nicht jetzt — `dotnet tool` deckt den
  Bedarf ab; ein self-contained Publish bleibt mechanisch ableitbar.

## Offene Architekturentscheidung für die Umsetzung

Vor Phase 2 ist festzulegen, ob der Offline-Analyzer als Teil des bestehenden
Hauptpakets oder als separates Paket ausgeliefert wird. Empfehlung: zunächst im
bestehenden Paket als Namespace `RalfHuesing.Mcp.Observability.Analysis`, weil
Reader und Schema unmittelbar zusammengehören und keine zusätzliche
Versions-/Abhängigkeitsmatrix entstehen soll. Falls ein Consumer später nur den
Reader ohne Runtime-Integration benötigt, kann daraus mit wenig Aufwand ein
separates Paket extrahiert werden.

## Vollständigkeitsaudit (2026-08-22)

Nach dem Nachschärfen wurde das Konzept systematisch gegen neun Linsen geprüft:
API/SemVer · JSONL-Schema/Kompatibilität · Datenschutz · Tests ·
Build/CI/Release/Auslieferung · Plattform/Betrieb · Migration/Rollback ·
Dokumentation · Scope-Grenzen/Non-Goals. Gefundene Lücken (Release-Pfad,
Plattform-Annahme, Major-Schema-Fremdversionen, CLI-Testabdeckung,
Berechtigungs-Fehlerfall) sind oben eingearbeitet; die beiden verbleibenden
Entscheidungen stehen in `open_questions`. Der Roadmap-Kritiker des drift-loops
prüft jeden Step zusätzlich gegen dieses Konzept — Abweichungen fließen als
Korrektur-Steps oder Tech-Debt-Einträge zurück.
