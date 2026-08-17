---
status: done (pending audit)
type: step-plan
task: refine
step: 002
corrects: null
title: "Sanitizer-Generalisierung, LogRecord-Type-Wechsel und Response-Logging"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T23:08:00+02:00
related_to:
  - step-001/step-result.md
  - step-001/step-review.md
---

# Step 002: Sanitizer-Generalisierung, LogRecord-Type-Wechsel und Response-Logging

## Bezug

- **Task:** `refine`
- **Epic:** `EPIC-02` aus `roadmap.md` — der größte Block des Tasks.
- **Konzept-Referenz:** `Konzept.md` §„Muss-Haven" → Bullets
  - „Optionen-Erweiterung" (für `EnableResponseLogging` + `MaxResponseLength`),
  - „ArgumentSanitizer generalisieren" (komplett),
  - „Response-Logging" (komplett),
  - „JSONL-Schema-Stabilität" (`Arguments`-Type-Wechsel + 5 additive Felder),
  - „Tests" Punkte 5, 6, 7 (`ToolCallRecordSchemaStabilityTests`,
    `ResponseLoggingTests`, `RequestFullLoggingTests`).
  Konzept §„Entdeckte Mängel/Redundanzen" → Mitdenken-Funde
  - „`ArgumentSanitizer.SanitizeElement` ist ineffizient" (JsonNode.Parse-Elimination),
  - „`CallToolResult.Content` ist polymorph" (Filter-Logik für non-Text-Blöcke).
  Konzept §„Wie" → Schritt 2 (komplette Bündelung).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Quellcodes vorgefunden — beeinflusst den Plan:

