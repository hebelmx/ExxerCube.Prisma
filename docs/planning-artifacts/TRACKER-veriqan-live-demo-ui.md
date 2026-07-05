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
| VLD-S3 | IMarkedPageRenderer PDF→PNG service | ✅ DONE | build 0/0; 8/8; commit 361dd68f |
| VLD-S2 | IDemoRunner live/canned fallback + DemoOptions + warm-up + test project | ✅ DONE | build 0/0; 15/15; commit c78e2ffd |
| Epic B review remediation | readiness-truthfulness + never-throws | ✅ DONE | build 0/0; 19/19; commit 4d4c86cd |

**EPIC B COMPLETE** (commits c3457903→4d4c86cd, pushed). Adversarially reviewed (2 reviewers):
Epic B genuinely done, tests not hollow, host boots + /health 200. 2 confirmed defects fixed
(readiness lie, never-throws gap). Web.UI.Tests: 19/19.
| VLD-P1 | Locator bbox audit per visual rule (parallel) | ✅ DONE | audit → veriqan-locator-bbox-audit-2026-07.md; 7/11 tight bbox |
| VLD-P2 | LAW-vs-BRAND citation ledger (parallel) | ✅ DONE | ledger .md + .json; 10 LAW / 1 BRAND of 11 visual rules |

## Prep-gate findings that reshape the demo (feed VLD-S4/S5, later epic)
- **VLD-P2 credibility catch:** `CL-35` (the CURRENT flagship fixture `bad-font-cl35.pdf`) is **BRAND, not LAW** (font family is tenant-configurable) → do NOT badge it as law to a legal audience. Lead the visual demo with **LAW-TYPO-BOLD / LAW-TYPO-MINSIZE / LAW-SEC-SIZECAP** instead (real DOF Acuerdo, DOF 29-Dec-2022, transcribed in CondusefVerbatimCatalog.cs).
- **VLD-P1 + P2 converge:** best demo-lead findings = **LAW-TYPO-MINSIZE, LAW-SEC-SIZECAP, LAW-TYPO-BOLD** (tight bbox AND strong LAW citation).
- **VLD-P2 IsVisual fix:** the epic's `CL-37`=contrast premise is FALSE (CL-37 = points-to-pesos exchange rate). Drop CL-37 from IsVisual. `IsVisual` classification lives in the P2 JSON ledger (`isVisual` field), not the fictional ChecklistIds.cs.
- **VERIFY-later (P2):** exact Annex clause for CL-28/CL-29; tiers for LAW-§23-ABONO-LINK / LAW-DUC-ART27-GAT / CL-41/42/43 (absent from checklist-tiers.csv).

## CARRIED RISKS from Epic B adversarial review (address in the named later story — NOT Epic B defects)
- **[VLD-S5/S7] Shared circuit-breaker blackout.** The resolved `IVerificationPipeline` is actually
  `ResilientVerificationPipeline` wrapping a PROCESS-WIDE Polly breaker (FailureRatio 0.8, MinThroughput 5,
  Break 30s — `GateResilienceOptions.cs`). ≥5 live failures in 60s (e.g. an auditor uploading garbage, or the
  path bug below) trips it OPEN for 30s → EVERY fixture card silently falls to canned `DEMO DATA` with only a
  log line. Graceful but invisible. Options for VLD-S5/S7: (a) surface the LIVE/DEMO badge prominently (already
  planned) + a presenter-visible "live degraded" indicator; (b) give the demo host a breaker-disabled / relaxed
  pipeline config. Decide in VLD-S5. Caller-timeout cancellation does NOT trip the breaker (verified — fine).
- **[VLD-S7] Container path resolution WILL break.** `DemoCorpusPathResolver` walks up to `CLAUDE.md`; the
  published container (worker Dockerfile = `COPY --from=build /app/publish .`, no source, no CLAUDE.md) →
  `FindRepoRoot` returns null → reference-data path unresolved → EVERY live submission fails from the first
  click (and, pre-fix, readiness lied — now fixed to stay false). VLD-S7 MUST set an explicit env-var config
  path (`Veriqan__CsvReferenceData__RootDirectory`) + bake the reference bundle into the image; do NOT rely on
  the walk-up in-container. Reviewer reproduced the null-resolution empirically. (Proof test silently SKIPS on
  a non-standard build layout — acceptable, but note it.)
