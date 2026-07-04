---
name: tracker-veriqan-live-demo-ui
date: 2026-07-04
branch: Liv
epic: Epic B — Live Pipeline Backend Seam (scoped for this orchestration run)
intended-solution: docs/planning-artifacts/epics-veriqan-live-demo-ui-2026-07-04.md
design-memory: veriqan-live-demo-ui-design-2026-07-04.md
status: IN PROGRESS (orchestrated)
---

# Veriqan LIVE Demo UI — Orchestration Tracker

Scope of THIS run = **Epic B (Live Pipeline Backend Seam)**: VLD-S1 → VLD-S3 → VLD-S2.
Epic A prep gates (VLD-P1, VLD-P2) run in parallel; must finish before VLD-S4 (a later epic).
Epics C/D (VLD-S4..S7) are OUT of this run — stop + hand off at the Epic B boundary.

## Owner-settled / orchestrator decisions
- **Persistence (VLD-S1 dev-run):** in-memory (omit `ConnectionStrings:VeriqanDb`). Rationale: sales-demo
  surface, not a system of record. The **container** SQL-vs-in-memory choice (VLD-S7) is an OPEN decision —
  CHECKPOINT with owner at the deploy story, do not silently default.

## Status
| Story | Title | Status | Verified (build/test/git) |
|-------|-------|--------|---------------------------|
| VLD-S1 | Wire real pipeline into Web.UI + proof test | ✅ DONE | build 0/0; proof test 1/1 via dotnet exec; good.pdf→Red→marked PDF→SKBitmap 1275×1649; Veriqan suites green (Orch 122, Reporting 75, App 151) |
| VLD-S3 | IMarkedPageRenderer PDF→PNG service | PENDING | — |
| VLD-S2 | IDemoRunner live/canned fallback + DemoOptions + warm-up + NEW test project | PENDING | — |
| VLD-P1 | Locator bbox audit per visual rule (parallel) | ✅ DONE | audit → veriqan-locator-bbox-audit-2026-07.md; 7/11 tight bbox |
| VLD-P2 | LAW-vs-BRAND citation ledger (parallel) | ✅ DONE | ledger .md + .json; 10 LAW / 1 BRAND of 11 visual rules |

## Prep-gate findings that reshape the demo (feed VLD-S4/S5, later epic)
- **VLD-P2 credibility catch:** `CL-35` (the CURRENT flagship fixture `bad-font-cl35.pdf`) is **BRAND, not LAW** (font family is tenant-configurable) → do NOT badge it as law to a legal audience. Lead the visual demo with **LAW-TYPO-BOLD / LAW-TYPO-MINSIZE / LAW-SEC-SIZECAP** instead (real DOF Acuerdo, DOF 29-Dec-2022, transcribed in CondusefVerbatimCatalog.cs).
- **VLD-P1 + P2 converge:** best demo-lead findings = **LAW-TYPO-MINSIZE, LAW-SEC-SIZECAP, LAW-TYPO-BOLD** (tight bbox AND strong LAW citation).
- **VLD-P2 IsVisual fix:** the epic's `CL-37`=contrast premise is FALSE (CL-37 = points-to-pesos exchange rate). Drop CL-37 from IsVisual. `IsVisual` classification lives in the P2 JSON ledger (`isVisual` field), not the fictional ChecklistIds.cs.
- **VERIFY-later (P2):** exact Annex clause for CL-28/CL-29; tiers for LAW-§23-ABONO-LINK / LAW-DUC-ART27-GAT / CL-41/42/43 (absent from checklist-tiers.csv).

## Adversarial review checkpoints
- After Epic B stories land (or every 3 tasks): fan out skeptics against the intended-solution doc.

## Log
- 2026-07-04: Orchestration started; tracker created.
- 2026-07-04: VLD-S1 delegated (dev), VLD-P1 (Explore), VLD-P2 (analyst) in parallel. All returned.
- 2026-07-04: VLD-S1 VERIFIED from ground truth (build 0/0, proof test 1/1). VLD-P1/P2 docs persisted. Committing.
- Persistence decision recorded: dev-run in-memory (no SQL); container SQL-vs-in-memory = OPEN, checkpoint at VLD-S7.
