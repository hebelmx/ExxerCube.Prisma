# SCOPING — Veriqan C1: geometric extraction-confidence for money/rate fields

**Status:** SCOPING (owner decision pending — not yet an approved epic) · **Branch:** `Liv` · **Date:** 2026-07-16
**Follows:** B2 (`docs/planning-artifacts/TRACKER-veriqan-b2-gating-fields.md`), whose residual it addresses.
**Ground truth:** framing spike 2026-07-16 (read-only, code-cited); confirmed against B2's verified seams.

---

## TL;DR — the reframe (read this first)

The B2 tracker logged the next lever as *"per-digit / positional confidence"* and called it *"a separate,
larger lever."* **The spike refuted the framing.** All the target fields — Tasa, Cat, and the 11
RESUMEN/NIVEL/DESGLOSE money fields — are read from the PDF **text layer** (PdfPig `Word` tokens, exact
digital glyphs), NOT from OCR. On a text layer a digit **cannot be garbled** — the glyph *is* the
character. So:

- **"Per-digit confidence" is the wrong model.** There are no uncertain digits to score. Real per-char
  confidence exists only where a field is rasterized+OCR'd, and **none of the money/rate fields are**
  (the only OCR path, E7's `TesseractHeaderProductOcrEngine`, serves `Product` alone).
- **The real misread risk is GEOMETRIC:** the extractor latching the **wrong token / wrong column / wrong
  row** — most concretely the documented `ExtractTasaAndCat` **CAT-vs-TASA `%`-token swap** (CAT = first
  `%` token, TASA = second, no cross-check). B2's magnitude validators can't see this — a swapped-but-
  in-range value passes at confidence `1.0`.
- **The honest lever is a geometric/positional-plausibility confidence** emitted by the positional
  extractor, consumed by the **already-wired** 0.8 confidence guard. It is **small-to-medium**, not large:
  no OCR plumbing, no re-extraction, **no rule changes**.

---

## Problem statement & the failure class this closes

B2 shipped plausibility validators that abstain on **gross** misreads (a 12-digit garbage token, a
decimal-point drop out of the rate band). They cannot catch a **small, in-range** misread — and the
spike shows the dominant small-misread class here is **geometric mis-selection**, not digit noise:

1. **CAT/TASA swap** (`PdfPigStatementFieldExtractor.cs:1265-1287`): two `%` tokens in the header band,
   picked by X-order with no disambiguation. A swap yields two plausible-but-transposed rates, both in
   band, both confidence `1.0` → feeds CL-10 (CAT formula) and §19 with the wrong operands → false verdict.
2. **Wrong-column / wrong-row amount picks** (`ScanResumenColumn` ~`:1438-1512`): a RESUMEN/DESGLOSE
   label matched to an adjacent amount in the wrong column band or the wrong row → a real but misassigned
   number, in range, confidence `1.0` → feeds CL-21/CL-44 identity checks.

Today these produce a **confident false verdict**. The lever converts an *ambiguous/low-plausibility*
geometric pick into `InsufficientData` (honest abstain), exactly like B2 did for gross misreads — closing
the residual B2 explicitly logged.

---

## Ground-truth map (spike, code-cited)

- **Confidence is `1.0` everywhere for positional reads.** `ExtractedField.Found` hardcodes `1.0`
  (`01 Core/…/ExtractedField.cs:82`); `InvalidFormat` 0.7; `Missing` 0.0. Every money/rate value flows
  through `.Found(...)`. The resolution layer preserves it unchanged (`PositionalStage` → `candidate.Score`
  → `ToExtractedField` `FieldResolutionOrchestrator.cs:317-343`); empty-rung ladders return it verbatim.
- **Only two non-constant confidences exist, both irrelevant here:** `FuzzyLabelStage`/`LabelWindowMatcher.cs:144`
  (a *label*-match ratio, PaymentDueDate only) and `HeaderImageOcrStage` (a **constant** 0.9, Product only).
  → **greenfield** for money/rate; nothing positional to build on.
- **The geometric signals already exist but are discarded.** `ExtractTasaAndCat`/`ScanResumenColumn`
  already hold, per token: `Word.BoundingBox` (X/Y, column band, label→value distance), the count of
  competing `%`/amount tokens, and adjacency of disambiguating siblings (`sin IVA`, `$`, the label). They
  are used transiently to *pick* a token, then thrown away — never retained as a quality signal.
- **Consumption is uniformly wired, no gaps.** 15 rules apply `ctx.ConfidenceBelowThreshold(field, 0.8)`
  incl. **CL-10 (CAT *and* TASA), CL-44 (totals), CL-21, §6/8/16/19/20**. A genuine sub-0.8 confidence on
  any armed field **would** gate to `InsufficientData` — **no rule change needed**. (Story 9.5 built the
  consumption side; the production side was never built — this epic is that missing half.)
- **Tesseract's real per-word confidence is discarded** (`TesseractHeaderProductOcrEngine.cs:124-125` calls
  only `GetText()`, then stamps a flat 0.9). This is a genuine improvement but **Product-only and
  orthogonal** — do NOT bundle it into this money/rate epic.

