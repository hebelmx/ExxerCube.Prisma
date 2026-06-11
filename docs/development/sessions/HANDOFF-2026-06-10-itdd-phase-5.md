# HANDOFF — ITDD Test-Suite Refactor: Phase 5 (Severe-drift repairs)

> **For the next agent.** Branch **Kt2** (tip `bfe8d45`, pushed). Phases 0–4 are DONE,
> all gates GO / GO-WITH-CONDITIONS. Phase 5 is the **severe-drift** phase — the first
> phase that *adds coverage that never existed* (it is not a zero-drift lift). Plan it
> as **2 sessions**: **Session A = `IRepository<T,TId>`** (the heavy one: persistence
> fixture + ~18 net-new tests), **Session B = `IManualReviewerPanel` + `IPersonIdentityResolver`**.

## Prompt

Execute **Phase 5** of the ITDD test-suite refactor on branch **Kt2**. This is the
**severe-drift** phase: unlike Phases 1–4 (lift/split with preserved coverage), here the
contract bases will *gain real tests that no implementation currently runs*, so **first
executions are findings to triage (contract-bug vs implementation-bug), not regressions**
(plan §6 invariant 2). Do **one interface end-to-end at a time**, each through the full
§4.1 playbook + gate. Suggested split: Session A = `IRepository`; Session B =
`IManualReviewerPanel` + `IPersonIdentityResolver`.

