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
