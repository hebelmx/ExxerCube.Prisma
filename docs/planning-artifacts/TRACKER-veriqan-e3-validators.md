# TRACKER — Veriqan E3: Validators + Abstention Discipline (fallback-chain program)

**Branch:** `Liv` · **Started:** 2026-07-08 · **Intended-solution doc:**
`docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md` (§Provenance & honesty vocabulary,
§Field escalation matrix, §Eval/honesty/determinism testing, program-plan row **E3**).

## Ground-truth reconciliation (2026-07-08, verified against code)

E3's design-doc row lists more than the code actually needs, because most matrix fields have **no ladder
rungs** yet (their ladders belong to E4/E5). Verified state:

| Design-doc E3 deliverable | Reality | E3 action |
|---|---|---|
| Confidence→status mapping | ✅ DONE (`FieldResolutionOrchestrator.ToExtractedField`, incl. `ExtractedByInference`) | none |
| PaymentDueDate validator | ✅ static-window validator exists; `TODO(E3)` to make period-relative | **S3.2** |
| Product catalog-validation | ✅ resolution+abstain lives inside `HeaderImageOcrStage`; ladder `Validator` is `null` | **S3.3** (belt-and-suspenders) |
| RFC/CLABE/Card/Tasa/Cat/RESUMEN/checksum validators | ⚠️ would be **inert** — those `FieldKind`s are positional-only (no rung → `ValidatorFailure` gate never fires) | **DEFERRED to E4/E5** (documented, not built) |
| Anti-false-confidence CI suite (NFR-5 guard) | 🟡 pieces exist (disagreement/escalation-seam/canary/ExtractionGap tests); **not** consolidated as the QA-section full-verdict-pipeline honesty gate | **S3.1** (primary) |

**Owner ruling (2026-07-08):** E3 = "Honesty gate + 2 TODOs." Build S3.1 + S3.2 + S3.3; defer the inert
matrix validators.

## Design decision (orchestrator, 2026-07-08)

Both TODOs (S3.2 period window, S3.3 product catalog) need **runtime document/tenant context** the
static `FieldEscalationLadderRegistry` cannot provide. Solution = **one additive seam**: an optional
per-call validator override on `FieldResolutionOrchestrator.ResolveAsync` (symmetric with the existing
`higherStages` override). When supplied it supersedes `ladder.Validator` in both `ShouldEscalate` and
`IsCredible`. `EscalatingStatementFieldExtractor` already resolves `periodStart`/`periodCutDate` **before**
`paymentDueDate` (`:158-160`), so the period window is in scope to build a per-document validator; the
tenant `VecReferenceBundle` catalog is likewise in scope for Product. No field-resolution reordering, no
ripple to positional-only fields.

## Stories

- **S3.2 — per-call validator-override seam + period-relative PaymentDueDate validator.** Add optional
  `IFieldValidator? validatorOverride` to `ResolveAsync`; honor it in `ShouldEscalate`/`IsCredible`.
  Extend `PaymentDueDatePlausibilityValidator` with optional period bounds (due date within a bounded
  window *after* the cut date). Wire the escalating extractor to pass a period-configured instance for the
  PaymentDueDate call only. Behavior stays abstain-safe (stricter, never looser-false).
  DoD: `Veriqan.Infrastructure.Extraction.Tests` green; build 0/0; static-window path unchanged when no
  period available.
- **S3.3 — Product catalog-membership ladder validator (belt-and-suspenders).** New
  `ProductCatalogMembershipValidator` (validates the resolved product string resolves in the tenant
  `VecReferenceBundle` catalog); passed via the S3.2 override for the `FieldKind.Product` resolve call.
  Independent of the stage's internal abstain (defense-in-depth per "validators gate every stage").
  DoD: extraction tests green; no regression to the existing Product/OCR canaries.
