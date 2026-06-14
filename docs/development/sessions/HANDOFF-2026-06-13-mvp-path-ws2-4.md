# Handoff — MVP-PATH Workstreams 2–4 + decks cleared (issue #1) — DONE, 2.1 + gate remain

**Date:** 2026-06-13 · **Branch:** `Kt2` (pushed) · **Driver:** orchestrator (ground-truth verified every chunk)
**Supersedes the "Next Workstream items" list in:** `HANDOFF-2026-06-13-mvp-path-1.5-1.6-security-spine.md`
**Source plan:** `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md`

## TL;DR
WS1 (1.1–1.6) was already done. This session cleared the decks (issue #1) and landed the **independent** WS2–4 tail. Two of those items were already complete from earlier sessions (the Jun-13 1.5/1.6 handoff's "next" list was stale). The MVP gate is now blocked on **one** remaining item — **2.1 multi-source fusion** — which the owner re-scoped mid-session into a larger **case-package ingestion** redesign (below). The **5.1/5.2 E2E gate** depends on 2.1.

## Done this session (Kt2 `b1a4b3c..caf383d`, all pushed, all ground-truth verified)
| Item | Commit | What | Verified |
|---|---|---|---|
| **issue #1** | `b1a4b3c` | `EfCoreRepository FileMetadataRepository_RemoveAsync` test asserted pre-Phase-6 behaviour (success-with-null after delete); tightened to `IsFailure` per the canonical RepositoryContract. GH issue #1 **closed**. | Tests.Infrastructure.Database 139→ green |
| **4.3 (E2)** | *(already done `73d0d39`)* | Config externalized + Counter/Weather deleted. Plan was stale — **no work needed**. | appsettings.json verified |
| **2.2 (B2)** | `c1e9c24` | Impl already done (`5a36b3a`); added the missing DoD test (searchable PDF→native text, OCR asserted never called; empty→OCR fallback). In-memory PDF via PdfPig `PdfDocumentBuilder`. | Extraction 411/411 |
| **4.1 (D2)** | `dda9405` | `ConfigureOpenTelemetry` → `.AddMeter("ExxerCube.Prisma.SLA")`; `SLAMetricsCollector` tracks gauge values internally (was hardcoded 0 → wrong UpDownCounter deltas). | SLAMetricsCollectorTests 3; Web.UI builds 0/0 |
| **4.2 (E1)** | `8b1aa6a` | Real readiness probes. New Domain port `IReadinessProbe`; the component each worker actually starts implements it (`ExtractionPipelineService.IsStarted`, `SiaraWatchLoop.IsRunning`); both `*HealthCheckService` read it. | HealthChecks 15+20, Worker host-DI 24+24, Arch 22, Athena.Processing 70, Orion.Ingestion 15 |
| **3.1 (C2+C3)** | `caf383d` | New `IUnifiedMetadataStore` + EF JSON store (`UnifiedMetadataRecords` table, migration `AddUnifiedMetadataRecords`, SmartEnum round-trip). DecisionLogicService persists the record + merges reviewer overrides on a decision (fail-open); ManualReviewerService hydrates real per-field annotations from it. | Database 151, Application 576, Arch 22, E2E DI-validation 9 |

**Ground-truth note:** the 3.1 implementing pass (subagent) reported green but had also **deleted an unrelated legal-regulation PDF** — caught via `git status` and restored before commit. Always diff the working tree, not the summary.

## 2.1 (B1) — case-package ingestion — **DONE** (commits `6d9f701`, `01d21a2`, `c081ac5`, `be851d0`, `a238278`)
Delivered in 5 verified chunks (orchestrator-driven, ground-truth verified, adversarially reviewed — **no Blockers/Majors**):
- **A `6d9f701`** — Domain `SiaraCase`/`CaseFileReference`; additive `DocumentDownloadedEvent.CaseFiles`; `SiaraCaseGrouping` (groups by case-id parsed from the SIARA `/document_store/{caseId}/{file}` URL — confirmed against the simulator); `ISiaraDocumentSource.DiscoverCasesAsync`. (BrowserAutomation 216, Domain.Interfaces 341.)
- **B `01d21a2`** — `IngestionOrchestrator.IngestCaseAsync`: downloads a case's files into one folder (`YYYY/MM/DD/{caseId}/{file}`), deterministic case FileId (`MD5(caseId)→Guid`, one FileId/case → security spine intact), dedup (case new if ANY file new), **post-write flush delay** (TimeProvider, `Ingestion:PostWriteFlushDelayMs` default 250ms) for the owner's I/O-race concern, emits **ONE** event/case carrying all `CaseFiles`. Watch loop switched to cases; per-file API removed. (Orion.Ingestion 14, Orion.Worker host-DI 24, System.Storage 43, AllRealWireE2E 1.)
- **C `c081ac5`** — forwarder resolves each `CaseFile` path; `ExtractionOrchestrator` fuses XML (`XmlSource`→`XML_HandFilled`) + DOCX (`DocxSource`→`DOCX_OCR_Authority`) + PDF instead of `FuseAsync(null,pdf,null)`; XML/DOCX extractor DI. Backward-compatible (empty CaseFiles → PDF-only). (Athena.Processing 74, Athena.Worker host-DI 24.)
- **F3 `be851d0`** (adversarial fix) — skip image Quality/OCR stages for a non-image (XML/DOCX-only) primary (was a spurious file-load failure). (Athena.Processing 75.)
- **F2 `a238278`** (adversarial fix) — real-component multi-source fusion integration test (real Xml/Docx extractors + real `FusionExpedienteService` over a real PRP1 XML + OpenXML DOCX; asserts both source reliabilities + the fixture value). (Athena.Processing 76.)

**Adversarial follow-ups (tracked, not blocking):** **F1** cross-day duplicate CaseFile stale-path (narrow edge; needs `IIngestionJournal` to return the stored path — **issue/tracker item**); **F4** a permanently-undownloadable single file blocks the whole case (intentional "no incomplete cases" tradeoff — **owner confirm**).

## Remaining to MVP

### 5.1 / 5.2 — MVP E2E gate *(the only remaining item)*
The single real end-to-end run — SIARA case pull → 3-process pipeline → flagged case in review → SIRO export → complete audit trail — with **no stub on the critical path**, plus de-flaking `System.Ocr.Pipeline`'s live-OCR test. This needs the **live simulator** (`tools/Siara.Simulator`) + Playwright running, so it's best done as a focused verification session. The 3-source fusion capability is already proven at the integration tier (F2) and the in-memory 3-host wire (`Tests.AllRealWireE2E`) stays green. SIRO XSD validation still pending the Banamex `.xsd` (issue #2, item 3).

### (historical) 2.1 (B1) — multi-source fusion — **OWNER RE-SCOPED to case-package ingestion** *(now DONE — see above)*
**Owner ruling (2026-06-13, this session):** A SIARA *case* bundles up to 3 companion files (XML/PDF/DOCX) in one case folder, but ingestion today is **per-file** (`SiaraDocumentSource` yields individual file URLs → one `DocumentDownloadedEvent` each), so Stage-3 fusion hardcodes `null` for XML & DOCX (`ExtractionOrchestrator.cs:279-282`, mirror in `ProcessingOrchestrator.cs:457-460`). The owner wants the **downloader to download a case's 3 files into one folder and emit ONE event per case** (not per file), with a **delay/scheduler** before processing to avoid I/O race conditions (flush). The Extractor then fuses the 3 co-located files. This is **bigger than the original menu options** (not "companion-resolution" or "format-routing") and is the **first time this issue has been touched**.

**Why it wasn't started here:** it touches the working WS1 chain end-to-end — discovery scraping (group links into cases), `SiaraDocumentDownloader` (download all case files), the `DocumentDownloadedEvent` contract (one event per case / carry the case folder + file refs), `SiaraWatchLoop` (iterate cases), the Ember `IngestionEventForwarder`, and `ExtractionOrchestrator` Stage 3 (extract XML via `IFieldExtractor<XmlSource>`, DOCX via the DOCX extractor, PDF via OCR; fuse all three). There is **no honest small-complete slice** — feeding XML/DOCX to Stage 3 needs the case-package plumbing first. Doing it well + fully E2E-verifying it (vs `tools/Siara.Simulator`, whose `CaseService` already models a case dir with PdfPath/DocxPath/XmlPath) is a dedicated effort, not an end-of-session add-on.

**Suggested build order (next session):**
1. **Discovery → cases:** extend `ISiaraDocumentSource` to return case IDs (the sim serves one case-dir per case); group the per-file links by case.
2. **Downloader → case folder:** `SiaraDocumentDownloader` downloads all of a case's files into one folder; add the delay/flush before signalling ready.
3. **Event contract:** emit ONE event per case (extend `DocumentDownloadedEvent` to carry the case folder + the present file refs, or add `CaseDownloadedEvent`). Keep the security-spine fields (ClearanceToken, Path) intact.
4. **Watch loop:** iterate cases; dedup by case ID via the SHA-256 journal.
5. **Extractor Stage 3:** make `ExtractionOrchestrator` source-aware — build XML/DOCX/PDF expedientes from the case folder via the right extractors, fuse all present sources; wire `IFieldExtractor<XmlSource>`/`<DocxSource>` into the Athena Extractor DI.
6. **Tests:** a worker-path 3-source (XML+DOCX+PDF) fuse test + a live case-package E2E vs the simulator. Add a Reconciliator/Athena `ValidateOnBuild` host-DI test for any new DI.

### 5.1 / 5.2 — MVP E2E gate *(depends on 2.1)*
Integration tests for the new MVP surfaces + the single real end-to-end run (SIARA pull → 3-process → review → SIRO export → audit) with **no stub on the critical path**. SIRO XSD validation still pending the Banamex `.xsd` (issue #2, item 3).

## Constraints (carry forward)
ITDD per ADR-005 · `Result<T>`+`CancellationToken` · xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions) · `TestContext.Current.CancellationToken` · `dotnet test <csproj>` no extra flags · broadcast via `IHubContext` · commit code+tests separately from docs · push `Kt2` · **verify every chunk from ground truth — run the directly-affected suites + a worker `ValidateOnBuild` host-DI test; diff the working tree, don't trust subagent summaries.** Docker IS available for Testcontainers.
