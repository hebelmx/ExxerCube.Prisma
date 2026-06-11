# HANDOFF — ITDD Test-Suite Refactor: Phase 4 (Classification split) — FINALIZATION

> **✅ PHASE 4 COMPLETE (2026-06-10).** The gate ran: `/itdd-adversarial-review phase-4`
> → **GO WITH CONDITIONS** (3 reviewer agents + cross-check; **0 reproduced Blocker/Major**).
> Every "weakening" finding died on reproduction (exact-field/`PassesArticle4`/R29 assertions
> RELOCATED impl-side per ADR-005 §5, not deleted; thresholds are virtual properties the real
> impl overrides to the exact original bound). One genuine deviation — 5 ExpedienteClasifier
> test renames `_ReturnsNNN`→`_ClassifiesAs{Type}` — downgraded Blocker→Minor (kill-power-safe:
> MutationTests untouched + Stryker Survived 0; the `NNN` is an impl-specific CNBV literal).
> Verdict + discarded findings recorded in plan §5 tracker row 4; playbook addenda in §4.1.
> **NEXT: Phase 5.** The rest of this doc is the original finalization handoff (now satisfied).

> **For the next agent.** Branch **Kt2**. Phase 4's **build is DONE + green + pushed**
> (origin/Kt2 @ `9d13026`); **Stryker PASSED (Survived 0)** and **arch is 19/19**. The ONLY
> remaining DoD item is the **`/itdd-adversarial-review phase-4` gate** (deliberately left for
> you with a fresh budget — it spawns large reviewer outputs), then flip the tracker + final
> commit/push. Phases 0–3 are DONE and pushed.

## Prompt

Finalize **Phase 4** of the ITDD test-suite refactor on branch **Kt2**. The code is
written, builds 0/0, and all affected test projects are green; your job is to verify
mutation kill power, run the gate, record it, and push.

1. **Read first:** master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`
   (§4 Phase 4, §4.1 playbook incl. Phase 2 & 3 addenda, §5 tracker, §6 invariants);
   ADR-005 (`docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`). The
   Phase 3 record (`docs/development/sessions/HANDOFF-2026-06-10-itdd-phase-3.md`) shows
   the gate/commit cadence. Auto-memory `itdd-test-refactor-plan.md` has the running state.

2. **Stryker — ✅ ALREADY RUN AND PASSED** (no re-run needed; spot-check only if you wish). Result:
   1416 mutants tested, **Survived 0**, Killed 358 + Timeout 1058 (all covered mutants detected),
   NoCoverage 33 = the exact documented floor (18 + 15), score 97.72%. Kill-power preserved with 0
   killable survivors. The report is at `Tests.Infrastructure.Classification/StrykerOutput/2026-06-10.21-39-27/`.
   For reference, the original launch command (from the Classification test-project dir) was:
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

3. **Tests.Architecture — ✅ ALREADY 19/19** (run after a clean non-StrykerCompat solution rebuild,
   0 errors). Re-run only if you rebuild under StrykerCompat for any reason (the IL stub-detection
   rule flaps 18/19 against flattened artifacts — §4.1 step 11).

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
| Stryker | ✅ **PASSED** (45m): 405 tests discovered, 1416 mutants tested, **Survived 0**, Killed 358 + Timeout 1058 (all detected), NoCoverage 33 = **exact baseline floor** (18 ExpedienteClasifier catch-blocks + 15 Fusion Serilog/catch), score 97.72%. Kill-power preserved, **0 killable survivors** (cleaner than Phase 3's 1 Serilog survivor). The Killed/Timeout split shifted toward Timeout vs the 199/1217 baseline — documented regex/compute timeout-inflation; the stable signal Survived=0 holds. |
| Arch | ✅ **19/19** (confirmed after a clean non-StrykerCompat solution rebuild, 0 errors) |
| Gate | not yet run — **your task** (the one remaining DoD item; context-heavy → left for a fresh agent) |

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