---

## The lever's true shape — a geometric-plausibility score

Add a confidence-carrying factory to `ExtractedField<T>` (the type's ctor already accepts any `[0,1]`), and
have the positional extractor compute, per field, a score from signals already in hand:

- **Token competition / isolation** — how many candidate tokens competed for this slot (2 `%` tokens ⇒
  swap-prone ⇒ lower); is there a unique, unambiguous match?
- **Disambiguating siblings** — presence of an expected neighbour (`sin IVA` for CAT, the field label
  within an expected gap, a `$`/`%` marker) raises confidence; absence lowers it.
- **Column / label alignment** — X-band alignment against the field's calibrated column and the label→value
  gap vs. an expected range (a value grabbed from the wrong column or an over-long jump ⇒ lower).
- *(Optional, corpus-gated)* **cross-field agreement** — §19 Ordinarios per-row rate vs `PeriodSummary.Tasa`
  — but S-B2.0 Q2 **refuted** that this is recoverable text in the current corpus; include only if a future
  corpus renders it.

Default/clean-pick score must stay **≥ 0.8** so unambiguous reads keep producing verdicts.

---

## THE cardinal risk (design must lead with this)

The 0.8 guard is a **hard floor**. If the geometric score under-rates clean picks, this epic **mass-converts
working GREEN/RED verdicts into false `InsufficientData`** — the "false-abstain" failure mode B2's Lens C
hunted, now systemic. Mitigation is non-negotiable and is itself a gate:

- **Calibrate against the synthetic god's-eye corpus** (`synthetic/corpus-manifest.json`, from the E6.S6.2
  generator) BEFORE arming: every clean specimen must score ≥ 0.8 on every field; only genuinely ambiguous
  specimens (a deliberately-generated two-`%`-token band, a wrong-column decoy) may dip below.
- **Ship DARK first** — emit the confidence but keep the guard reading it behind a tenant/config flag until
  the calibration suite proves no regression on the demo verdicts (Extraction 366 / Validation 532 /
  Orchestration 147 / Application 158 must stay green, and the 5 live-demo verdicts must not flip).
- This mirrors the B2 discipline: a validator/score that abstains must never neuter a working verdict.

---

## Options (owner appetite)

| Option | Scope | Value | Cost/Risk |
|---|---|---|---|
| **A — Tasa/Cat slice** (recommended first) | `ExtractedField` confidence factory + geometric score in `ExtractTasaAndCat` only; calibrate; arm behind flag | Closes the highest-value, best-documented misread (CAT/TASA swap → CL-10/§19) | Small; one method + one factory; calibration gate |
| **B — + RESUMEN/DESGLOSE** | Extend the same factory to `ScanResumenColumn` (column-alignment/label-distance signals) for the 11 money fields → CL-21/CL-44 | Broader coverage of identity-check operands | Medium; more calibration surface |
| **C — full + generator upgrade** | A+B plus adding ambiguous-geometry specimens to the synthetic generator as the calibration harness | Durable regression harness for confidence | Medium+; touches the E6.S6.2 generator |
| **✗ NOT this** | per-char OCR confidence / re-extraction plumbing | — | Mis-scoped: fields are text-layer, no digits to score |

