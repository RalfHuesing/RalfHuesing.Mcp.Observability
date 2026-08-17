Hier ist das **vollständige, agenten-fertige Konzept**. Es enthält keine offenen Fragen mehr.

---

# Konzept: RalfHuesing.Mcp.Observability

**Version des Konzepts:** 1.0  
**Ziel:** Ein NuGet-Paket, das MCP-Server (basierend auf dem offiziellen `ModelContextProtocol`-SDK) einheitliches, überlebensfähiges Logging und einen strukturierten Feedback-Kanal für LLM-Agenten gibt.

---

## 1. Ziele

- Einheitliches Logging aller Tool-Aufrufe über alle MCP-Server hinweg.
- Logs liegen **außerhalb** des Server-Release-Verzeichnisses und überleben Delete + Neu-Extract.
- Ein einziges, immer sichtbares Feedback-Tool, mit dem der Agent Issues und Feature Requests melden kann.
- Alles im gleichen Schema (später leicht durchsuchbar).
- Minimaler Integrationsaufwand für bestehende Server (AiNetLinter, SqlToAi, KnowHowToAi, …).
- Keine Datenbank, kein OpenTelemetry in v1 – pragmatisch und einfach.
- Multi-Prozess-sicher (mehrere parallele Instanzen desselben Servers überschreiben sich nicht).

## 2. Nicht-Ziele (v1)

- OpenTelemetry / Metrics / Traces
- SQLite oder andere Datenbanken
- Log-Rotation / automatische Löschung alter Dateien
- Dedizierter Log-Server / Query-MCP (kann später draufgebaut werden)
- Unterstützung für .NET < 10

---

## 3. Package-Identität

| Eigenschaft              | Wert                                      |
|--------------------------|-------------------------------------------|
| PackageId                | `RalfHuesing.Mcp.Observability`           |
| Namespace                | `RalfHuesing.Mcp.Observability`           |
| Target Framework         | `net10.0`                                 |
| Lizenz                   | MIT                                       |
| Autoren                  | Ralf Huesing                              |
| Repository               | (später GitHub von RalfHuesing)           |
| Abhängigkeiten           | `ModelContextProtocol` (aktuellste stabile 2.x), `Microsoft.Extensions.Options`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Abstractions` |

---

## 4. Verzeichnis- und Dateistruktur der Logs

**Standard-Root** (wenn nicht anders konfiguriert):

```
%LOCALAPPDATA%\RalfHuesing\McpObservability\
```

**Struktur darunter:**

```
%LOCALAPPDATA%\RalfHuesing\McpObservability\
└── {ServerName}\                          // z. B. AiNetLinter
    └── {yyyy-MM-dd}\                      // UTC-Datum
        └── {ServerName}_{ProcessId}_{InstanceId}.jsonl
```

- `InstanceId` = neuer `Guid` bei jedem Prozessstart (kurz, ohne Bindestriche).
- Eine Datei pro laufender Prozess-Instanz → **keine Concurrent-Write-Probleme**.
- Format: **JSON Lines** (jede Zeile ein vollständiges JSON-Objekt + `\n`).

Beispiel-Dateiname:
`AiNetLinter_18432_a1b2c3d4e5f67890.jsonl`

---

## 5. Datenschema (JSONL-Records)

Jedes Log-Ereignis ist ein JSON-Objekt mit folgenden gemeinsamen Feldern:

```json
{
  "schemaVersion": 1,
  "timestamp": "2026-08-17T17:22:01.123Z",          // immer UTC, ISO-8601
  "recordType": "tool_call" | "feedback",
  "serverName": "AiNetLinter",
  "serverVersion": "1.4.2",
  "processId": 18432,
  "instanceId": "a1b2c3d4e5f67890"
}
```

### 5.1 recordType = `"tool_call"`

Zusätzliche Felder:

```json
{
  "toolName": "analyze_code",
  "arguments": { ... },                    // bereits sanitized
  "durationMs": 142,
  "success": true,
  "isErrorResult": false,                  // MCP-Result hatte isError=true
  "errorMessage": null                     // oder Exception-Message / isError-Text
}
```

### 5.2 recordType = `"feedback"`

Zusätzliche Felder:

```json
{
  "feedbackType": "issue" | "feature_request",
  "title": "False positive on nullable reference",
  "description": "When analyzing ... the tool reported ...",
  "relatedTool": "analyze_code",           // optional
  "severity": "low" | "medium" | "high",
  "expectedBehavior": "...",               // optional
  "actualBehavior": "...",                 // optional
  "additionalContext": "..."               // optional, freier Text
}
```

---

## 6. Das einzige Feedback-Tool

**Name:** `report_observability_feedback`

**Beschreibung (für den Agenten sichtbar):**

> Report an issue or a feature request about this MCP server.  
> Use this tool whenever something is wrong (bugs, false positives, unexpected results, confusing output) or when a needed capability is missing.  
> After reporting, continue with the best available workaround.

**Parameter:**

| Name                 | Typ                          | Pflicht | Beschreibung |
|----------------------|------------------------------|---------|--------------|
| `feedbackType`       | `"issue"` \| `"feature_request"` | ja     | Art des Feedbacks |
| `title`              | string                       | ja      | Kurzer, klarer Titel (max. 120 Zeichen) |
| `description`        | string                       | ja      | Was passiert ist / was fehlt (detailliert) |
| `relatedTool`        | string?                      | nein    | Name des betroffenen Tools (falls bekannt) |
| `severity`          | `"low"` \| `"medium"` \| `"high"` | nein | Default = `"medium"` |
| `expectedBehavior`   | string?                      | nein    | Was der Agent erwartet hat |
| `actualBehavior`     | string?                      | nein    | Was tatsächlich passiert ist |
| `additionalContext`  | string?                      | nein    | Weitere freie Informationen |

Das Tool schreibt einen `"feedback"`-Record in die aktuelle Instanz-Datei und gibt dem Agenten eine kurze Bestätigung zurück (z. B. „Feedback recorded. Thank you.“).

---

## 7. Konfiguration

```csharp
namespace RalfHuesing.Mcp.Observability;