- **S3.1 — anti-false-confidence honesty CI suite (primary).** Consolidated, baseline-locked test classes
  running the **full verdict pipeline** over the E6.S6.2 synthetic god's-eye gold: (1) abstention-
  preservation (truth=abstain stays abstain through all stages), (2) verdict-flip guard (an injected wrong
  value into a verdict-gating field never flips PASS↔FAIL), (3) cross-field contamination (a stage for
  field A never returns field B's value). False-confidence rate as a first-class metric that must not
  increase stage-over-stage; zero-ceiling on verdict-gating fields (Tasa/Cat/totals/PaymentDueDate/RESUMEN)
  for demo/synthetic gold rows. DoD: new suite green + wired into the CI gate; build 0/0.

## Sequencing

S3.2 → S3.3 (share the override seam; sequential to avoid registry/orchestrator conflict) → S3.1 (locks in
the final behavior). Adversarial review at the phase boundary before closing E3.

## S3.3 — owner ruling + design (2026-07-08)

Owner ruled **BUILD the bundle threading** (twice, fully informed that the catalog gate already exists
downstream in `BundleBinder`/`ProductResolver` and that threading needs pipeline reordering — accepted as
genuine defense-in-depth: the extraction stage abstains *earlier*, pre-binding). Composition map
(Explore, 2026-07-08): extraction (Stage 2) is process-wide **singleton** and runs **before** bundle
binding (Stage 4); the seam carries only PDF bytes; `submission.ContextKey` (enough to load the bundle) is
available before Stage 2; `IProductResolver.Resolve(token, bundle)→Result<VecProduct>` is the canonical
OCR-token catalog matcher (FuzzySharp, threshold 85, margin 5, abstains→BLOCKED UnknownProduct).

**Design decisions (orchestrator):**
- Reuse `IProductResolver` as the single source of catalog-match truth (extraction accepts exactly what
  `BundleBinder` accepts → E7 COSTCO demo stays green). Flip its DI lifetime **scoped→singleton** (pure
  stateless fn) so the singleton extractor can inject it without a captive dependency.
- **Terminal-validator-abstain rule** in `FieldResolutionOrchestrator`: after the rung loop, if the
  terminal `best` has a value but fails the (override-or-ladder) validator → abstain (`Missing`). Needed
  because the override only gated escalation/disagreement, not the returned value — so this also finally
  activates S3.2's period-relative window end-to-end. Behavior-neutral for all current green suites (no
  field emits a terminal validator-failing value today).
- Thread the per-request bundle via an **optional** `VecReferenceBundle? referenceBundle = null` param on
  `IStatementFieldExtractor.ExtractFullAsync` (optional → all existing test callers compile unchanged).

**Split (de-risk):**
- **S3.3a — extraction-side (behavior-neutral until a catalog is passed).** `ProductCatalogMembershipValidator`
  (reuses `IProductResolver`); orchestrator terminal-abstain rule; optional bundle param on ExtractFullAsync;
  `EscalatingStatementFieldExtractor` builds the catalog validator + passes it as the Product `validatorOverride`
  when a bundle is supplied; DI `IProductResolver`→singleton. Unit-tested with a catalog passed directly. E7
  demo unaffected (pipeline still passes no catalog until 3b). DoD: Extraction.Tests + Orchestration.Tests green.
- **S3.3b — pipeline wiring.** `VerificationPipeline` resolves the bundle early (via ContextKey) and passes
  it to `ExtractFullAsync`; thread the resolved bundle into `BundleBinder` to avoid double-resolution;
  preserve existing BLOCKED ordering/behavior. Verified by the E7 Product-OCR E2E suite (COSTCO still
  resolves → demo verdicts unchanged). DoD: full Orchestration + LiveOcr Product E2E green.

## Log

- 2026-07-08 — E3 opened; ground-truth reconciliation; owner scoped "Honesty gate + 2 TODOs".
- 2026-07-08 — S3.2 DONE + verified (Extraction 314/314, build 0/0) + committed/pushed `cf5da855`.
- 2026-07-08 — S3.3 composition-mapped; owner ruled build-the-threading (informed of redundancy); split
  into S3.3a (extraction, behavior-neutral) + S3.3b (pipeline wiring). Terminal-abstain rule folded in.
- 2026-07-08 — S3.3a DONE + verified (Extraction 329/329, Orchestration 129/129, Application 158/158,
  build 0/0), committed/pushed `8d38d563`. Verified the one modified S3.2 test preserves intent (not a
  weakening). Terminal-validator-abstain rule reviewed clean.
- 2026-07-08 — S3.3b DONE (self-implemented, VerificationPipeline pre-resolves catalog → ExtractFullAsync).
  Caught a 21-test breakage from ground truth (pipeline unit tests mock IVecReferenceDataProvider without
  stubbing GetBundleAsync → null Result → NRE); fixed with ONE defensive line (`is { IsSuccessNotNull: true }`,
  port-robustness) rather than churning 21 tests. Re-verified Orchestration 129/129 INCLUDING
  VecChecklistDemoE2ETests (real COSTCO full-pipeline live-OCR demo) → demo verdicts unchanged. Committed
  /pushed `fb00c4d9`.