**Recommended:** **A first**, as a spike-gated, ship-dark increment; then B; C only if the team wants a
durable calibration harness. The Tesseract-mean-confidence capture is a separate Product-only ticket.

---

## Proposed story breakdown (if owner green-lights A→B)

- **C1.0 (spike/calibration-first, GATES all):** on the synthetic corpus, measure what a candidate geometric
  score would assign to every clean Tasa/Cat pick. Prove a scoring function exists where clean picks ≥ 0.8
  and the injected CAT/TASA-swap specimen scores < 0.8. If no such separation exists on the corpus →
  STOP/redesign (don't arm a score that can't separate). *(Make-or-break, like S-B2.0.)*
- **C1.1:** `ExtractedField<T>.Found(value, locator, confidence)` overload (behavior-neutral; default 1.0).
- **C1.2:** compute + emit the geometric score in `ExtractTasaAndCat`; ship DARK (guard reads it only behind
  a flag). Unit + corpus calibration tests.
- **C1.3:** flip the flag on for Tasa/Cat after the full verdict-suite regression + 5-demo-verdict check.
- **C1.4 (Option B):** extend to `ScanResumenColumn` for the 11 money fields, same calibrate-then-arm cycle.
- **Adversarial review** each increment: hunt false-abstain (Lens C) and any verdict that flips the wrong way.

## Owner decisions (2026-07-16) — RULED

1. **Appetite: A → B → C (full + harness).** Tasa/Cat + the 11 RESUMEN/NIVEL/DESGLOSE fields + the
   E6.S6.2 synthetic-generator upgrade to emit ambiguous-geometry calibration specimens. This is now a
   **medium epic**, not a slice.
2. **Dark-first + calibration gate** — the cardinal-risk mitigation stands (non-negotiable).
3. **Design depth: BMAD party FIRST.** Design + pressure-test the geometric-score function (signals,
   weighting, calibration protocol) with architect + qa + analyst BEFORE any C1.1 code. Party output
   becomes the intended-solution design appended below.

> **Orchestrator scope note:** the party (design deliverable) is the immediate next action. The C1 A→B→C
> *implementation* is a fresh medium epic (cardinal false-abstain risk + generator upgrade) and should run
> as its own supervised orchestrator loop — ideally a fresh context — not a roll-on from the B2 session.

---

---

# C1 intended-solution design (BMAD party output, 2026-07-16)

Party: Winston (architect) + Tessa (QA/calibration) + Mary (analyst), each code-grounded. The party
**refuted the scoping doc's headline signal** and reordered the epic. This section is the design of record.

## Design decision 1 — the signal set (REVISED; the doc's headline was wrong)

The doc led with "token-competition count" as the signal for the CAT/TASA swap. Tessa + Mary independently
proved it **cannot catch a swap**: `ExtractTasaAndCat` disambiguates purely by ordinal position
`pctTokens[0]`=CAT/`[1]`=TASA, and **`pctTokens.Count` is structurally always 2** — a swap flips which token
is at index 0, it does not change the count. Converged signal set:

