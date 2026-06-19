# RC.3 — Negative-Space Lenses: Real-Data Journey + Scale/SLA/Failure-Mode (Veriqan VEC)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Phase:** RC.3 (negative-space lenses) · **Lenses:** Real-Data Journey + Scale/SLA/Failure-Mode
**Auditor:** Claude Code (read-only on production code; no source modified)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` §5
**Builds on:** RC0b (reality map), RC1-extraction, RC1-validation-computation, RC1-persistence-ingestion.
**Bar:** Full production. Believe wiring + file:line, never comments or prose. A green unit test is NOT evidence of E2E readiness.

> **Purpose:** surface *unknown-unknowns* — readiness gaps that no requirement in the 121-item register captured.
> These lenses walk a real statement's journey and stress the system's failure modes, hunting for the gaps
> that don't show up as a red test or a missing epic.

**Severity legend:** `Blocks` = production cannot run / cardinal-rule or correctness risk · `Degrades` = works but materially limited / unsafe on real data · `Cosmetic` = polish.

---

## Lens 1 — Real-Data Journey (one real CONDUSEF statement, stage by stage)

This lens walks a single, genuine CONDUSEF VEC statement PDF from a real bank through the eight pipeline
stages (ingest → extract → bind → validate → verdict → report → persist → notify) and logs every place it
**cannot run on real input today**.

| Stage / Scenario | What happens today (file:line) | Real-data gap | Severity |
|---|---|---|---|
| **0. Physical entry point** | The Worker (`04 Services/Veriqan.Worker/Program.cs`, 21 lines) exposes only `/health` + `/health/live`. No HTTP `POST /verify`, no `IHostedService` folder/queue watcher, no message-bus consumer, no CLI. `IBatchProcessor` is DI-registered (`VeriqanOrchestrationExtensions.cs:92`) but **never invoked** by the Worker. `IVerificationPipeline.ProcessAsync` is reachable only from test harnesses. | **A real PDF cannot physically enter the running system by ANY mechanism.** RC.1 said "nowhere" — *confirmed total*: no folder watcher, no HTTP POST, no queue consumer, no CLI exist in the shipped composition root. The entire downstream pipeline is unreachable. | **Blocks** |
| **1. Ingest** | `StatementIngestionService.IngestAsync` validates `%PDF-` magic (`StatementIngestionService.cs:65,144`), SHA-256 hashes (`:75`), idempotent dedup (`:80,97`). Persists `VerificationJob` only. Logic is production-grade. | Real-data-safe *as logic*, but unreachable (no caller in Worker). With no `appsettings.json` in the Worker, persistence defaults to **InMemory** (RC0b §Persist) → every ingested job evaporates on restart. | **Blocks** (unreachable + non-durable) |
| **2a. Extract — text-layer assumption** | `ExtractFullAsync` opens with `PdfDocument.Open(pdf)` then immediately `doc.GetPage(1).GetWords()` (`PdfPigStatementFieldExtractor.cs:189,192-193`). **PdfPig reads the embedded text layer only — it does NOT do OCR.** A scanned / image-only statement returns **zero words**, no exception. | **MAJOR unknown-unknown: an image-only/scanned statement does NOT fail — it succeeds with every field `Missing`/`NotFound`.** The model is built (`:292`), `Result.WithSuccess` returned (`:319`); the pipeline proceeds and the verdict aggregator sees a statement of all-InsufficientData → **GREEN** (`VerdictAggregator.cs:123`). A scanned statement is silently waved through as compliant. No requirement covers scanned PDFs. | **Blocks** |
| **2b. Extract — fixture geometry** | Header Y-bands (`HeaderYMin=530`…), DESGLOSE/§19/§20/§8 column X-ranges, and the layout block (`PdfPigStatementFieldExtractor.cs:46-91, 1084-1092, 3297-3327`) are empirically calibrated to **Dummie VEC fixture #1**. | A real bank's layout (different X/Y geometry, different fonts, multi-column variants) will silently **mis-associate or miss** fields — and report them `Extracted`@confidence 1.0 (constants, not measured — `ExtractedField.cs:69-83`). The "abstain on low confidence" safety net (NFR-8) **cannot fire** on a mis-extracted-but-present value. | **Blocks** |
| **2c. Extract — confidence is a constant** | `ExtractedField<T>` / `TableCell` hardcode `Confidence = 1.0 / 0.7 / 0.0` (`ExtractedField.cs:69-83`, `TableCell.cs:30-59`). | The `ConfidenceGuard.BelowThreshold` abstain path (`ConfidenceGuard.cs:53`) is driven by a **binary found/not-found** signal, not a measured score. A wrongly-extracted real value → `1.0` → no abstain → false FAIL/PASS. | **Blocks** |
| **3. Bind — reference data** | `BundleBinder.BindAsync` → `CsvReferenceDataAdapter.GetBundleAsync` (`CsvReferenceDataAdapter.cs:110-113`): if `RootDirectory/<institution>` doesn't exist → `Result.WithFailure` → BLOCKED. Worker configures **no CSV root** (no appsettings.json). | The **only** reference bundle that exists is `Demo_Bank_(Iqubica)` — and it lives inside a **test project's `TestAssets/csv/`** (`Veriqan.Infrastructure.ReferenceData.Tests/TestAssets/csv/Demo_Bank_(Iqubica)/*.csv`), not a deployable data dir. There is **zero real bank reference bundle** anywhere in the repo, and no defined provenance for where one would come from (who authors `interest-rates.csv`, `products.csv`, `tolerance-config.csv`, `mandatory-legends.csv` for a real institution?). On real input today: **every bind → BLOCKED**. | **Blocks** |
| **4. Validate — thresholds uncalibrated** | All tolerance defaults/ceilings are in-code constants flagged `⚠️ ESTIMATED` (`DefaultLegalToleranceProvider.cs:61-92`); §6 ±1-month, §19 rate %-vs-fraction ambiguous zone `(1.0,1.5]`, §16 9-column index map, MinFieldConfidence `0.8` are all spec-derived/guessed, **never measured against a real corpus** (RC1-validation D1–D9). | The pass/fail boundary of every arithmetic rule is an **unverified assumption**. On a real statement these can false-FAIL (boundary too tight) or false-PASS (too loose). The calibration *harness* exists (`Veriqan.Orchestration.Tests/Calibration/`) but its corpus is the 3 KnownSynthetic fixtures — **zero KnownGood, zero KnownBroken** (RC0b §2.3), so FPR/DR are vacuous. | **Blocks** |
| **4b. Validate — §6 moat is dark** | `Section6PaymentSimulationRule` returns InsufficientData on every available input because §6 is absent in all 3 fixtures and the §6 extractor is uncalibrated (`PdfPigStatementFieldExtractor.cs:3885,3904-3914`; RC1-validation D3). | The headline differentiator (Banxico revolving-balance recursion) has **never executed on a present §6 table** — neither synthetic nor real. Completely unproven on real data. | **Blocks** |
| **5. Verdict** | `VerdictAggregator.Aggregate` — BLOCKED > RED (any Fail) > GREEN (all Pass/InsufficientData) (`VerdictAggregator.cs:64-130`). InsufficientData never escalates to RED (`:123`). Pure, deterministic. | Logic correct and cardinal-rule-honoring *by construction*. But never demonstrated on a real KnownGood statement (none exist) — the "never false-blocks a compliant statement" property is **asserted, not empirically proven**. | **Degrades** |
| **6. Report (marked PDF)** | `MarkedPdfGenerator` (PdfSharp) is DI-wired (`ServiceCollectionExtensions.cs:40`). **But `IMarkedPdfGenerator` is referenced ONLY in its own file, its tests, and the `DemoChecklist` E2E test — NEVER in `VerificationPipeline` or `BatchProcessor`** (grep confirmed). | **The wired pipeline ends at verdict aggregation (Stage 7, `VerificationPipeline.cs:341`). There is NO report stage.** No marked PDF is ever produced for a processed statement in the composed system. A reviewer/regulator gets no annotated artifact. Unknown-unknown: the pipeline is 6 stages, not 8 — report + notify are orphaned adapters. | **Blocks** |
| **7. Persist (verdict + findings)** | The wired `VerificationPipeline` persists **only the `VerificationJob`** (via ingestion `:114`). The `VerificationOutcome` (verdict + findings) is returned **in memory** (`:341`) and **never written** to Findings/JobVerdict tables (RC1-persistence §1). Tables/configs/migrations exist but nothing writes them. | A regulator asking "show the stored verdict + findings for statement X" **cannot be served**. The audit/disposition trail (`AddVeriqanDisposition`) is orphaned — never called by `AddVeriqan`. Even with SQL configured, resume-state + reprocess-audit are InMemory-only (no EF impl exists). | **Blocks** |
| **8. Notify (alert email)** | `VecAlertService.SendRedAlertAsync` (Polly retry, never silently drops — `VecAlertService.cs:58,117`) is DI-wired. **But `IVecAlertService` is referenced ONLY in its own file + tests — NEVER in the pipeline** (grep confirmed). | **No RED alert is ever sent for a processed statement.** The notify stage, like report, is an orphaned adapter. A RED verdict produces no email, no marked PDF, no persisted finding — it simply returns an in-memory object to a caller that doesn't exist (no entry point). | **Blocks** |
| **Cross — multi-tenant baseline** | `TenantProfile = LegalBaseline()` single fixed profile (`VeriqanVerdictExtensions.cs`); per-statement selection is E13-gated. | A real deployment serving >1 bank cannot select per-tenant legal baseline ⊕ overlay; FR-8 bundle-`toleranceConfig` channel is not even wired (RC1-validation D10). | **Degrades** (E13-gated) |

### Lens-1 synthesis

The honest end-to-end picture for a **real** statement: it **cannot enter** (no intake); if hand-fed via a test
harness it would **BLOCK at bind** (no real reference bundle, no CSV root); if a bundle were provided, a
**scanned PDF would silently pass GREEN** (no OCR, all-Missing → no Fail); if text-layer, **fixture-locked
geometry** would mis-extract on a real bank layout with **constant 1.0 confidence** defeating the abstain net;
the verdict would compute correctly but **never be persisted, never produce a marked PDF, never send an alert**
(report + notify stages are orphaned adapters). The journey breaks at **every** stage on real input.

---

## Lens 2 — Scale / SLA / Failure-Mode

This lens reasons about robustness from the code (production volume can't be run here). It probes malformed
input, partial reference data, concurrency, shared state, downstream outage, and whether the cardinal rule
(NEVER false-block) survives stress.

| Scenario | What happens today (file:line) | Scale / failure-mode gap | Severity |
|---|---|---|---|
| **Malformed / truncated PDF** | `ExtractFullAsync` wraps the body in try/catch → `Result.WithFailure` (`PdfPigStatementFieldExtractor.cs:321-326`); empty bytes guarded (`:184`); `PdfDocument.Open` throwing on garbage is caught. Pipeline maps extraction failure → `Result.WithFailure` (`VerificationPipeline.cs:155-161`). | **Safe: abstains via Result-fail, no crash, no false-block.** A truncated/garbage PDF returns a typed failure → BatchProcessor routes it to the exception queue (`BatchProcessor.cs:200`), batch continues. Good. | **Cosmetic** (handled) |
| **Encrypted / password-protected PDF** | `PdfDocument.Open(pdf)` is called with **no password parameter** (`PdfPigStatementFieldExtractor.cs:189`). PdfPig throws on an encrypted PDF it can't open → caught → `Result.WithFailure`. | Degrades safely (Result-fail, not crash) — but there is **no handling for password-protected statements** (no password port, no config). If a bank PDFs are owner-password-encrypted (common for statements), **every one fails extraction** → exception queue. Not a crash, but a 100%-fail blind spot no requirement names. | **Degrades** |
| **Image-only / scanned PDF** | `PdfDocument.Open` succeeds, `GetWords()` returns empty, **no exception** (`:192-193`). Model built all-Missing, `Result.WithSuccess` (`:319`). | **This is the dangerous one (see Lens 1, 2a).** A scanned statement does NOT fail — it succeeds, every rule abstains (InsufficientData), verdict → **GREEN**. **A non-compliant scanned statement is waved through.** The cardinal rule "never false-block" holds, but its dual "never false-PASS a defective statement" is **silently violated**. There is no "is this statement extractable at all?" gate. | **Blocks** |
| **Missing / partial reference bundle** | Adapter requires only `bundle-metadata.csv`; other sections load as `null` per-absent-section (`CsvReferenceDataAdapter.cs:125-134`); `ReferenceDataAvailability.FromBundle` drives per-check degradation; rules dependent on a missing section → InsufficientData (FR-20). | **Mostly safe** — partial bundle degrades to InsufficientData on dependent checks only. BUT a **completely** missing bundle dir → `Result.WithFailure` → BLOCKED (whole statement), not per-check degradation (`CsvReferenceDataAdapter.cs:111-113`). And `bundle-metadata.csv` is a hard requirement — its absence blocks everything. Acceptable, but "no bundle at all = BLOCKED" is the *current* Worker default state. | **Degrades** |
| **Batch concurrency / backpressure** | `BatchProcessor` uses `SemaphoreSlim(maxParallelism)` (`:88`, default **4** — `BatchOptions.cs:23`), per-item `Task.Run` (`:123`), `Task.WhenAll` (`:242`). | **Genuinely bounded** — not unbounded fan-out. Semaphore caps in-flight work; backpressure is real. BUT: all N `Task.Run` tasks are created up-front into a `List<Task>(total)` (`:113,238`) — for a very large batch this materializes N task objects + N closures immediately (memory pressure at extreme volume), though only `maxParallelism` run concurrently. No streaming/channel intake. Bounded but not stream-shaped. Never run at real volume (NFR-1 p95 ≤ 10 000 ms unmeasured). | **Degrades** (unverified at scale) |
| **Failure isolation in batch** | Per-item `try/catch (Exception ex) when (ex is not OperationCanceledException)` → `ExceptionQueueEntry` (`BatchProcessor.cs:214-229`); pipeline-Result-failures also queued (`:200`); `Task.WhenAll` never short-circuits. | **Excellent: one bad statement does NOT halt the batch.** Exceptions and Result-failures are isolated to a `ConcurrentBag`, counted as `failed`, batch completes. This is correct failure-isolation. **But the exception queue is in-memory only** — it lives in the `BatchReport` returned to the (nonexistent) caller; nothing persists it. A crashed/restarted process loses the entire dead-letter list. | **Degrades** (no durable dead-letter) |
| **Shared state under concurrency** | `BatchProcessor` resolves a **fresh scope per item** via `IServiceScopeFactory.CreateAsyncScope` (`:166`) → scoped `VeriqanDbContext` is correctly per-item (no cross-thread DbContext sharing). InMemory repos use `ConcurrentDictionary`/`ConcurrentBag` (RC0b §InMemory). Counters use `Interlocked`/`Volatile` (`:127-128,287`). | **Thread-safety is sound** — no obvious DbContext-across-threads bug, no torn counters. BUT all four InMemory repos hold state in process memory: **jobs, dispositions, resume-state, reprocess-audit all lost on restart** (RC1-persistence §3). Under a real workload a process recycle = total state loss. State-loss-on-restart, not a race, is the risk. | **Blocks** (state non-durable) |
| **Downstream outage — SMTP down** | `VecAlertService` retries via Polly exponential back-off, then returns `Result.WithFailure` (never silently dropped, logs Error — `:99,117-124`). | Degrades correctly *in isolation* — **but the alert service is never called by the pipeline** (Lens 1, stage 8). So in the composed system an SMTP outage is moot: no alert is attempted at all. The robustness is real but unreachable. | **Degrades** |
| **Downstream outage — DB down** | EF repos return `Result.WithFailure` on failure; ingestion maps it → pipeline `Result.WithFailure` (`StatementIngestionService.cs:86-94`; `VerificationPipeline.cs:123-129`). Worker startup `VeriqanLegalBaselineStartupService` is **fail-loud** (throws if DB init fails) on the SQL path. | A DB outage *during processing* degrades to Result-fail (statement → exception queue, no crash). A DB outage *at startup* on the SQL path is fail-loud (won't boot) — correct for a load-bearing store. No dead-letter that persists failed-due-to-DB items, though. | **Degrades** |
| **Cardinal rule under stress (never false-BLOCK)** | Extraction failure → `Result.WithFailure` → exception queue (not a verdict at all). Rule exception → engine isolates → InsufficientData → never RED (`VecValidationEngine.cs:102-115`; `VerdictAggregator.cs:123`). Timeout/cancel → `Cancelled` Result (not RED). BLOCKED is a binding outcome, distinct from RED. | **The cardinal rule SURVIVES stress for false-BLOCK.** A timeout, exception, or malformed input maps to Cancelled / exception-queue / InsufficientData / BLOCKED — **never to a spurious RED**. RED requires an explicit rule `Fail` finding (`VerdictAggregator.cs:106`). No path maps an infra error to RED. **However**, BLOCKED *is* a non-pass gate, and a missing reference bundle → BLOCKED on a *real* statement that might be perfectly compliant — so "never block a good statement" is technically violable via the bind stage (infra gap → BLOCKED), though BLOCKED ≠ RED. | **Degrades** (RED-safe; BLOCKED-on-infra-gap caveat) |
| **The dual rule (never false-PASS a defective statement)** | All-InsufficientData → GREEN (`VerdictAggregator.cs:123`). Scanned PDF / total mis-extraction → all-abstain → GREEN. | **Not stress-safe.** The system has strong "never false-block" discipline but **no floor on extraction coverage** — a statement that extracts nothing (scanned, encrypted-but-opened-empty, layout-drift) passes GREEN. There is no "minimum N fields extracted or abstain the whole verdict" guard. This is the inverse failure no requirement guards. | **Blocks** |

### Lens-2 synthesis

Robustness-in-isolation is genuinely good: malformed PDFs abstain, batch failure-isolation is correct,
concurrency is bounded and thread-safe, the cardinal **never-false-BLOCK** rule survives every stress path
(infra errors map to Cancelled/InsufficientData/exception-queue, **never** to a spurious RED). The real risks
are (1) **state non-durability** (all InMemory; restart = total loss, no durable dead-letter), (2) the
**scanned/empty-extraction → silent GREEN** dual-rule violation, and (3) the report/notify robustness being
**unreachable** because those stages aren't wired into the pipeline.

---

## Unknown-unknowns surfaced by these lenses

Things **absent from the 121-requirement register** (FR-1..39, NFR-1..8, E1–E13 ACs) that these two lenses exposed:

1. **No scanned/image-only PDF handling — and it fails OPEN, not closed.** PdfPig is text-layer-only (no OCR).
   A scanned statement extracts zero words, builds an all-Missing model, and aggregates to **GREEN**. No
   requirement covers OCR, an extractability gate, or "is this even a text PDF?" The dual of the cardinal rule
   (never false-PASS a defective statement) is silently violated. *(Biggest unknown-unknown.)*

2. **No "minimum extraction coverage" floor / verdict-validity gate.** Nothing guards against a statement that
   extracts almost nothing (scanned, encrypted-opened-empty, or real-layout drift) sailing to GREEN because
   every rule abstained. There is no "if < N fields extracted → BLOCKED/abstain-whole-verdict" invariant. The
   abstain-safety design protects against false-RED but has no symmetric guard against false-GREEN-by-emptiness.

3. **The pipeline is 6 stages, not 8 — report (marked PDF) and notify (RED alert) are orphaned adapters.**
   `IMarkedPdfGenerator` and `IVecAlertService` are DI-wired and unit-tested but **never called** by
   `VerificationPipeline`/`BatchProcessor` (grep-confirmed). A RED verdict produces no annotated PDF and no
   email in the composed system. No requirement noticed that "verdict computed" ≠ "verdict reported + notified."

4. **No real reference-bundle provenance.** The only bundle (`Demo_Bank_(Iqubica)`) is a *test asset* inside a
   test project. There is no defined source, owner, schema-authoring process, or deployment path for a *real*
   bank's `interest-rates / products / tolerance-config / mandatory-legends` CSVs. "Where does a real bundle
   come from?" has no answer — and without it every real bind → BLOCKED.

5. **Password-protected statements are a 100%-fail blind spot.** `PdfDocument.Open` is called with no password;
   if a bank distributes owner/user-password-encrypted PDFs (common), every one fails extraction. No password
   port, no config, no requirement.

6. **No durable dead-letter / exception queue.** Failure-isolation is correct, but the exception queue lives in
   the in-memory `BatchReport`; a process restart loses every failed/un-processable statement with no record.
   For a load-bearing gate, "what failed and why" must persist — no requirement specifies this.

7. **Constant "confidence" defeats the abstain net on real layout drift.** `Confidence = 1.0/0.7/0.0` is a
   3-valued constant, not a measured score (`ExtractedField.cs:69-83`). A *wrongly-extracted* real value reports
   `1.0` and cannot self-flag for abstain — so the NFR-8 safety guarantee silently fails exactly when a real
   bank's geometry differs from the fixture. The register assumes confidence is a real score; it isn't.

8. **State-loss-on-restart is the dominant operational failure mode, not a race.** Every wired repo defaults to
   InMemory (no Worker `appsettings.json`); even with SQL, resume-state + reprocess-audit have no EF impl. A
   routine process recycle erases all jobs, dispositions, audit, and resume checkpoints. No NFR addresses
   durability-across-restart explicitly.

---

*These lenses cover the real-data-journey and scale/SLA/failure-mode negative spaces only. Deploy/ops and
security/compliance lenses are separate RC.3 agents; synthesis into the Readiness Gap Matrix is RC.4.*
