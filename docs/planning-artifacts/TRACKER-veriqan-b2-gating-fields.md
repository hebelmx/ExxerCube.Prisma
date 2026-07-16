# TRACKER — Veriqan B2: money/rate verdict-gating field ladders + validators

**Branch:** `Liv` · **Started:** 2026-07-08 · **Intended-solution doc:**
`docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md` (§Field escalation matrix rows
Tasa/Cat/RESUMEN; §Provenance & honesty vocabulary). Follows E3 (validators + abstention discipline),
which logged **B2 as the HIGH residual next target** (`TRACKER-veriqan-e3-validators.md` finding B2).

## Ground-truth map (Explore, 2026-07-08 — verified against code)

Root: `Prisma/Code/Src/CSharp`.

- **All named fields are real distinct `FieldKind`s** (`01 Core/Veriqan.Domain/Extraction/FieldKind.cs`):
  `Tasa` (70), `Cat` (73); RESUMEN = `AdeudoPeriodoAnterior, CargosRegularesNoMeses,
  CargosComprasAMesesCapital, MontoIntereses, MontoComisiones, IvaInteresesYComisiones, PagosYAbonos`;
  NIVEL = `SaldoCargosRegulares, SaldoCargosAMeses`; DESGLOSE = `TotalCargos, TotalAbonos`.
- **All of them sit on `FieldEscalationLadder.PositionalOnly` with a null validator** — only `PaymentDueDate`
  and `Product` have real rungs (`02 Infrastructure/…Extraction/Resolution/FieldEscalationLadderRegistry.cs`
  `BuildDefaultLadders()` 68–98). Adding rungs/validators is a **registration-table change** + new
  `IFieldValidator`/stage classes — **no orchestrator surgery**.
- **Orchestrator seams already exist** (`…/Resolution/FieldResolutionOrchestrator.cs`): per-call
  `validatorOverride`, `higherStages`, `ShouldEscalate` (StatusGate|ConfidenceFloor|**ValidatorFailure**|
  **Disagreement**), and the **terminal-validator-abstain rule** (171–182: value-but-fails-validator →
  `Missing`) + the **disagreement gate** (184–189 → `Missing`). Both downgrade to `Missing` (honest),
  never fabricate a value.
- **`IFieldValidator`** = `bool IsValid(object? value)` (`…/Resolution/IFieldValidator.cs`). Two impls today
  (PaymentDueDate window, Product catalog). **None for Tasa/Cat/RESUMEN.**
- **Positional extraction** (`…/Extraction/PdfPigStatementFieldExtractor.cs`): `ExtractTasaAndCat` (1207–1293,
  geometric: CAT = first `%` token, TASA = second — **swappable/misreadable, no cross-check**);
  `ExtractResumenField` (1381–1423 — already emits the honest `Extracted`/`ExtractedInvalidFormat`/`Missing`
  distinction). Values live on `PeriodSummary` as `ExtractedField<decimal>` (Tasa/Cat stored as **decimal
  fractions**, e.g. 0.2736 not 27.36).
- **Confidence mechanics** (`…/Extraction/ExtractedField.cs` 82–96): `Found` hardcodes **confidence 1.0 /
  Status=Extracted**; `InvalidFormat` → 0.7 / `ExtractedInvalidFormat`; `Missing` → 0.0 / `NotExtracted`.
  **No per-digit confidence** → a parseable misread carries 1.0.
- **Arithmetic rules already abstain on Missing/low-confidence** (`02 Infrastructure/…Validation/Rules/`):
  CL-21 (RESUMEN operands, implied-zero design), CL-22, CL-24, CL-44 (DESGLOSE vs movement sums), CL-10
  (Cat **and** Tasa → CAT formula), §19 (Ordinarios-row rate cross-check vs `PeriodSummary.Tasa`; silently
  skips if Tasa absent/low-conf). Confidence guard threshold = **0.8** (`TenantProfile` /
  `ConfidenceGuard.BelowThreshold`). **A Missing gating field → InsufficientData, not RED — confirmed.**

### The real defect (map §6 flag 1)

The confidence guard is **inert for these fields**: positional `Found` = confidence 1.0, so nothing to gate
on. A **parseable misread** → Status=Extracted, conf 1.0 → straight into arithmetic → **false RED**. The
only misreads that abstain today are ones the parser rejects (`ExtractedInvalidFormat`) or drops (`Missing`).

### Stage-provenance caveat (map §6 flag 2 — load-bearing for rung choice)

`FuzzyLabel` / `Levenshtein` recovery → Status=**`Extracted`** → **feeds the rules**. But
`SemanticSearch` / `LlmExtraction` / `HeaderImageOcr` recovery → **`ExtractedByInference`** → every
arithmetic rule treats it as not-extracted → **abstains**. So a recovery rung meant to *feed* a gating
arithmetic check MUST be Fuzzy/Levenshtein-class; semantic/LLM rungs can only ever *abstain*, not repair.

### `StageId.Levenshtein` reserved but unimplemented

