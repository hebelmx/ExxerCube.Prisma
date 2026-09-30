# ITDD Test-Suite Refactor — Phase 6 closing lessons (2026-06-11)

> Phase 6 was the **final** phase of the ITDD contract-test refactor (master plan
> `docs/planning/itdd-test-suite-refactor-plan-2026-06.md`). Unlike Phases 1–5 (which lifted test
> bodies faithfully and *deferred* the implementation bugs they surfaced), Phase 6 **landed** the
> carried fixes — so it changed production code and was owner-gated per change. Branch `Kt2`.

## What Phase 6 did

**(A) Carried impl-fixes (production behaviour changes, owner-approved):**

1. **`EfCoreRepository` not-found dead guard (Phase-5 Session-A triage).** The guard
   `if (result.IsSuccess && result.Value is null)` was unreachable: IndQuestResults' `IsSuccess` is
   `false` on a null value, so a not-found lookup returned a *neither* success-nor-failure result,
   contradicting `IRepository`'s XML doc. Fixed both guards to test **`IsSuccessMayBeNull`**. The two
   `RepositoryContract` not-found tests tightened back from `IsSuccess.ShouldBeFalse()` to
   `IsFailure.ShouldBeTrue()`. The one integration test that asserted the buggy behaviour
   (`...GetByIdAsync_ShouldReturnNull_WhenNotExists` → `IsSuccessMayBeNull`) was updated to `IsFailure`
   and renamed.

2. **CancellationToken honoured across the 6 carried impls + `FileTypeIdentifierService`.** The
   Export.Adaptive (TemplateFieldMapper/TemplateRepository/SchemaEvolutionDetector/AdaptiveExporter) and
   Classification (ExpedienteClasifierService/FusionExpedienteService) impls ignored the token. Owner
   chose the **full-mandate** scope: a guard on **every** public async method + a contract cancel test
   per **Result-returning** method (24 new contract tests), with each reference fake updated to honour
   cancellation so the blueprints stay green.

3. **`FileTypeIdentifierService` converted to a contract instance** (ADR-005 §7 worked example, carried
   Phase-0 Minor): new `FileTypeIdentifierServiceContractTests : FileTypeIdentifierContract` (its
   cancellation test now passes); the redundant Pdf/UnknownExtension impl tests were removed (covered by
   the base via the instance); XML/DOCX signature-detection + null/empty message pins kept impl-side.

**(B) Sweep + guardrails + docs (no behaviour change):**

4. **Architecture guardrail** (`Contract_Bases_Must_Be_Abstract_With_At_Least_One_Inheritor`): every
   `*Contract` in `Testing.Contracts` must be abstract AND have ≥1 inheriting test class. Tests.Architecture
   19 → 20.
5. **Repo-wide unpaired-candidate sweep** (see "Sweep outcome" below).
6. **Docs:** superseded `iitdd-contract-test-tasks.md`; rewrote guidelines §6.5 to the N+1 shape; added an
   explicit mutation-testing exclusion list (EF/DB + ManualReviewerService). Primer was already aligned.

## Sweep outcome (the "no silent caps" record)

- **No orphaned mock-SUT blueprints remain.** Phases 1–5 converted every standalone `I*ContractTests` /
  `II*Tests` class into a contract base + blueprint instance. Grep of `Tests.Domain` confirms none left.
- **`FieldSanitizerContractTests` / `FieldPatternValidatorContractTests` — consciously SKIPPED.** Their
  SUTs (`FieldSanitizer`, `FieldPatternValidator`) are **static utility classes** with no interface and a
  single implementation by definition — the ADR-005 inheritable-base pattern (inject a `Sut`, N+1
  adapters) does not apply. The `…ContractTests` name is descriptive ITDD style, not the ADR-005 shape.
  No conversion warranted.
