# ITDD Test-Suite Refactor — Master Plan (2026-06-10)

> **Goal:** bring the test suite into conformance with
> `docs/Interface-Driven-Test-Driven-Development.md` — one abstract **contract test
> suite** per stable interface, **inherited** (instanced) by each implementation's
> test class — replacing today's landscape of standalone mock "contract" classes and
> copy-pasted, drifted implementation twins.
>
> **Design stance (owner decision, 2026-06-10):** mock-based contract tests are not
> merely allowed — they are **encouraged as the design step**. The mock class is the
> **blueprint**: it is authored first, when the contract is designed and no concrete
> class exists yet, and every implementation must follow it. Therefore the mock
> classes are **never deleted** by this refactor. They become the first *instance*
> of each contract base — a deriving class whose SUT is supplied by a **dedicated
> mock-factory helper** — sitting alongside one deriving instance per real
> implementation.
>
> **Status:** PLANNING COMPLETE — plan adversarial-review gate passed 2026-06-10
> (`/itdd-adversarial-review plan` → **GO WITH CONDITIONS**; all six conditions
> applied to this doc + primer the same day). Inventory + drift map below. Execution
> is split into phases, one or two sessions each. Update the tracker table as phases land.

---

## 1. Current state — four coexisting shapes (none is the target)

The ITDD primer mandates (and ADR is supposed to fix) this shape:

```csharp
// Testing.Contracts (shared lib)
public abstract class FieldMergeStrategyContract        // abstract ⇒ not discovered itself
{
    protected abstract IFieldMergeStrategy CreateSut(); // or ctor-injected interface-typed Sut
    [Fact] public async Task MergeAsync_ShouldHandleNullEntries() { var sut = CreateSut(); ... }
}

// Tests.Domain.Interfaces (runnable test project) — the BLUEPRINT instance (designed
// first, before any implementation). SUT creation lives in a dedicated helper (in
// Testing.Contracts) that configures the NSubstitute mock to exhibit every contract
// behavior — the mock setup IS the executable design spec.
public sealed class MockFieldMergeStrategyContractTests : FieldMergeStrategyContract
{
    protected override IFieldMergeStrategy CreateSut()
        => FieldMergeStrategyMockFactory.CreateContractConformingMock();
}

// Tests.Infrastructure.Extraction.Adaptive (one per implementation)
public sealed class EnhancedFieldMergeStrategyContractTests : FieldMergeStrategyContract
{
    protected override IFieldMergeStrategy CreateSut() => new EnhancedFieldMergeStrategy(_logger);
}
```

Every contract base thus has **N + 1 inheritors**: 1 mock/blueprint instance + N real
implementations — all running the identical test bodies.

What exists instead:

| # | Shape | Where | Problem |
|---|-------|-------|---------|
| 1 | **Standalone mock-SUT "contract tests" (the blueprints)** — `Substitute.For<IFoo>()` is the SUT; each test stubs `.Returns(...)` inline and asserts the stub | 9 classes / **168 test methods**: `Tests.Domain\Domain\Interfaces\I*ContractTests.cs` (7), `Tests.Domain.Interfaces\IIManualReviewerPanelTests.cs`, `Tests.Domain\Repositories\IRepositoryContractTests.cs` | These are the **design blueprints** (authored first, by intent — they are KEPT). Structural problem: they are standalone copies, so implementations can't inherit them and the per-test inline stubbing isn't shareable. Conversion: test bodies → contract base; inline stubbing → dedicated mock-factory helper; the class itself → the blueprint instance of the base. |
| 2 | **Copy-paste implementation twins** — contract test bodies duplicated into per-implementation classes (`*LiskovTests.cs`, `*Tests.cs`) with a real SUT, then drifted | `Tests.Infrastructure.Export.Adaptive`, `Tests.Infrastructure.Extraction.Adaptive`, `Tests.Infrastructure.Database`, … | The copies are the *real* tests, but contract intent and implementation specifics are interleaved; drift ranges from zero to severe (map in §2). |
| 3 | **Conflated real-SUT classes *named* ContractTests** living in the implementation test project | `Tests.Infrastructure.Classification\ExpedienteClasifierServiceContractTests.cs`, `FusionExpedienteServiceContractTests.cs` | Contract and implementation tests are one class in the wrong layer; nothing reusable by a second implementation. |
| 4 | **Static-helper shared contract** (closest to intent, wrong mechanism) + a **fake-green placeholder** | `09 Testing\01 Abstractions\Testing\Contracts\IPersonIdentityResolverContractTests.cs` (1 static helper, never invoked by any impl test) + `Tests.Domain.Interfaces\IPersonIdentityResolverContractExecutionTests.cs` (body = `await Task.CompletedTask`) | Helper orphaned; the "execution" test passes while executing nothing. |