- **[VLD-S5/S7] Warm-up is narrow.** `PipelineWarmupHostedService` only loads the reference CSV; it does NOT
  touch `ProcessAsync`/PDFium/`PDFtoImage`/the Extraction+Visual+Reporting JIT. First live click still pays
  native-load + cold-JIT. Also `DemoOptions.LiveTimeout`=20s < Gate `TimeoutPerRequest`=30s, so DemoRunner's
  timeout always wins with no cold-start slack. VLD-S5: extend warm-up to a real throwaway ProcessAsync at
  startup, and/or raise LiveTimeout above the Gate timeout.
- **[VLD-S4] VerificationOutcomeMapper is MINIMAL** — hardcodes `Tier = ChecklistTier.Condusef` + `Label =
  CheckId` for every finding. VLD-S4 must enrich from the LAW-vs-BRAND ledger JSON (veriqan-real-check-ledger-2026-07.json)
  + real per-finding tier + bbox rail data. Nothing renders it yet, so not a live bug — but it's the S4 landmine.

## CARRIED RISK from S4b (raise at S5 checkpoint — legal-facing COPY, owner's call)
- **[VLD-S5] AMBER tier-chip copy contradicts the domain model.** The design memo / epic-doc VLD-S4 AC
  mandates the Bank-tier chip read `"[AMBER · fails law only — not on bank checklist]"`. But
  `ChecklistTier.Bank` means the check is on the BANK's improvement checklist and is NOT a CONDUSEF law
  mandate (enum doc + two-tier thesis: Bank→YELLOW opportunity, CONDUSEF→RED law floor). "fails law only"
  is backwards — a Bank-tier failure is a bank opportunity, not a law failure. `VerdictResult.razor`
  renders the phrase verbatim as specced (faithful to the AC), but it would MISLEAD a legal/bank audience.
  S5 is the first story that shows this to the audience → fix the copy there. Suggested: `"[AMBER ·
  oportunidad de mejora del banco — no es mandato CONDUSEF]"`. OWNER must ratify legal copy.

## Adversarial review checkpoints
- ✅ Epic B boundary (2026-07-04): plan-completion-reviewer + qa. Epic B confirmed done; 2 defects fixed; risks above carried.
- Next: after VLD-S4/S5 land.

## ⚠️ GROUND-TRUTH CORRECTION (orchestration run 2026-07-04c)
The 2026-07-04b handoff below claimed "the demo pages already consume `IDemoRunner` + `IMarkedPageRenderer`
(live seam wired)." **That is FALSE — verified from ground truth.** `IDemoRunner` is referenced ONLY in
`Program.cs` (DI registration); NO page or component calls `RunAsync`. Every page
(`RedCase/GreenCase/YellowCase/BlockedCase/Disposition/Overview/Upload`) injects `DemoDataService` and renders
**canned** data. The live seam Epic B built is wired in DI but **DARK at the UI**. Also `VerdictResult.razor`
does NOT exist and bUnit is NOT referenced. Consequence: the epic doc's original VLD-S4 (mapper enrichment
**+ `VerdictResult.razor` render component**) and VLD-S5 (the page that first consumes `IDemoRunner`) are BOTH
real and match reality — the handoff's "just enrich the mapper" narrowing was based on the false premise.
Following the epic doc, not the narrowed handoff.

**Reconciled scope for this run (owner chose Full C/D arc S4→S5→S7; S6 page-retirement excluded):**
- **VLD-S4a** (data core, no Razor, unit-testable): `RealCheckLedger` (embed `veriqan-real-check-ledger-2026-07.json`
  as EmbeddedResource — container-safe, NOT a docs/ read) + `DemoFinding.{IsVisual,Locator}` +
  `DemoStatementCase.MarkedPagePngs` + rewrite `VerificationOutcomeMapper` to `Result<T>` signature with
  ledger-driven Tier/Label/IsVisual/DofNumeral enrichment (uncatalogued CheckId → visible engineering-gap label,
  never silent CheckId-as-label). Update interface + `DemoRunner` + `Program.cs`. Unit tests in `Veriqan.Web.UI.Tests`.
- **VLD-S4b** (render component): `Components/Shared/VerdictResult.razor` + bUnit smoke (VERIFY-FIRST; documented
  fallback to plain view-model method if bUnit ⟂ xunit.v3.mtp-v2).
- **VLD-S5** (un-dark + design-fork checkpoint): the live page that FIRST calls `IDemoRunner.RunAsync` + wires the
  marked-PDF→PNG hero chain (`IMarkedPdfGenerator`→`IMarkedPageRenderer`) into `DemoRunner`. `VerificationOutcome`
  carries NO marked PDF — the chain is genuinely new wiring here, not a badge on an already-live page.
- **VLD-S7** (owner checkpoint): compose + reference-bundle bake + persistence decision.