1. **Read first:** master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
   (§2 drift map rows for these three interfaces, §3 conventions, §4 Phase 5, §4.1
   playbook incl. **Phase 2 addenda** — the EF-InMemory `CreateSut()` + seed-hook pattern
   is the direct precedent for `IRepository`, §6 invariants). ADR-005
   (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`) §3 (the
   `CreateSut()` fallback + persistence example C), §5 (scope rule), §6 (mock-first).
   Auto-memory `itdd-test-refactor-plan.md` has the running state and per-phase lessons.

2. **Per interface, follow §4.1 verbatim:** baselines first (run the affected projects,
   record exact `[Fact]`/`[Theory]` totals before writing any `.cs`); author the
   `{Name}Contract` base + `{Name}MockFactory` (reference fake) in Testing.Contracts;
   convert the standalone mock class into the `Mock{Name}ContractTests` blueprint in
   `Tests.Domain.Interfaces`; derive the impl instance in the impl test project; delete
   ONLY the superseded copy-paste twin (never the blueprint); verify N+1 counts exactly;
   scoped Stryker where the unit is mutation-hardened; clean rebuild → `Tests.Architecture`
   19/19; `/itdd-adversarial-review phase-5`; record verdict in §5 tracker + auto-memory.

3. **No new ProjectReferences needed:** `Tests.Domain.Interfaces`,
   `Tests.Infrastructure.Database`, and `Tests.Infrastructure.Classification` already
   reference `Testing.Contracts` (verified 2026-06-10). The only structural move is the
   `IRepository` blueprint **relocating** from `Tests.Domain\Repositories\` into
   `Tests.Domain.Interfaces` (the blueprint home, §3.1).

4. **Gate:** `/itdd-adversarial-review phase-5`. Because this phase *adds* tests, the
   reviewer should also check that **every new base test is genuinely contract-grade**
   (would every correct impl pass it?) and that no first-execution failure was
   silently "fixed" by weakening the assertion. Cross-check every Blocker/Major by
   reproducing from cited evidence before accepting (Phases 1–4 each had headline
   findings that died on reproduction — see those tracker rows).

5. **Docs + commit + push** after each interface (or each session): §4.1 Phase 5
   addendum, flip §5 tracker, update auto-memory, `git push origin Kt2`.

## State snapshot (2026-06-10, after Phase 4 gate)

| Item | Value |
|------|-------|
| Branch / tip | Kt2 @ `bfe8d45` (pushed) |
| Phases done | 0,1,2,3,4 — all gates GO / GO-WITH-CONDITIONS (commits e49f145 / 0b4197a / 1df7735 / d19ae6b / 97f9a0e; gate docs through bfe8d45) |
| Solution build | 0/0 at last check; **re-verify at session start** |
| Carried into Phase 6 (not Phase 5) | Cancellation tests absent for the 4 Export.Adaptive contracts + both Classification contracts (fix impls to honor the token, THEN add a pre-cancelled-token base test); optional blueprint `MinHighClassificationConfidence` tightening; arch guardrail (every `*Contract` in Testing.Contracts is abstract + has ≥1 inheritor). |

## The three targets (verified on disk 2026-06-10)

### A. `IRepository<T,TId>` — SEVERE (real coverage ~28%); the heavy one
- **Interface:** `01 Core/Domain/Interfaces/IRepository.cs` (15 async methods).
- **Blueprint (mock-SUT, 25 `[Fact]`):** `08 Tests/01 Core/Tests.Domain/Repositories/IRepositoryContractTests.cs`
  — **currently in `Tests.Domain`, not the blueprint home.** Convert → `MockRepositoryContractTests`
  and **relocate to `Tests.Domain.Interfaces`** (so `Tests.Domain` loses these 25 from its total).
- **Real twin (7 `[Fact]`):** `08 Tests/02 Infrastructure/Tests.Infrastructure.Database/EfCoreRepositoryTests.cs`
  + `…IntegrationTests`. **~18 contract tests have NO real-SUT execution** (Remove / Update /
  FirstOrDefault / Count / Exists / Save-failure paths — per §2). These become the net-new coverage.
- **Production:** `02 Infrastructure/Infrastructure.Database/Repositories/EfCoreRepository.cs`.
- **SUT mechanism:** `CreateSut()` + persistence fixture — **mirror Phase 2's
  `TemplateRepositoryContract`** (`09 Testing/01 Abstractions/Testing/Contracts/TemplateRepositoryContract.cs`
  + `…Export.Adaptive/TemplateRepositoryContractTests.cs`): EF InMemory for unit-grade; optionally a
  Testcontainers-derived class reusing `SqlServerContainerFixture` for integration-grade. Seed/verify
  **through the interface** (`AddAsync`/`GetByIdAsync`), not via `DbContext` directly (Phase 2 addendum).
- **Watch:** the blueprint is generic `IRepository<T,TId>` — pick a concrete entity for the contract
  (whatever the 25 mock tests already use). First real executions of Remove/Update/Count/Exists **will**
  surface behavior the mock never validated — triage each (plan §6.2).

### B. `IManualReviewerPanel` — drift UNQUANTIFIED (do the per-method diff FIRST)
- **Interface:** `01 Core/Domain/Interfaces/IManualReviewerPanel.cs`.
- **Blueprint (mock-SUT, 18 `[Fact]`):** `08 Tests/01 Core/Tests.Domain.Interfaces/IIManualReviewerPanelTests.cs`
  — already in the blueprint home (note the `II` double-prefix in the filename); convert →
  `MockManualReviewerPanelContractTests`.
- **Real twin (22 + 4 `[Fact]`):** `…Tests.Infrastructure.Database/ManualReviewerServiceTests.cs` (22)
  + `ManualReviewerServiceIntegrationTests.cs` (4).
- **Production:** `02 Infrastructure/Infrastructure.Database/ManualReviewerService.cs`.
- **Do first:** the §2 row is "UNQUANTIFIED" — run the per-`[Fact]` blueprint-vs-twin diff (playbook
  step 2) before deciding what's contract-grade vs impl richness. 22 real tests over an 18-test
  blueprint suggests the real suite is *richer*, not thinner — most of the contract probably has real
  execution already; the work is mostly structural (base + blueprint + impl instance).

### C. `IPersonIdentityResolver` — SEVERE (orphaned helper + fake-green placeholder)
- **Interface:** `01 Core/Domain/Interfaces/IPersonIdentityResolver.cs`.
- **Orphaned static helper (0 `[Fact]`):** `09 Testing/01 Abstractions/Testing/Contracts/IPersonIdentityResolverContractTests.cs`
  — a `public static class` with `public static async Task VerifyFindByRfcAsync_…(…)` that no impl test
  ever invokes. **Fold its assertion body into the new `PersonIdentityResolverContract` base**, then
  delete the static helper.
- **Fake-green placeholder (1 `[Fact]`):** `08 Tests/01 Core/Tests.Domain.Interfaces/IPersonIdentityResolverContractExecutionTests.cs`
  — its body is literally `await Task.CompletedTask;` (line 46): it passes while executing nothing.
  **DELETE it** (it is a no-op, NOT a blueprint — invariant 5 only protects real mock blueprints). Its
  intent is replaced by the proper `MockPersonIdentityResolverContractTests` blueprint.
- **Real tests:** `…Tests.Infrastructure.Classification/PersonIdentityResolverServiceTests.cs` (8) +
  `…EdgeCaseTests.cs` (11) + **`…MutationTests.cs` (26) — UNTOUCHED, the kill-power pins.**
- **Production:** `02 Infrastructure/Infrastructure.Classification/PersonIdentityResolverService.cs`.
- **⚠️ Mutation-hardened — preserve kill power (see [[mutation-testing-stryker]]):** only
  `FindByRfcAsync` is a DB stub (returns success-with-null today). `GenerateRfcVariants` /
  `NormalizeName` / `GetNormalizedName` / `DeduplicatePersonsAsync` are **pure** and Stryker-complete
  (Killed 107, 0 killable). Known dead-code floor: the normalized-RFC dedup branch +
  `NormalizeRfcForComparison` are **unreachable** (GenerateRfcVariants already emits the
  middle-letter-dropped variants). Known kill lesson: a same-name dedup test affects both persons
  identically → only a *differs-in-one-part* pair kills the per-part guard. **Scope the contract to
  current behavior** (FindByRfcAsync → success-with-null is the documented stub contract); note the DB
  persistence TODO as a gap, do NOT fail the phase on it (plan §4 Phase 5 note).

## Recipe reminders specific to Phase 5 (fold into §4.1 after the gate)

- **This phase ADDS coverage — that is the point, not a violation.** §4.1 was written for
  zero-drift lifts; for `IRepository` the base will run ~18 tests `EfCoreRepository` never saw.
  Read the production method before restoring each, predict the result, and treat first-run failures
  as triage items (contract-bug → fix impl; mock-was-wrong → explicit contract decision in writing).
- **Persistence SUT = the Phase 2 pattern, already proven.** Don't re-invent: copy the
  `TemplateRepositoryContract` shape (`CreateSut()` + EF-InMemory in the impl ctor; seed via the
  interface). Stryker excludes EF/DB repositories, so body rewrites to go through the interface are
  acceptable here (no kill-power to preserve verbatim for the repository itself).
- **`IPersonIdentityResolver` is the one mutation-hardened target** — the 26 `*MutationTests` stay
  untouched and are the kill-power source; the contract base only needs the behavioural surface of the
  pure methods. Scoped Stryker re-run on `PersonIdentityResolverService` after the split (Survived 0 /
  NoCoverage == floor is the pass signal, not the headline %).
- **Two no-ops to delete, one helper to fold, zero blueprints to delete.** The fake-green
  `IPersonIdentityResolverContractExecutionTests` and the superseded copy-paste twins are the only
  deletions; the static helper is *folded then removed*; every real mock class is *converted*.

## Carried / open (not Phase 5)
- All Phase 6 items above (cancellation across 6 contracts; blueprint threshold tightening; arch
  guardrail; repo-wide unpaired-candidate sweep incl. the `IFieldExtractor<T>` family; supersede
  `iitdd-contract-test-tasks.md`; update guidelines §6.5 + primer; full-solution test run).
