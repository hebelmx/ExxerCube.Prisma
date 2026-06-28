---
name: epic-4-veriqan-retrospective
date: 2026-06-28
branch: Liv
epic: Epic 4 — Verdict Confidence + Honest BLOCKED/ExtractionGap Semantics
facilitator: orchestrator (Amelia/Developer voice)
participants: Winston (Architect), Amelia (Developer), John (PM), Mary (Analyst), Dana (QA), hebelmx (Project Lead)
tracker: docs/planning-artifacts/TRACKER-veriqan-epic4.md
handoff: docs/planning-artifacts/HANDOFF-veriqan-epic4-2026-06-28.md
status: first Veriqan retrospective (no prior to follow up on)
---

# Veriqan VEC — Epic 4 Retrospective

## Delivery snapshot
8 commits (`d9fcf3c9`→`5e8f6344`), 6 work chunks (A1/A2/A3/B1a/B1b/B2) + a mid-epic BMAD design party
+ a 2-skeptic boundary review + cleanup. Verified from ground truth: Application 151/151,
Validation 483/483, Orchestration 90/90, Persistence 8/8 (Testcontainers SQL), all builds 0/0.
Boundary review verdict: **abstain-safety holds, no CRITICAL, all S4.1+S4.2 acceptance criteria met.**

## What went well
- **Verify-before-build paid off twice.** Spec said 46 ConfidenceGuard sites → reality 23. Owner
  expected a transient-routing bug to fix → there wasn't one (cancellation already returned
  `Cancelled<T>`). Both times, checking the code first avoided manufacturing work against a wrong premise.
- **Confidence reused an existing pattern** (the central `DofNumeral` stamp in `VecValidationEngine`),
  keeping the 23-site change mechanical instead of a 49-rule sweep.
- **Every chunk verified by running real test binaries** (`dotnet exec`), not trusting subagent
  summaries — caught/confirmed the `HasSentinel(-1.0)` persistence detail and the production-path engine stamp.
- **Party-before-taxonomy sequencing** surfaced the needed 3rd state (TransientFailure) and 2
  spurious-GREEN risks before any code was written.

## What was tricky / risky
- **Scope grew mid-epic.** A binary "ExtractionGap vs Blocked" question became a 3-state taxonomy
  across 6 origin bands (Mary's MECE map). Healthy discovery, but it forced an owner scope call mid-stream.
- **Multi-statement guard is an honest-but-weak stub** (`PageCount > 20`). No cheap signal exists in
  `StatementModel` (header tokens repeat per page). Misses the dangerous case (two short concatenated
  statements). Documented in 3 places, but most likely to be mistaken for real coverage.
- **Confidence overstates certainty (review M1).** `Confidence = 1.0` means "no *guarded scalar* field
  was low," not "fully confident verdict" — rules judging movements/sections/legends stamp 1.0 regardless
  of fidelity. We fixed the *wording* (docs + UI legend), not the *computation*.
- **Shared `Liv` branch divergence** — an infra commit landed mid-epic, push rejected; rebased + re-verified.

## Key lessons (reusable)
1. **Party-before-taxonomy** for any enum that encodes meaning (verdict states, block reasons, tiers).
2. **Verify the assumption before building against it** — cheap grep/read first; don't manufacture motion.
3. **A "stub guard" must be labeled a stub everywhere it appears**, or it reads as coverage.
4. **A score shown to a client is a liability without precise semantics** travelling with it (legend/tooltip).
5. **On a shared demo branch, expect mid-epic divergence** — rebase, don't merge; re-run affected suites after.

## Carried debt → Epic 6 (logged in tracker/handoff)
- M1 deeper fix (guard non-scalar evidence / sub-1.0 sentinel); multi-statement real detection
  (account-number anchors — needs extractor plumbing); password/corrupt = S6.7; full deferred taxonomy
  (tamper, FilePreflight, language, version-mismatch) per Mary's 6-band MECE map.
- **Epic-1 LATENT:** `CL-27/CL-30/CL-47` compound-key mis-tier (can downgrade a section RED→YELLOW) —
  pending owner decision on canonical CheckId format.

## Action items
| # | Action | Owner | When |
|---|--------|-------|------|
| 1 | Epic 6: lead with M1 confidence-honesty deeper fix + multi-statement real detection; design the extractor signal contract first | architect + dev | Epic 6 kickoff |
| 2 | Owner ruling on CL-27/CL-30/CL-47 compound-key format (split rows vs adapter Split('/')) — blocked 3 epics | hebelmx | before Epic 3 |
| 3 | Confirm demo corpus stays ≤20 pages so guard #2 never false-fires (make it a pre-capture checklist item) | qa | before demo capture |
| 4 | Adopt "party-before-taxonomy" + "verify-the-assumption" as standing orchestration practice | orchestrator | ongoing |

## Next-epic readiness — Epic 5 (CL-21 detection)
Small, self-contained (extract payment-distribution operands so CL-21 fires on `bad-math-cl21`). NO
dependency on Epic 4's work; Epic 4 left the build green, no blockers. Readiness is clean. The only
verdict-tier carryover (CL-27/30/47 ruling) is an Epic-3 concern, not Epic-5.

## Significant-discovery check
No discovery from Epic 4 invalidates the Epic-5 plan. (It DID enrich Epic 6's scope — the deferred
detection taxonomy — but that's additive, captured above.)