## NEXT EPIC (handoff 2026-07-04b) — Epics C/D = VLD-S4 → VLD-S5 → VLD-S7 — SUPERSEDED BY THE CORRECTION ABOVE
Re-grounded in a later orchestrator session: **Epic B confirmed still DONE**; the demo pages
(`RedCase/GreenCase/YellowCase/BlockedCase/Disposition/Overview/Upload`) ~~already consume `IDemoRunner` +
`IMarkedPageRenderer` (live seam wired)~~ **[WRONG — see correction above; pages render canned data]**. Owner
reviewed the remaining scope and chose to **HAND OFF** rather
than start it this session (it's a real multi-story epic with a design fork + an owner checkpoint, not the
"small build" the design-memory implied). Resume points, in order:
- **VLD-S4 (recommended first — self-contained, no gate):** enrich `VerificationOutcomeMapper` (currently
  hardcodes `Tier=ChecklistTier.Condusef` + `Label=CheckId`) from the LAW-vs-BRAND ledger
  `veriqan-real-check-ledger-2026-07.json` → real per-finding tier + law citation + bbox rail, so the wired
  pages render TRUTHFUL findings. Lead visual findings = LAW-TYPO-MINSIZE / LAW-SEC-SIZECAP / LAW-TYPO-BOLD
  (VLD-P1+P2 convergence); do NOT badge CL-35 as law (it's BRAND). Verify: build 0/0 + Web.UI.Tests.
- **VLD-S5 (design fork — decide before coding):** warm-up is narrow (CSV only, not ProcessAsync/PDFium/JIT)
  and the process-wide Polly breaker (FailureRatio 0.8/MinThroughput 5/Break 30s) silently drops ALL cards to
  canned DEMO on ≥5 live fails in 60s. Fork: (a) presenter-visible "live degraded" badge vs (b) breaker-disabled
  demo config. Also raise `DemoOptions.LiveTimeout` (20s) above Gate `TimeoutPerRequest` (30s).
- **VLD-S7 (owner checkpoint):** Veriqan Web.UI is in NO compose file — add it + set
  `Veriqan__CsvReferenceData__RootDirectory` env (the `DemoCorpusPathResolver` walk-up to CLAUDE.md returns
  null in the source-less published container) + bake the reference bundle into the image. **OPEN owner decision:
  container SQL vs in-memory persistence — do not silently default.**

## Epic C/D status (run 2026-07-04c)
| Story | Title | Status | Verified (build/test/git) |
|-------|-------|--------|---------------------------|
| VLD-S4a | RealCheckLedger + mapper enrichment + model fields | ✅ DONE | build 0/0; Web.UI.Tests 30/30 (19+11 new); commit fdae0ce2, pushed |
| VLD-S4b | VerdictResult.razor + bUnit smoke | ✅ DONE | build 0/0; Web.UI.Tests 38/38 (31+1 smoke+6); bUnit 2.7.2 worked (no fallback); commit pending |
| VLD-S5 | Live page (un-dark IDemoRunner) + hero chain | ⛔ design-fork checkpoint | — |
| VLD-S7 | Docker compose deploy | ⛔ owner checkpoint | — |

## Log
- 2026-07-04c: Orchestration run — owner chose Full C/D arc (S4→S5→S7). Ground-truth re-grounding found
  the pipeline is DARK at the UI (no page calls IDemoRunner; all render canned DemoDataService). Corrected
  tracker. VLD-S4 split into S4a (data core) + S4b (render component). **S4a DONE + verified + pushed
  (fdae0ce2):** RealCheckLedger embedded-resource loader, mapper Result<T> + ledger enrichment, DemoFinding
  {IsVisual,Locator} + DemoStatementCase.MarkedPagePngs. 30/30 green.
- 2026-07-04: Orchestration started; tracker created.
- 2026-07-04: VLD-S1 delegated (dev), VLD-P1 (Explore), VLD-P2 (analyst) in parallel. All returned.
- 2026-07-04: VLD-S1 VERIFIED from ground truth (build 0/0, proof test 1/1). VLD-P1/P2 docs persisted. Committing.
- Persistence decision recorded: dev-run in-memory (no SQL); container SQL-vs-in-memory = OPEN, checkpoint at VLD-S7.
- 2026-07-04b: Re-grounded in a multi-epic session (S4-C plan + Prisma Manual-Review fixes done first). Confirmed
  Epic B done + pages wired. Owner chose to HAND OFF Epics C/D (VLD-S4/S5/S7) for a fresh-context run. No code
  changed in Veriqan this session; handoff pointer added above.
