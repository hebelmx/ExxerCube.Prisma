# HANDOFF — ITDD Test-Suite Refactor: Phase 4 (Classification split) — FINALIZATION

> **For the next agent.** Branch **Kt2**. Phase 4's **build is DONE, green, and committed
> as WIP** (`97f9a0e`, unpushed). What remains is **verification + gate + docs + push** —
> the context-heavy adversarial gate is deliberately left for you with a fresh budget.
> Phases 0–3 are DONE and pushed (origin/Kt2 @ `e7153b0`).

## Prompt

Finalize **Phase 4** of the ITDD test-suite refactor on branch **Kt2**. The code is
written, builds 0/0, and all affected test projects are green; your job is to verify
mutation kill power, run the gate, record it, and push.

1. **Read first:** master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
   (§4 Phase 4, §4.1 playbook incl. Phase 2 & 3 addenda, §5 tracker, §6 invariants);
   ADR-005 (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`). The
   Phase 3 record (`docs/development/sessions/HANDOFF-2026-06-10-itdd-phase-3.md`) shows
   the gate/commit cadence. Auto-memory `itdd-test-refactor-plan.md` has the running state.

2. **Check the Stryker run (it was running at handoff).** A scoped re-run was launched from
   `08 Tests/02 Infrastructure/Tests.Infrastructure.Classification/` via
   `StrykerCompat=true dotnet stryker --mutate "**/ExpedienteClasifierService.cs" --mutate "**/FusionExpedienteService.cs"`.
   Background task id `b2vcc00r9`; its output file is under the session tasks dir
   (`...\tasks\b2vcc00r9.output`) — or just re-run the same command (cheap to relaunch; ~30–60 min).
   **Baselines to beat (from `docs/qa/test-plans/mutation-testing.md` Units 46–48):**
   `ExpedienteClasifierService` **Killed 199, 0 survived, 0 killable** (18 NoCoverage = catch-block
   Serilog); `FusionExpedienteService` **Killed 1217, 0 survived, 0 killable** (15 NoCoverage =
   Serilog + the two top-level catch blocks). The `*MutationTests` (35 + 37) that drive this kill
   power are **UNTOUCHED**, so expect **Killed ≈ baseline + 0 killable survivors**. Trust
   Killed-delta + killable-survivor analysis, not the headline % (parse the survivor list; any
   survivor must be the documented Serilog/catch-block equivalent floor — read the cited line to
   confirm, exactly as Phase 3 confirmed its lone `TableBasedDocxStrategy.cs:283` Serilog survivor).
   **If a real (killable) survivor appears**, it means a behavioural assertion was dropped in the
   split that the impl-side tests don't re-pin — triage it (add the assertion back impl-side or to
   the base) before proceeding; do NOT weaken to pass.

3. **Tests.Architecture** — run `08 Tests/09 Architecture/Tests.Architecture/...csproj` **after a
   normal (non-StrykerCompat) `dotnet build` of the solution** (the IL stub-detection rule flaps
   18/19 against StrykerCompat-flattened artifacts — §4.1 step 11). Expect **19/19**.

4. **Gate:** run `/itdd-adversarial-review phase-4`. Fan out the 3 reviewer lenses, then
   **cross-check every Blocker/Major by reproducing from the cited evidence before accepting**
   (Phase 2 & 3 each had a headline finding that died on reproduction). Record the verdict in the
   plan §5 tracker row + auto-memory, with any discarded findings and their reproduction.

5. **Docs + commit + push:** add a Phase 4 §4.1 playbook addendum (lessons below), flip the §5
   tracker row to ✅ with the gate verdict + commit hash, update auto-memory
   (`itdd-test-refactor-plan.md` — set NEXT to Phase 5), commit the docs, then
   `git push origin Kt2` (the WIP `97f9a0e` is unpushed — push it too).

## State snapshot (2026-06-10, end of Phase 4 build session)

| Item | Value |
|------|-------|
| Branch / tip | Kt2 @ `97f9a0e` (**unpushed**; origin/Kt2 @ `e7153b0` = end of Phase 3) |
| Phase 4 commit | `97f9a0e` (build WIP — contracts + factories + blueprints + reparented impls + csproj/GlobalUsings) |
| Counts (exact, green) | Tests.Domain.Interfaces 160→**186** (+17 ExpedienteClasifier, +9 Fusion); Classification 400→**405** (ExpedienteClasifier 18→23, Fusion 9→9); Tests.Domain unchanged (212) |
| Solution build | **0 warnings / 0 errors** |
| Stryker | **launched, result pending** (task `b2vcc00r9`) — verify Killed≈199/1217, 0 killable survivors |
| Arch | not yet re-run (do after clean rebuild) |
| Gate | not yet run |

## What was built (the shape — shape #3 "conflated", different from Phases 1–3)

These two were **real-SUT classes merely *named* `*ContractTests`** (no pre-existing mock
blueprint). Per **owner direction this session**, the split was driven by an explicit
**behavioural-vs-implementation analysis**, and full **N+1** (a fresh mock blueprint + factory was
authored for each, since none existed):

- **`ExpedienteClasifierContract`** (Testing.Contracts, **17 behavioural tests** + 12 abstract
  fixture hooks + virtual `MinHighClassificationConfidence`). Behavioural = the CNBV contract:
  type 100–104 classification, Article 4 pass/fail, the six Article 17 grounds, the 5 Situations.
  - `ExpedienteClasifierMockFactory` — reference-fake classifier keyed on the documented markers
    (Referencia keywords, `TieneAseguramiento`, FundamentoLegal/EvidenciaFirma/AreaDescripcion).
  - `MockExpedienteClasifierContractTests` (Tests.Domain.Interfaces) — blueprint; fixtures carry
    those markers.
  - `ExpedienteClasifierServiceContractTests` (Classification) — **reparented** to the base;
    overrides `CreateSut()` (real service + real `SemanticAnalyzerService`/`LevenshteinTextComparer`),
    the 12 fixtures, `MinHighClassificationConfidence => 0.80`; **keeps 6 impl-detail tests** (exact
    required-field strings per type + the R29 42-field enumeration).
- **`FusionExpedienteContract`** (Testing.Contracts, **9 behavioural tests**; the scenario data
  — agreeing/disagreeing values, source quality — is plain data the contract owns directly via
  protected builders). Behavioural = the full `FusionDecision` ladder (AllAgree / FuzzyAgreement /
  WeightedVoting / Conflict / AllSourcesNull) + `NextAction` routing. Virtual thresholds
  `AutoProcessThreshold`, `FuzzyMatchThreshold`, `MinExactMatchConfidence`.
  - `FusionExpedienteMockFactory` — **reference fusion engine** implementing the decision ladder
    (exact/accent-fuzzy/plurality/2-source-higher-reliability/3-distinct-conflict; base-weight ×
    quality-factor reliability; single-low-reliability → ManualReviewRequired).
  - `MockFusionExpedienteContractTests` (Tests.Domain.Interfaces) — blueprint over the engine.
  - `FusionExpedienteServiceContractTests` (Classification) — **reparented**; `CreateSut()` = real
    `FusionExpedienteService` + default `FusionCoefficients`; threshold overrides
    (auto-process/fuzzy from `new FusionCoefficients()`, exact-match 0.80); **no impl-detail test**
    (every assertion is behavioural).
- Added `Testing.Contracts` ProjectReference + `global using ...Testing.Contracts;` to the
  Classification test project.

## Phase 4 lessons (fold into §4.1 playbook after the gate)

- **Conflated shape needs an explicit behavioural-vs-implementation pass, per-assertion.** When a
  class is real-SUT named "ContractTests" with no blueprint, the split is not a verbatim lift: read
  each `[Fact]`, keep the assertions any correct impl must pass (the documented interface semantics —
  here the CNBV types/grounds/situations and the fusion decision ladder ARE the contract) in the
  base, and demote impl detail (exact internal field-name strings, regulation-specific enumerations).
- **Inputs become abstract fixture hooks** when they encode impl-specific test data (the classifier);
  but when the inputs are plain scenario data the contract can own them directly (the fusion engine
  scenarios). Decide per interface.
- **Thresholds as first-class virtual contract properties (owner pattern).** Materialise a threshold
  *only when it is a genuine contract clause* ("clear request ⇒ high confidence", "all agree ⇒
  auto-process", "fuzzy ⇒ clears the bar", "exact match ⇒ high confidence"), as a `protected virtual`
  property with a **minimal-plausible default** (0.5) that the real impl **overrides to its actual
  coefficient** — the behavioural test asserts against the property, so the contract isn't
  over-constrained yet the exact bound (and its kill power) is preserved on the real impl. Do NOT
  materialise arbitrary test-data averages.
- **Full N+1 even with no prior blueprint:** author a reference-fake factory that *re-implements the
  documented methodology* (not canned stubs) so the same behavioural bodies pass against both the fake
  and the real service. The blueprint supplies marker-carrying inputs the fake reads.
- **Null-safety in Testing.Contracts:** the library enforces nullable warnings-as-errors (the impl
  test project NoWarn'd CS8602/CS8604), so after `result.IsSuccess.ShouldBeTrue()` add
  `result.Value.ShouldNotBeNull();` before dereferencing `result.Value` (Roslyn flows the non-null
  state across subsequent member accesses). For fused-value setters onto non-nullable entity
  properties, null-forgive (`= v!`) where the scenario guarantees a value.
- **Counts:** the conflated deriving class's net delta = (inherited base facts + kept impl-detail
  facts) − (its old fact count). ExpedienteClasifier 18→23 (17 inherited + 6 impl-detail); Fusion
  9→9 (9 inherited + 0). The blueprint home (Tests.Domain.Interfaces) gains base-facts × 1 each.

## Carried / open

- Cancellation tests still absent for both interfaces (neither original had one); carried to Phase 6
  alongside the Export.Adaptive cancellation gap.
- **Next: Phase 5** — severe-drift repairs (`IRepository<T,TId>` author `RepositoryContract` + execute
  against `EfCoreRepository` adding ~17 never-run tests; `IManualReviewerPanel` quantify+convert;
  `IPersonIdentityResolver` delete the fake-green placeholder + fold the orphaned static helper into a
  real base+factory). Likely 2 sessions.
