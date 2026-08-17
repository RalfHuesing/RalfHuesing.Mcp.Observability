---
status: done
type: step-result
task: refine
step: 002
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: minimax-m3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-17T23:25:00+02:00
code_commit_hash: 50ac699
status_after: done
blocker_category: n/a
---

# Result Step 002: Sanitizer-Generalisierung, LogRecord-Type-Wechsel und Response-Logging

<**Wer das liest:** der Kritiker (prüft dich gegen den Plan) und der
Planer beim nächsten Step. Entscheidungsrelevant sind vor allem
„Abweichungen vom Plan", „Beobachtungen" und „Bekannte Unschärfen" —
dort lieber konkret als knapp. Alles andere: knapp halten, nichts aus
dem Step-Plan wiederholen, was unverändert umgesetzt wurde.>

## Zusammenfassung

Alle 9 Konkreten Änderungen aus dem Step-Plan umgesetzt: 4 Source-Dateien
(McpObservabilityOptions, ArgumentSanitizer, LogRecords, ToolCallLoggingHandler)
erweitert, 3 neue Test-Dateien (ToolCallRecordSchemaStabilityTests,
ResponseLoggingTests, RequestFullLoggingTests) angelegt, ArgumentSanitizerTests
um 3 Cases erweitert, JsonlLogWriterTests mechanisch aktualisiert. Die
JSONL-Schema-Invariante §5 bleibt gewahrt: `ToolCallRecordSchemaStabilityTests`
verifiziert byte-identischen v1.0.0-Output bei `EnableResponseLogging = false`.
Build grün (0 Warnungen, 0 Fehler unter `TreatWarningsAsErrors`), alle
41 Tests grün, AiNetLinter `RunLinterShouldBeClean` clean (siehe Bemerkung
in Beobachtungen zur Tool-Version).

## Geänderte Dateien

- `src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs` — zwei
  additive `public`-Properties (`EnableResponseLogging` default `true`,
  `MaxResponseLength` default `0`) plus XML-Doc-Update an
  `AdditionalSensitiveKeys` von „inert" auf „aktiv konsumiert seit EPIC-02".
- `src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs` —
  komplette Neufassung: generalisierte `Sanitize(object?, IEnumerable<string>?)`
  (akzeptiert `IReadOnlyDictionary<string, JsonElement>`, `Dict<string, object?>`,
  `JsonObject`, `IDictionary<string, object?>`), neuer `Sanitize(string?, …)`-
  Overload für Response-Strings (zwei Regex-Patterns pro Key), direkte
  `JsonElement`-Traversierung (kein `JsonNode.Parse`-Round-Trip mehr),
  `JsonValueKind.Null` → echtes `null`. `SensitiveKeys` wird per
  `BuildKeySet` mit `additionalKeys` gemerged.
- `src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs` — `ToolCallRecord`:
  `Arguments`-Typ-Wechsel auf `IReadOnlyDictionary<string, object?>?` plus
  5 additive Konstruktor-Parameter (`Response`, `ResponseLength`,
  `ResponseLines`, `ResponseTruncated`, `NonTextContentBlocks`) vor
  `ErrorMessage` mit `JsonIgnoreCondition.WhenWritingNull/Default`. Feedback-
  Record bleibt unverändert.
- `src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs` —
  `CreateRecord` ruft generalisierten Sanitizer ohne Cast, `ExtractResponse`
  (neu, `internal static`) extrahiert TextContent-Blocks, zählt non-text
  blocks, wendet Sanitizer auf Response an, truncate bei
  `MaxResponseLength > 0`. `ResponseExtraction` als top-level `internal
  readonly record struct` (AiNetLinter `BanPublicNestedTypes`).
- `tests/.../Internal/ArgumentSanitizerTests.cs` (erweitert) — 3 neue
  Cases (`Sanitize_AcceptsDictionaryOfObject_AndProducesSameOutputAsJsonElementInput`,
  `Sanitize_AcceptsJsonObject_AndRedactsNestedSensitiveKeys`,
  `Sanitize_String_Overload_RedactsKeyValuePairs`) plus alle 4 bestehenden
  Cases an die neue `IReadOnlyDictionary<string, object?>?`-Rückgabe angepasst.
- `tests/.../Internal/JsonlLogWriterTests.cs` (Regression-Update) — 5 neue
  positional args (`Response: null`, `ResponseLength: 0`, `ResponseLines: 0`,
  `ResponseTruncated: false`, `NonTextContentBlocks: 0`) vor `ErrorMessage`.
- `tests/.../Internal/ToolCallRecordSchemaStabilityTests.cs` (neu) — zwei
  Cases: byte-Identität gegen hartkodiertes v1.0.0-Baseline-JSON bei
  `Response = null` und allen Defaults; serialisiert Response-Felder bei
  gesetzten Non-Default-Werten.
