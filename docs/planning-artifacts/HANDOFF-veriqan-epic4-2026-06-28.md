---
name: handoff-veriqan-epic4
date: 2026-06-28
branch: Liv
status: Epic 4 (Verdict Confidence + Honest BLOCKED Semantics) DELIVERED — handoff for Epic 5/3/6
intended-solution: docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md (Epic 4, lines 193-213)
tracker: docs/planning-artifacts/TRACKER-veriqan-epic4.md
predecessor: docs/planning-artifacts/HANDOFF-veriqan-epic2-2026-06-28.md
---

# Veriqan VEC — Epic 4 Handoff (Confidence + Honest BLOCKED)

Orchestrated build of Epic 4. Every chunk verified from ground truth (build 0/0 + `dotnet exec`
test runs + `git diff` + persistence integration vs Testcontainers SQL), committed in self-contained
chunks, pushed to `Liv`. A two-skeptic adversarial review ran at the epic boundary; its cheap
honesty/observability findings were fixed in a follow-up. A **BMAD design party** (architect + dev +
PM + analyst) was convened mid-epic to settle the BLOCKED taxonomy before coding.

Commits: `b76927c6` (A1), `d68d2df8` (A2), `a5ad1738` (A3), `e796f02c` (B1a), `e49ec905` (B1b),
`19e8ebda` (B2), `eec52c98` (review cleanup) + tracker docs.

## What was delivered

| Story | What | Verification |
|---|---|---|
| S4.1 confidence (A1) | `RuleFinding.Confidence` (0-1), auto-captured as the MIN confidence of the GUARDED scalar fields a rule consumes (via `VerificationContext.ConfidenceBelowThreshold`, 23 rule sites) and stamped centrally by `VecValidationEngine` — mirroring the existing DofNumeral stamp. `VerdictSummary.Confidence` = min across findings. | App 134/134, Validation 479/479 |
| S4.1 persistence (A2) | `Finding.Confidence` + `JobVerdict.Confidence` (SQL float, default 1.0, `HasSentinel(-1.0)` so a real 0.0 isn't promoted to default); threaded through Ef + InMemory persistence; migration `AddConfidenceColumns`. | Persistence.IntegrationTests 8/8 (Testcontainers SQL) |
| S4.1 UI (A3) | `DemoFinding.Confidence` + Confianza % column in RedCase/YellowCase; honest values (structural=1.0, arithmetic≈0.94 "representative-not-measured"). | Web.UI 0/0 |
| S4.2 taxonomy (B1a) | `VerdictSignal.ExtractionGap`(=4) + `TransientFailure`(=5). All 4 known BlockReasons route to ExtractionGap (3 pipeline emit paths); `Blocked` RESERVED (no emitter); 9 future-taxonomy BlockReasons added as documented enum values (no detection). Non-verdicts excluded from compliance pass/fail (aggregator absolute precedence, batch tally, report gate). | App 134/134, Orch 81/81, Validation 479/479 |
| S4.2 guards (B1b) | Guard #1 confident-absent mandatory SECTION = RED (verified already-correct + regression test). Guard #2 multi-statement stub (Stage 2d, PageCount>20 → ExtractionGap/AmbiguousDocumentScope). | Orch 90/90, Validation 483/483, App 151/151 |
| S4.2 UI (B2) | VerdictBanner distinct non-compliance arms for ExtractionGap/TransientFailure (+ Blocked reserved); scanned demo case reframed Blocked→ExtractionGap with system-gap copy; confidence legend + InsufficientData "—". | Web.UI 0/0 |
| review (eec52c98) | Honesty: confidence-semantics docs/tooltip; 3 stale "BLOCKED" logs→ExtractionGap; batch doc/labels. | Orch 90/90, App 151/151 |

## The settled design (owner + party)
Three non-verdict states by the rule **"could the same document bytes, resubmitted tomorrow with NO
human action, produce a verdict?"** — Yes-maybe → **TransientFailure** (retryable); No-but-engineering-
could-fix → **ExtractionGap** (permanent system gap); No-document-must-change → **Blocked** (genuine
document defect, human callback — reserved). Full party enumeration (Mary's 6-band MECE map of every
total-rejection scenario) is the design-of-record for the deferred detection work.

## Adversarial-review verdict
Two independent skeptics ran the suites for real and both concluded: **abstain-safety HOLDS, no
CRITICAL, no new spurious-GREEN, all acceptance criteria met.** Strongest concern (M1, addressed by
documentation): a `Confidence=1.0` overstates certainty because rules deciding on non-scalar evidence
(movement/section/legend) never record a confidence — the real abstain-safety still rests on the three
pre-validation floors, not on the confidence number. See the tracker's "DONE" section for the full list.

## Carried items / deferred (in tracker + commit msgs — do NOT lose)
- **M1 deeper fix** (guard movement/section/legend evidence or a sub-1.0 sentinel) → Epic 6.
- **M2:** confident-absent=RED is SECTION-scoped; mandatory FIELD absence abstains by design (floors backstop). Intended; B1b "ALL mandatory rules" phrasing was overstated.
- **M3:** multi-statement guard is a coarse PageCount>20 stub; misses 2-short-concatenated; real detection (account anchors) needs extractor plumbing → Epic 6.
- **TransientFailure has no emitter** by design (transient = Result.WithFailure for caller retry; verified no misrouting bug; residual = extractor swallowing a crash into empty-success = Epic 6 S6.7).
- A2 DRY (confidence recomputed in persistence vs summary); ExtractionGap lacks a persistence integration test (low risk).
- **Epic-1 LATENT:** CL-27/CL-30/CL-47 compound-key mis-tier can downgrade a section RED→YELLOW. Still pending owner decision on canonical CheckId format.

## Gotchas for the next agent (carried + new)
- Tests: `dotnet test <csproj>` falsely reports "Zero tests ran". Build, then `dotnet exec /home/abel/ExxerProjects/IndFusion/BuildArtifacts/Prisma/bin/<Asm>/Debug/net10.0/<Asm>.dll`.
- `docs/qa/calibration/calibration-report.md` regenerates on some runs — `git checkout --` it, don't commit.
- Demo Web.UI has NO test project — UI bar is build 0/0 + DemoDataService reasoning.
- `EnforceCodeStyleInBuild` + TreatWarningsAsErrors: 0/0 is the real bar (warnings fail the build).
- `Liv` is shared — another infra commit landed mid-epic (`1a6860c6`); rebase, don't merge.

## Remaining Epic plan (not started)
Per the plan's suggested sequence (Epic 1,2,4 now done): **Epic 5** (CL-21 completeness — small) →
**Epic 3** (enhanced compliant master, proves GREEN path) → **Epic 6** (Tier-B hardening +
all the Epic-4 deferred detection work above).
