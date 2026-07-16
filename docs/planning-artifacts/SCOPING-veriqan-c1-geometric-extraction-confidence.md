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

## Open owner decisions

1. **Appetite:** A only, A→B, or A→B→C? (Recommendation: A now, decide B after A's calibration lands.)
2. **Dark-first vs. arm-immediately:** recommend dark-first + calibration gate (the cardinal risk).
3. **Design depth:** the geometric-score function is a genuine design fork (which signals, how weighted,
   how calibrated). Recommend a short **BMAD party** (architect + qa + analyst) to design + pressure-test
   the scoring function and the calibration protocol BEFORE C1.1, given the false-abstain blast radius.

---

## Log
- 2026-07-16 — Scoping opened. Framing spike reframed the lever from "per-digit confidence" (mis-scoped —
  fields are text-layer) to **geometric-plausibility confidence**. Consumption fully wired (0.8 guard, 15
  rules); production greenfield. Cardinal risk = false-abstain via mis-calibrated floor. Recommended:
  Option A, spike/calibration-gated, ship-dark. Owner decision pending.
