# HANDOFF — ITDD Test-Suite Refactor: Phase 3 (Adaptive DOCX cluster)

> **For the next agent.** Branch **Kt2**. Phases 0–2 are DONE and adversarially
> gated GO (commits `e49f145..368f5cd`, 2026-06-10). Your job is **Phase 3** — the
> payoff phase (first true multi-implementation contract: 5 strategy inheritors).

## Prompt

Continue the ITDD test-suite refactor on branch **Kt2** — execute **Phase 3
(Adaptive DOCX cluster)**, the payoff phase.

1. **Read first:** master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
   — §4.1 playbook (the 11-step recipe **plus the Phase 2 addenda**), §4 Phase 3
   scope, §5 tracker, §2 drift map. ADR-005
   (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`) governs the
   shape. Reference example: the Phase 2
   `09 Testing/01 Abstractions/Testing/Contracts/{TemplateFieldMapper,SchemaEvolutionDetector,AdaptiveExporter}Contract.cs`
   + `*MockFactory.cs` (composed / `CreateSut()`+seed-hook fakes), with instances in
   `Tests.Domain.Interfaces` (blueprints) and `Tests.Infrastructure.Export.Adaptive`
   (impls).

2. **Phase 3 scope:** `IAdaptiveDocxStrategy` → `AdaptiveDocxStrategyContract`
   inherited by **5 strategy test classes** (Contextual / Structured / TableBased /
   Search / Complement — the first true multi-implementation contract), and
   `IAdaptiveDocxExtractor` → `AdaptiveDocxExtractorContract` for the orchestrator.
   All twins live in `Tests.Infrastructure.Extraction.Adaptive` (which already
   references `Testing.Contracts` from the Phase 1 pilot — verify). **Capture
   baseline test counts before writing any `.cs` file** (SDK globbing pulls new files
   into builds).

3. **The hard part (§2 + §6.2):** ~6 contract tests are documented in the mock
   blueprints but never executed against any SUT (strategy: perf test, medium/low-
   confidence tiers, 2 Liskov meta-tests; extractor: default-mode + 2 meta-tests). For
   each, **triage explicitly**: restore the genuinely contract-grade ones into the
   base (expect real failures at first execution → decide contract-bug vs
   implementation-bug, **don't blind-fix**), and demote/keep impl-side the
   perf/confidence-tier and meta tests with written rationale (ADR-005 §5 — exactly as
   Phase 2 kept the SchemaEvolutionDetector "real-world scenario" tests impl-side).

4. **Rules:** lift twin bodies **verbatim** (names preserved **including the
   `_Liskov` suffix** — Stryker history is keyed to them; the only edit is the SUT
   line), mock classes are **converted, never deleted** (→ `Mock{Name}ContractTests`
   in Tests.Domain.Interfaces + `{Name}MockFactory` in Testing.Contracts),
   `*MutationTests` / `*MutationKilling` stay impl-side, **no package version
   changes** (Testing.Contracts stays a non-runnable `xunit.v3.extensibility.core`
   library; never `xunit.v3.mtp-v2`). This area is **mutation-hardened** (Adaptive-DOCX
   complete per `docs/qa/test-plans/mutation-testing.md`) — verify the N+1 count math
   **exactly**, run a **scoped Stryker re-run** (`StrykerCompat=true dotnet stryker
   --mutate "**/{Class}.cs"` from the test-project dir; **trust Killed-delta +
   Timeout-0**, not the headline % — the combined-run % shifts via NoCoverage in the
   denominator), solution build 0/0, then run `/itdd-adversarial-review phase-3` and
   record the verdict in the §5 tracker.

5. **Gotchas (learned Phases 1–2):** rerun Tests.Architecture only after a **normal
   (non-StrykerCompat) rebuild** — the IL-scanning stub rule flaps (phantom 18/19)
   against StrykerCompat-flattened artifacts; check `git status` for a **detached
   HEAD** after reviewer subagents run; **commit messages with bodies need PowerShell
   here-strings (`@'…'@`, closing `'@` at column 0) via the PowerShell tool — the Bash
   tool mangles them into a literal `@` subject line**; update the auto-memory file
   (`itdd-test-refactor-plan.md`) and the plan §5 tracker + §4.1 playbook when done.

## State snapshot (2026-06-10, end of Phase 2 session)

| Item | Value |
|------|-------|
| Branch / tip | Kt2 @ `368f5cd` (unpushed) |
| Tracker | Plan §5: Phases 0, 1, 2 ✅ GO; Phase 3 next |
| Phase 2 commits | `5b83e9e` (conversion) + `368f5cd` (gate verdict + playbook) |
| Phase 2 counts | Tests.Domain 321→249 · Tests.Domain.Interfaces 52→125 · Export.Adaptive 173→173 (net 0) — all green |
| Phase 2 Stryker | Killed 408 = baseline sum (Units 17-20: 150+154+96+8), Survived 64 (floor), Timeout 0 — config untouched |
| Build / arch | Solution 0/0; Tests.Architecture 19/19 (clean rebuild) |
| Phase 2 gate | GO WITH CONDITIONS — missing-cancellation "Blocker" downgraded to Minor-carried (neither source had a cancellation test + all 4 impls ignore the token → net-new, out of zero-drift scope); 0.7→0.5 similarity "weakening" discarded (twin = executable truth, fake returns real 0.7) |
| Carried → Phase 6 | Fix the 4 Export.Adaptive impls to honor `CancellationToken`, then add a pre-cancelled-token test to each contract base (recorded in plan §4 Phase 6) |
| Open Minor (Phase 0) | promote-or-justify XML/DOCX content tests + optional explicit IndQuestResults ref (→ Phase 6) |
| Open Minor (Phase 1) | optional single-source SourceCount assertion in FieldMergeStrategyContract |