`StageId.Levenshtein` exists (`Domain/Extraction/StageId.cs:31`) with **no `IFieldResolutionStage` wired**.
Levenshtein primitive ready: `Domain/Extraction/VecTextMatcher.cs` (`NormalizedLevenshteinRatio`, `Matches`).
FuzzySharp referenced in Extraction + Application. Existing `FuzzyLabelStage` + `LabelWindowMatcher` are the
template for a new stage.

## Orchestrator engineering-correctness ruling (2026-07-08)

**Gating-field validators MUST be plausibility- or redundancy-based, NEVER "abstain iff the compliance
identity fails."** The design doc's "arithmetic identity = strongest validator" line, applied literally to
RESUMEN/totals, would make a validator abstain exactly when CL-21/CL-44 should fire RED → it neuters the
compliance rule (false GREEN — the opposite cardinal-rule violation). Safe levers only:
1. **Range/sign/magnitude plausibility** (orthogonal to the arithmetic): Tasa∈[0,200]%, Cat plausible &
   ≥ Tasa co-location, RESUMEN non-negative / digit-count sanity (catches decimal-point drops, sign flips).
2. **Independent-source redundancy cross-check** (a *second rendering* of the same value, not the
   downstream identity): Tasa header-vs-footer-reflow vs §19 Ordinarios rate; disagreement → abstain.
   Requires a spike proving the second source is actually recoverable text in the corpus (cf. E2.3 which
   was REFUTED when the "second source" turned out not to be extractable text).

## Stories (draft — scope pending owner ruling below)

- **S-B2.1 — Tasa redundancy cross-check + plausibility validator.** Spike first: confirm a second Tasa
  rendering (footer reflow and/or §19 Ordinarios rate) is recoverable text in the demo + synthetic corpus.
  If yes → `TasaPlausibilityValidator` (fraction ∈ [0, 2.0]) on the ladder + a Levenshtein/Fuzzy recovery
  rung that re-reads Tasa and lets the existing Disagreement gate abstain on mismatch (Status=Extracted →
  feeds CL-10/§19). If the spike REFUTES a recoverable second source → ship plausibility validator only +
  log the refutation (no speculative rung). Abstain-safe throughout.
- **S-B2.2 — Cat plausibility + homonym-disambiguation validator.** `CatPlausibilityValidator`
  (Cat plausible range; guard against the `CAT:` phone-number homonym collision; co-location with Tasa).
  Recovery rung only if a spike shows a recoverable second Cat source.
- **S-B2.3 — RESUMEN/NIVEL/DESGLOSE plausibility validators (cheap wins, scope-gated).** Per-field
  non-negativity / magnitude-sanity validators that catch *gross* misreads (decimal drops, sign flips)
  WITHOUT touching the arithmetic identity. Explicitly NOT identity-abstain. Marginal value (small
  in-range misreads still pass) — include only if owner wants the full sweep.

## Scope decision (owner) — 2026-07-16

**Owner ruling: FULL 16-FIELD SWEEP.** All three stories in play: S-B2.1 (Tasa) + S-B2.2 (Cat) +
S-B2.3 (RESUMEN/NIVEL/DESGLOSE plausibility validators). Recovery rungs remain spike-gated per story.

## Make-or-break wiring spike (S-B2.0 — MUST pass before any validator ships)

The terminal-validator-abstain rule lives in `FieldResolutionOrchestrator.ResolveAsync` (178-182). A
ladder validator only gates the returned value **if Tasa/Cat/RESUMEN actually flow through that
orchestrator** on the path that feeds the compliance rules. E3's adversarial review found a
"sim-not-real-wiring" gap once already. **S-B2.0 must prove the real call path**: does the value that
reaches CL-10/CL-21/§19 come through `FieldResolutionOrchestrator`/`EscalatingStatementFieldExtractor`,
or does `PdfPigStatementFieldExtractor` write straight onto `PeriodSummary`, bypassing the ladder? If it
bypasses, adding a ladder validator is theater and the abstain must be applied where the value is
actually produced. Verdict + real repair point recorded before S-B2.1 starts.

## S-B2.0 spike VERDICT (2026-07-16, Explore, code-cited) — DONE

**Q1 — Tasa/Cat/RESUMEN DO flow through `FieldResolutionOrchestrator.ResolveAsync`.**
`VeriqanExtractionExtensions.cs:80-84` registers the production `IStatementFieldExtractor` as
`EscalatingStatementFieldExtractor` (wraps `PdfPigStatementFieldExtractor` as `_inner`); the only
production consumer is `VerificationPipeline.cs:215` → its `PeriodSummary` is what every rule reads
(§19 at `Section19InterestPerRowRule.cs:323`). The decorator routes ALL 16 fields through
`ResolveAsync` (`EscalatingStatementFieldExtractor.cs:198-212`), not just PaymentDueDate/Product.
**THE TRAP:** the terminal-abstain (line 178) is only reached when `ladder.Rungs.Count > 0`. Every
target field is `PositionalOnly` (0 rungs) → short-circuits at `FieldResolutionOrchestrator.cs:102-108`
and returns the positional value **UNCHECKED**. A validator alone on the ladder is theater.

