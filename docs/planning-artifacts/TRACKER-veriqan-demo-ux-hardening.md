---
name: tracker-veriqan-demo-ux-hardening
date: 2026-07-05
branch: Liv
epic: Veriqan Demo — UI/UX Hardening (post Epic C/D live-demo)
predecessor: docs/planning-artifacts/TRACKER-veriqan-live-demo-ui.md (Epic C/D DONE)
web-ui: Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI
container: docker-compose.veriqan.yml service `veriqan-web-ui` (host 18091, standalone in-memory)
status: IN PROGRESS (orchestrated)
---

# Veriqan Demo — UI/UX Hardening Tracker

Scope confirmed with owner 2026-07-05: **all four items below.** The live demo itself
(Epic C/D) is DONE + verified — this epic is polish/credibility hardening for the
bank/legal demo audience. Verified in-browser on the running `veriqan-web-ui` container.

## Ground-truth test results (2026-07-05, container on :18091)
Live pipeline works E2E: `/live` → green EN VIVO badge → real ProcessAsync (CL-21 case,
RED, 8 Pass / 15 Fail / 35 InsufficientData, 3.81s) → marked-page hero renders with red
annotations → findings list with law citations + expected/observed + 3-tier chips. No
canned fallback, no crash. This is the baseline we must not regress.

## Stories
| ID | Title | File(s) | Status | Verified |
|----|-------|---------|--------|----------|
| VUX-S1 | Fix dead theme toggle — global interactivity | `Components/App.razor` | ✅ DONE | build 0/0; browser: /  toggles to LIGHT (moon icon), was dead before |
| VUX-S2 | PostgreSQL → SQL Server content fix | `Components/Pages/Overview.razor:99` | ✅ DONE | browser: "SQL Server (esquema veriqan.*)" renders |
| VUX-S3 | Tame hero (70vh scroll frame) + collapse InsufficientData accordion | `Components/Shared/VerdictResult.razor` | ✅ DONE | browser: hero has internal scrollbar; 35-item accordion collapsed; Hallazgos reachable in 10 ticks (was 25+) |
| VUX-S4a | Hide uncatalogued eng-gap finding from audience rail | `VerdictResult.razor` + test | ✅ DONE | browser get_page_text: `NotificationFailure` ABSENT from expanded rail; accordion count 35 now consistent w/ banner 35; Web.UI.Tests 50/50 |
| VUX-S4b | Canned-page parity AUDIT (read-only) | pages 2–6 | ✅ DONE (audit) | **DIVERGENCE FOUND — fix is an owner design fork, see below** |

## VUX-S4b audit result — canned-page divergence (OWNER DECISION NEEDED)
None of the 5 canned "Capturas del Demo" pages consume `<VerdictResult>` (only `/live` does). They are
independently hand-rolled and PRE-DATE the /live component — so today's hero/accordion/hide-gap fixes do
NOT propagate to them:
- `/red` (RedCase): own PDF **iframe** hero (not the PNG hero, no scroll cap) + own tier-grouped MudTables
  with a **plain gray `Color.Default` tier chip** (NOT the RED/ORANGE/YELLOW ramp `/live` now uses) + no
  uncatalogued filtering.
- `/yellow` (YellowCase): same hand-rolled table + gray tier chip; no hero.
- `/green`, `/blocked`: check-grid only, no rail/chips (mostly moot).
- `/disposition`: disposition form, renders no findings at all.
**Consequence:** `/red` and `/yellow` show a DIFFERENT tier-chip visual language than `/live`, and none
hide the eng-gap finding. This is pre-existing divergence, NOT introduced today. The FIX (refactor
RedCase/YellowCase to consume `<VerdictResult>`) is a moderate refactor + a design fork (their tier-grouped
tables tell the two-tier story differently) → surface to owner, do NOT silently refactor. **Recommend a
follow-up story VUX-S5 gated on owner decision:** (a) refactor canned pages onto VerdictResult for full
parity, vs (b) leave canned pages as-is (they are static screenshots of a narrative, /live is the hero).

## VUX-S5 — Enhance VerdictResult into the shared findings component (OWNER-RATIFIED 2026-07-05)
Owner chose: enhance `VerdictResult` and share it EVERYWHERE; **/live UPGRADES to the tier-grouped
tables** (owner ratified the appearance change). De-risked: `DemoStatementCase` is ALREADY the shared
view-model for both canned + live cases (has Findings w/ Confidence+Tier+Severity, ExtractedFields,
MarkedPdfPath AND MarkedPagePngs) — no data-model reconciliation needed.

**Design (single-column component, consumes `DemoStatementCase`):**
1. Keep: VerdictBanner + Pass/Fail/InsufficientData/duration chips; VISUAL/DATA ribbon; bounded PNG
   hero; InsufficientData collapsed accordion; eng-gap (uncatalogued) filtering; aggregate-gated
   "Sin incumplimientos".