Supporting facts (verified 2026-06-10):

- **The governing ADR does not exist yet.** Existing ADRs are ADR-001…004, 008, 009
  (no leading-zero style). The primer originally deferred the mandatory mechanism to a
  nonexistent "ADR-0003"; its line-3 reference has since been repointed at **ADR-005**
  (marked "to be authored"). **The ADR must be authored in Phase 0, before Phase 1.**
- `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §6.5 prescribes placement
  (`Tests.Domain.Interfaces/` for shared contract suites) but not the Sut mechanism.
- `docs/qa/test-plans/iitdd-contract-test-tasks.md` holds the original story
  templates (Story 1.3–1.8) — they prescribe the **mock-based
  `II{Name}Tests.cs` pattern** as standalone classes. The mock-first design step they
  encode is correct and kept; what the new ADR supersedes is the *standalone* shape
  (blueprints must be instances of an inheritable contract base, with mock setup in
  a dedicated factory).
- **No abstract contract base class exists anywhere** in the solution (grep:
  `abstract class *Contract` ⇒ 0 hits).
- **Reference topology (corrected at plan review 2026-06-10):** `Testing.Contracts`
  (in `09 Testing\01 Abstractions`) references Domain + Testing.Abstractions and is a
  **non-runnable library** (packages: Shouldly + `xunit.v3.extensibility.core` 3.2.2,
  centrally pinned). It is the correct home for the abstract bases and mock factories.
  **However, the phase-target test projects do NOT yet reference it:** `Tests.Domain`,
  `Tests.Infrastructure.Extraction.Adaptive`, `Tests.Infrastructure.Export.Adaptive`,
  and `Tests.Infrastructure.Classification` all lack the `ProjectReference` — each
  phase adds it as a first step. (Only `Tests.Domain.Interfaces`,
  `Tests.Infrastructure.Database`, and 8 projects outside this refactor's scope
  reference it today.)
- **Architecture tests are compatible:** stub-detection (IL size) skips abstract
  classes; only constraint is contract-base names must not duplicate adapter class
  names across layers.
- ~229 test classes total in `08 Tests`; the mock-SUT problem is concentrated in
  `01 Core` (9 classes); `02 Infrastructure` (~132 classes) mocks dependencies
  correctly.

## 2. Inventory & drift map (contract ↔ implementation)

Drift = method-name comparison between the mock "contract" class and the real-SUT
twin(s). "Missing" = contract documents it, no real test executes it.

> **Accounting rule (plan review 2026-06-10):** all counts here and in phase
> reviews are `[Fact]`/`[Theory]` **attribute** counts, never public-method counts —
> three rows below originally over-counted a public `Dispose()`/helper as a test
> (SchemaEvolutionDetectorTests 19→18, AdaptiveExporterTests 18→17,
> EfCoreRepositoryTests 8→7; now corrected).

| Interface | Contract class (shape #) | Impl(s) | Real-SUT twin | Drift | Notes |
|-----------|--------------------------|---------|---------------|-------|-------|
| `IFieldMergeStrategy` (16 tests) | `IFieldMergeStrategyContractTests` (1) | `EnhancedFieldMergeStrategy` | `EnhancedFieldMergeStrategyLiskovTests` (16, `_Liskov` suffix) | **ZERO** — 1:1 | Ideal pilot. + MutationKilling class (impl-side, stays). |
| `ITemplateFieldMapper` (22) | `ITemplateFieldMapperContractTests` (1) | `TemplateFieldMapper` | `TemplateFieldMapperTests` (22) | **ZERO** | + MutationTests (stays impl-side). |
| `ITemplateRepository` (18) | `ITemplateRepositoryContractTests` (1) | `TemplateRepository` | `TemplateRepositoryTests` (18) | **ZERO** | |
| `ISchemaEvolutionDetector` (15) | `ISchemaEvolutionDetectorContractTests` (1) | `SchemaEvolutionDetector` | `SchemaEvolutionDetectorTests` (18) | **MINIMAL** | ~3 impl-side tests that are actually contract-grade (null/boundary) → promote into base (per-method recount in Phase 2). |
| `IAdaptiveExporter` (17) | `IAdaptiveExporterContractTests` (1) | `AdaptiveExporter` | `AdaptiveExporterTests` (17) + Caching/ErrorPath/Mutation classes | **ZERO (by count)** | 17:17 — confirm per-method in Phase 2. Caching/error-path classes are impl-specific, stay. |
| `IAdaptiveDocxExtractor` (15) | `IAdaptiveDocxExtractorContractTests` (1) | `AdaptiveDocxExtractor` | `AdaptiveDocxExtractorLiskovTests` (12) | **MODERATE** | 3 contract tests have no real execution (default-mode + 2 meta-tests); twin reorganized by mode. |
| `IAdaptiveDocxStrategy` (22) | `IAdaptiveDocxStrategyContractTests` (1) | **5 strategies** (Contextual/Structured/TableBased/Search/Complement) | per-strategy `*LiskovTests` (~16 each) + MutationKilling | **SIGNIFICANT** | ~6 contract tests never executed against any strategy (perf test, medium/low-confidence tiers, 2 Liskov meta-tests). Biggest payoff for a shared base (5 inheritors). Decide per missing test: restore into base vs consciously demote (perf + confidence-tier tests likely NOT contract-grade). |
| `IManualReviewerPanel` (18) | `IIManualReviewerPanelTests` (1) | `ManualReviewerService` (Infrastructure.Database) | `ManualReviewerServiceTests` + `…IntegrationTests` | **UNQUANTIFIED** | Needs the per-method diff in its phase (survey didn't finish the count). |
| `IRepository<T,TId>` (25) | `IRepositoryContractTests` (1) | `EfCoreRepository<T>` | `EfCoreRepositoryTests` (7) + IntegrationTests | **SEVERE** | Real tests cover ~28% of the documented contract; Remove/Update/FirstOrDefault/Count/Exists/Save-failure paths have NO real-SUT execution. Contract base will need a persistence-capable SUT factory (InMemory provider or Testcontainers fixture hook). |
| `IPersonIdentityResolver` | static helper in `Testing.Contracts` (4) + fake-green placeholder | `PersonIdentityResolverService` | `PersonIdentityResolverServiceTests` (8) + EdgeCase + Mutation | **SEVERE** | 1-method contract never invoked; impl tests don't reference it. Delete placeholder, build real contract base. |
| `IExpedienteClasifier` (18 real tests) | — conflated (3) | `ExpedienteClasifierService` | same file IS the impl test | **CONFLATED** | Split: interface-generic behaviors → base; CNBV/fixture/semantic-mock specifics → impl class. Mutation-hardened (Kt2) — preserve kill power. |
| `IFusionExpediente*` (~12–15 real tests) | — conflated (3) | `FusionExpedienteService` | same file | **CONFLATED** | Same split. 2789-line service, Stryker 0-survivors — refactor must be behavior-preserving. |

**Other candidates discovered, not yet paired** (sweep in Phase 6): interface-named
test classes elsewhere; `FieldPatternValidatorContractTests` / `FieldSanitizerContractTests`
(Tests.Domain — real-SUT, likely fine, just naming); any port in §6.5's spirit that
has multiple adapters (e.g., `IFieldExtractor<T>` family across Txt/Xml/Docx/Pdf
extractors — strong future candidate for a contract base even though no mock-class
exists today).

## 3. Target conventions (to be ratified by the new ADR — Phase 0)

1. **Home:** abstract contract bases + mock factories live in
   **`ExxerCube.Prisma.Testing.Contracts`** (`09 Testing\01 Abstractions\Testing\Contracts\`).
   Production reference: Domain only (plus Testing.Abstractions; packages Shouldly +
   `xunit.v3.extensibility.core`, all already present). It stays a **non-runnable
   library** — it must NOT reference `xunit.v3.mtp-v2`, which would turn it into an
   executable test project and change the solution test sweep. **Blueprint instances
   live in `Tests.Domain.Interfaces`** — a runnable test project that already
   references Testing.Contracts. (Home + package shape decided at plan review
   2026-06-10; ADR-005 ratifies both in Phase 0.)
2. **Shape:** abstract class, **constructor-injected interface-typed `Sut`** as the
   default (primer says ADR makes this default), `protected abstract T CreateSut()`
   as sanctioned fallback where construction needs per-impl fixtures (DB, files).
   Abstract ⇒ xUnit does not discover the base; every `[Fact]`/`[Theory]` runs once
   per deriving class. (xunit.v3 + MTP handles inherited facts natively.)
3. **Naming:** base = `{InterfaceNameWithoutI}Contract` (e.g. `FieldMergeStrategyContract`)
   — avoids duplicate-name arch rule and the confusing `II*` prefix; deriving class =
   `{ImplementationName}ContractTests` in the implementation's existing test project.
4. **Scope rule:** a test belongs in the base iff **any correct implementation must
   pass it** (Result semantics, null handling, cancellation, ordering/invariants).
   Implementation-specific tests (fixtures, regex specifics, mutation-killing pins,
   performance, caching, logging) stay in the impl test project — alongside, never
   inside, the contract.
5. **Mock-SUT classes are KEPT and converted, never deleted.** Each becomes the
   blueprint instance of its contract base (`Mock{Name}ContractTests : {Name}Contract`).
   Its SUT comes from a **dedicated mock-factory helper** in Testing.Contracts
   (`{Name}MockFactory.CreateContractConformingMock()`), which centralizes the
   NSubstitute configuration that today lives inline per test. (Testing.Contracts
   gains an NSubstitute package reference in Phase 0 — the existing centrally-pinned
   version; no version changes.) The factory's setup
   is the executable design spec: it encodes, in one place, the behavior any
   implementation must exhibit. Where contract bodies assert behavior a naive
   `.Returns(...)` stub cannot satisfy (e.g., "merge actually combines fields"),
   the factory may grow argument-sensitive stubbing or hand-written fake logic —
   that evolution toward a *reference fake* is expected and desirable.
6. **Mutation-test classes (`*MutationTests`, `*MutationKillingTests`) are never
   merged into contracts** — they are implementation-pinning by definition.
7. Supersede `docs/qa/test-plans/iitdd-contract-test-tasks.md` (standalone-class
   shape) with a pointer to the ADR — the mock-first design step it teaches remains
   the encouraged way to author a new contract.

## 4. Phased execution plan (one session ≈ one phase unless noted)

> Per-phase definition of done: solution builds 0/0; affected test projects green;
> for mutation-hardened units, a scoped Stryker re-run (or manual-mutant spot check)
> confirms kill power didn't regress; converted interfaces have base + blueprint
> (mock-factory) instance + one instance per implementation, and the superseded
> copy-paste twin is removed; **adversarial review gate passed**
> (`/itdd-adversarial-review phase-N` — persisted at
> `.claude/commands/itdd-adversarial-review.md`; verdict GO, no open
> Blocker/Major); tracker table updated.
>
> The same gate reviews this plan itself before Phase 0 starts:
> `/itdd-adversarial-review plan`.

### Phase 0 — Governance & template (small session)
- Author **ADR-005-itdd-contract-tests-injected-sut.md** ratifying §3 (number choice
  confirmed: ADR-0003 collides in spirit with existing ADR-003 metrics). ✅ Already
  done 2026-06-10: the primer was updated — line-3 reference now points at ADR-005
  (marked "to be authored"), and it documents the blueprint-instance + mock-factory
  convention (Mock-First Design section, N+1 inheritors, reconciled "What Contract
  Tests Are Not", Repository Rule). Phase 0 only needs to write the ADR itself and
  remove the "to be authored" note.
- Decide & document the two SUT mechanisms (injected `Sut` vs `CreateSut()`) with
  examples for: pure service, service-with-logger, persistence-backed.
- ADR-005 must also ratify the §3.1 decisions: blueprint-instance home =
  `Tests.Domain.Interfaces`; Testing.Contracts stays a non-runnable
  `xunit.v3.extensibility.core` library (never `xunit.v3.mtp-v2`).
- Add NSubstitute (existing pinned version) to Testing.Contracts for the mock factories.
- Add the contract-base skeleton + **mock-factory helper pattern** + one worked
  example template (base + blueprint instance + implementation instance) to
  Testing.Contracts.
- **Empirical proof gate (DoD):** the worked example must demonstrate, not assume:
  (a) `[Fact]`/`[Theory]` compile in Testing.Contracts via `xunit.v3.extensibility.core`
  alone without making it runnable; (b) the abstract base is NOT discovered as a test
  class; (c) inherited facts run once per deriving class under MTP (verify via test
  counts). No abstract contract base exists in the solution today, so this mechanism
  is unproven here until this gate passes.
- Optional guardrail (may defer to Phase 6): Tests.Architecture rule — every class
  named `*Contract` in Testing.Contracts must be abstract and have ≥1 inheritor.

### Phase 1 — Pilot: `IFieldMergeStrategy` (zero drift, 1 impl)
- Add the `Testing.Contracts` ProjectReference to
  `Tests.Infrastructure.Extraction.Adaptive` (it does not have one yet).
- Lift the 16 **real** test bodies from `EnhancedFieldMergeStrategyLiskovTests` into
  `FieldMergeStrategyContract` (the Liskov twin has the executable truth; the mock
  class is the design checklist the base must fully cover).
- Build `FieldMergeStrategyMockFactory` from the mock class's inline `.Returns(...)`
  setups, and convert the mock class into the blueprint instance
  `MockFieldMergeStrategyContractTests : FieldMergeStrategyContract` — it moves from
  `Tests.Domain` to `Tests.Domain.Interfaces` (the blueprint home, §3.1).
- Derive `EnhancedFieldMergeStrategyContractTests : FieldMergeStrategyContract`.
- Remove only the superseded copy `EnhancedFieldMergeStrategyLiskovTests`.
- Keep `EnhancedFieldMergeStrategyMutationKillingTests` untouched.
- Validate: test counts, MTP discovery, scoped Stryker on Extraction.Adaptive — this
  Stryker run is the empirical gate for cross-assembly inherited-`[Fact]` + Stryker
  compatibility (§6.1), not a formality.
- **Record lessons in a playbook section of this doc** — the per-interface recipe
  the remaining phases will follow.

### Phase 2 — Export.Adaptive cluster (4 interfaces, zero/minimal drift)
- Add the `Testing.Contracts` ProjectReference to
  `Tests.Infrastructure.Export.Adaptive` (it does not have one yet).
- `ITemplateFieldMapper`, `ITemplateRepository`, `ISchemaEvolutionDetector`,
  `IAdaptiveExporter` — same recipe as pilot; all twins live in
  `Tests.Infrastructure.Export.Adaptive`.
- Promote the ~3 SchemaEvolutionDetector impl-side contract-grade tests into the base
  (per-method recount first — see §2 accounting rule).
- `TemplateRepository`/`SchemaEvolutionDetector` use EF InMemory setup → first real
  use of the `CreateSut()` fallback with per-impl fixture.

### Phase 3 — Adaptive DOCX cluster (1 extractor + 5 strategies; the payoff phase)
- `AdaptiveDocxStrategyContract` inherited by **5** strategy test classes — the
  first true multi-implementation contract; reconcile the ~6 never-executed
  contract tests: restore the genuinely contract-grade ones (expect real failures —
  triage each as contract-bug vs implementation-bug, don't blind-fix), demote
  perf/confidence-tier tests to impl classes or delete with rationale.
- `AdaptiveDocxExtractorContract` for the orchestrator (restore 3 missing, incl.
  default-mode behavior).
- Heaviest session; mutation-hardened area — verify kill power after.

### Phase 4 — Split the conflated Classification pair
- Add the `Testing.Contracts` ProjectReference to
  `Tests.Infrastructure.Classification` (it does not have one yet).
- `ExpedienteClasifierServiceContractTests` → `ExpedienteClasifierContract` (base;
  interface-generic: classification result invariants, Result semantics,
  cancellation) + `ExpedienteClasifierServiceContractTests` (deriving class keeps
  CNBV 100–104 fixtures and semantic-analyzer-mock specifics).
- Same for `FusionExpedienteServiceContractTests` → `FusionExpedienteContract`.
- These suites are freshly Stryker-hardened (Kt2, 0 survivors) — moves must be
  name-preserving where possible; scoped Stryker re-run mandatory here.

### Phase 5 — Severe-drift repairs (likely 2 sessions)
- **`IRepository<T,TId>`**: author `RepositoryContract<TEntity,TId>` from the
  25-method mock checklist; execute against `EfCoreRepository<T>` (InMemory for
  unit-grade, optional Testcontainers-derived class for integration-grade — reuse
  `SqlServerContainerFixture`). This *adds* ~17 real tests that never existed.
- **`IManualReviewerPanel`**: quantify drift (pending), then convert.
- **`IPersonIdentityResolver`**: delete the fake-green placeholder (`await
  Task.CompletedTask` — it's a no-op, not a blueprint); fold the orphaned static
  helper into a proper contract base + mock factory; derive a blueprint instance
  and a `PersonIdentityResolverService` instance.
  (DB persistence is a known TODO in this service — contract tests may surface it;
  scope contract to current behavior, note gaps.)

### Phase 6 — Sweep, guardrails, docs
- Repo-wide sweep for unpaired candidates (multi-adapter ports like
  `IFieldExtractor<T>` family; `FieldPatternValidator`/`FieldSanitizer` naming).
- Convert any remaining standalone mock-SUT classes into blueprint instances of
  their bases, in `Tests.Domain.Interfaces` (blueprint home settled at plan review —
  §3.1; ratified by ADR-005 in Phase 0).
- Add the Tests.Architecture guardrail (if deferred from Phase 0).
- Update `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §6.5 + primer + supersede
  `iitdd-contract-test-tasks.md`. Full solution test run.