- `tests/.../Internal/ResponseLoggingTests.cs` (neu) — 5 Cases (true/false,
  MaxLen 100, IsError, nonTextContentBlocks) gegen `ExtractResponse` direkt
  (via `InternalsVisibleTo`).
- `tests/.../Internal/RequestFullLoggingTests.cs` (neu) — 3 Cases (Top-Level-
  Keys inkl. null, komplexe Typen inkl. DateTime/Guid/Array, AdditionalSensitiveKeys).

## Commit

- **Code-Commit-Hash:** `50ac699`
- **Message:**
  ```
  feat(observability): sanitizer generalisiert und response-feld ergaenzt [refine]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin —
  Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build --configuration Release → grün (0 Warnungen, 0 Fehler)
dotnet test  --configuration Release --verbosity normal → grün (41 Tests, 0 Fehler, 0 übersprungen, 10 s)
```

AiNetLinter `RunLinterShouldBeClean` lief im selben Testlauf mit und war
clean — der Side-Effect `--sync-agent-rules` hat die
`.agents/rules/AiNetLinter.mdc` ggf. auf eine neuere Linter-Tool-Version
gezogen; dieser Diff gehört in den nachfolgenden Doku-Commit.

## Abweichungen vom Plan

- **MCP-SDK-Block-Typen heißen in 2.2.0 `*ContentBlock`, nicht `*Content`.**
  Der Plan nannte `TextContent`, `ImageContent`, `AudioContent`,
  `EmbeddedResource` — diese gibt es in `ModelContextProtocol.Protocol` 2.2.0
  nicht. Tatsächliche Typen: `TextContentBlock`, `ImageContentBlock`,
  `AudioContentBlock`, `EmbeddedResourceBlock`. Konzept und Step-Plan waren
  auf einer älteren SDK-Version geschrieben (oder die Typen sind beim
  2.2.0-Refactor umbenannt worden). Verhalten identisch zum Plan;
  `EmbeddedResourceBlock.Resource` muss im Objekt-Initialisierer gesetzt
  werden (required member).
- **`JsonIgnoreAttribute(JsonIgnoreCondition)`-Konstruktor existiert in
  .NET 10 nicht mehr.** Der Compiler warf `CS1729` mit der im Plan
  skizzierten Syntax. .NET 10 hat den parameterlosen Konstruktor
  beibehalten und nur die `Condition`-Property als settable belassen.
  Behoben mit der named-argument-Syntax
  `[property: JsonIgnoreAttribute(Condition = JsonIgnoreCondition.WhenWritingNull)]`.
  Verhalten identisch zum Plan; semantisch dasselbe wie die nicht
  (mehr) existierende Konstruktor-Überladung.
- **`ExtractResponse` und `ResponseExtraction` als `internal` deklariert,
  nicht `private`.** Der Plan skizzierte `private` Out-Parameter. Ich habe
  auf einen `internal static`-Helper + `internal readonly record struct`
  Returntyp umgestellt (statt 5 Out-Params). Das hält `ExtractResponse`
  unter dem 60-Zeilen-Limit, hält die Komplexität pro Methode
  unter 12 (CC), und macht die Methode für `ResponseLoggingTests` direkt
  testbar (via `InternalsVisibleTo`, das bereits gesetzt war). Die Tests
  prüfen Verhalten statt Implementierungsdetail.
- **Anonyme `*_options.RecordType`-Tests statt direkter `CallToolResult`-
  Default-Konstruktor.** In MCP-SDK 2.2.0 hat `CallToolResult` einen
  parameterlosen Konstruktor und `Content` ist `IList<ContentBlock>?` mit
  `init`-Setter — ich nutze `new CallToolResult { Content = new List<…> { … } }`
  statt manueller Property-Sets. Kein Verhaltensunterschied.
- **CA1859-Rückgabetyp-Anpassung in privaten Helper-Methoden von
  `ArgumentSanitizer`.** `SanitizeFromJsonElementDict`/`ObjectDict`/
  `JsonObject` geben jetzt `Dictionary<string, object?>` zurück (statt
  `IReadOnlyDictionary<string, object?>`); die public `Sanitize`-Methode
  gibt weiterhin `IReadOnlyDictionary<string, object?>?` zurück.
  Vermeidet die Boxing-Warnung des .NET-10-Analyzers.

## Beobachtungen