2. Hero unify: PNG hero (70vh) if `MarkedPagePngs.Count>0`; ELSE iframe (`vec-pdf-frame`) if
   `MarkedPdfPath != null`; ELSE the dashed placeholder.
3. Findings: REPLACE the flat chip-rail with the two TIER-GROUPED TABLES (from RedCase, Story 2.2):
   CONDUSEF/Both fails → RED table, Bank fails → YELLOW table; columns incl. **Confianza** (— for
   InsufficientData). Use the 3-color TierChip ramp (TierChipStyle/TierChipText already in the file)
   for the Tier cell/chip. Filter uncatalogued from both. InsufficientData → keep the collapsed
   accordion below the tables. Pass → "N checks superados".
4. New optional params/slots: `bool ShowCheckGrid` (the flex-wrap `vec-check-cell` grid, from canned
   pages); `RenderFragment? Narrative` (for /yellow's "¿Qué significa YELLOW?" prose panel);
   `bool ShowExtractedFields` (renders the "Datos del documento" table from `Case.ExtractedFields`).
   All default OFF so /live is unaffected by them.
5. Migrate: `/red` = `<VerdictResult Case ShowCheckGrid="true"/>` (iframe hero via MarkedPdfPath) +
   keep its header; `/yellow` = `<VerdictResult Case ShowCheckGrid ShowExtractedFields><Narrative>…
   </Narrative></VerdictResult>` + keep header. `/live` (LiveVerification) unchanged usage → auto-upgrades.
   Remove the now-duplicated bespoke tables/grid/iframe markup + the `_condusefFailFindings`/
   `_bankFailFindings` code-behind from RedCase/YellowCase.
6. Tests: extend `VerdictResultTests` (tier tables render, Confianza cell, tier grouping, hero
   PNG-vs-iframe fallback, ShowCheckGrid/Narrative/ShowExtractedFields slots). Keep the 51 green.
**Risk:** /live is the demo hero — verify it end-to-end in-browser after (real ProcessAsync still RED,
hero renders, tables replace rail, honesty guarantees intact). Adversarial review before closing.
**Status: DONE + browser-verified (local run).** Delegated to 1 dev subagent; verified from ground truth:
- build 0/0; Web.UI.Tests **57/57** (added 6 tier-table/grid/fields/hero-fallback tests).
- `/red`: shared component — check-grid + CONDUSEF-RED table (CL-21, 3-color tier chip, Confianza 94%) +
  Bank-YELLOW table (CL-35) + collapsed InsufficientData + pass summary. No double banner. NEW ribbon.
- `/yellow`: shared component — CONDUSEF "Sin incumplimientos CONDUSEF." + YELLOW table (CL-35/CL-37) +
  Narrative slot ("¿Qué significa YELLOW?") + Datos-del-documento fields table. No double banner.
- `/live`: UPGRADED to the tier tables, real ProcessAsync (RED, CL-21..CL-48 with 3-color chips).
- **Follow-on fix (found in browser):** the 8-col tables CLIPPED Esperado/Observado/DOF/Confianza in
  /live's narrower result column. Added `HorizontalScrollbar="true"` to both MudTables → columns now
  reachable via horizontal scroll on /live, no clip; full-width canned pages unaffected. Re-verified.
- Consolidation: RedCase/YellowCase shed ~400 lines into VerdictResult (+ optional ShowCheckGrid,
  Narrative slot, ShowExtractedFields params; hero = PNG→iframe→placeholder fallback).
- Subagent notes: bUnit needed a `MudPopoverProvider` root (MudTooltip in tables); 2 pre-existing tests
  adjusted (Bank table intentionally has no Tier column; the S4a honesty test narrowed to the exact
  overall "Sin incumplimientos." string since per-tier "Sin incumplimientos CONDUSEF." is a TRUE state).
**PENDING: adversarial review + container rebuild.**

## Known-acceptable limitations (logged for adversarial review)
- Aggregate banner counts (Pass/Fail/InsufficientData, VISUAL/DATA ribbon) are computed upstream on the
  FULL finding set; the rail is filtered. With the eng-gap finding hidden, the accordion count matches the
  banner (35=35) in the demo corpus, but in principle a hidden uncatalogued finding could make a banner
  read 1 higher than the visible rail. Acceptable (display vs aggregate) — not fixed upstream.
- ~~Edge case: a case whose ONLY findings are uncatalogued would leave both rails empty → "Sin
  incumplimientos" success alert despite a Red signal. Theoretical only.~~ **FIXED (adversarial-review
  finding #1, commit after 788390f0):** this was NOT theoretical — the reviewer reproduced it against
  the built assembly, and the real corpus DID hit `NotificationFailure` (it was masked by co-occurring
  real fails). The Hallazgos empty-state now gates on the upstream aggregate `Case.FailCount == 0 &&
  Case.InsufficientDataCount == 0` (mirroring the ribbon) instead of the filtered rail lists, so
  "Sin incumplimientos" can never appear under a RED banner. Regression test added:
  `VerdictResult_RedCaseWithOnlyUncataloguedFinding_DoesNotClaimNoViolations`. Web.UI.Tests 51/51.
- Theme choice does not persist across a FULL page reload (URL navigation restarts the Blazor circuit →
  MainLayout `_isDarkMode` resets to dark default). SPA nav-link clicks preserve it. Out of scope; noted.

## Root-cause notes (code-grounded, do not re-derive)
- **VUX-S1:** Every *page* has `@rendermode InteractiveServer` but `MainLayout`/`App.razor`/
  `Routes.razor` do NOT. A layout is the page's PARENT in the render tree → AppBar renders
  static SSR → `MainLayout.ToggleTheme` OnClick never wires → toggle dead. Fix: global
  interactivity — `<HeadOutlet @rendermode="InteractiveServer" />` + `<Routes @rendermode="InteractiveServer" />`
  in App.razor (per-page `@rendermode` then becomes redundant; may be left or removed).
  `MudThemeProvider @bind-IsDarkMode` is already correct — it just needs an interactive host.
- **VUX-S2:** `Overview.razor:99` literal `"...en PostgreSQL (veriqan.* schema)."`. Real infra =
  SQL Server 2022 (`docker-compose.veriqan.yml` sqlserver = mssql/server:2022; VeriqanDb). Fix
  the copy (SQL Server, or neutral "base de datos relacional").
- **VUX-S3:** `VerdictResult.razor` hero (lines ~33-54) `@foreach` renders every marked PNG at
  `max-width:100%` stacked — 9 full pages = huge scroll. Constrain the hero `MudPaper` with a
  max-height + `overflow-y:auto`. InsufficientData rail (lines ~91-105) renders all 35 expanded;
  wrap in a `MudExpansionPanel`/collapse titled e.g. "N checks con datos insuficientes".
  KEEP the Fail-order preservation (matches hero numbered callouts — see comment line 132).
- **VUX-S4:** `IsUncatalogued()` (line 163) already gives uncatalogued findings a neutral
  "Sin catalogar" chip, but the finding STILL renders (e.g. `NotificationFailure`). Decide:
  hide uncatalogued findings entirely from the audience rail (recommended) vs keep neutral.
  Then audit canned pages RedCase/GreenCase/YellowCase/BlockedCase/Disposition (2–6) — do they
  use VerdictResult.razor or their own markup? Verify tier-legend/visual parity with /live.

## Verification approach
Fast loop: run Web.UI locally on host via `dotnet run` (DemoCorpusPathResolver walks up to
CLAUDE.md → bundle resolves on host) and browse; `dotnet build` must stay 0/0. Final delivery:
rebuild `veriqan-web-ui` image + container smoke (/, /live, theme toggle, health 200).
Web.UI.Tests baseline was 50/50 (Epic C/D) — keep green.

## Log
- 2026-07-05: Epic opened. Tested running container, cataloged 6 issues, owner scoped all four
  stories. Tracker created.
- 2026-07-05: S1–S4a delegated to 2 parallel dev subagents (disjoint files), verified from ground
  truth (git diff + build 0/0 + local `dotnet run` browser smoke). Fixed 1 test that encoded the
  superseded S5c neutral-chip behavior → Web.UI.Tests 50/50. Committed 788390f0, pushed to Liv.
- 2026-07-05: `veriqan-web-ui` image rebuilt + container recreated (host :18091). Deployed-container
  smoke PASSED: theme toggle → light mode (was dead); "SQL Server (esquema veriqan.*)" copy; /live
  real pipeline RED 8/15/35 @3.80s with bounded 70vh hero. BONUS: theme PERSISTS across SPA nav
  (nav-link), only full URL reloads reset it. Adversarial review (plan-completion-reviewer) launched.
- STATUS: 4 scoped stories DONE + deployed-verified. VUX-S5 (canned-page parity refactor) is a
  surfaced owner design fork — NOT started (see VUX-S4b audit block). Awaiting adversarial-review
  verdict, then epic boundary → hand off.
- 2026-07-05: Adversarial review (plan-completion-reviewer) verdict = COMPLETE WITH GAPS. Independently
  reproduced build 0/0 + tests 50/50; could NOT refute global-interactivity boot (ran the compiled host:
  /,/live,/health all 200, prerender markers intact, no render-mode conflict), hero bounding, MudBlazor
  8.11 API validity, test integrity, or the S4b audit. Found 1 MAJOR (finding #1: "Sin incumplimientos"
  reachable under a RED banner via the S4a filter) → FIXED + regression-tested (commit 43ce570e, tests
  51/51). 1 MINOR (redundant per-page @rendermode) = harmless tech debt, reviewer said no action → left.
- 2026-07-05: Image rebuilt w/ fix, container recreated (:18091), smoke /,/live,/health 200.
  **EPIC COMPLETE.** Commits 788390f0 (S1–S4a) + 43ce570e (review fix), pushed to Liv. Stop at epic
  boundary per scope discipline. VUX-S5 canned-page parity = owner decision, handed off (NOT started).
