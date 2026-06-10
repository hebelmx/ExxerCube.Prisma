# HANDOFF — ITDD Test-Suite Refactor: Phase 2 (Export.Adaptive cluster)

> **For the next agent.** Branch **Kt2**. Phases 0–1 are DONE and adversarially
> gated GO (commits `e49f145..ed91125`, 2026-06-10). Your job is **Phase 2**.

## Prompt

Continue the ITDD test-suite refactor on branch **Kt2** — execute **Phase 2
(Export.Adaptive cluster)**.

1. Read the master plan: `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
   — especially **§4.1 playbook (the 11-step recipe from the Phase 1 pilot — follow
   it exactly)**, the §5 tracker, and §3 conventions. ADR-005
   (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`) governs
   the shape. Reference example from the pilot: `FieldMergeStrategyContract` +
   `FieldMergeStrategyMockFactory` in
   `Prisma/Code/Src/CSharp/09 Testing/01 Abstractions/Testing/Contracts/`, with
   instances in `Tests.Domain.Interfaces` (blueprint) and
   `Tests.Infrastructure.Extraction.Adaptive` (impl).
2. Phase 2 scope: convert **ITemplateFieldMapper, ITemplateRepository,
   ISchemaEvolutionDetector, IAdaptiveExporter**. First step: add the
   Testing.Contracts `ProjectReference` to `Tests.Infrastructure.Export.Adaptive`.
   Capture baseline test counts **before writing any `.cs` file** (SDK globbing
   pulls new files into builds). Recount SchemaEvolutionDetector's ~3 promote
   candidates per-method (§2 accounting rule — attribute counts, not
   public-method counts). TemplateRepository/SchemaEvolutionDetector are the
   **first real use of the `CreateSut()` fallback** (EF InMemory fixtures) —
   ADR-005 Example C shows the shape.
3. Rules: lift twin bodies **verbatim** (names preserved — Stryker history is
   keyed to them; the only edit is the SUT line), mock classes are **converted,
   never deleted** (→ `Mock{Name}ContractTests` in Tests.Domain.Interfaces +
   `{Name}MockFactory` in Testing.Contracts), `*MutationTests` /
   `*MutationKillingTests` stay impl-side, **no package version changes**
   (xunit.v3.mtp-v2 + MTP 2.1.0 coupling is fragile; Testing.Contracts must stay
   a non-runnable `xunit.v3.extensibility.core` library). Verify the N+1 count
   math **exactly**, run a scoped Stryker re-run for the mutation-hardened units
   (`StrykerCompat=true dotnet stryker --mutate "**/{Class}.cs"` from the test
   project dir; compare against the baselines in
   `docs/qa/test-plans/mutation-testing.md` — parse the Timeout bucket, trust
   Killed-delta + 0-killable-survivors, not the headline %), solution build 0/0,
   then run `/itdd-adversarial-review phase-2` and record the verdict in the §5
   tracker.
4. Gotchas (learned at the Phase 1 gate): rerun Tests.Architecture only after a
   normal (non-StrykerCompat) rebuild — the IL-scanning stub rule flaps against
   flattened Stryker artifacts; check `git status` for a **detached HEAD** after
   reviewer subagents run (one left the repo detached — fast-forward Kt2 if a
   commit lands off-branch); update the auto-memory file
   (`itdd-test-refactor-plan.md`) when done.

## State snapshot (2026-06-10, end of Phase 1 session)

| Item | Value |
|------|-------|
| Branch / tip | Kt2 @ `ed91125` (5 ahead of origin, unpushed) |
| Tracker | Plan §5: Phases 0, 1 ✅ GO; Phase 2 next |
| Test counts | Tests.Domain 321 · Tests.Domain.Interfaces 52 · Extraction.Adaptive 318 — all green |
| Stryker (pilot unit) | EnhancedFieldMergeStrategy: Survived 7→0, 91.89%, config untouched — cross-assembly inherited [Fact]s proven |
| Build | Solution 0 warnings / 0 errors; Tests.Architecture 19/19 |
| Open Minors | Phase 0: promote-or-justify XML/DOCX content tests + optional explicit IndQuestResults ref (→ Phase 6); Phase 1: optional single-source SourceCount assertion in FieldMergeStrategyContract |
