# Audit-Report & Refinement-Konzept: RalfHuesing.Mcp.Observability

**Projekt:** `RalfHuesing.Mcp.Observability`  
**Zielgruppe:** Entwickler und nachfolgende LLM-Agenten, die das Paket überarbeiten und als Version `1.1.0` (oder `2.0.0`) veröffentlichen sollen.  
**Erstellt am:** 17. August 2026  
**Referenz-Integration:** `AiNetLinter` (Roslyn-basierter MCP-Server mit 22 Tools)

---

## 1. Executive Summary & Audit-Ergebnis

Das Paket `RalfHuesing.Mcp.Observability` (v1.0.0) erfüllt seine Kernaufgabe: Tool-Aufrufe werden über den `CallToolFilter` abgefangen und thread-sicher in standardisierten JSONL-Dateien gespeichert. 

Bei der praktischen Integration in einen realen, produktiven MCP-Server (`AiNetLinter`) traten jedoch **vier signifikante Hürden** auf, die Workarounds im konsumierenden Code erforderten:

1. **Tool-Schatten-Effekt bei manueller `ToolCollection` (Kritisch):**  
   In `ModelContextProtocol.NET` 2.x ignoriert der Server alle über DI (`builder.WithTools<T>()`) registrierten Tools, sobald der Server `options.ToolCollection` explizit setzt. Dadurch war das Tool `report_observability_feedback` unsichtbar.
2. **Zu restriktive Sichtbarkeit (`internal`):**  
   `FeedbackTools`, `ObservabilityContext` und `JsonlLogWriter` sind komplett `internal`. Der konsumierende Server konnte den Registrierungsfehler nicht ohne Reflection beheben und hat keine Möglichkeit, den Logging-Status oder den Log-Dateipfad programmatisch abzufragen.
3. **Fragiles Casting im `ArgumentSanitizer`:**  
   `request.Params?.Arguments as IReadOnlyDictionary<string, JsonElement>` schlägt fehl und wird `null`, wenn Argumente als `Dictionary<string, object?>` oder `JsonObject` übergeben werden.
4. **Fehlende `ServerName` / `ServerVersion`-Overrides in `McpObservabilityOptions`:**  
   In Test-Runners oder schlanken CLI-Servern ohne `Host.CreateApplicationBuilder` ermittelt der Server oft `testhost` oder `UnknownServer` als Servername, da die Optionen keine direkte Konfiguration dafür bieten.

---

## 2. Detaillierte Analyse der aufgetretenen Workarounds

### 2.1 Workaround 1: `builder.WithTools<FeedbackTools>()` vs. `McpServerOptions.ToolCollection`

- **Problemursache:**  
  Viele fortgeschrittene MCP-Server (z. B. mit dynamischen Tools, Closure-basierten Handlern oder strikter DI-Vermeidung) initialisieren ihre Tools über:
  ```csharp
  serverOptions.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
  ```
  Im MCP-SDK (`ModelContextProtocol.Server`) führt das Setzen von `ToolCollection` dazu, dass der Server **nur** diese Collection auflistet und `builder.WithTools<T>()` vollständig ignoriert.
- **Folge in v1.0.0:**  
  `report_observability_feedback` taucht weder in `tools/list` auf, noch kann der Agent Feedback geben.
- **Notwendiger Workaround im Host-Projekt:**  
  Der Host musste `FeedbackTools.ReportFeedback` per Reflection aus der Assembly laden und manuell per `McpServerTool.Create` in seine `ToolCollection` einhängen.

---

### 2.2 Workaround 2: Interne Sichtbarkeit der Kernklassen

- **Problemursache:**  
  Alle Klassen unter `Internal/` (`FeedbackTools`, `ObservabilityContext`, `JsonlLogWriter`, Records) sind `internal`.
- **Folge in v1.0.0:**  
  - Ein MCP-Server kann seinen Zustand (z. B. für `get_server_health`) nicht befragen (`IsActive`, `CurrentLogFilePath`, `RecordsWritten`).
  - Ein Server kann keine benutzerdefinierten Fehlereinträge (z. B. aus globalen Unhandled-Exception-Filtern) in das Log schreiben.
- **Notwendiger Workaround im Host-Projekt:**  
  Reflection auf `FeedbackTools.ReportFeedback`.

---

### 2.3 Workaround 3: Argument-Typ-Casting in `ToolCallLoggingHandler`

