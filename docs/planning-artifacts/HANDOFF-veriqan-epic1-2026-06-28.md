---
name: handoff-veriqan-epic1
date: 2026-06-28
branch: Liv
status: Epic 1 (Two-Tier Compliance Verdict) DELIVERED — handoff for Epic 2+
intended-solution: docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md
---

# Veriqan VEC — Epic 1 Handoff (Two-Tier Bank/CONDUSEF Verdict + YELLOW)

Orchestrated build of Epic 1 of the demo-hardening plan. Every story verified from ground
truth (build 0/0 + `dotnet exec` test runs + git), committed in self-contained chunks, pushed
to `Liv`. Commits: `56f0f8c4`..`481998d5`.

## What was delivered

| Story | Commit | What | Verification |
|---|---|---|---|
| 1.1 | `56f0f8c4` | `VerdictSignal.Yellow`; `VerdictSummary.BankTierVerdict`/`CondusefTierVerdict` + `CombineOverallSignal` (Red if CONDUSEF Red; Yellow if Bank Yellow & CONDUSEF not Red; Green iff both Green; Blocked precedence) | Application 134, Orchestration 78, 0/0 |
| 1.2 | `8ef9f9db` | `ChecklistTier{Bank,Condusef,Both}`; `checklist-tiers.csv` (56 rows = 10 Bank / 26 Both / 20 CONDUSEF) in both bundles; `GetChecklistTiersAsync` loader; aggregator partitions fails + computes tier verdicts (null map → legacy single-tier) | Application 134, ReferenceData 46, Orchestration 78, 0/0 |
| 1.3 | `c01c24d3` | Pipeline Stage 6b fetches tier map → `Aggregate`; graceful null fallback; E2E asserts honest tiers | **LIVE E2E (corpus present, 0 skipped): good.pdf = Signal Red, Bank YELLOW, CONDUSEF RED — no forcing.** Orchestration 78 |
| review | `2867209d` | Adversarial-review fixes: YELLOW handled in `BatchProcessor` tally, pipeline report gate (Stage 9), alert gate (Stage 10) + `VecAlertService` (signal-aware wording) | Reporting 69, Orchestration 81, Application 134 |
| 1.4 | `481998d5` | Persist 2 tier verdicts on `JobVerdict` + `Finding.Tier`; EF migration `AddTwoTierVerdictColumns`; `HasSentinel(-1)` for `ChecklistTier.Bank=0` | **Persistence integration 8/8 LIVE on real SQL Server Testcontainer** (two-tier round-trip), Orchestration 81, Application 134 |

## Key decisions (owner-approved)
- **Two checklists, not one axis.** Bank checklist = per-tenant DATA (`checklist-tiers.csv`); CONDUSEF = law-derived (intrinsic). They are NEITHER super- nor sub-set of each other — the value is that the tool finds law gaps the bank's own checklist never listed (the 20 CONDUSEF-only `LAW-*` rules). Tier mapping pre-analysed in `LAW-VS-CHECKLIST-GAP-2026-06-17.md`; owner signed off the per-check assignment.
- **Verdict semantics.** Bank tier → Green/**Yellow** (never Red; bank gaps = improvement opportunities). CONDUSEF tier → Green/**Red** (regulatory). Abstain-safety preserved (InsufficientData never escalates).
- **YELLOW behavior policy:** a YELLOW statement generates a marked-PDF report AND sends an alert email. Yellow alert wording = "bank improvement opportunities; CONDUSEF regulatory compliance is GREEN" — it never claims a regulatory failure.
- **Conservative defaults:** unmapped CheckId / null tier map → CONDUSEF (never silently dropped from RED). Legacy DB rows → Green/Condusef defaults.

## Remaining work (NOT started — needs a deliberate go)
- **Epic 2 (report/UI)** — incl. tracked deferrals (task tracker #6): `DemoDataService` has no Yellow case (`GetBySignal(Yellow)`→null); UI `07 UI/.../Services/ChecklistIds.cs` has STALE/WRONG CL labels (real source = rule files + Iqubica CSV); per-tier marked-PDF styling + the tri-state banner.
- **Epics 3–6** — enhanced compliant master; confidence + honest BLOCKED; CL-21 completeness; Tier-B hardening. See the plan doc.

## Gotchas for the next agent
- Tests: `dotnet test <csproj>` falsely reports "Zero tests ran". Build single projects, then `dotnet exec /home/abel/ExxerProjects/IndFusion/BuildArtifacts/Prisma/bin/<Asm>/Debug/net10.0/<Asm>.dll`.
- `ChecklistTier.Bank=0` == CLR int default → EF needs `HasSentinel((ChecklistTier)(-1))` or it writes the DB default instead.
- `docs/qa/calibration/calibration-report.md` regenerates (timestamp) whenever certain test suites run — it is a spurious working-tree change; revert it, don't commit it.
- The UI `ChecklistIds.cs` labels are fiction; trust the rule files' `CheckId` literals + the bank CSV.
- §5c golden-master: `good.pdf` is production-grade (only confidential fields anonymized). Never force a verdict, never bypass a guard (no `minExtractionCoverageCount:0`). CL-50–53 failures on the demo corpus are anonymization artifacts (Bank-tier; they never force CONDUSEF-RED).