## 5. Progress tracker

| Phase | Scope | Status | Session/commit |
|-------|-------|--------|----------------|
| — | Plan adversarial-review gate | ✅ GO WITH CONDITIONS (2026-06-10) — all 6 conditions applied to plan + primer same day | |
| 0 | ADR + template + playbook | ☐ Not started | |
| 1 | Pilot: IFieldMergeStrategy | ☐ Not started | |
| 2 | Export.Adaptive ×4 | ☐ Not started | |
| 3 | Adaptive DOCX ×6 | ☐ Not started | |
| 4 | Classification split ×2 | ☐ Not started | |
| 5 | IRepository / IManualReviewerPanel / IPersonIdentityResolver | ☐ Not started | |
| 6 | Sweep + guardrails + docs | ☐ Not started | |

## 6. Risks & invariants

1. **Mutation-testing investment is the crown jewel** (months of Stryker hardening,
   0-killable-survivor baselines). Moving a test must never weaken it: keep method
   names and bodies intact when lifting. Contract bases compile into
   `Testing.Contracts.dll`; xUnit discovers the inherited `[Fact]`s via each deriving
   class in the test assembly, so per-project Stryker configs are *expected* to keep
   working unchanged — Phase 1's scoped Stryker run is the empirical gate for that
   expectation, not a formality.