**Q2 — NO recoverable second text source for Tasa OR Cat.** §19 "Ordinarios" rate is `NA`/absent in
every fixture (Dummie VEC, real Banamex, s6211); each rate + CAT appears exactly once (header only).
Direct rerun of the E2.3 refutation. → **No recovery rungs. Plausibility validators only.**

**DESIGN DECISION (orchestrator, 2026-07-16): Option B, not a phantom rung.** Registration-only would
force a fake `StatusGate` rung on an unregistered `StageId` (warning-log noise, dishonest semantics).
Instead: a ~6-line, inert-by-construction guard in the `Rungs.Count == 0` short-circuit
(`FieldResolutionOrchestrator.cs:102-108`) that honors a registered validator (or per-call override) —
an implausible positional value is downgraded to `Missing` there, mirroring the terminal-abstain rule.
Inert for every existing field (all empty-ladder fields carry `null` validator today) → behavior-neutral.
Then Tasa/Cat/RESUMEN ladders stay honestly `PositionalOnly` **+ a validator**, no fake rungs.

## Status (tracker tasks mirrored in TaskCreate)

- [x] **S-B2.0** wiring spike — DONE (verdict above)
- [ ] **S-B2.0.5** orchestrator: positional short-circuit honors a registered validator (Option B, inert)
- [ ] **S-B2.1** `TasaPlausibilityValidator` (fraction ∈ [0, 2.0]) + Tasa ladder (validator, empty rungs). No rung (Q2 refuted).
- [ ] **S-B2.2** `CatPlausibilityValidator` (fraction band; phone-homonym caught by upper bound) + Cat ladder. No rung.
- [ ] **S-B2.3** RESUMEN/NIVEL/DESGLOSE magnitude-sanity validators (11 money fields), NOT identity-abstain, sign only where definitionally non-negative.

## Adversarial review (2026-07-16, 3 skeptics, commit b2ab849a)

- **Lens A (false-GREEN / cardinal-rule): 1 HIGH finding — F1.** Two lenses SURVIVED; this one
  refuted. See F1 below.
- **Lens B (real-wiring + correctness): SURVIVES.** `ExtractedField.Provenance` is non-null by
  constructor invariant (no NRE on the abstain path). The guard's fresh `Missing` instance flips
  `EscalatingStatementFieldExtractor`'s `!ReferenceEquals` → `anyEscalated` → the downgrade IS
  rebuilt into `PeriodSummary` and reaches the rules (NOT theater). Change confined to the empty-rung
  branch; terminal-abstain/escalating path untouched.
- **Lens C (false-abstain / bounds / units): SURVIVES.** Tasa/Cat provably stored as FRACTIONS
  (`ParsePercent` → `pct/100m`, `PdfPigStatementFieldExtractor.cs:2211`). Corpus: Tasa 0.183–0.289,
  Cat 0.226–0.305, money ≤ $67,796 — bands sit ~2–10× above worst real-world card values (highest
  documented MX classic-card CAT ≈ 105.9%; extreme rate ≈ 151%). No false-abstain vector.

### F1 (HIGH, CONFIRMED) — CL-21 implied-zero defeats the Missing downgrade

The B2 guard downgraded to `Missing`/`NotExtracted`. CL-21 (`Cl21PagoParaNoGenerarInteresesRule.cs:136-157`)
treats `NotExtracted` on `AdeudoPeriodoAnterior`/`PagosYAbonos` as an IMPLIED ZERO (Banamex zero-row
suppression), NOT abstain — only `ExtractedInvalidFormat` abstains there. So a gross misread of either
field → `Missing` → CL-21 computes on a fabricated `0` → false GREEN, reopening the dishonest-implied-zero
hole the Epic-5 F1 honesty fix closed. **Fix: guard downgrades to `ExtractedInvalidFormat` (present-but-
unusable), not `Missing` (never-on-statement).** Strictly correct for all 13 armed fields — every other
consumer treats both non-Extracted statuses as `InsufficientData`, only the two implied-zero operands
change (wrong implied-zero → honest abstain). Delegated to dev.

## Log

- 2026-07-08 — B2 opened; ground-truth map (Explore) captured; engineering-correctness ruling recorded
  (plausibility/redundancy, never identity-abstain). Scope question to owner pending.
- 2026-07-16 — Owner ruled FULL 16-FIELD SWEEP. Orchestrator (this session) verified validator seams
  against code (IFieldValidator, PaymentDueDate/Product validators, ladder registry, terminal-abstain
  rule). Added S-B2.0 make-or-break wiring spike as a hard gate before any validator ships.
- 2026-07-16 — S-B2.0 spike DONE (verdict above). Implementation (S-B2.0.5 guard + S-B2.1/2/3
  validators) landed `b2ab849a`; ground-truth verified: Extraction build 0/0, Extraction.Tests 366,
  Validation 531, Orchestration 147, Application 158 — all green. Adversarial review: 2 lenses SURVIVE,
  F1 (HIGH) confirmed + delegated. Fix = downgrade to ExtractedInvalidFormat not Missing.
