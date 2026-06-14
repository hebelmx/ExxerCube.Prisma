# HANDOFF / INTENDED-SOLUTION — 2026-06-14 — GH #6: IsComplete → review dashboard (full wire)

> **✅ DONE 2026-06-14 (Kt2 `2c5b01d` feat + `e3fd56a` test, pushed).** All chunks A–E landed and
> verified from ground truth. Adversarial review (plan-completion-reviewer): **intent genuinely met —
> no Blocker, no Major; 3 Minor.** Findings #3 (contract base lacked IncompleteCase coverage) and #2
> (null-classifier partial coverage) CLOSED in chunk E; finding #1 (PersistReviewCaseAsync `catch
> (Exception)` also swallows OCE) LEFT AS-IS deliberately — it matches the established fail-open
> audit-helper pattern in the same files, and the pipeline re-checks the token immediately after.
> Verified: solution 0/0; Web.UI 0/0; Architecture 22/22; Tests.Domain.Interfaces 342/342,
> Tests.Infrastructure.Database 158/158 (1 unrelated pre-existing perf flake
> `SLAEnforcerServicePerformanceTests.BulkUpdates_MultipleFiles_PerformsEfficiently` — green in
> isolation), Athena.Processing.Tests 88/88. **Remaining honest nuance (logged, not a defect):** when
> classification is null AND the case is partial, the synthesized `new ClassificationResult()`
> (Confidence 0 / Level2 null) also yields LowConfidence + Ambiguous rows alongside IncompleteCase —
> rare (the classifier is always wired in prod) and arguably correct (an unclassifiable partial *is*
> low-confidence). The key guarantee (IncompleteCase still persisted) is test-pinned.

**Branch:** `Kt2` · **Driver:** bmad-orchestrator (delegate → **verify every chunk from ground truth**).
**Owner rulings (this session):** (1) **Full wire** — actually persist review cases from the pipeline so a
partial case appears flagged at runtime; (2) model "incomplete" as a **new `ReviewReason.IncompleteCase`**
enum value (NO EF migration — the reason column already exists, we only store a new value).

---

## The problem (ground truth, verified 2026-06-14)

`DocumentDownloadedEvent.IsComplete` (Domain/Events) is **set** by Orion's `IngestionOrchestrator`
(`IngestionOrchestrator.cs:363`, `caseRefs.Count == siaraCase.Files.Count`) but **nothing consumes it**.
Worse, tracing the pipeline showed the **review-case table is never populated at runtime**:

- `ManualReviewerService.IdentifyReviewCasesAsync` (the ONLY code that writes `ReviewCase` rows) is **never
  called in production** — only UI DI registration + tests.
- The worker pipeline (`ReconciliationOrchestrator` Stage 4) only **publishes** a `DocumentFlaggedForReviewEvent`;
  its only consumer is `EventPersistenceWorker` (generic event store, not a `ReviewCase`).
- So the dashboard reads `ReviewCases` but the running system never writes them.

GH #6 therefore requires wiring review-case persistence INTO the pipeline AND threading `IsComplete` to it.

## Key facts / constraints

- `Athena.Processing.csproj` references **only Domain** → the production review-case call must go through the
  Domain port **`IManualReviewerPanel`** (NOT Application `IDecisionLogicService`). No new project refs (keeps
  Architecture tests green — currently 22/22).
- **DI lifetime trap:** `ReconciliationOrchestrator` is a **singleton** built via `new` from the root provider;
  `IManualReviewerPanel → ManualReviewerService` is **scoped** (scoped `PrismaDbContext`). MUST resolve
  `IManualReviewerPanel` **per-document via `IServiceScopeFactory`** (same pattern the pipeline services use for
  `IAuditLogger` — see `ExtractionPipelineService.EmitAuditAsync`). Direct injection trips `ValidateScopes`.
- `ReviewReason` is a `EnumModel` SmartEnum (`Domain/Enum/ReviewReason.cs`); add
  `IncompleteCase = new(3, "IncompleteCase", "Incomplete case (missing companion file)")`.
- Backward-compat everywhere: add `bool isComplete = true` defaults so existing callers/tests compile unchanged
  (mirrors the existing `IsComplete = true` default on the event).
- `ExtractionCompletedEvent` (the Extractor→Reconciliator cross-process handoff) does **NOT** carry IsComplete —
  it must, so the split path can flag. Plain `bool` serializes fine over SignalR (unlike the SmartEnum-on-wire bug).

## The two threading paths (both must carry IsComplete to ReconcileAsync)

- **Monolith** (`ProcessingOrchestrator.ProcessDocumentAsync`): has `downloadEvent.IsComplete` directly →
  pass to `ReconcileAsync(..., isComplete: downloadEvent.IsComplete)` (`ProcessingOrchestrator.cs:159`).
- **Split** (Extractor → Reconciliator):
  - `ExtractionPipelineService.ProcessAsync` builds `ExtractionCompletedEvent` (`:217`) → stamp
    `IsComplete = downloadEvent.IsComplete`.
  - `ReconciliationEventForwarder` republishes the event verbatim (IsComplete rides along).
  - `ReconciliationPipelineService.ProcessAsync` (`:161`) → `ReconcileAsync(..., isComplete: completedEvent.IsComplete)`.

## Review-case creation + idempotency (in `ManualReviewerService.IdentifyReviewCasesAsync`, new `bool isComplete`)

Restructure the method (currently `:428-570`) so the **incomplete dimension is orthogonal** to the existing
confidence/ambiguity/extraction reasons and survives the dedup short-circuit:

1. Query existing non-`Completed` cases for `fileId` once.
2. **Incomplete dimension:**
   - `!isComplete` AND no existing pending `IncompleteCase` → add a `ReviewCase` with
     `RequiresReviewReason = ReviewReason.IncompleteCase`, `Status = Pending`.
   - `isComplete` AND ≥1 pending `IncompleteCase` exists → **heal**: set those rows' `Status = Completed`
     (partial→complete idempotency; the case is no longer incomplete). Persist the status change.
3. **Confidence/ambiguity/extraction dimension:** keep current behavior — only create these when NO existing
   non-Completed case exists (the dedup guard at `:464` stays, but applied to *these* reasons only, not to
   the incomplete-heal).
4. Save once if anything was added OR healed.

Deterministic `FileId` (Orion `DeterministicGuid(caseId)`) is stable across re-emits → it's the idempotency key.

## Insertion point (production wire)

`ReconciliationOrchestrator`:
- Add optional ctor param `IServiceScopeFactory? reviewCaseScopeFactory = null`.
- Add `bool isComplete = true` to `ReconcileAsync`.
- After Stage 4 (have `classificationResult` + `fusionResult.FusedExpediente`), if `reviewCaseScopeFactory != null`:
  build `var metadata = new UnifiedMetadataRecord { Expediente = fusionResult?.FusedExpediente };`,
  open a scope, resolve `IManualReviewerPanel`, call
  `IdentifyReviewCasesAsync(fileId.ToString(), metadata, classificationResult, isComplete, ct)`. **Fail-open**
  (log Warning, never throw — a review-persistence failure must not crash the pipeline).
  - When `classificationResult` is null (classifier absent/failed) but `!isComplete`, still flag: pass a minimal
    `ClassificationResult` (Confidence 0) so the IncompleteCase row is never silently dropped. Verify
    `ClassificationResult` is constructible; if awkward, guard + log and note as a follow-up.
- Wire the scope factory in both construction sites:
  - `ProcessingOrchestrator` ctor: add optional `IServiceScopeFactory? scopeFactory = null`, pass to the
    `new ReconciliationOrchestrator(...)` (`:108`).
  - Reconciliator Worker factory (`Program.cs:72`): pass `sp.GetService<IServiceScopeFactory>()`.
  - When the scope factory or `IManualReviewerPanel` is absent (no DB wired), persistence is a silent no-op
    (matches the audit no-op pattern) — pipeline still runs.

## Signature ripple (add `bool isComplete = true`, default keeps callers compiling)

`IManualReviewerPanel.IdentifyReviewCasesAsync` (Domain) · `ManualReviewerService` (Infra.Database) ·
`IDecisionLogicService.IdentifyAndQueueReviewCasesAsync` + `DecisionLogicService` (Application, forward the flag) ·
Testing contract/mock/fakes (`ManualReviewerPanelMockFactory`, `ManualReviewerPanelContract`, any fake).

## Dashboard surfacing (Web.UI)

- `ManualReviewDashboard.razor` — add `ReviewReason.IncompleteCase` to the reason filter dropdown + a distinct
  colored chip (e.g. Warning/Orange) in the queue table reason column.
- `ReviewCaseDetail.razor` — render the IncompleteCase reason chip (reuse the reason→color mapping).

## Tracker (status) — ALL COMPLETE

- [x] **A — production wire** (enum + IManualReviewerPanel/ManualReviewerService logic + IDecisionLogicService
      forward + ReconcileAsync threading + ExtractionCompletedEvent + ExtractionPipelineService stamp +
      ReconciliationOrchestrator scopeFactory + ProcessingOrchestrator + Reconciliator Worker DI + Testing
      contract/mock/fakes). **DoD: whole solution builds 0/0.**
- [x] **B — dashboard UI** (chip + filter). DoD: Web.UI builds 0/0.
- [x] **C — tests** (ManualReviewerService incomplete/heal/no-dup via Testcontainers; ReconciliationOrchestrator
      threads isComplete=false → panel called with isComplete=false; ExtractionCompletedEvent carries IsComplete
      over serialization). DoD: target test projects green.
- [x] **D — verify (ground truth) + adversarial review + commit (code+tests separate from docs) + push Kt2 + memory.**
- [x] **E — close review findings** (#3 contract base + #2 null-classifier coverage; #1 left as-is).

## Open / deferred (post-#6)

- GH #6 is functionally closed. Remaining honest nuance: the null-classifier + partial spurious-row
  behavior above (low risk; owner can revisit if it proves noisy in practice).
- Still deferred from the best-effort feature family: per-case retry-budget counter (issue #3);
  SIRO XSD validation pending the Banamex `.xsd` (issue #2); multi-page PDF OCR through the worker.

## Hard constraints (unchanged)

- ITDD per ADR-005; `Result<T>` + `CancellationToken` on every async method; `ConfigureAwait(false)` in library code.
- xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions); `TestContext.Current.CancellationToken`.
- Never pipe `dotnet test` through `tail`; redirect to a file. MTP filter-query needs 4 segments.
- Commit code+tests SEPARATELY from docs; push to `Kt2` (never `main`).
- **Verify every chunk from ground truth** — build the touched projects, run the affected suites, inspect `git diff`.