public sealed class McpObservabilityOptions
{
    /// <summary>
    /// Master-Schalter. Default = true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Automatisches Logging jedes Tool-Aufrufs. Default = true.
    /// </summary>
    public bool EnableToolCallLogging { get; set; } = true;

    /// <summary>
    /// Registriert das Feedback-Tool. Default = true.
    /// </summary>
    public bool EnableFeedbackTool { get; set; } = true;

    /// <summary>
    /// Überschreibt den Standard-Log-Pfad.
    /// null = %LOCALAPPDATA%\RalfHuesing\McpObservability\
    /// </summary>
    public string? LogDirectory { get; set; }
}
```

**appsettings.json-Beispiel (in den konsumierenden MCP-Servern):**

```json
{
  "McpObservability": {
    "Enabled": true,
    "EnableToolCallLogging": true,
    "EnableFeedbackTool": true
    // "LogDirectory": "D:\\Logs\\Mcp"
  }
}
```

---

## 8. Öffentliche Integrations-API

Es gibt **eine** zentrale Extension-Methode:

```csharp
public static class McpObservabilityExtensions
{
    /// <summary>
    /// Aktiviert Observability (Tool-Call-Logging + Feedback-Tool).
    /// Options können null sein → Defaults werden verwendet.
    /// </summary>
    public static IMcpServerBuilder WithObservability(
        this IMcpServerBuilder builder,
        McpObservabilityOptions? options = null);
}
```

**Typische Verwendung in einem MCP-Server:**

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Options aus appsettings lesen (oder Defaults)
var obsOptions = builder.Configuration
    .GetSection("McpObservability")
    .Get<McpObservabilityOptions>() 
    ?? new McpObservabilityOptions();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithObservability(obsOptions);

await builder.Build().RunAsync();
```

Wenn `options.Enabled == false`, macht `WithObservability` nichts (kein Logging, kein Feedback-Tool).

---

## 9. Interne Architektur (für den Implementierer)

### 9.1 Kernkomponenten

- `ObservabilityContext`  
  Hält ServerName, ServerVersion, ProcessId, InstanceId, Options und den aktuellen Log-Writer.

- `JsonlLogWriter`  
  Thread-sicherer Writer für die aktuelle Instanz-Datei (einfaches `FileStream` mit `FileShare.Read`, Append-Mode). Erstellt bei Bedarf Verzeichnisse.

- `ArgumentSanitizer`  
  Redaktiert bekannte sensible Keys (case-insensitive):  
  `password`, `pwd`, `secret`, `token`, `apiKey`, `apikey`, `accessToken`, `authorization`, `connectionString`, `privateKey` usw.  
  Werte werden durch `"***REDACTED***"` ersetzt. Funktioniert rekursiv auf Dictionaries/JSON-Elementen.

- `ToolCallInterceptor` / Middleware  
  Wird über die MCP-SDK-Möglichkeiten (Handler-Wrapping oder Activity/Filter) so eingehängt, dass **jeder** `tools/call` vor und nach der Ausführung geloggt wird (Duration, Success, isError, Exception).