- 2026-07-08 — S3.1 started. Gold source = `SyntheticGoldManifest`/`SyntheticGoldManifestLoader` (per-field
  ExpectedStatus incl. abstentions) over `Fixtures/PRP2/synthetic` corpus; synthetic specimens are
  text-layer (positional Product → no native OCR) → honesty suite is deterministic + CI-gateable (NOT
  LiveOcr). Delegated to dev (the `qa` BMAD persona is advisory-only — won't write code).
- 2026-07-08 — S3.1 DONE + verified (Honesty 17/17, full Orchestration 147/147, build 0/0), committed
  /pushed `3f976c47`. Reviewed the verdict-flip test's narrowed assertion — legitimate (a wrong Tasa
  SHOULD fail its own CL-10; test proves NO unrelated check flips + signal doesn't swing), not a gutting.

## Adversarial review (2 skeptics, phase boundary, 2026-07-08)

Two independent reviewers (correctness/inert-code + honesty/over-abstention) attacked the 4 committed E3
commits against the design doc. **Verdict: E3 is substantially complete + correct — no functional bug,
clean build, DI-lifetime safe, honesty suite non-tautological (god's-eye gold, real non-vacuity guards).**
The terminal-abstain rule can only turn fail→Missing, never fabricate a value in. Findings + disposition:

- **F1 (Major) — end-to-end wiring proven by simulation.** S3.2 period-window + S3.3 extraction-stage
  Product abort were proven by a unit test that hand-builds the validator, not one driving the real
  `EscalatingStatementFieldExtractor` wiring. → **#1(b) Product already covered** by
  `EscalatingExtractorProductCatalogGateTests` (reviewer missed it). **#1(a) PaymentDueDate → FIXED** by
  new `EscalatingExtractorPaymentDueDateWindowTests` (this closure pass).
- **F2 (Minor) — Stage 1b missing cancellation check** (CLAUDE.md pattern). → **FIXED** (explicit
  `IsCancelled()` after the catalog pre-resolve).
- **F3 (Minor–Major) — PaymentDueDate excluded from the S3.1 false-confidence metric** (one of the 4
  design-named gating fields; no synthetic manifest tracks its gold). → **DEFERRED** (owner/E6): needs a
  synthetic fixture carrying PaymentDueDate/PeriodCutDate gold. Partially compensated by F1(a)'s new
  direct end-to-end abstain test. Logged here per "no silent deferral."
- **A1/A2 (Low–Mod, NEW, verdict-safe) — poisoned-cut-date false-abstention.** A *misread* cut date
  (status Extracted, wrong value) builds a garbage [cut,cut+60] window that can abstain a correct
  positional PaymentDueDate. Reviewer confirmed it CANNOT flip a verdict (PaymentDueDate gates no
  arithmetic; typography consumers degrade to InsufficientData). Worst case = marginal extraction-coverage-
  floor tip to ExtractionGap; also CI-invisible (see F3). → **ACCEPTED/LOGGED** (bounded, verdict-safe).
  Revisit if PaymentDueDate ever becomes arithmetic-gating.
- **B1 (Mod, conditional, amplified by E3) — Product abstain → null → context-key fallback.** A nulled
  (catalog-abstained) Product lets `VerificationPipeline` productToken fall through to
  `ContextKey.ProductId`; if a caller supplied a valid product *hint*, the verdict runs on the hint
  product the document never confirmed. Bounded: ProductId null by default → productToken "" → BLOCK
  (honest); does NOT affect the demo (COSTCO resolves). → **LOGGED for owner** — pre-existing fallback
  semantics amplified by E3; a guard ("document-unconfirmed product must not silently adopt the hint")
  is an owner decision, out of E3 scope.
- **B2 (HIGH residual, PRE-EXISTING, out of E3 scope) — the 16 money/rate verdict-gating fields
  (Tasa/Cat/RESUMEN/NIVEL totals) have NO validator and NO ladder**, so a misread digit still reaches the
  arithmetic rules → a false RED, the exact cardinal-rule violation. E3 correctly did NOT add inert
  validators to ladder-less fields (the reconciliation above). → **THIS IS THE NEXT TARGET (E4/E5)** —
  give those fields ladders + validators/checksums so abstention discipline reaches them. The single most
  important honesty lever remaining.

## E3 status: COMPLETE (pending F1(a) test verify + close-out commit)

DoD met: 5-verdict-bar green suites (Extraction, Orchestration incl. live COSTCO demo, Application), build
0/0, honesty CI suite in place, all owner-scoped stories delivered + adversarially reviewed. Deferred
items (F3, A1, B1) logged above; B2 handed to E4/E5.
