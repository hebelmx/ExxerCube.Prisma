---
name: handoff-veriqan-epic2
date: 2026-06-28
branch: Liv
status: Epic 2 (Highly-Visible Per-Issue Compliance Report) DELIVERED — handoff for Epic 3+
intended-solution: docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md
predecessor: docs/planning-artifacts/HANDOFF-veriqan-epic1-2026-06-28.md
---

# Veriqan VEC — Epic 2 Handoff (Per-Issue Compliance Report)

Orchestrated build of Epic 2. Every story verified from ground truth (build 0/0 +
`dotnet exec` test runs + git diff), committed in self-contained chunks, pushed to
`Liv`. An adversarial completion review ran at the epic boundary; its Major findings
were fixed in a follow-up commit. Commits: `a45a5b27`, `0bd4efd7`, `b262384a`.

## What was delivered

| Story | Commit | What | Verification |
|---|---|---|---|
| 2.1 | `a45a5b27` | Marked-PDF per-tier emphasis: amber for `ChecklistTier.Bank`-only fails, red for Condusef/Both/unmapped/null (conservative). New optional `IReadOnlyDictionary<string,ChecklistTier>? checklistTiers` param on `IMarkedPdfGenerator.Generate` (null = legacy all-red). Sequential numbered callouts `"N. CheckId"` tie each highlight to the report list. No-locator page-marker path preserved. | Reporting.Tests **69/69** (23 existing unchanged + 6 new), 0 skipped |
| 2.2 | `0bd4efd7` | `DemoFinding.Tier` + `DemoStatementCase.Bank/CondusefTierVerdict`. RedCase/YellowCase render two sections — "Incumplimiento CONDUSEF (RED)" (Condusef\|Both fails) and "Oportunidades de mejora del banco (YELLOW)" (Bank-only fails). Tier map embedded in `ChecklistIds.Tier()` from the bundle `checklist-tiers.csv` (10 Bank / 26 Both / 20 Condusef; unmapped → Condusef floor). | Web.UI build 0/0 |
| 2.3 | `0bd4efd7` | Shared `Components/Shared/VerdictBanner.razor` — GREEN/YELLOW/RED/Blocked tri-state + one-line legend, used by all case pages. `.vec-signal-yellow` CSS. New `YellowCase.razor` (`/yellow`) + nav link. **Closed tracked deferral:** `DemoDataService.GetBySignal(Yellow)` now returns a good.pdf-style case (Bank YELLOW, CONDUSEF GREEN; fails CL-35/CL-37, both Bank). | Web.UI build 0/0 |
| review | `b262384a` | Adversarial-review fixes (see below). | Orchestration.Tests **81/81**, Web.UI 0/0, Reporting.Tests 69/69 |

## Adversarial review — findings & resolutions
- **MAJOR (fixed):** the pipeline never forwarded `checklistTiers` to `Generate()` — production marked PDF was all-red, the S2.1 tier styling was test-only theater. Now wired on the main RED/YELLOW report path (`VerificationPipeline.cs:863`). Blocked/insufficient early-return paths keep null (no per-tier fails there).
- **Minor (fixed):** `ComputeBankTierVerdict` now counts `ChecklistTier.Both` (matches canonical `VerdictAggregator`); Severidad column added to both tier tables (real `FindingSeverity` data); duplicate "Captura 4" fixed (Blocked→5, Disposition→6); docstring three→four.
- **Refuted (no action):** S2.1 tier-color conservative default, numbered-callout counter, VerdictBanner completeness, and 7/8 spot-checked tier mappings were all confirmed correct.

## Carried items (in the task tracker)
- **DEFERRED → Epic 4:** per-finding **confidence** column (S2.2 AC) is owned by Epic 4 S4.1. Per-finding **locator/page** column needs real-pipeline marked-PDF data. Both documented in a comment block in `DemoFinding.cs` — deliberately NOT fabricated for the demo. (Tracker task #4.)
- **FLAG → Epic 1:** `checklist-tiers.csv` row 2 is a compound key `CL-27/CL-30/CL-47` and `CsvReferenceDataAdapter.GetChecklistTiersAsync` stores it **literally** (no slash-split). A production `VerdictAggregator` lookup for `CL-27`/`CL-30`/`CL-47` individually MISSES and falls back to the Condusef floor → those 3 rules are mis-tiered (should be Bank). The UI is unaffected (it uses the hardcoded `ChecklistIds.Tier()` which splits them). **Needs an owner decision:** is the canonical rule `CheckId` the compound string or three separate ids? Then split the CSV rows or add a `Split('/')` in the adapter. There is a test (`SlashCheckId_ResolvesCorrectly`) that currently *asserts the literal-key behavior* — it would need updating if the decision is to split. (Tracker task #5.)

## Gotchas for the next agent (carried + new)
- Tests: `dotnet test <csproj>` falsely reports "Zero tests ran". Build the project, then `dotnet exec /home/abel/ExxerProjects/IndFusion/BuildArtifacts/Prisma/bin/<Asm>/Debug/net10.0/<Asm>.dll`.
- `docs/qa/calibration/calibration-report.md` regenerates (timestamp) when certain suites run — spurious working-tree change; `git checkout --` it, don't commit.
- The Veriqan **demo Web.UI has no test project** — verification bar for UI stories is `dotnet build` 0/0 + reasoning about the (testable) `DemoDataService` logic. Don't spin up a new fragile xunit.v3-MTP test project mid-loop.
- `ChecklistTier.Both` belongs to BOTH tiers — for verdict math count it in BOTH Bank and CONDUSEF; for the UI *display* partition, a Both-fail shows only in the CONDUSEF (RED) section (it's a regulatory breach, not a mere improvement opportunity), and the Bank section is Bank-ONLY.

## Remaining Epic plan (not started)
Per the plan's suggested sequence: **Epic 5** (CL-21 completeness, small) → **Epic 3** (enhanced compliant master, proves GREEN path) → **Epic 4** (confidence + honest BLOCKED — unblocks the deferred confidence column) → **Epic 6** (Tier-B hardening, parallelizable).