- **`JsonValueKind.Null` → echtes `null` ist eine kleine Schema-Änderung
  im JSON-Output.** Vorher: ein `JsonElement` mit `ValueKind = Null` wurde
  als nativer `null`-Wert im JSON-Output serialisiert (visuell identisch
  zu `null`, aber Wert-Identity unterschiedlich). Nachher: tatsächlicher
  `null`-Wert im `Dictionary<string, object?>`. Visueller JSON-Output
  bleibt gleich (`"d": null`); Konzept-Text („Nichts wird stillschweigend
  verworfen") wird strikt eingehalten. Das ist der Grund, warum der
  `Sanitize_PreservesAllTopLevelKeysIncludingNull`-Test jetzt grün ist.
- **`ImageContentBlock.FromBytes` / `AudioContentBlock.FromBytes` haben
  keinen parameterlosen Konstruktor in MCP-SDK 2.2.0** — sie MÜSSEN via
  Factory erzeugt werden. Die Tests nutzen das korrekt, aber ein
  Konsument, der eigene `ContentBlock`-Subtypen baut, muss das ebenfalls.
- **`EnableResponseLogging = false` triggert `ResponseLength` /
  `ResponseLines` / `NonTextContentBlocks` weiterhin in `ExtractResponse`.**
  Diese werden vor der `if (!options.EnableResponseLogging)`-Verzweigung
  gemessen und ins Record geschrieben — aber im JSON-Output per
  `JsonIgnoreCondition.WhenWritingDefault` weggelassen (alle haben dann
  den Default-Wert). Das ist genau die `Designentscheidung 1` aus dem
  Plan. `ToolCallRecordSchemaStabilityTests` verifiziert die
  byte-Identität.
- **AiNetLinter-Side-Effect während des Test-Runs** (Side-Effect aus
  step-001, weiterhin aktiv): der `AiNetLinterTests.RunLinterShouldBeClean`
  ruft am Ende `--sync-agent-rules` auf, was die generierte
  `.agents/rules/AiNetLinter.mdc` ggf. auf eine neuere Linter-Tool-Version
  zieht. Dieser Diff ist **nicht** Teil des Code-Commits (nicht in der
  gezielten `git add`-Liste); ich übergebe ihn an den Doku-Commit.
- **Cycle- und Cognitive-Complexity in `ExtractResponse` waren im
  ersten Wurf über den Limits** (CC=13, Cyclomatic=14 — Grenzwerte
  12/10). Behoben durch drei private Helper: `ConcatResponseContent`
  (Loop + switch), `AppendTextBlock` (nur `'\n'`-Join), `BuildResponseText`
  (Sanitize + Truncation). Endstand CC=2, Cyclomatic=2 pro Helper,
  `ExtractResponse` selbst bei CC=3, Cyclomatic=4 — deutlich unter Limit.
- **Beim ersten Test-Lauf fielen 6 Tests aus drei Ursachen:** (a) `JsonValueKind.Null`
  wurde nicht zu echtem `null` (Sanitizer-Bug, gefixt); (b) Tests haben
  versucht, `JsonElement` aus verschachtelten Dict-Werten zu casten, wo
  der Sanitizer bereits `Dictionary<string, object?>` produziert (Test-
  Anpassung, gefixt); (c) Test-Literale waren auf eine Annahme
  („secret output" = 12 chars) gestützt, die um 1 daneben lag (Test-
  Anpassung, gefixt). Alles innerhalb des 3-Versuchs-Budgets.

## Bekannte Unschärfen

- **`MaxResponseLength`-Truncation-Marker ist auf Englisch** (`"... [truncated at N chars]"`).
  Konsistent mit dem Konzept, das den Marker exakt so spezifiziert. Bei
  späterer i18n wäre das ein Tech-Debt-Eintrag.
- **`Response`-Sanitization per Regex kann `key=value` mit mehreren `=`-
  Vorkommen falsch behandeln** (z. B. `password=ab=cd` wird zu
  `password=***REDACTED***` — der zweite `=` verschwindet). Konzept
  akzeptiert das als „Defense-in-Depth, nicht perfekte Heuristik". Ein
  späterer Schritt könnte auf eine Tokenizer-basierte Lösung umstellen.
- **JsonIgnore-Condition-Semantik auf positional Records via `[property:]`-
  Target ist im System.Text.Json-Source-Generator nicht offiziell
  dokumentiert** — funktioniert in .NET 10 (Test bestätigt), aber bei
  zukünftigen STJ-Versionen könnte das Verhalten brechen. Aktuell
  unkritisch.
- **`ResponseExtraction.Empty` ist als statisches `readonly` Feld
  deklariert**, was eine theoretische Race-Condition mit sich bringt, falls
  die Methode multithreaded aufgerufen würde (initiale Zuweisung). Da
  `readonly` aber nur einmal im Type-Initializer passiert und der Struct
  keine veränderlichen Referenzen hat, ist das in der Praxis sicher.
- **MCP-SDK-Typnamen-Bruch:** wenn die SDK-Version in einem späteren
  Schritt zurück auf `*Content` (ohne `Block`) geht, müssten die Switch-
  Cases in `ConcatResponseContent` und der Test-Code angepasst werden.
  Aktuelle Step-Result-Aufzeichnung enthält die korrekten 2.2.0-Namen.
- **Doku-Commit** (mit `step-result.md`, `codemap.md`, ggf. AiNetLinter.mdc)
  ist Teil von Schritt 7 und steht noch aus. Der Code-Commit `50ac699`
  ist self-contained und abgeschlossen.

## Falls Status `blocked`

Nicht zutreffend.