- **`McpObservabilityOptions`** (`src/.../McpObservabilityOptions.cs:1-76`):
  hat nach step-001 bereits 4 additive `public`-Properties
  (`ServerName`, `ServerVersion`, `FeedbackConfirmationMessage`,
  `AdditionalSensitiveKeys`) + 1 `public const`. `AdditionalSensitiveKeys`
  ist heute **inert** (kein Konsument liest die Property); EPIC-02
  aktiviert sie, indem der `ArgumentSanitizer` sie als zusätzliche Quelle
  für die `SensitiveKeys`-Menge konsumiert (Konzept: „wird ab EPIC-02 vom
  `ArgumentSanitizer` zusätzlich zur hartkodierten Default-Liste
  berücksichtigt"). XML-Doc-Hinweis im File ist zu aktualisieren, da der
  „Inert"-Hinweis dann obsolet ist.
- **`ArgumentSanitizer`** (`src/.../Internal/ArgumentSanitizer.cs:1-103`):
  - Signatur heute: `Sanitize(IReadOnlyDictionary<string, JsonElement>?)`
    — der einzige Eingabetyp ist ein explizit typisiertes Dict mit
    `JsonElement`-Werten. Rückgabe: `IReadOnlyDictionary<string, JsonElement>?`
    (Werte bleiben `JsonElement`).
  - **Mitdenken-Fund (Konzept §„Entdeckte Mängel" Punkt 3):** die private
    `SanitizeElement(JsonElement)` macht `JsonNode.Parse(element.GetRawText())`
    + `JsonSerializer.SerializeToElement(node)` — unnötiger Round-Trip,
    besonders teuer bei großen verschachtelten Strukturen. Bei der
    Generalisierung soll dieser Round-Trip eliminiert werden.
  - `SensitiveKeys` ist heute eine statische `HashSet<string>` mit
    hartkodierten Defaults. `additionalKeys`-Parameter existiert noch
    nicht.
  - `Sanitize(string?, ...)`-Overload existiert noch nicht — wird neu
    eingeführt für Response-Strings.
- **`LogRecords.cs`** (`src/.../Internal/LogRecords.cs:9-22`):
  `ToolCallRecord` ist `internal sealed record` mit 14 Konstruktor-Params.
  `Arguments` ist heute `IReadOnlyDictionary<string, JsonElement>?`. 5
  additive Felder fehlen. `FeedbackRecord` bleibt **unverändert** (kein
  Response-Logging für Feedbacks — Konzept: „FeedbackRecord bleibt
  unverändert").
- **`ToolCallLoggingHandler`** (`src/.../Internal/ToolCallLoggingHandler.cs:54-95`):
  - `CreateRecord` macht heute den Cast
    `request.Params?.Arguments as IReadOnlyDictionary<string, System.Text.Json.JsonElement>`.
    Mit der generalisierten Sanitizer-Signatur entfällt dieser Cast.
  - Hat **keine** Response-Extraktion; `ErrorMessage` ist die einzige
    Stelle, die `result.Content` anfasst (nur `FirstOrDefault()?.ToString()`).
  - Hat keinen Zugriff auf `Options.EnableResponseLogging` /
    `Options.MaxResponseLength` — `ctx.Options` ist heute `internal`
    sichtbar, also lesbar (Konzept: „der Handler reicht
    `request.Params?.Arguments` ohne Cast direkt an `Sanitize` weiter").
- **`McpObservabilityOptions.AdditionalSensitiveKeys`** ist `HashSet<string>`
  mit `OrdinalIgnoreCase`-Vergleich (step-001). Die Übergabe erfolgt im
  Handler via `ctx.Options.AdditionalSensitiveKeys`.
- **Bestehende Tests, die den aktuellen Pfad absichern** (müssen alle
  grün bleiben):
  - `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`
    (Zeile 23-86) baut `Arguments = new Dictionary<string, JsonElement>`,
    erwartet `password` als `***REDACTED***`, andere Felder (text) als
    Originalwert. Bleibt grün, weil `Dictionary<string, JsonElement>` einer
    der akzeptierten Sanitizer-Inputs ist.
  - `JsonlLogWriterTests.WriteRecord_AppendsValidJsonLines` (Zeile 56-117)
    konstruiert `ToolCallRecord` **direkt** mit positional constructor —
    die Signature-Änderung von `Arguments` und die 5 neuen Felder machen
    hier einen Update des Tests nötig (siehe „Konkrete Änderungen → Tests
    → JsonlLogWriterTests-Regression-Update").
  - `McpFeedbackIntegrationTests` ist nicht betroffen (Feedback-Record
    bleibt unverändert).
  - `McpOptionsFlagsTests`, `McpOptionsServerNameOverrideTests` sind nicht
    betroffen (kein Sanitizer-Pfad, kein Response-Pfad).
  - `ArgumentSanitizerTests` ist betroffen (Signatur + 2 neue Cases für
    `Dictionary<string, object?>` und `JsonObject`).
- **Bestehende Source-Datei-Referenzen auf `IReadOnlyDictionary<string, JsonElement>`-Arguments:**
  - `McpObservabilityIntegrationTests.cs:53` — SDK-Aufrufseite, nicht
    anpassbar (das ist der SDK-Output).
  - `JsonlLogWriterTests.cs:64-77` — direkter Record-Konstruktor, muss
    angepasst werden (siehe oben).
- **CodeMap-Status:** Karte ist aktuell; die in EPIC-02 geplanten Änderungen
  sind als Annotation bereits verzeichnet (Zeile 86-103 + Tests-Block
  Zeile 132-134). Keine Lücken oder Widersprüche.

## Intention

EPIC-02 generalisiert den `ArgumentSanitizer` so, dass er beliebige
Dict-/JsonObject-Inputs akzeptiert und auch Response-Strings sanitiert,
eliminiert dabei den `JsonNode.Parse`-Round-Trip (Mitdenken-Fund), und
führt vollständiges Response-Logging im `ToolCallRecord` ein (5 additive
Felder + Sanitizer + Truncation). Die JSONL-Schema-Invariante §5 bleibt
gewahrt: bei `EnableResponseLogging = false` sind die 5 neuen Felder im
Output nicht sichtbar, der bestehende 14-Felder-Record bleibt
byte-identisch zu v1.0.0. Die `ToolCallRecord.Arguments`-Signatur
wechselt intern auf `IReadOnlyDictionary<string, object?>?`, was die
Generalisierung erst möglich macht — der JSON-Output bleibt durch die
Serialisierung der `JsonElement`-Werte byte-identisch.

## Konkrete Änderungen

### Datei 1: `src/RalfHuesing.Mcp.Observability/McpObservabilityOptions.cs` (Zeile 8-76, +2 Properties)

- **Was:** Zwei additive `public`-Properties am Ende der Klasse anhängen
  (vor `AdditionalSensitiveKeys`, gruppiert mit den Response-bezogenen
  Properties):
  - `public bool EnableResponseLogging { get; set; } = true;`
    mit XML-Doc: „Master-Schalter für das Response-Inhalt-Feld im
    `tool_call`-Record. Wenn `false`, werden `Response` und
    `ResponseTruncated` aus dem JSONL-Output weggelassen und
    `ResponseLength`/`ResponseLines`/`NonTextContentBlocks` weggelassen,
    wenn sie ihren Default-Wert haben. Konsumenten-spezifische Aktivierung
    via `appsettings.json` (kein globaler Default-Wert hier). Default:
    `true`."
  - `public int MaxResponseLength { get; set; }` mit XML-Doc:
    „Harte Längengrenze in Zeichen für den `response`-String. Bei `> 0`
    und Response-Länge > Limit: kürzen + Truncation-Marker
    `… [truncated at N chars]` anhängen. Bei `0` (Default) kein
    Truncation. Wirkt nur, wenn `EnableResponseLogging = true`. Wird in
    `appsettings.json` des konsumierenden Servers gesetzt (kein globaler
    Default)."
  - **Außerdem:** Den XML-Doc-Kommentar an `AdditionalSensitiveKeys`
    (Zeile 68-75) anpassen: „wird vom `ArgumentSanitizer` zusätzlich zur
    hartkodierten Default-Liste berücksichtigt (seit EPIC-02 aktiv
    konsumiert)" statt „inert in diesem Release".
- **Warum:** Konzept §„Response-Logging" verlangt genau diese zwei
  Properties mit den dokumentierten Defaults. `AdditionalSensitiveKeys`
  wird in diesem Step aktiviert — die Inert-Markierung im XML-Doc ist
  dann obsolet und muss weg, sonst stimmt Doku nicht mit Verhalten
  überein (Richtlinie §8 „Dokumentations-Objektivität").

### Datei 2: `src/RalfHuesing.Mcp.Observability/Internal/ArgumentSanitizer.cs` (~komplette Neufassung)

- **Was:**
  - Zwei neue `private static readonly`-Mengen, die zur Laufzeit gemerged
    werden:
    - `SensitiveKeys` (wie bisher, hartkodiert) bleibt als `private static
      readonly HashSet<string> ... = new(StringComparer.OrdinalIgnoreCase)`.
    - Neue private Methode `private static HashSet<string> BuildKeySet(IEnumerable<string>? additional)`
      kombiniert die hartkodierte `SensitiveKeys` mit `additional` (in
      einen neuen `HashSet<string>` mit `OrdinalIgnoreCase` — damit der
      Aufrufer die übergebene Collection nicht manipulieren kann und die
      Default-Liste nicht wächst).
  - **Generalisierte Hauptmethode (neu):**
    `internal static IReadOnlyDictionary<string, object?>? Sanitize(object? rawArguments, IEnumerable<string>? additionalKeys = null)`.
    Erkennt den Eingabetyp via `is`-Pattern-Matching (Reihenfolge der
    Try-Matches spezifisch → generisch):
    1. `null` → `null` zurück.
    2. `IReadOnlyDictionary<string, JsonElement>?` (heutiger Pfad) →
      Verarbeitung wie bisher, Werte als `JsonElement` weiterreichen
      (siehe „Optimierter Pfad" unten).
    3. `IReadOnlyDictionary<string, object?>` → aufzählen, jeden Wert
      durch `SanitizeValue` jagen.
    4. `JsonObject` → über `JsonObject`-Properties aufzählen (jeder Wert
      kann `JsonNode`/`JsonElement`/beliebiger Typ sein), Ergebnis in
      `Dictionary<string, object?>`.
    5. Beliebiges `IDictionary<string, object?>` → Fallback: über
      `IEnumerable<KeyValuePair>` enumerieren.
    6. Sonst (nicht unterstützt) → leeres Dictionary zurück
      (defensiv — Konzept „nichts stillschweigend verwerfen" wird hier
      bewusst gebrochen für unbekannte Typen; alternativ könnte man
      `throw new ArgumentException` werfen, aber der Aufrufer in
      `ToolCallLoggingHandler` darf nicht crashen). **Hinweis:** dieser
      Fall kommt in der Praxis nicht vor (der SDK-Output ist immer
      `IReadOnlyDictionary<string, JsonElement>?` oder `null`); die
      leere-Dict-Rückgabe ist nur ein Defensiv-Schutz.
    Rückgabe: `IReadOnlyDictionary<string, object?>?` — Werte als
    `object?` (im JSON-Output via `System.Text.Json`-Serialisierung als
    `JsonElement`/String/Number/etc. passend dargestellt).
  - **Neue `Sanitize(string?, IEnumerable<string>?)`-Overload für
    Response-Strings:**
    `internal static string? Sanitize(string? rawText, IEnumerable<string>? additionalKeys = null)`.
    - Bei `null`/`leer` → unverändert zurück.
    - Sonst: zwei Regex-Pattern pro Key (siehe „Notes" → Designentscheidung 2):
      1. `key=value` (Wert endet an Whitespace, Komma oder Semikolon).
      2. `"key":"value"` (JSON-artig, Wert endet am nächsten `"`).
    - `RegexOptions.IgnoreCase`, `Regex.Escape(key)` zur sicheren
      Interpolation. Reihenfolge: erst alle Keys durchlaufen, für jeden
      Key beide Patterns anwenden (nicht beide Patterns über alle Keys —
      wäre effizienter, aber unleserlicher; bei <20 Keys kein
      messbarer Unterschied).
  - **Optimierter Pfad — `JsonNode.Parse`-Elimination** (Mitdenken-Fund):
    Statt `JsonNode.Parse(element.GetRawText()) + JsonSerializer.SerializeToElement(node)`
    baut eine neue private Methode
    `private static object? SanitizeValue(JsonElement element, HashSet<string> keys)`
    das Ergebnis direkt auf:
    - Bei `JsonValueKind.Object`: neues `Dictionary<string, object?>`
      befüllen, für jeden Property-Key prüfen ob in `keys` (→ Redacted
      einsetzen) und rekursiv in den Wert absteigen.
    - Bei `JsonValueKind.Array`: neue `List<object?>` befüllen, jeden
      Eintrag rekursiv sanitizen.
    - Sonst (Primitive): den ursprünglichen `JsonElement` direkt
      weiterreichen (kein Round-Trip — `System.Text.Json` serialisiert
      `JsonElement` als seine native JSON-Repräsentation).
    Das ist genau die Generalisierung des heutigen
    `JsonNode`-basierten `SanitizeElement` auf `object?`-Werte, **ohne**
    den Parse-Round-Trip. Identische Semantik, keine Allokation der
    `JsonNode.Parse` + `JsonSerializer.SerializeToElement`-Zwischenstufe.
  - Bestehende `SanitizeObject`/`SanitizeArray`/`SanitizeNode`-
    Hilfsmethoden (Zeilen 66-102) entfallen komplett — sie werden durch
    `SanitizeValue` ersetzt.
- **Warum:** Konzept §„ArgumentSanitizer generalisieren" verlangt die
  neuen Eingabetypen + den zusätzlichen `Sanitize(string?, ...)`-Overload.
  Die `JsonNode.Parse`-Elimination ist im Konzept §„Entdeckte Mängel"
  Punkt 3 explizit als Optimierung in EPIC-02 verankert. Beide Änderungen
  greifen in dieselbe Datei → ein Schritt.

### Datei 3: `src/RalfHuesing.Mcp.Observability/Internal/LogRecords.cs` (Zeile 9-22, +5 Felder + 1 Typ-Wechsel)

- **Was:**
  - `ToolCallRecord.Arguments`: Typ-Wechsel von
    `IReadOnlyDictionary<string, JsonElement>?` auf
    `IReadOnlyDictionary<string, object?>?`. Die `System.Text.Json`-
    Serialisierung schreibt jedes Wert-Objekt polymorph — `JsonElement`
    wird als JSON, `string` als String, `Dictionary<string, object?>`
    als JSON-Objekt, `List<object?>` als JSON-Array, `null` als `null`.
    → JSON-Output bleibt byte-identisch (siehe `ToolCallRecordSchemaStabilityTests`).
  - 5 additive Konstruktor-Parameter (am Ende der Parameterliste, vor
    `ErrorMessage`, um die existierende Position von `ErrorMessage` nicht
    zu verschieben — wichtig für Regressions-Test
    `JsonlLogWriterTests.WriteRecord_AppendsValidJsonLines` der das
    Record positional konstruiert; durch Reihenfolge-Tausch wird das
    Update mechanisch):
    1. `string? Response` — XML-Doc: „Konkatenierte `TextContent`-Blöcke
       aus dem Tool-Response (sanitized, `\n`-getrennt). `null`, wenn
       `EnableResponseLogging = false`."
    2. `int ResponseLength` — „Gesamtanzahl Zeichen des **unkürzten,
       unsanitized** Response-Textes (vor Sanitization + Truncation
       gemessen)."
    3. `int ResponseLines` — „Zeilenanzahl des unkürzten Response-Textes
       (gezählt anhand `\n`-Trennern + 1, wenn Text nicht leer)."
    4. `bool ResponseTruncated` — „`true`, wenn Truncation-Marker
       angehängt wurde."
    5. `int NonTextContentBlocks` — „Anzahl `ImageContent`/
       `AudioContent`/`EmbeddedResource`-Blöcke im Response."
  - Alle 5 Felder mit `[JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)]`
    (aus `System.Text.Json.Serialization`) attributieren:
    - `Response` (string?): `WhenWritingNull` (omittet, wenn `null`).
    - `ResponseLength` (int): `WhenWritingDefault` (omittet bei `0`).
    - `ResponseLines` (int): `WhenWritingDefault`.
    - `ResponseTruncated` (bool): `WhenWritingDefault` (omittet bei `false`).
    - `NonTextContentBlocks` (int): `WhenWritingDefault`.
    → Effekt: bei `EnableResponseLogging = false` setzt der Handler alle
    5 Felder auf `null`/`0`/`false` und sie werden im JSON weggelassen —
    der Output ist **byte-identisch** zu v1.0.0. Bei
    `EnableResponseLogging = true` und nicht-leerem Response werden
    alle 5 Felder mit ihren tatsächlichen Werten geschrieben.
  - `FeedbackRecord` bleibt **unverändert** (Konzept: „bleibt unverändert
    (kein Response-Logging für Feedbacks)"). Kein zusätzlicher
    `JsonIgnore`-Import nötig, wenn `JsonIgnoreCondition` aus
    `System.Text.Json.Serialization` kommt — der Using-Block oben in
    der Datei muss ggf. ergänzt werden.
- **Warum:** Konzept §„JSONL-Schema-Stabilität" verlangt den
  Type-Wechsel; Konzept §„Response-Logging" verlangt die 5 additiven
  Felder. Die `JsonIgnore`-Attribute sichern die byte-identische
  Schema-Invariante ab, ohne den Record semantisch zu verkomplizieren.

### Datei 4: `src/RalfHuesing.Mcp.Observability/Internal/ToolCallLoggingHandler.cs` (Zeile 54-95, ~komplette Neufassung von `CreateRecord`)

- **Was:** `CreateRecord` umbauen:
  - `var additionalKeys = ctx.Options.AdditionalSensitiveKeys;`
  - `var sanitized = ArgumentSanitizer.Sanitize(request.Params?.Arguments, additionalKeys);`
    (kein Cast mehr — der generalisierte Sanitizer akzeptiert `object?`).
  - **Response-Verarbeitung** in neue private Methode
    `ExtractResponse(result, additionalKeys, options, out string? response, out int length, out int lines, out bool truncated, out int nonTextCount)`
    extrahieren, damit `CreateRecord` selbst unter dem 60-Zeilen-Limit
    bleibt (AiNetLinter §Grenzwerte):
    1. Wenn `result?.Content` `null` oder leer → alle 5 Out-Parameter auf
       Default (`null`/`0`/`false`).
    2. `StringBuilder responseBuilder` (initial leer).
    3. `int nonTextCount = 0;`
    4. Foreach über `result.Content`:
       - `if (block is TextContent text)` → `if (responseBuilder.Length > 0) responseBuilder.Append('\n'); responseBuilder.Append(text.Text);`
       - `else if (block is ImageContent or AudioContent or EmbeddedResource)` → `nonTextCount++;`
       - else (defensiv, sollte nicht vorkommen) → `nonTextCount++;`
         (Konzept: „Erweiterung um binäre Inhalte bleibt explizit
         Non-Goal für v1.1" — alle nicht-Text-Typen werden konsistent
         als non-Text gezählt, niemals ins `response`-Fald geschrieben).
    5. `var rawText = responseBuilder.ToString();`
    6. `length = rawText.Length;` — **vor** Sanitization.
    7. `lines = string.IsNullOrEmpty(rawText) ? 0 : rawText.Count(c => c == '\n') + 1;`
       — **vor** Sanitization.
    8. Wenn `options.EnableResponseLogging`:
       - `var sanitizedText = ArgumentSanitizer.Sanitize(rawText, additionalKeys) ?? string.Empty;`
       - Wenn `options.MaxResponseLength > 0 && sanitizedText.Length > options.MaxResponseLength`:
         `sanitizedText = sanitizedText.Substring(0, options.MaxResponseLength) + "… [truncated at " + options.MaxResponseLength + " chars]";`
         `truncated = true;`
       - `response = sanitizedText;`
       Sonst: `response = null; truncated = false;` (die anderen Out-Params
       behalten die gemessenen Werte, werden aber via JsonIgnore im
       Output ohnehin weggelassen, wenn 0).
    9. `nonTextCount` zurückgeben.
  - `record = new ToolCallRecord(... Arguments: sanitized, ..., Response: response, ResponseLength: length, ResponseLines: lines, ResponseTruncated: truncated, NonTextContentBlocks: nonTextCount, ErrorMessage: ...);`
- **Warum:** Konzept §„Response-Logging" + §„ArgumentSanitizer
  generalisieren" + Mitdenken-Fund „`CallToolResult.Content` ist
  polymorph" (Filter-Logik). Extraktion in eigene Methode hält
  `CreateRecord` unter 60 Zeilen.

### Datei 5: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ArgumentSanitizerTests.cs` (erweitert, +2-3 Cases)

- **Was:** Drei zusätzliche `[Fact]`-Cases an die bestehende
  Test-Klasse anhängen:
  1. `Sanitize_AcceptsDictionaryOfObject_AndProducesSameOutputAsJsonElementInput`:
     Inputs sind je ein `Dictionary<string, object?>` und ein
     `IReadOnlyDictionary<string, JsonElement>` mit identischen
     Werten (z. B. `{ ["text"] = "hello", ["limit"] = 50,
     ["nested"] = new Dictionary<string, object?> { ["k"] = "v" } }`).
     Erwartung: beide liefern JSON-Output, der via
     `JsonSerializer.SerializeToElement(sanitized)` **denselben** JSON-
     String ergibt (Round-Trip-Identität).
  2. `Sanitize_AcceptsJsonObject_AndRedactsNestedSensitiveKeys`:
     Input ist ein `JsonObject` (aus `System.Text.Json.Nodes`),
     befüllt mit Properties, darunter ein `password` in einer
     verschachtelten `JsonObject`. Erwartung: der Sanitizer redacted
     `password` auch in der verschachtelten Ebene und liefert einen
     `IReadOnlyDictionary<string, object?>` zurück, der die ursprüngliche
     Struktur (ohne Redaction) abbildet.
  3. `Sanitize_String_Overload_RedactsKeyValuePairs` (neu für den
     `Sanitize(string?, ...)`-Overload, der in Datei 2 hinzukommt):
     Input-Strings: `"token=abc123 foo=bar"`,
     `'"sessionId":"xyz" password=pw'`. Erwartung: `token`, `sessionId`,
     `password` werden ersetzt (anhand hartkodierter + zusätzlicher
     Keys), `foo`/`bar` bleiben unverändert.
- **Warum:** Konzept §„Tests" Punkt 1 (Cases für `Dictionary<string, object?>`
  + `JsonObject`) + Konzept §„Tests" Punkt 5 (Sanitizer auf Response).

### Datei 6: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ToolCallRecordSchemaStabilityTests.cs` (neu)

- **Was:** Neue Datei mit zwei `[Fact]`-Cases:
  1. `ToolCallRecord_WithResponseLoggingDisabled_IsByteIdenticalToV1_0_0`:
     - Erstellt ein `ToolCallRecord` mit `Arguments = new Dictionary<string, JsonElement>`
       (genau die Eingabe aus
       `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`).
     - Setzt `Response = null`, `ResponseLength = 0`, `ResponseLines = 0`,
       `ResponseTruncated = false`, `NonTextContentBlocks = 0`.
     - Serialisiert via `JsonSerializer.Serialize(record, JsonlSerializerOptions.Default)`.
     - **Baseline:** ein hartkodiertes JSON-String-Literal, das exakt
       dem v1.0.0-Output entspricht (14 Felder, camelCase, keine der
       5 neuen Felder, da alle per `JsonIgnoreCondition.WhenWritingDefault`
       weggelassen werden). Das String-Literal wird im Test selbst
       definiert (nicht aus v1.0.0-Snapshot geladen — der Test muss
       in sich geschlossen sein).
     - Erwartung: `Assert.Equal(baseline, actual);` (byte-genauer
       Vergleich).
  2. `ToolCallRecord_WithResponseLoggingEnabled_ContainsResponseFields`:
     - Erstellt ein `ToolCallRecord` mit denselben Werten wie oben,
       aber `Response = "echo:hello"`, `ResponseLength = 11`,
       `ResponseLines = 1`, `ResponseTruncated = false`,
       `NonTextContentBlocks = 0`.
     - Serialisiert, parst via `JsonDocument`.
     - Erwartung: alle 5 neuen Felder sind im JSON vorhanden mit den
       korrekten Werten (bestätigt, dass `JsonIgnore` nur bei
       Default-Werten greift).
- **Warum:** Konzept §„JSONL-Schema-Stabilität" + §„Tests" Punkt 5.

### Datei 7: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/ResponseLoggingTests.cs` (neu)

- **Was:** Neue Datei mit fünf `[Fact]`-Cases, die alle per direktem
  `ToolCallRecord`-Konstruktion arbeiten (kein Integration-Test-Overhead
  nötig — der Handler-Pfad ist in Datei 4 mit klar definierten Out-
  Parametern isoliert; eine End-to-End-Integration ist nicht in
  EPIC-02 gefordert, weil die `McpObservabilityIntegrationTests` den
  Happy-Path bereits abdecken):
  1. `Response_EnableResponseLogging_True_AppearsConcatenatedWithNewline`:
     Konstruiert einen `CallToolResult` mit zwei `TextContent`-Blöcken
     (`"a"`, `"b"`), ruft `ExtractResponse`-Logik nach (über einen
     Test-internen Aufbau oder über `ToolCallLoggingHandler` mit einem
     `request`/`result`-Mock — siehe „Notes" → Designentscheidung 3 für
     die Coder-Wahl). Erwartung: `Response = "a\nb"`,
     `ResponseLength = 3` (vor Sanitization), `ResponseLines = 2`,
     `ResponseTruncated = false`, `NonTextContentBlocks = 0`.
  2. `Response_EnableResponseLogging_False_AllFieldsAreDefaults`:
     Wie oben, aber `Options.EnableResponseLogging = false`. Erwartung:
     `Response = null`, die anderen 4 Felder `0`/`false` (in Code;
     im JSON weggelassen per `JsonIgnore`).
  3. `Response_MaxResponseLength_TruncatesAndAddsMarker`:
     Response-Text 250 Zeichen lang, `Options.MaxResponseLength = 100`.
     Erwartung: `Response` ist 100 Zeichen lang + Marker
     `… [truncated at 100 chars]`, `ResponseLength = 250` (echte Länge,
     vor Truncation), `ResponseTruncated = true`.
  4. `Response_IsErrorResult_True_ContainsErrorText`:
     `CallToolResult.IsError = true`, `Content = [TextContent("boom"),
     TextContent("details")]`. Erwartung: `Response = "boom\ndetails"`
     (Voltext, sanitized), `ErrorMessage` (aus dem bestehenden
     `ExtractErrorMessage`-Helper) ist `"boom"` (Summary = erster Block).
  5. `Response_NonTextContentBlocks_AreCountedAndNotInResponse`:
     `Content = [TextContent("ok"), ImageContent(...),
     AudioContent(...), EmbeddedResource(...)]`. Erwartung:
     `Response = "ok"`, `NonTextContentBlocks = 3`. Das `ImageContent`/
     `AudioContent`/`EmbeddedResource` darf nicht im `Response`-String
     auftauchen (kein Base64-Bleed).
- **Warum:** Konzept §„Tests" Punkt 6 (alle 5+1 Varianten).

### Datei 8: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/RequestFullLoggingTests.cs` (neu)

- **Was:** Neue Datei mit drei `[Fact]`-Cases (alle arbeiten über
  `ArgumentSanitizer.Sitize` direkt — keine Record-/Handler-Indirektion
  nötig):
  1. `Sanitize_PreservesAllTopLevelKeysIncludingNull`:
     Input: `Dictionary<string, JsonElement>` mit Keys `["a", "b",
     "c", "d", "e"]` und Werten `1`, `"text"`, `true`, `null`, komplexem
     Objekt. Erwartung: alle 5 Keys erscheinen im sanitized Dictionary,
     inkl. `d = null` (nicht ausgelassen).
  2. `Sanitize_PreservesComplexTypesAndCollections`:
     Input mit `DateTime.UtcNow`, `Guid.NewGuid()`, `new[] { 1, 2, 3 }`,
     verschachteltem `Dictionary<string, object?>`. Erwartung: alle
     Werte erscheinen im sanitized Output (nicht gefiltert, nicht
     komprimiert). Verifikation via `JsonSerializer.SerializeToElement`
     und Property-Vergleich.
  3. `Sanitize_HonorsAdditionalSensitiveKeys`:
     `options.AdditionalSensitiveKeys = new(StringComparer.OrdinalIgnoreCase) { "sessionId" }`.
     Input: `Dictionary<string, JsonElement> { ["sessionId"] = "abc",
     ["user"] = "alice" }`. Erwartung: `sessionId` wird durch
     `***REDACTED***` ersetzt, `user` bleibt.
- **Warum:** Konzept §„Tests" Punkt 7 (Vollständigkeit des Request-Logs).

### Datei 9: `tests/RalfHuesing.Mcp.Observability.Tests/Internal/JsonlLogWriterTests.cs` (Regressions-Update)

- **Was:** Im Test `WriteRecord_AppendsValidJsonLines` (Zeile 56-117)
  den `ToolCallRecord`-Konstruktor-Aufruf (Zeile 64-77) an die neue
  Signatur anpassen: 5 neue Parameter (`Response: null`,
  `ResponseLength: 0`, `ResponseLines: 0`, `ResponseTruncated: false`,
  `NonTextContentBlocks: 0`) vor `ErrorMessage: null` einfügen.
  Verhalten des Tests bleibt identisch (er serialisiert ohnehin und
  prüft nur die ersten beiden Felder + Schema-Invariante) — die
  Default-Werte sorgen dafür, dass die 5 neuen Felder per
  `JsonIgnore` weggelassen werden.
- **Warum:** Die `ToolCallRecord`-Signatur ändert sich. Der bestehende
  Test muss grün bleiben (Definition of Done — alle Regression-Tests
  grün). Mechanisches Update, keine Logik-Änderung.

## Tests

**Neu zu implementieren (8 Cases über 3 neue Test-Dateien + 3
erweiterte Cases in `ArgumentSanitizerTests`):**

- [ ] `ArgumentSanitizerTests.Sanitize_AcceptsDictionaryOfObject_AndProducesSameOutputAsJsonElementInput`
- [ ] `ArgumentSanitizerTests.Sanitize_AcceptsJsonObject_AndRedactsNestedSensitiveKeys`
- [ ] `ArgumentSanitizerTests.Sanitize_String_Overload_RedactsKeyValuePairs`
- [ ] `ToolCallRecordSchemaStabilityTests.ToolCallRecord_WithResponseLoggingDisabled_IsByteIdenticalToV1_0_0`
- [ ] `ToolCallRecordSchemaStabilityTests.ToolCallRecord_WithResponseLoggingEnabled_ContainsResponseFields`
- [ ] `ResponseLoggingTests.Response_EnableResponseLogging_True_AppearsConcatenatedWithNewline`
- [ ] `ResponseLoggingTests.Response_EnableResponseLogging_False_AllFieldsAreDefaults`
- [ ] `ResponseLoggingTests.Response_MaxResponseLength_TruncatesAndAddsMarker`
- [ ] `ResponseLoggingTests.Response_IsErrorResult_True_ContainsErrorText`
- [ ] `ResponseLoggingTests.Response_NonTextContentBlocks_AreCountedAndNotInResponse`
- [ ] `RequestFullLoggingTests.Sanitize_PreservesAllTopLevelKeysIncludingNull`
- [ ] `RequestFullLoggingTests.Sanitize_PreservesComplexTypesAndCollections`
- [ ] `RequestFullLoggingTests.Sanitize_HonorsAdditionalSensitiveKeys`

**Regression (muss grün bleiben, kein Schreiben von Cases, nur
Sicherstellung):**

- [ ] `McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`
- [ ] `McpOptionsServerNameOverrideTests` (alle 4 Cases)
- [ ] `McpOptionsFlagsTests` (alle 3 Cases)
- [ ] `McpFeedbackIntegrationTests`
- [ ] `JsonlLogWriterTests.WriteRecord_AppendsValidJsonLines` (mechanisches Update des Record-Konstruktors, siehe Datei 9 oben)
- [ ] `JsonlLogWriterTests.WriteRecord_CreatesFileInSpecifiedDirectoryWithCorrectNaming` (unverändert)
- [ ] `JsonlLogWriterTests.WriteRecord_ThreadSafeConcurrentWrites` (unverändert)
- [ ] Bestehende 4 `ArgumentSanitizerTests`-Cases (unverändert in Verhalten — die alten rufen
  `Sanitize(Dictionary<string, JsonElement>)` auf, was intern auf den
  neuen `Sanitize(object?, ...)`-Pfad gemappt wird; die alte Signatur
  wird **entfernt**, daher müssen die alten Cases die
  Cast-Syntax `Sanitize((object?)dict, null)` o. ä. nicht zwingend
  ändern, weil die Auflösung über den ersten Parameter-Typ erfolgt —
  genau das ist der Vorteil der Generalisierung). **Coder-Achtung:**
  Wenn der Coder aus der alten Signatur eine Wrapper-Methode macht
  (z. B. `Sanitize(IReadOnlyDictionary<string, JsonElement>?)` ruft
  `Sanitize((object?)dict, null)` auf), ist das eine bewusste Wahl —
  in dem Fall ist die alte Signatur obsolet, und die bestehenden
  Tests laufen ohne Änderung. Wenn der Coder stattdessen die alten
  Tests auf die neue Signatur umschreibt, ist das auch ok. Beides
  ist „grün".

**Build/Integration:**

- [ ] `dotnet build --configuration Release` grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors`)
- [ ] `dotnet test --configuration Release --verbosity normal` grün (alle 28 bestehenden + 13 neue Tests = 41 Cases)

## Definition of Done

- [ ] Alle 9 „Konkrete Änderungen" umgesetzt (5 Source-Dateien + 4 Test-Dateien, davon 3 neu + 1 erweitert + 1 Regressions-Update)
- [ ] `JsonNode.Parse`-Elimination in `ArgumentSanitizer.cs` umgesetzt (Mitdenken-Fund, Teil des selben Diffs)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün — 28 bestehende + 13 neue Tests
- [ ] AiNetLinter `safeguard` (Richtlinie §4.4) — Mindest-Score 8.0; alle neuen `public`-Member der `McpObservabilityOptions` mit XML-Doc
- [ ] `JsonlLogWriterTests.WriteRecord_AppendsValidJsonLines` mechanisch aktualisiert
- [ ] `ToolCallRecordSchemaStabilityTests` mit hartkodiertem v1.0.0-Baseline-String verifiziert
- [ ] Commit auf aktuellem Branch (Conventional Commit, deutsch, imperativ, Subject ≤72, Suffix `[refine]`). Empfohlener Code-Commit:
  - `feat(observability): sanitizer generalisiert und response-feld ergaenzt [refine]`
  - Doku-Commits (separat nach Code-Commit):
    - `docs(refine): codemap um response-logging-felder ergaenzen [refine]`
    - `docs(refine): task-state um step-002 erweitern [refine]`
- [ ] `step-002/step-result.md` geschrieben (Coder-Pflicht, nicht Inhalt dieses Plans)
- [ ] `status` in `step-plan.md` (dieser Datei) von `open` auf `done (pending audit)` gesetzt
- [ ] `task-state.md`-Steps-Tabelle um `step-002` ergänzt
- [ ] `roadmap.md`-Notiz „(in Arbeit → step-002)" an EPIC-02 (vom Planer bereits gesetzt) — wird beim Abschluss von EPIC-02 auf „done" umgestellt

## Rules-Refs

- `.agents/rules/McpObservabilityRichtlinien.mdc#5 Datenformat-Invarianten` —
  `schemaVersion = 1` bleibt unverändert; `recordType`-Enumeration
  (`"tool_call" | "feedback"`) bleibt unverändert; `instanceId` ist
  nicht betroffen; `arguments`-Sanitizer-Signatur erweitert (additiv).
  `recordType`-Enum-Werte werden nicht angerührt. **5 additive Felder
  in `ToolCallRecord` sind explizit erlaubt** (Konzept §5: „Neue Felder
  dürfen addiert werden (additiv, non-breaking)"). Die
  `[JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)]`-Attribute
  sichern die byte-identische Schema-Invariante ab.
- `.agents/rules/McpObservabilityRichtlinien.mdc#6 Öffentliche API-Stabilität` —
  In EPIC-02 werden **keine** neuen public-Typen eingeführt. Nur
  bestehende `public`-Properties in `McpObservabilityOptions` werden
  additiv erweitert. Die §6-Lockerung für `McpObservabilityTools`
  (EPIC-03) wird in diesem Step **nicht** angefasst.
- `.agents/rules/McpObservabilityRichtlinien.mdc#7 Tests` —
  xUnit v3; die 3 neuen Test-Dateien folgen dem Pattern aus
  `ArgumentSanitizerTests` (pure Unit-Tests, keine
  `IntegrationTestBase` nötig — der Sanitizer ist isoliert testbar;
  der Handler-Pfad ist ebenfalls isoliert, weil `CreateRecord`/`ExtractResponse`
  deterministisch arbeiten und keine Service-Auflösung brauchen, wenn
  der Test `request`/`result`/`ctx` direkt konstruiert).
  `JsonlLogWriterTests` schreibt in `_tempDirectory` (per `Guid`),
  bleibt unter §7-Isolationsregel.
- `.agents/rules/McpObservabilityRichtlinien.mdc#8 Dokumentation & Qualität` —
  Zero-Warning-Direktive: alle 2 neuen `public`-Properties der
  `McpObservabilityOptions` mit XML-Doc (sonst schlägt
  `TreatWarningsAsErrors` fehl). `AdditionalSensitiveKeys`-XML-Doc
  wird vom „inert"-Hinweis befreit (siehe Datei 1).
  Kein README-Sync in diesem Step (README-Update ist EPIC-05).
- `.agents/rules/McpObservabilityRichtlinien.mdc#10 Commit-Pflicht` —
  Conventional Commit, deutsch, imperativ, Suffix `[refine]`, Subject
  ≤72 Zeichen.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `sealed` bleibt (Records
  sind implizit sealed). Keine neuen Klassen. `JsonIgnore`-Attribut
  zählt nicht als neue `public`-Member (Property-Attribut, nicht
  Property selbst).
- `.agents/rules/AiNetLinter.mdc#Grenzwerte` — `MaxMethodLineCount = 60`
  in Produktion. `CreateRecord` in `ToolCallLoggingHandler` darf
  nicht über 60 Zeilen wachsen → die Response-Extraktion in eigene
  Methode `ExtractResponse` extrahieren (siehe Datei 4).
  `MaxCyclomaticComplexity = 10` / `MaxCognitiveComplexity = 12` —
  die `Sanitize(object?, ...)`-Switch-Anweisung in `ArgumentSanitizer`
  zählt mehrere Branches (5 Typ-Pattern + 1 Defensiv-Fallback);
  bei Überschreitung ggf. die Typ-Erkennung in private Hilfsmethoden
  extrahieren (`TryReadJsonObject`, `TryReadObjectDict`).
- `.agents/rules/AiNetLinter.mdc#test-coverage` — `EnableTestSentinel`
  gilt für die 3 neuen Test-Klassen. Jede Klasse braucht
  Test-Sentinel (`typeof(T)`-Assert am Anfang oder
  `// @covers T`-Kommentar).

## Bekannte Ausnahmen

- **`McpObservabilityIntegrationTests.ToolCall_WritesToolCallRecordToJsonl`**
  baut `Arguments = new Dictionary<string, JsonElement>` und ruft das
  Tool auf. Wenn die Sanitizer-Generalisierung den `Dictionary<string, JsonElement>`-
  Pfad beibehält (Pfad 2 in der Erkennungsreihenfolge), läuft dieser
  Test ohne Änderung grün. Wenn der Coder die alte Signatur komplett
  entfernt und die Typ-Erkennung stattdessen über `IReadOnlyDictionary<,>`-
  Interfaces läuft, ist das auch ok — der Test wäre weiterhin grün,
  weil `Dictionary<string, JsonElement>` beide Interfaces implementiert.
  **Risiko: niedrig.** Bei Review prüfen.
- **`JsonlLogWriterTests.WriteRecord_AppendsValidJsonLines`** wird
  mechanisch aktualisiert (5 neue Parameter im `ToolCallRecord`-
  Konstruktor). Die Default-Werte (`null`/`0`/`false`) sorgen dafür,
  dass die 5 neuen Felder per `JsonIgnore` weggelassen werden — der
  serialisierte Output bleibt unverändert. Risiko: niedrig, aber
  explizit als „muss angepasst werden" vermerkt.
- **`ToolCallRecordSchemaStabilityTests`-Baseline-String** wird im
  Test selbst hartkodiert (nicht aus v1.0.0-Snapshot geladen). Wenn
  der Coder die Baseline aus
  `McpObservabilityIntegrationTests.cs:64-85` (das `Assert.True`-/
  `Assert.Equal`-Setup) ableitet, ist das legitim — der Baseline-
  String ist die JSON-Repräsentation des `ToolCallRecord` mit den 14
  v1.0.0-Feldern, alles in camelCase, `WriteIndented = false`. Die
  exakte Reihenfolge der 14 v1.0.0-Felder (schemaVersion, timestamp,
  recordType, serverName, serverVersion, processId, instanceId,
  toolName, arguments, durationMs, success, isErrorResult,
  errorMessage) wird durch die Position der Konstruktor-Parameter
  festgelegt.
- **`Sanitize(object?, ...)` für nicht-erkannte Typen** gibt leeres
  Dictionary zurück (defensiv). Konzept „nichts stillschweigend
  verwerfen" wird hier bewusst gebrochen — unbekannte Typen
  kommen in der Praxis nicht vor (SDK-Output ist immer
  `IReadOnlyDictionary<string, JsonElement>?` oder `null`); die
  leere-Dict-Rückgabe ist nur ein Defensiv-Schutz gegen künftige
  SDK-Änderungen. Wenn der Coder stattdessen `throw new
  ArgumentException` bevorzugt, ist das auch akzeptabel — würde
  aber den Aufrufer in `ToolCallLoggingHandler` zum
  try/catch zwingen, was AiNetLinter §agent-resilience
  (`EnforceNoSilentCatch`) erschwert. **Empfehlung: leeres Dict
  zurückgeben** — defensiv, aber unsichtbar.

## Code-Skizze (optional)

```csharp
// ArgumentSanitizer.cs — neue Struktur
internal static class ArgumentSanitizer
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase) { /* 10 Keys */ };

    private static HashSet<string> BuildKeySet(IEnumerable<string>? additional) { /* merge */ }

    internal static IReadOnlyDictionary<string, object?>? Sanitize(object? rawArguments, IEnumerable<string>? additionalKeys = null)
    {
        if (rawArguments is null) return null;
        var keys = BuildKeySet(additionalKeys);
        return rawArguments switch
        {
            IReadOnlyDictionary<string, JsonElement> typed => SanitizeFromDict(typed, keys),
            IReadOnlyDictionary<string, object?> objDict  => SanitizeFromObjectDict(objDict, keys),
            JsonObject jsonObj                            => SanitizeFromJsonObject(jsonObj, keys),
            IDictionary<string, object?> anyDict          => SanitizeFromObjectDict(anyDict, keys),
            _ => new Dictionary<string, object?>()  // defensiv
        };
    }

    private static IReadOnlyDictionary<string, object?>? SanitizeFromDict(
        IReadOnlyDictionary<string, JsonElement> dict, HashSet<string> keys)
    {
        if (dict.Count == 0) return dict.ToDictionary(kv => kv.Key, _ => (object?)null);
        var result = new Dictionary<string, object?>(dict.Count, StringComparer.Ordinal);
        foreach (var (k, v) in dict)
            result[k] = keys.Contains(k) ? ObservabilityConstants.RedactedMarker : SanitizeValue(v, keys);
        return result;
    }

    // KEIN JsonNode.Parse mehr — direkte JsonElement-Traversierung
    private static object? SanitizeValue(JsonElement element, HashSet<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                    obj[prop.Name] = keys.Contains(prop.Name) ? ObservabilityConstants.RedactedMarker : SanitizeValue(prop.Value, keys);
                return obj;
            case JsonValueKind.Array:
                var arr = new List<object?>(element.GetArrayLength());
                foreach (var item in element.EnumerateArray())
                    arr.Add(SanitizeValue(item, keys));
                return arr;
            default:
                return element;  // primitive JsonElement direkt weiterreichen — kein Round-Trip
        }
    }

    internal static string? Sanitize(string? rawText, IEnumerable<string>? additionalKeys = null)
    {
        if (string.IsNullOrEmpty(rawText)) return rawText;
        var keys = BuildKeySet(additionalKeys);
        var result = rawText;
        foreach (var key in keys)
        {
            var escaped = Regex.Escape(key);
            result = Regex.Replace(result, $@"\b{escaped}\s*=\s*[^\s,;]+", $"{key}={ObservabilityConstants.RedactedMarker}", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, $@"(\""{escaped}\""\s*:\s*\"")[^\""]+(\"")", $"$1{ObservabilityConstants.RedactedMarker}$2", RegexOptions.IgnoreCase);
        }
        return result;
    }
}

// LogRecords.cs — neue ToolCallRecord-Signatur
internal sealed record ToolCallRecord(
    int SchemaVersion, string Timestamp, string RecordType,
    string ServerName, string ServerVersion, int ProcessId, string InstanceId,
    string ToolName,
    IReadOnlyDictionary<string, object?>? Arguments,  // TYP-WECHSEL
    long DurationMs, bool Success, bool IsErrorResult,
    string? ErrorMessage,
    [property: JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)] string? Response = null,
    [property: JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)] int ResponseLength = 0,
    [property: JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)] int ResponseLines = 0,
    [property: JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)] bool ResponseTruncated = false,
    [property: JsonIgnore(JsonIgnoreCondition.WhenWritingDefault)] int NonTextContentBlocks = 0);

// ToolCallLoggingHandler.cs — CreateRecord-Outline
var additionalKeys = ctx.Options.AdditionalSensitiveKeys;
var sanitized = ArgumentSanitizer.Sanitize(request.Params?.Arguments, additionalKeys);
ExtractResponse(result, additionalKeys, ctx.Options,
    out var response, out var responseLength, out var responseLines,
    out var responseTruncated, out var nonTextCount);

return new ToolCallRecord(
    SchemaVersion: ObservabilityConstants.SchemaVersion,
    Timestamp: DateTime.UtcNow.ToString(ObservabilityConstants.TimestampFormat, CultureInfo.InvariantCulture),
    RecordType: ObservabilityConstants.ToolCallRecordType,
    ServerName: ctx.ServerName, ServerVersion: ctx.ServerVersion,
    ProcessId: ctx.ProcessId, InstanceId: ctx.InstanceId,
    ToolName: request.Params?.Name ?? string.Empty,
    Arguments: sanitized,
    DurationMs: durationMs, Success: exception is null,
    IsErrorResult: isErrorResult,
    ErrorMessage: ExtractErrorMessage(result, exception, isErrorResult),
    Response: response, ResponseLength: responseLength, ResponseLines: responseLines,
    ResponseTruncated: responseTruncated, NonTextContentBlocks: nonTextCount);
```

## Notes

### Designentscheidung 1 — Byte-Identität-Strategie

Konzept sagt zwei Dinge, die auf den ersten Blick im Konflikt stehen:
- „Records **ohne** Response-Logging bleiben **byte-identisch** zu v1.0.0."
- „`responseLength` und `responseLines` werden **immer** befüllt (auch
  bei `EnableResponseLogging = false`)."

**Auflösung:** die 5 neuen Felder sind im Record **immer vorhanden** (im
Code-Pfad gemessen, d. h. „immer befüllt" ist erfüllt), aber im JSON-
Output **per `JsonIgnoreCondition.WhenWritingDefault` weggelassen**,
wenn sie ihren Default-Wert haben. Bei `EnableResponseLogging = false`
setzt der Handler alle 5 Felder auf ihre Defaults → sie werden
weggelassen → der JSON-Output ist byte-identisch zu v1.0.0.

Dies ist die einzige technisch saubere Interpretation, die beide
Konzept-Aussagen gleichzeitig erfüllt. Alternative
(„Felder immer serialisieren, auch bei Defaults") würde die
byte-identische Invariante brechen — das widerspricht dem expliziten
Konzept-Text.

Der `ToolCallRecordSchemaStabilityTests` testet genau diese
Invariante: bei `EnableResponseLogging = false` und Default-Feldern
muss der JSON-Output exakt einem hartkodierten v1.0.0-Baseline-String
entsprechen.

### Designentscheidung 2 — `Sanitize(string?, ...)`-Pattern-Detail

Zwei Regex-Pattern pro Key:
1. `\b{key}\s*=\s*[^\s,;]+` für `key=value` (in HTTP-Headern, Query-
   Strings, Log-Zeilen, Stack-Traces üblich).
2. `"{key}"\s*:\s*"[^"]+"` für JSON-artige Formate (häufig in
   Tool-Responses, die versehentlich JSON-Fragmente zurückgeben).

Beide mit `RegexOptions.IgnoreCase` und `Regex.Escape(key)`. Reihenfolge:
alle Keys durchlaufen, für jeden Key beide Patterns (einfacher zu
lesen als „alle Patterns über alle Keys", und bei <20 Keys kein
messbarer Performance-Unterschied). Die `\b`-Word-Boundary verhindert
False-Positives bei zusammengesetzten Keys (z. B. `apiKey` matcht nicht
in `myapiKey`).

Nicht-Perfekt, aber pragmatisch: die Konzept-Anforderung ist „bekannte
sensitive Keys redacted", nicht „perfekte Heuristik". Der Sanitizer
ist ein **Defense-in-Depth**-Layer; die primäre Geheimhaltung liegt
bei den Tool-Autoren.

### Designentscheidung 3 — `nonTextContentBlocks`-Counter

Pattern-Matching via `is` (idiomatisch in C# 14):

```csharp
foreach (var block in result.Content)
{
    switch (block)
    {
        case TextContent text:
            if (responseBuilder.Length > 0) responseBuilder.Append('\n');
            responseBuilder.Append(text.Text);
            break;
        case ImageContent:
        case AudioContent:
        case EmbeddedResource:
            nonTextCount++;
            break;
        default:
            nonTextCount++;  // defensiv
            break;
    }
}
```

Alternativen (Type-Check via `GetType()`, Reflection auf
`Content`-Subtypen) sind weniger idiomatisch und bringen keinen
Vorteil. Der `default`-Branch fängt künftige SDK-Erweiterungen ab
(gleicher Defensiv-Gedanke wie in `ArgumentSanitizer.Sanitize`).

### Designentscheidung 4 — `ExtractResponse` als eigene Methode

`CreateRecord` darf nicht über 60 Zeilen wachsen (AiNetLinter
§Grenzwerte). Die Response-Extraktion mit Regex-Sanitization +
Truncation-Logik ist 20-25 Zeilen, also wird sie in `ExtractResponse`
extrahiert. `CreateRecord` reduziert sich auf das reine Record-Building
(ca. 20 Zeilen) — gut unter dem Limit.

### Designentscheidung 5 — Test-Isolation für `ResponseLoggingTests`

Die `ResponseLoggingTests`-Cases 1-5 arbeiten über direkten
`ToolCallRecord`-Konstruktion + serialisiertem JSON-Vergleich. Sie
brauchen **keine** `IntegrationTestBase` (kein MCP-Server, keine
Duplex-Pipes, keine `TempDirectory`) — das wäre Over-Engineering für
reine Unit-Tests. Der `CreateRecord`-Pfad wird indirekt über
`ExtractResponse` getestet, das aus `ToolCallLoggingHandler`
extrahiert wird; die Test-Klasse ruft `ExtractResponse` direkt auf
(mit `internal`-Sichtbarkeit via `InternalsVisibleTo` ist das
möglich — `RalfHuesing.Mcp.Observability.Tests` muss
`InternalsVisibleTo` haben, was es bereits hat, sonst wären die
bestehenden `ArgumentSanitizerTests` nicht möglich).

Falls der Coder einen Integrations-Test bevorzugt (Echo-Tool, das
einen bekannten Response zurückgibt, JSONL lesen, Response-Feld
asserten), ist das auch akzeptabel — aber Aufwand höher. Empfehlung:
Unit-Tests, einfacher und deterministischer.