- **Problemursache:**  
  In `ToolCallLoggingHandler.cs`:
  ```csharp
  var sanitized = ArgumentSanitizer.Sanitize(
      request.Params?.Arguments as IReadOnlyDictionary<string, System.Text.Json.JsonElement>);
  ```
  `request.Params?.Arguments` ist vom Typ `IReadOnlyDictionary<string, object?>` oder `JsonObject` je nach Client-Serialisierung.
- **Folge in v1.0.0:**  
  Der `as`-Cast liefert häufig `null`, wodurch `Arguments` im `tool_call`-Record leer (`null`) bleibt, obwohl Parameter übergeben wurden.
- **Notwendiger Workaround im Host-Projekt:**  
  Keine direkte Lösung im Host möglich (stille Datenverarmung im JSONL).

---

### 2.4 Workaround 4: File Locking in Test- und Reader-Szenarien

- **Problemursache:**  
  `JsonlLogWriter` hält den `FileStream` mit `FileShare.ReadWrite` offen. Das ist gut für parallele Server, aber Standardmethoden wie `File.ReadAllLines(path)` nutzen unter Windows `FileShare.Read`, was fehlschlägt (`IOException: The process cannot access the file...`), solange der Server/Writer nicht disposed ist.
- **Empfehlung:**  
  Die Dokumentation muss explizit darauf hinweisen, wie Logdateien im laufenden Betrieb gelesen werden (mit `FileShare.ReadWrite`), und `JsonlLogWriter` sollte `IAsyncDisposable` sowie eine `FlushAsync()`-Methode unterstützen.

---

## 3. Ziel-Architektur für `RalfHuesing.Mcp.Observability` (v1.1 / v2.0)

### 3.1 Erweiterung von `McpObservabilityOptions`

Die Options-Klasse sollte konfigurierbar sein für Server-Namen, Severity-Stufen und Log-Steuerung:

```csharp
namespace RalfHuesing.Mcp.Observability;

public sealed class McpObservabilityOptions
{
    public bool Enabled { get; set; } = true;
    public bool EnableToolCallLogging { get; set; } = true;
    public bool EnableFeedbackTool { get; set; } = true;
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Expliziter Server-Name für Log-Pfade und Records.
    /// Falls null, wird McpServerOptions.ServerInfo.Name oder EntryAssembly genutzt.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Explizite Server-Version für Records.
    /// Falls null, wird McpServerOptions.ServerInfo.Version oder Assembly-Version genutzt.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Antwortnachricht für den Agenten nach erfolgreichem Feedback.
    /// Default: "Feedback recorded. Thank you."
    /// </summary>
    public string FeedbackConfirmationMessage { get; set; } = "Feedback recorded. Thank you.";

    /// <summary>
    /// Zusätzliche Keys, die bei der Argument-Sanitization geschwärzt werden sollen.
    /// </summary>
    public HashSet<string> AdditionalSensitiveKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```

---

### 3.2 Dual-Registration Support (DI + Manuelle ToolCollection)

Um Servern, die `options.ToolCollection` manuell befüllen, die nahtlose Integration zu ermöglichen, soll das Paket **zwei** Wege zur Registrierung anbieten:

#### Weg A: Automatischer DI-Weg (Bestehend)
```csharp
builder.WithObservability(options);
```
*Verbesserung:* Wenn `options.ToolCollection` später belegt wird, soll das Paket zusätzlich einen `IPostConfigureOptions<McpServerOptions>` registrieren, der prüft, ob `ToolCollection` gesetzt ist, und falls ja, das Feedback-Tool dort automatisch anfügt!

#### Weg B: Öffentliche Tool-Factory für manuelle Collections (Neu)
```csharp
public static class McpObservabilityTools
{
    /// <summary>
    /// Erstellt das standardisierte report_observability_feedback Tool als McpServerTool.
    /// Zur direkten Registrierung in manuellen ToolCollections.
    /// </summary>
    public static McpServerTool CreateFeedbackTool(
        IServiceProvider services,
        McpServerToolCreateOptions? createOptions = null);

    /// <summary>
    /// Hängt das Feedback-Tool direkt an eine bestehende ToolCollection an.
    /// </summary>
    public static void AddFeedbackTool(
        this McpServerPrimitiveCollection<McpServerTool> tools,
        IServiceProvider services);
}
```

---

### 3.3 Robuste Argument-Sanitization

`ArgumentSanitizer` soll universell mit allen JSON-/Dictionary-Typen umgehen können:

```csharp
public static class ArgumentSanitizer
{
    public static IReadOnlyDictionary<string, object?>? Sanitize(object? rawArguments, IEnumerable<string>? additionalKeys = null)
    {
        if (rawArguments is null) return null;

        // Unterstützt:
        // 1. IReadOnlyDictionary<string, JsonElement>
        // 2. IReadOnlyDictionary<string, object?>
        // 3. JsonObject
        // 4. Beliebige IDictionary
        // Rekursive Bereinigung sensibler Keys -> "***REDACTED***"
    }
}
```

---

### 3.4 Öffentlicher `IObservabilityService` für Diagnostik

```csharp
namespace RalfHuesing.Mcp.Observability;

public interface IMcpObservabilityService
{
    bool IsEnabled { get; }
    string ServerName { get; }
    string? CurrentLogFilePath { get; }
    int ProcessId { get; }
    string InstanceId { get; }

    void LogCustomRecord(string recordType, object payload);
    void Flush();
}
```
Dieser Service wird als Singleton im DI-Container registriert (`services.AddSingleton<IMcpObservabilityService, ...>()`).

---

## 4. Konkrete Handlungsanweisungen für das umsetzende LLM

Liebes bearbeitendes LLM, führe bitte folgende Schritte im Repository `C:\Daten\Entwicklung\Ralf\RalfHuesing.Mcp.Observability` durch:

### Schritt 1: `McpObservabilityOptions.cs` erweitern
- Füge `ServerName`, `ServerVersion`, `FeedbackConfirmationMessage` und `AdditionalSensitiveKeys` hinzu.
- XML-Dokumentation pflegen.

### Schritt 2: `ArgumentSanitizer.cs` robust machen
- Entferne die harte Bindung an `IReadOnlyDictionary<string, JsonElement>`.
- Akzeptiere `object?` oder `IReadOnlyDictionary<string, object?>` / `JsonElement` und normalisiere zu einem serialisierbaren JSON-Baum mit geschwärzten Keys.

### Schritt 3: Öffentliche Tool-Factory `McpObservabilityTools.cs` bereitstellen
- Erstelle die Klasse `McpObservabilityTools` mit `CreateFeedbackTool(...)` und `AddFeedbackTool(...)`.
- Mache `FeedbackTools.ReportFeedback` öffentlich oder delegate an die interne Logik, sodass sowohl Reflection-freie manuelle Registrierung als auch automatische DI-Registrierung funktioniert.

### Schritt 4: `IPostConfigureOptions<McpServerOptions>` einhängen
- In `McpObservabilityExtensions.WithObservability`: Registriere ein `IPostConfigureOptions<McpServerOptions>`, das nach Abschluss aller Konfigurationen prüft:
  ```csharp
  if (options.EnableFeedbackTool && serverOptions.ToolCollection is not null)
  {
      if (!serverOptions.ToolCollection.Any(t => t.ProtocolTool.Name == "report_observability_feedback"))
      {
          serverOptions.ToolCollection.AddFeedbackTool(serviceProvider);
      }
  }
  ```
  *Dadurch funktioniert `WithObservability()` magisch immer, egal ob `builder.WithTools()` oder `serverOptions.ToolCollection` genutzt wird!*

### Schritt 5: `IMcpObservabilityService` & Status-Exposition
- Schnittstelle `IMcpObservabilityService` definieren und `ObservabilityContext` diese implementieren lassen.
- Im DI-Container als Singleton registrieren.

### Schritt 6: Tests ergänzen & `README.md` aktualisieren
1. Test für `McpServerOptions.ToolCollection` (Verifizieren, dass `report_observability_feedback` auch bei manueller ToolCollection vorhanden ist).
2. Test für verschiedene Argument-Typen (`JsonObject`, `Dictionary<string, object?>`, `JsonElement`).
3. Test für `ServerName`-Override in `McpObservabilityOptions`.
4. `README.md` aktualisieren: Dual-Use-Dokumentation (Automatischer DI-Server vs. manueller `ToolCollection`-Server) mit Copy-Paste-Codebeispielen.

---

## 5. Fazit

Mit diesen Anpassungen wird `RalfHuesing.Mcp.Observability` von einem guten "Happy-Path-DI"-Paket zu einer **extrem robusten, universal kompatiblen Enterprise-Observability-Bibliothek** für jedes MCP-Server-Setup in .NET.
