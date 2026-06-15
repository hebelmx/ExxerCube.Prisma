# Item B (#8) + F (#12) — Manifest reconciliation + per-cycle downloaded-file report — Design

**Owner decision (B):** expected "Listado" is **config/file-driven**.
**Orchestrator sub-decisions:**
- Reconcile at the **cycle boundary** in `SiaraWatchLoop.PollOnceAsync` — it is the only place that sees ALL
  discovered cases in a cycle, so "expected oficio that never arrived" (MISSING) is knowable there.
  `IngestionOrchestrator` stays per-case and is NOT modified (it is heavily tested).
- **Opt-in** via `ExpectedManifestOptions.Enabled` (default `false`) → existing watch-loop behavior and its tests
  are unchanged when no manifest is configured.
- The reconciler is a **pure domain service** (no I/O) → highly testable + mutation-killable.
- The report is **logged (structured Serilog) AND persisted** as a per-cycle JSON artifact to shared storage via
  `IStoragePathResolver` (client-visible, queryable). F (the downloaded-file list) is part of the same report.

## Reconciliation model (file + oficio level)
Expected manifest lists oficios, each with a base id + expected file formats. Reconcile discovered/downloaded
cases for the cycle against it and bucket:
- **Complete** — oficio present and all expected formats downloaded.
- **Partial** — oficio present but ≥1 expected format missing (this is the existing best-effort partial; reuse
  `ReviewReason.IncompleteCase`).
- **Missing** — oficio in the expected Listado but NOT discovered this cycle.
- **Extra (sobra)** — oficio discovered but NOT in the expected Listado.
Plus per-oficio **missing-files** / **extra-files** (format-level) within a present oficio.

## Expected manifest file format (JSON, demo-controllable)
```json
{
  "oficios": [
    { "caseId": "222AAA-44444444442025", "expectedFormats": ["Pdf", "Xml", "Docx"] },
    { "caseId": "333BBB-44444444442025", "expectedFormats": ["Pdf", "Xml", "Docx"] }
  ]
}
```
`caseId` matches the `SiaraCaseGrouping` key (URL path segment / base name). `expectedFormats` parse to
`FileFormat`. Loader is fault-tolerant (missing/blank/garbage file → `Result.WithFailure`, reconciliation skipped
with a warning, cycle continues).

## Components
1. **`ExpectedManifestOptions`** (config) — `SectionName="ExpectedManifest"`, `Enabled` (bool, default false),
   `ManifestPath` (string?). In Orion ingestion options namespace, mirror `WatchLoopOptions`/`StorageOptions`.
2. **`ExpectedManifest` / `ExpectedOficio`** — domain value objects (Domain): the parsed Listado.
3. **`IExpectedManifestProvider` + `FileExpectedManifestProvider`** — `Task<Result<ExpectedManifest>> LoadAsync(ct)`
   reads + parses the JSON file (fault-tolerant). Returns empty/failure gracefully.
4. **`IManifestReconciler` + `ManifestReconciliationService`** (Domain.Services, **pure**) —
   `ManifestReconciliationReport Reconcile(ExpectedManifest expected, IReadOnlyList<DiscoveredOficio> actual)`.
   `DiscoveredOficio` = { CaseId, DownloadedFiles (name+extension+format), IsComplete }. Report carries the buckets
   above + a flat per-cycle file list (F): `FileDownloadSummary { FileName, Extension, Format, CaseId, IsComplete }`.
5. **`CycleReconciliationReport`** record — cycle window, counts (discovered/ingested/duplicate/failed),
   the reconciliation buckets, and the F file list. JSON-serializable.
6. **Wire into `SiaraWatchLoop.PollOnceAsync`** — when `ExpectedManifestOptions.Enabled`: after the per-case
   ingest loop, build `DiscoveredOficio` list from the cycle's cases/results, load the expected manifest, run the
   reconciler, then (a) log the report structured at INFO with key `"PerCycleReconciliationReport"`, and (b) write
   it to storage `reports/cycle-{utcStamp}.json` via `IStoragePathResolver` (skip persist gracefully if resolver
   null/fails). Do NOT change behavior when disabled.
7. **DI** in Orion `Program.cs` — bind `ExpectedManifestOptions`; register provider + reconciler; inject into the
   watch loop (optional deps so the loop still constructs when disabled).

> Thread the per-file detail from each case into the cycle accumulation. Prefer reading the **discovered**
> `SiaraCase.Files` (names/extensions/formats) + the `IngestionResult.IsComplete` already returned — avoid
> modifying `IngestionOrchestrator` internals. If `IngestionResult` lacks what's needed, extend it additively only.

## Tests (ITDD per ADR-005)
- **Unit** `ManifestReconciliationServiceTests` (pure): all buckets — complete, partial (missing 1 format),
  missing oficio, extra(sobra) oficio, extra file, empty-expected, empty-actual. This is the mutation-killable core.
- **Unit** `FileExpectedManifestProviderTests`: valid JSON parses; missing/blank/garbage file → graceful failure.
- **Loop** extend `SiaraWatchLoopTests`: with `Enabled=true` + a fake source/manifest, a cycle produces a report
  with the right buckets and the F file list; with `Enabled=false`, behavior + existing assertions unchanged.

## Definition of done
Build 0/0 for touched projects; `Prisma.Orion.Ingestion.Tests` (+ any Domain test project for the pure reconciler)
green; existing `SiaraWatchLoopTests` / `IngestionOrchestratorTests` unchanged and green. `git diff` confined to the
new options/provider/reconciler/report types, the watch-loop hook, the Orion DI, and the new tests.
