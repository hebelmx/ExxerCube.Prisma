# HANDOFF — ITDD Test-Suite Refactor: Phase 6 (Sweep, guardrails, docs + carried impl-fixes)

> **For the next agent.** Branch **Kt2** (tip `983caaa`, pushed). **Phases 0–5 are DONE**,
> all gates GO / GO-WITH-CONDITIONS. Phase 6 is the **final** phase: it closes the carried
> conditions (the contract bases that were faithfully built around real implementation bugs),
> does the repo-wide unpaired-candidate sweep, adds the architecture guardrail, and updates the
> governing docs. Unlike Phases 1–5 this phase **changes production code** (cancellation +
> the IRepository not-found defect), so it is **gated by the owner per change**, not a silent lift.

## Prompt

Execute **Phase 6** of the ITDD test-suite refactor on branch **Kt2**. This phase has two
distinct kinds of work — keep them in separate commits and do not blur them:

**(A) Carried impl-fixes that unblock contract-grade cancellation/Result tests.** Several
contract bases were *faithfully* built around implementations that violate the repo-wide
mandates; the fix is to correct the implementation, THEN tighten/add the base test (plan §6.2:
the blueprint is the spec; fix the impl, don't weaken the contract). Each is a real behaviour
change — **confirm with the owner before changing production**, and check callers.

1. **`IRepository` latent not-found defect (Phase 5 Session A triage).** `EfCoreRepository.GetByIdAsync`
   and `FirstOrDefaultAsync` return a result that is *neither* success nor failure on not-found
   (`IsSuccess=false, IsFailure=false, Value=null`) because IndQuestResults' `IsSuccess` is false on a
   null value, making the guard `if (result.IsSuccess && result.Value is null) return WithFailure(...)`
   **dead code** (`02 Infrastructure/Infrastructure.Database/Repositories/EfCoreRepository.cs:55-59,212-216`).
   The interface XML doc promises a *failure* on not-found. Fix the guard (e.g. use `IsSuccessMayBeNull`
   / check the value directly, not `IsSuccess`), then tighten the two base tests in
   `RepositoryContract` (`09 Testing/.../Contracts/RepositoryContract.cs`) from `IsSuccess.ShouldBeFalse()`
   back to `IsFailure.ShouldBeTrue()`. **Caller check first:** `GetByIdAsync`/`FirstOrDefaultAsync` are
   consumed via the generic `IRepository<T,TId>` (e.g. `FileMetadataQueryService`); confirm none rely on
   the current "neither" behaviour. The repo is Stryker-excluded, so no kill-power concern.

2. **Cancellation across the Export.Adaptive ×4 + Classification ×2 contracts (carried from Phase 2 & 4
   gates).** `TemplateFieldMapper`, `TemplateRepository`, `SchemaEvolutionDetector`, `AdaptiveExporter`,
   `ExpedienteClasifierService`, `FusionExpedienteService` ignore the `CancellationToken` (no
   `IsCancellationRequested` / `Cancelled`), violating the CancellationToken mandate (CLAUDE.md). Add the
   `IsCancellationRequested` early-return to each impl, then add **one** pre-cancelled-token test to each
   of the 6 contract bases (assert `IsCancelled()` or `IsFailure`, matching how the impl signals cancel).
   These are mutation-hardened units (Export.Adaptive, Classification) → **scoped Stryker re-run after**
   (Survived 0 / NoCoverage == floor is the pass signal). The `FileTypeIdentifierService` worked-example
   case (ADR-005 §7) is the same shape — convert it and let its cancellation test pass once the impl honours
   the token; also promote-or-justify its XML/DOCX content tests (Phase 0 carried Minor).

**(B) Sweep + guardrails + docs (no production behaviour change).**

3. **Architecture guardrail.** Add a `Tests.Architecture` rule: every class named `*Contract` in
   `Testing.Contracts` must be `abstract` and have **≥1 inheritor** in a test assembly. (Optional-earlier
   per ADR-005 Consequences; do it now.) Re-run `Tests.Architecture` → expect 20/20 (one new rule).

4. **Repo-wide unpaired-candidate sweep.** Find multi-adapter ports that deserve a contract base but have
   none: the **`IFieldExtractor<T>` family** (Txt/Xml/Docx/Pdf extractors — strongest candidate),
   `FieldPatternValidator`/`FieldSanitizer` (real-SUT, likely just naming), and any remaining standalone
   mock-SUT `I*ContractTests` classes (grep `Tests.Domain\Domain\Interfaces`). Convert what's clearly
   worth it; **log what you consciously skip** (don't silently leave candidates unpaired).

5. **Supersede & update docs.** Point `docs/qa/test-plans/iitdd-contract-test-tasks.md` at ADR-005 (its
   standalone-`II{Name}Tests` shape is superseded — mock-first design step is retained). Update
   `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §6.5 + the ITDD primer to the N+1 / injected-Sut-vs-CreateSut()
   shape. Add `ManualReviewerService` (and the EF/DB repositories) to the explicit Stryker-exclusion list in
   `docs/qa/test-plans/mutation-testing.md` (currently only in code comments — Phase 5 gate Minor).

6. **Full-solution test run** (Docker + Playwright capable box) — the first since the refactor began; record
   the green numbers. Then flip plan §5 tracker row 6 to ✅ and write the refactor's closing lessons-learned.

**Gate:** `/itdd-adversarial-review phase-6` after the work lands. Reproduce every Blocker/Major from cited
evidence before accepting (Phases 1–5 each had headline findings that died on reproduction — see those
tracker rows). Record the verdict in §5 + auto-memory; commit + `git push origin Kt2` per logical chunk.

**Read first:** master plan `docs/planning/itdd-test-suite-refactor-plan-2026-06.md` (§4 Phase 6, §4.1
**all** addenda incl. Phase 5 A+B, §5 tracker row 5 for the carried-condition detail, §6 invariants);
ADR-005 §5–§7; auto-memory `itdd-test-refactor-plan.md` (running state + per-phase lessons).

## State snapshot (2026-06-11, after Phase 5)

| Item | Value |
|------|-------|
| Branch / tip | Kt2 @ `983caaa` (pushed) |
| Phases done | 0,1,2,3,4,5 — all gates GO / GO-WITH-CONDITIONS |
| Solution build | 0/0 at last check; **re-verify at session start** |
| Tests.Architecture | 19/19 (becomes 20/20 with the new guardrail) |
| Contract bases shipped | FileTypeIdentifier, FieldMergeStrategy, TemplateFieldMapper, TemplateRepository, SchemaEvolutionDetector, AdaptiveExporter, AdaptiveDocxStrategy(×5 inheritors), AdaptiveDocxExtractor, ExpedienteClasifier, FusionExpediente, **Repository<T,TId>**, **PersonIdentityResolver**, **ManualReviewerPanel** |
| Counts (post-Phase-5) | Tests.Domain 187 · Tests.Domain.Interfaces 227 · Classification 418 · Tests.Infrastructure.Database 139 |

## Carried conditions ledger (close these in Phase 6)

| # | Source | Item | Where |
|---|--------|------|-------|
| C-Repo | P5 Session A triage | Fix `EfCoreRepository` not-found dead guard → tighten 2 base tests to `IsFailure` | `EfCoreRepository.cs`, `RepositoryContract.cs` |
| C-Cancel | P2 + P4 gates | Honor `CancellationToken` in the 4 Export.Adaptive + 2 Classification impls → add 1 base cancel test each + scoped Stryker | Export.Adaptive / Classification impls + their `*Contract` bases |
| C-FTI | P0 minor | Convert production `FileTypeIdentifierService` to inherit `FileTypeIdentifierContract` (cancel test passes once impl honours token); promote-or-justify its XML/DOCX content tests | Infrastructure.Extraction |
| C-Guard | ADR-005 | Arch rule: `*Contract` in Testing.Contracts ⇒ abstract + ≥1 inheritor | Tests.Architecture |
| C-Sweep | P2 §2 note | Unpaired candidates: `IFieldExtractor<T>` family, `FieldPatternValidator`/`FieldSanitizer`, remaining mock-SUT classes | repo-wide |
| C-Docs | §4 Phase 6 | Supersede `iitdd-contract-test-tasks.md`; update guidelines §6.5 + primer; add EF/DB + ManualReviewerService to mutation-testing.md exclusion list | docs |
| C-Min | P1/P4 minors | Single-source `SourceCount` assertion (FieldMergeStrategy); optionally tighten blueprint `MinHighClassificationConfidence` override; optional explicit IndQuestResults PackageReference in Testing.Contracts | low priority |

## Recipe reminders specific to Phase 6

- **Fix-impl-then-test is the inverse of Phases 1–5.** Those lifted faithfully and deferred impl bugs; here
  you *land* the deferred fixes. Each production change is owner-gated and caller-checked — a cancellation
  early-return or a not-found guard fix can change downstream control flow.
- **Mutation discipline still applies.** Export.Adaptive + Classification are mutation-complete; after adding
  a cancellation test + an impl early-return, re-run scoped Stryker and confirm Survived 0 / NoCoverage ==
  floor. EF/DB repositories + ManualReviewerService are excluded — no Stryker, body edits are safe.
- **Don't silently leave the sweep half-done.** If you pair `IFieldExtractor<T>` but skip
  `FieldPatternValidator`, say so in the commit + tracker with the reason (the "no silent caps" rule).