2. **Blueprint behaviors were never validated against reality.** When a documented
   contract behavior gets its first real execution (Phases 3/5), failures are
   findings, not regressions — triage contract-bug vs implementation-bug explicitly.
   The blueprint is the spec: if the implementation is wrong, fix the implementation;
   only change the blueprint with an explicit contract decision.
3. **xunit.v3 + MTP coupling is fragile** (see CLAUDE.md) — no package *version*
   changes are needed or wanted in this refactor. Testing.Contracts already references
   `xunit.v3.extensibility.core` 3.2.2 (verified 2026-06-10) and must **NOT** gain
   `xunit.v3.mtp-v2` — that would make it a runnable test project (§3.1). The only
   additions are existing centrally-pinned packages/references: NSubstitute in
   Testing.Contracts (for the mock factories) and `Testing.Contracts`
   ProjectReferences in the phase-target test projects.
4. **Don't touch dormant/by-design areas** (Python/VLM, GotOcr2 skipped suites).
5. Each phase is independently shippable; no phase leaves deleted coverage behind.
   Mock/blueprint classes are never deleted — only converted. The only files removed
   are superseded copy-paste twins, and only after their bodies live in the base and
   run via the deriving implementation instance.
6. **Mock factories must stay contract-conforming.** If a contract base gains a test
   the factory's mock can't satisfy, extend the factory (argument-sensitive stubs or
   fake logic) in the same change — a red blueprint instance means the design spec
   itself is incomplete.
