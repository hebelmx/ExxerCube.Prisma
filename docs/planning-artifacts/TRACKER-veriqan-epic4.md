---
name: tracker-veriqan-epic4
date: 2026-06-28
branch: Liv
epic: Epic 4 — Verdict Confidence + Honest BLOCKED Semantics
intended-solution: docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md (Epic 4, lines 193-213)
predecessor-handoff: docs/planning-artifacts/HANDOFF-veriqan-epic2-2026-06-28.md
status: IN PROGRESS (orchestrated)
---

# Veriqan VEC — Epic 4 Tracker (Confidence + Honest BLOCKED)

## Owner-settled design decisions (this session)
- **S4.1 confidence source = Option 2 (auto-capture min of consumed fields).**
  `RuleFinding` gains a `Confidence` (0–1). A context-aware confidence-recorder captures
  every field confidence the rule consumes through `ConfidenceGuard.BelowThreshold`
  (46 uniform call sites). The engine stamps `min(recorded)` onto the finding centrally —
  exactly like it already stamps `DofNumeral` (`RuleFinding.cs:79-91`). Pure structural
  rules that never guard a field default to `1.0`. Verdict-level rollup = `min` of finding
  confidences. Persist + surface a REAL number in the UI (fills the Epic-2 deferred column).
- **S4.2 BLOCKED model — EXPANDED after BMAD party (2026-06-28).** Three non-verdict states:
  - **`VerdictSignal.ExtractionGap`** (=4) — PERMANENT system/engineering capability gap; not retryable
    without engineering action. The 4 known reasons route here: InsufficientExtractionCoverage,
    InsufficientTextLayer, InvalidBundle, UnknownProduct. The demo's `scanned` case reframes
    BLOCKED → ExtractionGap.
  - **`VerdictSignal.TransientFailure`** (=5, NEW per party) — RETRYABLE operational failure
    (cancellation, transient infra/DB, extractor crash/timeout). MUST NOT be ExtractionGap —
    lumping them is a latent bug (caller retry-logic abandons a never-processed doc; split audit codes).
  - **`VerdictSignal.Blocked`** — RESERVED for genuine document defects / human callback. No current
    emitter (documented). The full taxonomy of future Blocked reasons added as documented BlockReason
    enum values WITHOUT wiring detection this epic.
  - **Decision rule (Winston, adopted):** "Could the same document bytes, resubmitted tomorrow with no
    human action, produce a verdict?" Yes-maybe → TransientFailure; No-but-engineering-fixable →
    ExtractionGap; No-document-must-change → Blocked.
  - **Cardinal guard (John/Mary):** a CONDUSEF-required field CONFIDENTLY ABSENT = RED verdict, NOT a
    gap. Routing confident-absence to a gap hides a real compliance violation (false-safe). Add explicit
    guard; VERIFY current behavior first (a missing mandatory field must not abstain→GREEN).
  - **Multi-statement stub-guard (owner pulled IN):** cheap heuristic (count statement-boundary anchors
    / account numbers / "ESTADO DE CUENTA"); if >1 suspected → ExtractionGap (BlockReason.AmbiguousDocumentScope),
    never a wrong-statement GREEN. Full detection deferred.
  - **DEFERRED + documented as known-risk trust boundary:** tamper detection; FilePreflightGuard
    (zero-byte/oversized/magic-bytes); password/corrupt PDF exception inspection (= Epic 6 S6.7/N1);
    unsupported-language; ref-data version-mismatch; persist/infra dead-letter. Logged for Epic 6 / new passes.
  - ExtractionGap & TransientFailure must NOT count as a compliance pass/fail anywhere
    (batch tally, report gate, alerts) — mirror the YELLOW handling Epic 1 added.
  - Party transcript synthesis: see commit message + this tracker. Full enumeration (Mary's 6-band MECE)
    is the design-of-record for the deferred work.

## Global constraints (carried from handoffs)
- §5c golden-master honesty: corpus = production-quality master. No guard bypass, no forced verdicts.
- Stack: .NET 10, `Result<T>`, `CancellationToken` on every async, nullable + TreatWarningsAsErrors.
- Tests: xUnit v3 + Shouldly + NSubstitute. `dotnet test <csproj>` FALSELY reports "Zero tests ran";
  build the project then `dotnet exec /home/abel/ExxerProjects/IndFusion/BuildArtifacts/Prisma/bin/<Asm>/Debug/net10.0/<Asm>.dll`.
- `docs/qa/calibration/calibration-report.md` regenerates on some test runs — `git checkout --` it, don't commit.
- Demo Web.UI has NO test project — UI verification bar = `dotnet build` 0/0 + reasoning on DemoDataService.
- Verify every claim from ground truth (build/test/git). Commit in self-contained chunks. Push to `Liv`.

## Ground-truth file map (verified this session)
- `VerdictSignal` enum: `01 Core/Veriqan.Domain/Enums/VerdictSignal.cs` (Green=0,Red=1,Blocked=2,Yellow=3)
- `RuleFinding`: `01 Core/Veriqan.Domain/Verification/RuleFinding.cs` (record; DofNumeral stamped centrally by engine)
- `ConfidenceGuard`: `01 Core/Veriqan.Domain/Verification/ConfidenceGuard.cs` (static; `BelowThreshold<T>(field,threshold)`, `Reason(...)`)
- `VerdictSummary`: `01 Core/Veriqan.Application/Verdict/VerdictSummary.cs`
- `VerdictAggregator`: `01 Core/Veriqan.Application/Verdict/VerdictAggregator.cs` (`Aggregate(findings, blocked, ct, tenantDeviations, checklistTiers)`)
- `IVecValidationRule`: `01 Core/Veriqan.Application/Validation/IVecValidationRule.cs`
- Rules (~49): `02 Infrastructure/Veriqan.Infrastructure.Validation/Rules/*.cs` (46 ConfidenceGuard sites)
- Pipeline floors (Blocked today): `03 Orchestration/Veriqan.Orchestration/Pipeline/VerificationPipeline.cs` (~213-299 coverage floor; MinTextLayerWordCount text-layer floor)
- Persistence: `01 Core/Veriqan.Domain/Entities/JobVerdict.cs`, `Finding.cs`;
  `02 Infrastructure/Veriqan.Infrastructure.Persistence/EntityFramework/VeriqanDbContext.cs`;
  migrations in `…Veriqan.Infrastructure.Persistence/Migrations/`
- UI: `07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Models/DemoFinding.cs` (deferred confidence comment ~42-48),
  `DemoStatementCase.cs`, `Components/Shared/VerdictBanner.razor`, `DemoDataService`, `RedCase.razor`/`YellowCase.razor`

## Story / chunk tracker
| # | Chunk | Story | Status | Commit | Verification |
|---|-------|-------|--------|--------|--------------|
| 1 | A1 confidence domain+app | S4.1 | DONE `b76927c6` | App.Tests 134/134, Validation.Tests 479/479; build 0/0 |
| 2 | A2 confidence persistence | S4.1 | DONE `d68d2df8` | Persistence.IntegrationTests 8/8 (Testcontainers SQL); migration AddConfidenceColumns |
| 3 | A3 confidence UI | S4.1 | DONE `a5ad1738` | Web.UI build 0/0; real Confianza % column; placeholder retired |
| 4a | B1a ExtractionGap+TransientFailure taxonomy | S4.2 | DONE `e796f02c` | App 134/134, Orch 81/81, Validation 479/479; 3 floors emit ExtractionGap; Blocked reserved |
| 4b | B1b correctness guards | S4.2 | DONE `e49ec905` | confident-absent=RED (verified+test); multi-statement stub. Orch 90/90, Validation 483/483, App 151/151 |
| 5 | B2 ExtractionGap UI | S4.2 | pending | | Web.UI build 0/0; scanned case -> ExtractionGap; banner renders ExtractionGap; confidence legend |
| R | Adversarial review | epic | pending | | refute against this tracker + epic doc; triage |

### More review items for gate R (do NOT lose)
- **TransientFailure has NO emitter (by design).** Verified the pipeline surfaces transient failures as Result.WithFailure (extraction stage `VerificationPipeline.cs:190-205`) / Cancelled<T> — never a persisted permanent verdict, so there was no misrouting bug. Residual risk = extractor swallowing a crash into empty-success → coverage floor → ExtractionGap; lives in the extractor impl = Epic 6 S6.7. Review should confirm this reasoning + that no batch path silently drops transient-failed items.
- **Guard #2 multi-statement is a COARSE stub** (PageCount>20). Does NOT catch 2 short concatenated statements (the dangerous case); could false-positive on a long single statement. Real detection (account-number anchors) deferred to Epic 6 + needs extractor plumbing. Review the honesty of the framing + that the demo corpus is ≤20 pages (it is — demo E2E green).
- **Persisted ExtractionGap (signal=4) not covered by a persistence integration test** (A2's test predates B1a). Low risk (enum→int stores 4), but review/should add coverage.

### Review items to scrutinize at gate R (do NOT lose)
- **A3 InsufficientData confidence = 1.0.** DemoDataService shows 1.0 on InsufficientData rows (rationale: confidence = input-read certainty, not verdict certainty). But A1's recorder records the LOW field confidence on the below-threshold abstain path, so a real low-confidence abstain yields <0.8, not 1.0. Verify the demo's InsufficientData rows represent non-low-confidence abstains (missing reference data / cannot-determine) — and consider a column legend/tooltip clarifying "confidence = certainty of the value read, not of the verdict." Also: the scanned case's 55 synthesized InsufficientData findings become moot once B2 reframes it to ExtractionGap.
- **A2 verdict-confidence recomputed in 2 persistence services** (min of findings) rather than reading VerdictSummary.Confidence — DRY/drift risk if the rollup rule ever changes. Currently identical; low priority.

## Notes / carried flags (do NOT lose)
- LATENT (Epic 1 flag, NOT Epic 4): `checklist-tiers.csv` row 2 compound key `CL-27/CL-30/CL-47`
  stored literally; production aggregator lookup misses → those 3 mis-tiered to Condusef floor.
  Owner decision still pending on canonical CheckId format. Leave alone unless it blocks Epic 4.