- **`IFieldExtractor<T>` family — consciously DEFERRED (recommended follow-up "Phase 7").** Strongest
  remaining candidate: 4–5 adapters over **distinct** source types (`XmlFieldExtractor`/`DocxFieldExtractor`/
  `PdfOcrFieldExtractor`/`AdaptiveTxtFieldExtractor`/`AdaptiveDocxFieldExtractorAdapter`). A proper generic
  `FieldExtractorContract<T>` would need per-source fixture hooks (Phase-5 RepositoryContract shape) across
  3 test projects (Extraction / Extraction.Adaptive / Extraction.Txt). Deferred because: (a) each extractor
  already has strong individual **and mutation** coverage (`*FieldExtractorTests` + `*FieldExtractorMutationTests`),
  so the marginal value of a shared base is modest; (b) the interface itself **lacks a CancellationToken**,
  so making cancellation contract-grade there would require a separate interface+impl change first; (c) it is
  a multi-implementation, own-phase effort, not a closing-phase sweep item. **Log, don't silently leave.**

## Lessons specific to Phase 6 (fix-impl-then-test, the inverse of Phases 1–5)

- **`ResultExtensions.Cancelled` lives in `IndQuestResults.Operations`, not `IndQuestResults`.** Files that
  only `using IndQuestResults;` (for `Result<T>`) compile fine until you reference `ResultExtensions` — then
  `CS0103`. Both the 4 Export.Adaptive impls and the 6 mock factories needed the extra `using`. (The
  Classification project's `GlobalUsings.cs` already had it, so those impls didn't.) `IsCancelled()` (the
  contract assertion) is also in `IndQuestResults.Operations` — add it to each contract base.
- **Cancellation mutation cleanliness is automatic IF every guarded Result method has both a success test
  and a cancel test.** A guard `if (token.IsCancellationRequested) return Cancelled;` has three mutants:
  condition→`true` (killed by any existing success test, which now gets Cancelled), condition→`false` and
  block-removal (both killed by the new pre-cancelled-token test). So the rule "guard every method →
  cancel-test every Result method" is exactly what keeps Stryker at Survived-0. Skipping the cancel test on
  any one guarded method leaves its `false`/block mutants alive. Verified empirically: the Export.Adaptive
  scoped re-run had **0 cancellation-guard survivors** (parsed out of the HTML report) — the 47 survivors are
  the unchanged pre-existing Serilog/ConfigureAwait/equivalent floor.
- **Non-Result async methods can't express `Cancelled`.** `TemplateRepository`'s three getters return a bare
  `TemplateDefinition?`/`IReadOnlyList<>`. They were guarded *pragmatically* (return null/empty instead of
  throwing OCE) but have **no contract cancel test** (the contract can only assert `IsCancelled()` on a
  `Result`). Documented in the contract. (TemplateRepository is Stryker-excluded anyway, so no kill-power
  concern.)
- **The reference fake must mirror the pragmatic choice exactly.** When the impl returns null/empty on cancel,
  the fake must too (and `Array.Empty<T>()` needs an explicit `(IReadOnlyList<T>)` cast in the ternary to
  satisfy the `Task.FromResult` target type).
- **A fixed impl invalidates the test that pinned the bug.** Fixing the `EfCoreRepository` guard broke
  `EfCoreRepositoryIntegrationTests`'s not-found test, which had asserted the *buggy* success-may-be-null
  behaviour. Per plan invariant 2, the test is updated to the corrected contract — not the impl reverted.
  Always grep for tests asserting the old behaviour **before** committing an impl fix (here: one integration
  test + the contract's two base tests; the only generic consumer `FileMetadataQueryService` already routed
  not-found to failure, so no production caller broke).
- **`StrykerCompat=true` flattens shared `obj/ref` paths, so a background Stryker run BLOCKS concurrent
  `dotnet build`** of other projects (`CS0006: Metadata file '…/ref/….dll' could not be found`). Don't try to
  build/run other test projects while a scoped Stryker run is in flight in this repo's custom artifacts layout
  — sequence them, or accept that the mutation run owns the build for its duration.
- **Verify "0 cancellation survivors" by parsing the report, not the headline.** The score headline shifts
  run-to-run (timeout flap) and the population differs when you scope `--mutate` to a subset of files; the
  stable signal is "the survived set contains no mutant whose mutated line mentions `IsCancellationRequested`/
  `Cancelled`" — extract the embedded JSON from `mutation-report.html` and filter by line text.

## Result

Phases 0–6 complete. The test suite now has one abstract contract base per converted interface, inherited by
a mock/blueprint instance plus one instance per implementation, with the carried implementation defects
(not-found ROP, cancellation) landed and contract-tested. The `IFieldExtractor<T>` family is the single
logged, deferred follow-up.