- `FeedbackTools` (interne Klasse mit `[McpServerTool]`)  
  Enthält nur die eine Methode `report_observability_feedback`. Wird nur registriert, wenn `EnableFeedbackTool == true`.

### 9.2 Server-Metadaten ermitteln

- `serverName` und `serverVersion` sollen aus dem MCP-Server selbst kommen (die Werte, die an `AddMcpServer` / `McpServerOptions` übergeben werden).  
  Fallback: Assembly-Name + Assembly-Version des Entry-Assemblys.

### 9.3 Lebenszyklus

- Beim ersten Aufruf von `WithObservability` wird der `ObservabilityContext` als Singleton registriert und die Datei geöffnet.
- Beim Prozessende wird der Writer sauber geschlossen (kein spezielles Flush nötig, da Append + `\n`).

---

## 10. Projektstruktur des NuGet-Pakets

```
RalfHuesing.Mcp.Observability/
├── src/
│   └── RalfHuesing.Mcp.Observability/
│       ├── RalfHuesing.Mcp.Observability.csproj
│       ├── McpObservabilityOptions.cs
│       ├── McpObservabilityExtensions.cs
│       ├── Internal/
│       │   ├── ObservabilityContext.cs
│       │   ├── JsonlLogWriter.cs
│       │   ├── ArgumentSanitizer.cs
│       │   ├── ToolCallLoggingHandler.cs
│       │   └── FeedbackTools.cs
│       └── README.md
├── tests/
│   └── RalfHuesing.Mcp.Observability.Tests/
│       └── ...
├── samples/
│   └── MinimalMcpServerWithObservability/
│       └── ...
└── README.md                 // Repo-Root
```

---

## 11. Tests (Mindestanforderungen)

- Unit-Tests für `ArgumentSanitizer` (verschiedene Verschachtelungen, case-insensitivity).
- Unit-Tests für `JsonlLogWriter` (Datei wird korrekt angelegt, Zeilen sind gültiges JSON, mehrere Writes hintereinander).
- Integrationstest: Minimaler MCP-Server mit `WithObservability` → Tool aufrufen → Log-Datei enthält korrekten `tool_call`-Record.
- Integrationstest: Feedback-Tool aufrufen → korrekter `feedback`-Record.
- Test, dass bei `Enabled = false` weder geloggt noch das Feedback-Tool registriert wird.
- Test, dass bei `EnableToolCallLogging = false` nur Feedback-Records entstehen (und umgekehrt).

---

## 12. Versionierung & Publishing

- Semantic Versioning (SemVer).
- Erste Version: `1.0.0`.
- Publish nach nuget.org (kostenlos, öffentlich).
- `README.md` im Package muss klare Integrationsanleitung + Schema-Dokumentation enthalten.

---

## 13. Beispiel-Integrationscode (für die konsumierenden Server)

```csharp
// Program.cs eines bestehenden MCP-Servers
var builder = Host.CreateApplicationBuilder(args);

var obsOptions = builder.Configuration
    .GetSection("McpObservability")
    .Get<McpObservabilityOptions>() 
    ?? new McpObservabilityOptions();   // Defaults = alles an

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AiNetLinter", Version = "1.4.2" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithObservability(obsOptions);

await builder.Build().RunAsync();
```

---

## 14. Spätere Erweiterbarkeit (nur zur Info, nicht implementieren)

- Optionaler zweiter Package `RalfHuesing.Mcp.Observability.AspNetCore` (falls jemals HTTP-spezifische Helfer nötig).
- Ein dedizierter localhost-Log-Server, der die JSONL-Dateien einliest und Query-Tools anbietet.
- OpenTelemetry-Exporter als zusätzliche Option.

---

## 15. Definition of Done

Das Package ist fertig, wenn:

1. Es sich als `RalfHuesing.Mcp.Observability` auf nuget.org publishen lässt.
2. Ein bestehender MCP-Server mit **einer** zusätzlichen Zeile (`.WithObservability(...)`) die Funktionalität erhält.
3. Tool-Aufrufe und Feedback-Aufrufe als JSONL in dem definierten Verzeichnis erscheinen.
4. Mehrere parallele Prozesse desselben Servers sich nicht gegenseitig überschreiben.
5. Die Unit- und Integrationstests grün sind.
6. Das README eine klare, kopierbare Anleitung enthält.

---

Dieses Konzept ist vollständig und enthält keine offenen Entscheidungen mehr.  
Ein Agent kann damit direkt die Implementierung starten.