| # | Signal | Catches | Cost | Verdict |
|---|---|---|---|---|
| 1 | **Sibling / order-marker presence** (`sin IVA` between the two `%` tokens; label/`$` adjacency) | **The CAT/TASA swap** — `Count==2` but order reversed = confident-WRONG (worse than abstain). The ONLY signal that catches it. | New code (small) | **LOAD-BEARING for A** |
| 2 | **Token-competition count** (count the candidates already built in `pctTokens` / `FindAmountInBand`'s `candidates` enumerable) | A *spurious extra* token — 3rd `%` token, decoy RESUMEN amount ("too many candidates") | ~free (stop discarding) | Ship, but for the DECOY failure, not the swap |
| 3 | **Column dual-pass disagreement** (binary: did `ExtractResumenField`'s left- AND right-column pass both fire with different values?) — NOT a continuous distance-from-center score | Wrong-column RESUMEN pick | Small (expose existing discard) | **B only; binary not graded** |
| 4 | ~~Label→value gap distance~~ | nothing real — detects label LENGTH, not error → false-abstain on long-labeled rows | — | **CUT (do not build)** |

Cross-field agreement term: **cut** (S-B2.0 refuted; no phantom no-op terms).

## Design decision 2 — architecture (Winston)

- **Seam:** add `ExtractedField<T>.Found(value, locator, double confidence, provenance = null)`; existing
  `Found(value, locator, provenance)` delegates with `1.0` — behavior-neutral by construction, ~40 call
  sites untouched.
- **Scorer:** a separate `internal static GeometricPlausibilityScorer.Score(Signals, FieldCalibration)` —
  pure function of a `Signals` record, no PdfPig types, no I/O → unit-testable against synthetic structs,
  fast calibration loop. Extractor does *selection*; scorer does *plausibility of the selection*.
- **Formula: MULTIPLICATIVE penalty** (start 1.0, each signal multiplies a `≤1.0` penalty), NOT weighted
  sum — a weighted sum lets one clean signal dilute a damning one into a passable average (the B2
  false-confidence shape). Any single red flag must be able to drag the score below 0.8 on its own.
  **Open (settle in C1.0, empirically): whether sibling-presence is a penalty factor or a GATE** that
  skips the competition penalty entirely — `0.75×0.85≈0.64` may over-penalize a legit disambiguated 2-token
  read (Winston risk #2). Do not pin constants by debate; the spike sets them.
- **Constants:** a static `internal FieldCalibrationTable` (per-field records), NOT tenant config — these are
  facts about template geometry, not tenant policy; tenant config invites "fixing" calibration by editing
  JSON instead of re-running the spike. The **0.8 guard threshold is untouched** (no move, no per-field floor).
- **Ship-dark switch lives at the extractor call site** (which `Found` overload is called), not in the scorer
  or the guard. Score is always computed (cheap/pure); dark = call the 1.0 overload.
- **Architecture-enforcement test:** every scored field has a `FieldCalibrationTable` entry exercised by ≥1
  calibration specimen (drift guard, in the spirit of the existing 19/19 arch tests).

## Design decision 3 — calibration protocol (Tessa) — and the epic REORDER

**The corpus has ZERO adversarial specimens today → C1.0 is unrunnable as scoped.** The generator work is a
**PREREQUISITE to C1.0, not the final increment.** Reordered epic:

- **C1.0a (generator, FIRST):** emit adversarial specimens + god's-eye manifest ground truth. Mary's set:
  `decoy-percent` (3rd `%` token), `missing-order-marker` (`sin IVA` removed — value still RIGHT but score
  must dip; proves signal #1 ≠ #2), `decoy-resumen-amount`, and a **`realbanamex`-profile** decoy (Mary's
  non-negotiable — the `dummievec` layout "does not occur in production"; the real left-column pass must be
  calibrated on its own code path, or it's the "synthetic ≠ real" trap a third time). Annotate
  `s6211-baseline` with `confidenceExpectations: {<field>: {band:"high", min:0.8}}`. New manifest fields:
  `geometryDefect{type,decoyToken,trueValue}`, `confidenceExpectations{<field>:{band,min|max}}`.
- **C1.0b (separation spike, MAKE-OR-BREAK):** **train/holdout split** — freeze weights on the design set,
  gate on a *blind* holdout specimen (scoring against tuned points = curve-fitting, not proof). Pass bar:
  `min(clean) ≥ 0.8` AND `max(ambiguous) < 0.8` AND **margin ≥ 0.15**. **Causal bar:** each signal states
  why it differs clean-vs-swapped (the count signal fails this for the swap — it is decorative there).
  **Perturbation stress:** jitter bounding boxes ±3pt, clean must stay ≥0.8 (a score surviving only exact
  fixture coords is memorized). Target clean cluster **0.92–1.0**, not "just above 0.8." **STOP condition:**
  margin < 0.15 or any clean < 0.8 → redesign the SIGNALS; NEVER lower the floor / shop the threshold /
  shrink the margin post-hoc (that is B2-Lens-C laundered into the calibration layer).
- **First artifact to build:** `s-c1-swap`, and prove it yields a confident WRONG verdict through the live
  pipeline today. It is the thing every later gate depends on.

## Design decision 4 — ship-dark arming gate (Tessa)

1. C1.0b passes (holdout, stop condition not tripped).
2. C1.1 (`Found` overload) → suites **bit-identical green** (matched counts: Extraction 366 / Validation 532
   / Orchestration 147 / Application 158 — a shift to N−3 green is a silent regression, not a pass).
3. C1.2 emits score behind flag; flag-off path byte-identical to pre-change.
4. **Before flipping default:** run the 5-fixture demo `[Theory]` (`VecChecklistDemoE2ETests`) twice in one
   process (flag off/on), diff `(fixture, field, confidence, verdict, failCheckIds)`. Assert **all 5
   verdicts + failCheckId sets identical**. Any diff = automatic STOP (even if the new verdict "looks more
   correct" — an unpredicted flip means the protocol didn't cover it). **Mandatory negative control:** add
   `s-c1-swap` as a 6th case that MUST flip confident-wrong → `InsufficientData` (a harness that only proves
   "nothing moved" can't distinguish a working fix from an inert one).
5. Flip tenant default; keep the flag as a killswitch ≥1 release.

**What a green suite does NOT prove (close each):** rule-unit tests hand-construct `ExtractedField` at conf
1.0 → the new confidence never flows through them (only the verdict-diff harness exercises
extractor→field→rule E2E); "behavior-neutral" ships with nobody checking the number is sane → add a
`Confidence ≥ 0.8` completeness sweep over every clean field in every specimen; author-built `BoundingBox`
fixtures ≠ real PdfPig tokenization → validate against `s622-realbanamex-baseline`; 5 demo fixtures are
single-bank → state the limitation, don't silently generalize.

## Revised story map (supersedes §"Proposed story breakdown")

- **C1.0a** generator: adversarial specimens + manifest ground truth (`realbanamex` variant included). *(was C)*
- **C1.0b** separation spike: train/holdout, margin ≥0.15, causal + perturbation bars, STOP condition. *MAKE-OR-BREAK.*
- **C1.1** `ExtractedField.Found` confidence overload (behavior-neutral). Bit-identical-green gate.
- **C1.2** `GeometricPlausibilityScorer` + sibling-marker(#1) & competition-count(#2) signals in
  `ExtractTasaAndCat`; multiplicative formula; `FieldCalibrationTable`; emit DARK. Scorer unit + calibration tests.
- **C1.3** arm Tasa/Cat: verdict-diff harness (5 demo + `s-c1-swap` negative control) → flip flag.
- **C1.4 (B)** extend to `ScanResumenColumn` (#1 + #3 binary dual-pass); `decoy-resumen-amount` +
  `realbanamex` decoy; calibrate-then-arm.
- **C1.5** architecture-enforcement test (every scored field ↔ calibration entry ↔ specimen).
- Adversarial review each increment (false-abstain / verdict-flip hunt).

## Residual open question for the C1 orchestrator (not blocking scope)
Sibling-presence as penalty-factor vs. gate (Winston risk #2) is deliberately LEFT to C1.0b to settle from
data. Do not pre-pin it.

## Log
- 2026-07-16 — Scoping opened. Framing spike reframed the lever from "per-digit confidence" (mis-scoped —
  fields are text-layer) to **geometric-plausibility confidence**. Consumption fully wired (0.8 guard, 15
  rules); production greenfield. Cardinal risk = false-abstain via mis-calibrated floor. Recommended:
  Option A, spike/calibration-gated, ship-dark. Owner decision pending.
- 2026-07-16 — Owner ruled A→B→C + party-first. BMAD party (Winston/Tessa/Mary) produced the design of
  record above. KEY: party refuted the doc's headline — token-competition count is structurally inert for
  the CAT/TASA swap (count always 2); the `sin IVA` sibling/order-marker is the load-bearing signal.
  Gap-distance signal CUT. Generator work REORDERED to a prerequisite (C1.0a) because the corpus has zero
  adversarial specimens today. Multiplicative formula; constants settle empirically in the C1.0b spike
  (train/holdout, margin ≥0.15). Epic ready to hand off to its own supervised orchestrator loop.
