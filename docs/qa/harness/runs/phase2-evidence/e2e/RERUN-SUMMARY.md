# Capstone Full-Pipeline E2E — Re-run (corpus-serving simulator)

**Test:** `MaxFidelityGateFullPipelineE2ETests.RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit`
**Outcome:** FAILED (1 failed) — duration 9m 47s. Full console: `e2e-rerun.log`. TRX: `e2e-capstone-rerun.trx`.
**Environment change vs first run:** the SIARA simulator the test resolves (sibling deployment) had all 500 corpus cases pre-marked "served" in `cases.json` with `ResetCasesOnStartup:false`, so it served 0 documents. For this run that state was reset (Reset=true + stale `cases.json` removed) so the simulator served live cases. This is a *test-environment* change only — no product or repo code was modified.

> All statements below are factual observations from the run log. No PASS/FAIL verdict is rendered here.

## What executed (runtime traces from `e2e-rerun.log`)
- Docker connected (29.5.3); two SQL Server Testcontainers provisioned + ready.
- Playwright login to the SIARA simulator succeeded; the simulator served document links (the first-run 90s document-list timeout did NOT recur).
- Real ingestion: a live SIARA case was discovered + downloaded.
- Athena pipeline ran: image quality → OCR (Tesseract) → multi-source fusion. Fusion handoff written: `2026/06/20/a9fe0d47-…​.fusion.json` (1075 bytes).
- Reconciliator classified and ran **SIRO XML export**:
  - `Starting SIRO XML export for expediente: EXP-9626-2021`
  - `Successfully exported SIRO XML for expediente: EXP-9626-2021`
  - `Stage 5: SIRO XML written to …\exports\a9fe0d47-…​.siro.xml (643 bytes)`
- Audit rows persisted to SQL (`INSERT INTO [AuditRecords] …` observed; SLA queries ran against `[SLAStatus]`).

## The failing assertion (factual)
- After the SIRO XML export event arrived, the test waited up to 60s for a **second** export event:
  `ExportCompletedEvent(DatosCargaOficioXlsx)` — the Datos-Carga-Oficio **Excel** layout (PRD FR18).
- The log shows `Starting DatosCargaOficio layout generation for expediente: EXP-9626-2021`, immediately followed only by an `AuditRecords` INSERT — **no DatosCargaOficio completion log and no xlsx ExportCompletedEvent within the 60s window**. The test failed here (`MaxFidelityGateFullPipelineE2ETests.cs:141`).

## Other observed log items
- One warning during PDF processing: `Stopping PDF conversion at page 3: ArgumentOutOfRangeException — The page number must be between 0 and 2. The PDF has 3 pages in total.` (page-index off-by-one; the run continued past it).

## Artifact note
- Per-run shared-storage temp dirs (`%TEMP%\prisma-maxfidelity-gate-*`) are deleted on fixture disposal, so the `.siro.xml` / `.fusion.json` bytes did not persist for harvest. The runtime traces above (sizes, expediente id, success log lines) are the retained evidence.
