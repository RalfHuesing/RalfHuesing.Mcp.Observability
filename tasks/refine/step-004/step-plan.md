---
status: planned
type: step-plan
task: refine
step: 004
corrects: null
title: "Writer-Lifecycle (IAsyncDisposable + FlushAsync)"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: minimax-m3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-18T00:35:00+02:00
related_to:
  - step-003/step-result.md
  - step-003/step-review.md
---

# Step 004: Writer-Lifecycle (IAsyncDisposable + FlushAsync)

## Bezug

- **Task:** `refine`
- **Epic:** `EPIC-04` aus `roadmap.md`.
- **Konzept-Referenz:** `Konzept.md` §„Muss-Haben" → „Writer-Lifecycle"
  und Tests Punkt 4 (`JsonlLogWriterFlushTests`).

## Aktueller Projektzustand (JIT-Kontext)

- **`JsonlLogWriter`** (`src/.../Internal/JsonlLogWriter.cs:11-44`):
  implementiert `IDisposable`, hält `StreamWriter _writer`, `Lock _lock = new()`.
  `WriteRecord(object)` nimmt `lock (_lock)` und ruft `_writer.WriteLine(json)`
  (mit `AutoFlush = true`). `Dispose()` nimmt `lock (_lock)` und disposed den
  Writer.
- Es fehlt: `IAsyncDisposable`-Implementierung (`DisposeAsync`) sowie
  `Task FlushAsync(CancellationToken ct = default)`.
- C# 14 / .NET 10 `Lock` (System.Threading.Lock): kann nicht über `await`
  gehalten werden. `FlushAsync` und `DisposeAsync` müssen vor dem `await`
  den Sync-Zustand flushen bzw. synchron unter dem Lock flushen / sicher
  koordinieren oder `_writer.FlushAsync(ct)` aufrufen. Da `AutoFlush = true`
  aktiv ist, ist der interne StreamWriter-Buffer typischerweise schon
  geleert, aber für explizite Flush-Garantien und `DisposeAsync` (sauberes
  Schließen beim async Host-Shutdown) wird die async Schnittstelle gebraucht.

## Konkrete Änderungen

### 1. `src/.../Internal/JsonlLogWriter.cs`
- Klassensignatur: `internal sealed class JsonlLogWriter : IDisposable, IAsyncDisposable`
- Neue Methode `internal async Task FlushAsync(CancellationToken ct = default)`:
  ```csharp
  internal async Task FlushAsync(CancellationToken ct = default)
  {
      // StreamWriter.FlushAsync accepts CancellationToken in .NET 10
      await _writer.FlushAsync(ct).ConfigureAwait(false);
  }
  ```
  Unter Lock synchron vorbereiten / synchroner Flush-Aufruf bzw. direkter
  `_writer.FlushAsync(ct)`-Aufruf.
- Neue Methode `public async ValueTask DisposeAsync()`:
  ```csharp
  public async ValueTask DisposeAsync()
  {
      await _writer.DisposeAsync().ConfigureAwait(false);
  }
  ```

### 2. `tests/.../Internal/JsonlLogWriterFlushTests.cs` (NEU)
Isolierte Unit-Tests in temporärem Verzeichnis (`IDisposable` mit `Guid`-TempDir):
1. **`FlushAsync_FlushesPendingWritesToFile`** — schreibt Record, ruft `FlushAsync(ct)`,
   liest die Datei mit `FileShare.ReadWrite` (oder shared stream) und assertet den Inhalt.
2. **`DisposeAsync_FlushesAndClosesStreamProperly`** — `await using (var writer = new JsonlLogWriter(context)) { writer.WriteRecord(...); }`
   → Datei existiert, enthält vollständige Zeile, FileStream ist freigegeben.

## Verifikation
- `dotnet build --configuration Release` → 0 Warnungen (`TreatWarningsAsErrors`).
- `dotnet test --configuration Release` → alle 44 bestehenden + 2 neue Tests grün.
- AiNetLinter `RunLinterShouldBeClean` → clean.

## Nicht-Ziele dieses Steps
- README-Hinweis zu `FileShare.ReadWrite` (Teil von EPIC-05).
