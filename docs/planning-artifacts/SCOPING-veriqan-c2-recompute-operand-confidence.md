# SCOPING — Veriqan C2: geometric confidence for the recompute-operand money fields

**Status:** SCOPING (owner decision pending — not yet an approved epic) · **Branch:** `Liv` · **Date:** 2026-07-17
**Follows:** C1 (`docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md` + tracker), whose
mechanism it reuses; the residual it closes was surfaced by the C1 close-out.
**Ground truth:** two read-only framing spikes 2026-07-17 (code-cited); load-bearing claims re-verified by the
orchestrator against files (`Section6PaymentSimulationRule.cs:197-199`, `GeometricPlausibilityScorer.cs`
`FieldCalibrationTable`, `PdfPigStatementFieldExtractorRealLayoutTests.cs`).

---

## TL;DR — the reframe (read this first)

The requested lever was **"per-digit confidence" (the B2 residual note)**. Two prior findings + this spike
kill that framing and point at a different, genuine lever:

1. **"Per-digit confidence" was already refuted (C1 scoping, commit `595874cf`).** All money/rate fields are
   read from the PDF **text layer** (exact glyphs) — there are no uncertain digits to score. The only real
   per-char OCR confidence lives in E7's `TesseractHeaderProductOcrEngine` (Product only), and **no rule
   consumes Product confidence** — capturing it is inert without also building a consumer. NOT this.
2. **"Harden C1's Tasa/Cat single-bank gap" — REFUTED and DANGEROUS (spike 1).** On the real Banamex
   `good.pdf`, Tasa/CAT/Total values exist in the text layer but carry **no labels** (the CAT acronym never
   appears; there is no "Total cargos" literal). The two bare percents (`7.77% 29.72%`) sit on an obfuscated
   code strip and are **semantically unidentifiable** (7.77% is implausibly low for a Mexican CAT). Forcing
   extraction would feed CL-10/§19 wrong operands → a **new false RED** (computed CAT ≈31.6% vs observed
   7.77%, diff ≫ tolerance) that breaks the exact-FailCheckId demo assertions — and **C1 would not catch it**
   (the code-strip percent pair is exactly the two-adjacent-tokens shape the scorer rewards). Current
   `NotExtracted`/killswitch state is the honest one. NOT this.
3. **The genuine lever (spike 2, verified): extend C1's existing geometric scorer to the six recompute-operand
   money fields that are guarded but not yet scored.** Unlike Tasa/Cat, **all six extract on the real demo**
   → the confidence is **non-vacuous**. It reuses the proven C1.6 `ScoreTotalRow` / `IsValueRankAdjacentToLabel`
   mechanism, carries the existing `EmitGeometricConfidence` killswitch, and needs no rule changes.

---

## The failure class this closes

Each of these six fields is a single-pass **label→value positional pick** via
`FindAmountInBand` / `FindAmountInBandSplitDollar` (`PdfPigStatementFieldExtractor.cs:2257,2311`), which take
the leftmost/rightmost amount-pattern token in a Y-band. The extractor's own comments document the hazard —
*"avoids picking up right-column values (e.g. CLABE) that merged into this band via Y-band tolerance"* (`:1141`),
*"amounts that belong to a different column that merged into this band"* (`:2298`). A leaked cross-column token
is picked at **confidence 1.0** (`ExtractedField.Found` default, `ExtractedField.cs:82-83`). Every rule that
consumes these fields is a **recompute** — a single misread fires a false **RED** (diff exceeds tolerance), and
a *compensating* misread (operand and total mis-picked from the same wrong column) fires a false **GREEN**.
Both are invisible to the 0.8 guard today because the operand asserts 1.0. This is the identical
wrong-token/wrong-column class C1.6 already scores for the DESGLOSE total row — just not extended to these six.

## Ground-truth map (spike, re-verified)

**Scored today** (`GeometricPlausibilityScorer.FieldCalibrationTable`): `TasaCat` (one entry), 7 RESUMEN
(`AdeudoPeriodoAnterior, CargosRegularesNoMeses, CargosComprasAMesesCapital, MontoIntereses, MontoComisiones,
IvaInteresesYComisiones, PagosYAbonos`), `TotalCargos`, `TotalAbonos`.

**The DELTA — guarded/consumed but NOT scored (confidence always 1.0):**

| Field | Extract method | Guard consumer(s) | Extracts on real `good.pdf`? |
|---|---|---|---|
| `PagoMinimo` | `ExtractPagoMinimo` (`:1219-1221`, unconstrained far-right fallback) | **§6 interest-recursion moat** (`Section6PaymentSimulationRule.cs:197-199`, inline `.Confidence < threshold`) | **Yes — 980.00** (`RealLayoutTests:237-238`) |
| `SaldoDeudorTotal` | `ExtractSaldoDeudorTotal` → `FindAmountInBandSplitDollar` | Cl24, Cl25 (both recompute) | Yes (counted, `:115`) |
| `CreditoDisponible` | `ExtractCreditoDisponible` → `FindAmountInBandSplitDollar` | Cl25 (recompute) | Yes — 26791.00 (spike; `:116`) |
| `SaldoCargosRegulares` | `ExtractNivelDeUsoField` | Cl22, Cl24 (recompute) | **Yes — 12604.55** (`:321-323`) |
| `SaldoCargosAMeses` | `ExtractNivelDeUsoField` | Cl24 (recompute) | **Yes** (`:327-332`) |
| `PagoParaNoGenerarIntereses` | `ExtractPagoParaNoGenerarIntereses` → `FindAmountInBand` (`:1110`) | Cl21, Cl22 | **Yes — 12604.55** (`:310-312`) |

> Correction re-verified by the orchestrator: §6 **does** guard `PagoMinimo` confidence — via an *inline*
> `pagoMinimoField.Confidence < confidenceThreshold` at `Section6PaymentSimulationRule.cs:197-199`, NOT the
> `ConfidenceBelowThreshold` helper (which is why a grep for the helper misses it). The claim holds.

## THE cardinal risk (unchanged from C1)

The 0.8 guard is a **hard floor**. If the geometric score under-rates a *clean* pick, this epic converts a
working GREEN/RED into a false `InsufficientData` — the "false-abstain" failure mode. **`PagoMinimo` is the
sharpest case:** `ExtractPagoMinimo` deliberately falls back to an *unconstrained* far-right column (`:1219-1221`)
for the real layout's X≈528 pago-mínimo column, so a naive rank-adjacency calibration would false-abstain the
legitimate value (the exact "MaxLabelToPickRankGap must clear the real gap" lesson already logged at
`FieldCalibrationTable` ~`:212`). Mitigation is the proven C1 discipline and is non-negotiable:
- **Calibrate per field against the god's-eye synthetic corpus** — clean specimens must score ≥ 0.8; only
  deliberately-adversarial decoys may dip below. Adversarial specimens for these fields **do not exist yet**
  (corpus has `decoy-percent`/`decoy-resumen-amount`/`decoy-total-amount` only) → generator work is a prereq.
- **Ship DARK first** behind `EmitGeometricConfidence` (already the killswitch), arm only after the
  `VecChecklistDemoE2ETests` armed-vs-off verdict-diff proves the 5 real demo verdicts + FailCheckId sets are
  UNCHANGED (the C1.4/C1.6 arming gate, reused verbatim).

## Options (owner appetite)

| Option | Scope | Value | Cost/Risk |
|---|---|---|---|
| **A — `PagoMinimo` slice** (recommended first) | score `PagoMinimo` only; §6 recursion **amplifies** its misread into a wrong months-to-payoff Fail → highest single-field severity; guard already wired inline | Closes the highest-severity operand; smallest surface | Small; but the far-right unconstrained fallback makes false-abstain calibration the crux |
| **B — + Saldo/Crédito triangle** | extend to `SaldoDeudorTotal, CreditoDisponible, SaldoCargosRegulares, SaldoCargosAMeses, PagoParaNoGenerarIntereses` (Cl22/24/25/21 recompute operands) | Broader, catches compensating-misread false-GREEN across the identity checks | Medium; 5 more `FieldKind` entries + `decoy-nivel`/`decoy-saldo` specimens; more calibration surface |
| **C — + date-window guard (Cl42/Cl49)** | add the missing confidence guard to `Cl42MovementDatesInPeriodRule` / `Cl49` | Closes a guard-gap | Weak/deferrable — different mechanism (date parse, not geometric); no scorer to build; well-formed-but-wrong dates are the only residual and there's no sub-1.0 date-confidence source, so it's largely a no-op today |
| **✗ NOT this** | per-char OCR confidence; Product Tesseract confidence; a separate joint-consistency layer; further Tasa/Cat work | — | Inert / redundant / vacuous (see Refuted list) |

**Recommended:** **A first** (spike-gated, ship-dark), then **B**; **C** only if trivially cheap. This is a
**small→medium epic**, a direct continuation of C1's mechanism, non-vacuous on the real demo.

## Proposed story map (if owner green-lights A→B)

- **C2.0a (generator, prereq):** add adversarial calibration specimens for these fields to the synthetic
  generator + god's-eye manifest (`decoy-pagominimo`; then `decoy-nivel`/`decoy-saldo` for B) — the corpus has
  none today, so the spike is unrunnable without this (same reorder C1 hit).
- **C2.0b (separation spike, MAKE-OR-BREAK):** train/holdout, `min(clean) ≥ 0.8 ∧ max(ambiguous) < 0.8 ∧
  margin ≥ 0.15`, perturbation-stress ±3pt. **STOP** if no separation — never lower the floor. Special attention
  to `PagoMinimo`'s legitimate far-right column (must stay ≥ 0.8).
- **C2.1:** wire `ScoreTotalRow`/`ScoreResumen` (reused) + a `FieldKind.PagoMinimo` calibration entry into
  `ExtractPagoMinimo`; emit DARK. Scorer unit + corpus calibration tests.
- **C2.2:** arm `PagoMinimo` — verdict-diff harness (5 demo + `decoy-pagominimo` negative control) → confirm no
  flip. (A done.)
- **C2.3 (B):** extend to the 5 Saldo/Crédito operands; 5 calibration entries + specimens; calibrate-then-arm.
- **C2.4:** extend the C1.5 architecture-enforcement drift-guard to the new scored fields (every scored field ↔
  calibration entry ↔ specimen).
- Adversarial review each increment (false-abstain / verdict-flip hunt).

## Refuted / already-covered (do NOT build)

- **Per-char / Product Tesseract confidence** — inert: no consumer of Product confidence exists anywhere;
  Product is protected by the `UnknownProduct` catalog gate; capturing it doubles the work and touches
  load-bearing Product resolution. Separate Product-only ticket at most.
- **Separate joint-consistency ("moat") confidence** — redundant: the recompute rule *is* the consistency
  check; per-operand geometric scoring (A/B) already catches per-operand misread AND the compensating-misread
  false-GREEN, multiplicatively.
- **Further Tasa/Cat/Total extractor work** — refuted + dangerous (spike 1); vacuous on the real demo;
  killswitch is the honest mitigation.
- **Cl50 fiscal-QR value guard** — inert: reads its fields only as a presence/`>0` boolean; a value misread
  doesn't flip it.
- **`ExtractedInvalidFormat` fields** — already abstain (confidence hard-set 0.7 < 0.8, `ExtractedField.cs:98`).

## Owner decisions — RULED (2026-07-17)
1. **Appetite: A→B→C (full).** `PagoMinimo` + the 5 Saldo/Crédito operands + the Cl42/Cl49 date-window
   guard-gap. C is authorized despite being weak — evaluate cost during the epic and log honestly if it
   proves a no-op (do not build inert guards for the sake of scope).
2. **Dark-first + calibration-gate discipline CONFIRMED** (non-negotiable).
3. **Design depth: fresh mini-party FIRST** on the `PagoMinimo` far-right-fallback false-abstain risk
   (architect + QA), before any C2.0 code. Party output becomes the design of record appended below.

> **Orchestrator scope note:** this is one epic (C2), run as a supervised orchestrator loop. The mini-party
> (design deliverable) is the immediate next action; C2.0a generator + C2.0b MAKE-OR-BREAK spike gate all
> implementation. If C2.0b finds no separation for `PagoMinimo` (its far-right fallback is geometrically
> indistinguishable from a decoy), that slice STOPS — never lower the floor to force it through.

---

# C2 intended-solution design (BMAD mini-party output, 2026-07-17)

Party: Winston (architect) + Tessa (QA/calibration), both code-grounded. Winston pulled the real `good.pdf`
bounding boxes; Tessa pressure-tested the calibration gate. Two findings **reshape the epic** (below). This
section is the design of record.

## Finding 1 — `PagoMinimo` is VACUOUS on the real demo → reorder the epic (both party members)

`PagoMinimo`'s only guard consumer is §6 (`Section6PaymentSimulationRule.cs:189-215`), which gates on **three**
fields — `PagoMinimo`, `Tasa`, `PagoParaNoGenerarIntereses` — and short-circuits to `InsufficientData` if any
is missing/low-confidence. **`Tasa` is `NotExtracted` on `good.pdf`** (the C1 finding), so §6 already returns
`InsufficientData` on the real demo unconditionally, regardless of `PagoMinimo`. Its "highest-severity via §6
recursion" value manifests **only on synthetic Tasa-present specimens** — same posture as Tasa/Cat. The
"`PagoMinimo` first" ordering in the options table above is therefore **wrong for real-demo value**.

**Verified reorder (ground truth `VecChecklistDemoE2ETests`):** the operands consumed by **CL-21 / CL-22**
(`SaldoCargosRegulares`, `PagoParaNoGenerarIntereses`) DO reach their recompute + confidence guard on real
data (CL-22 Pass on `good.pdf`, Fail on `bad-math-cl21.pdf`). `SaldoDeudorTotal`/`CreditoDisponible`/
`SaldoCargosAMeses` (CL-24/CL-25) recompute directly with no Tasa gate. **These are the non-vacuous,
demo-provable slice — lead with them.** `PagoMinimo` becomes a synthetic-only defensive slice (own killswitch,
arm only if its spike separates), NOT the head of the epic.

## Finding 2 — C2 cannot ship dark under the existing flag → needs its own killswitch (Winston)

`EmitGeometricConfidence` **already defaults `true`** as of C1.7 (`PdfExtractionOptions.cs:70`). Wiring these
fields through `_emitGeometricConfidence` therefore goes **LIVE on first deploy, not dark** — and because the
consuming guards are currently vacuous (confidence always 1.0 today, so the guard *can never fire*), a
calibration gap would produce a first-time real `InsufficientData` on day one for every tenant. To honor the
owner's dark-first ruling, C2 needs **a second, field-scoped killswitch** (e.g. `EmitHeaderMoneyGeometricConfidence`,
default `false`) that stays off until each slice's spike + calibration is proven — mirroring the C1.0a→C1.7
dark→armed staging. (Alternative: reuse the armed flag but require spike-margin-proof BEFORE merge, with no
"ship dark, arm later" grace period. Owner to rule.)

## Design decision — the signal set (resolved; architect's measurement settled the QA/architect split)

Tessa worried rank-adjacency false-abstains a "far by design" pick. Winston **measured the real fixture**: the
Pago-mínimo row contains only `Pago` / `mínimo:` / footnote `4` / `$980.00` — the value is ~166pt away but only
**rank 1–2** away (empty intervening space, not decoys). Since `IsValueRankAdjacentToLabel`
(`GeometricPlausibilityScorer.cs:428-444`) measures **ordinal rank, not pixel distance** (the explicit C1.0b
lesson), it *rewards* the legit far-right pick correctly. **Resolution: rank-adjacency stays as signal #1.**

| # | Signal | Catches | Verdict |
|---|--------|---------|---------|
| 1 | **Label→value rank-adjacency** (reuse `IsValueRankAdjacentToLabel` verbatim — field-agnostic) | wrong-row/wrong-column token merged into the band (big rank gap) | KEEP |
| 2 | **Token-competition count** (C1.6 `HasCompetingAmount` shape: >1 amount-pattern token in the band) | right row, two candidate amounts, `FindAmountInBand`'s rightmost-wins grabbed the wrong one (a right-column leak — CLABE/masked-card/footnote-digit, the extractor's own `:1141`/`:2298` hazard) | ADD |
| ✗ | Raw X-band/column anchor | — | **CUT** — reintroduces the raw-distance jitter fragility C1.0b disproved; duplicates the extractor's `maxX` logic in the confidence layer |
| ✗ | "Was the unconstrained fallback used" | — | **CUT** — it's a template *classifier* (`true` for every real-Banamex pick, `false` for every Dummie-VEC pick); penalizing it down-scores every clean real pick. Debug metadata only, never in the formula. |

Multiplicative penalty (each red flag can drag below 0.8 alone), same as C1.

## Design decision — architecture (Winston)

- **New slice type**, structurally identical to C1.6's `TotalRow` but kept **distinct** for the non-interference
  guarantee that let C1.4/C1.6 ship independently: `HeaderMoneyFieldGeometricSignals(bool LabelRankAdjacent,
  bool HasCompetingAmount)` + `HeaderMoneyFieldCalibration` + `ScoreHeaderMoneyField`, plus a
  `FieldCalibrationTable.HeaderMoney` dictionary keyed by `FieldKind`. Code duplication here is a **feature**
  (isolation), not debt. Do NOT reuse `ResumenGeometricSignals` (no dual-pass analog — these are single-pass).
- **All `FieldKind` values already exist** (`FieldKind.cs:60-79`: `PagoMinimo`, `PagoParaNoGenerarIntereses`,
  `PagoMinimoMasMeses`, `SaldoDeudorTotal`, `CreditoDisponible`) — no domain enum change. `SaldoCargosRegulares`/
  `SaldoCargosAMeses` come via `ExtractNivelDeUsoField` (verify their FieldKind entries).
- Scorer stays a **pure function of a signals record, no PdfPig types**. Ship-dark switch = which `Found`
  overload the extractor calls, behind the new field-scoped flag.
- `MaxLabelToPickRankGap` for these fields must be **proven against both template families** (Dummie-VEC + real
  layout), not assumed from one fixture.

## Design decision — calibration & the vacuous-gate test trap (Tessa) — LOAD-BEARING

The corpus has **zero** adversarial specimens for these fields, and **no clean far-right `PagoMinimo` specimen
at all** (`synth_gen.py` dummievec row is near-label X≤300; the realbanamex profile has no PagoMinimo row). So
C2.0a must add the **clean far-right specimen FIRST**, then the decoy — else "clean ≥ 0.8" has nothing to run
against but the single real fixture (curve-fit by construction).

**The regression-gate trap (the C1 lesson, one level deeper):** because §6 is already `InsufficientData` on
`good.pdf` (Tasa gate), an armed-vs-off `VecChecklistDemoE2ETests` diff for the `PagoMinimo` slice would show
"5 verdicts unchanged" **whether the scorer is correct, backwards, or a no-op** — vacuous proof. The gate must
therefore be, in order:
1. **Unit-level isolation** — extend `Section6SimulationRuleTests.cs:526` (`Evaluate_PagoMinimoLowConfidence_
   ReturnsInsufficientData`, hand-builds fields with Tasa held Extracted/≥0.8) to prove the *rule* wiring.
   For the CL-21/CL-22 slice, the analogous rule-unit tests are non-vacuous on real operands.
2. **Extractor-level calibration tests** against the C2.0a synthetic specimens (new
   `GeometricPlausibility*CalibrationTests`, flag-on/off + clean/ambiguous split) — this is where the GO/STOP
   margin is actually proven.
3. **Then** the armed-vs-off `VecChecklistDemoE2ETests` — but **log explicitly** whether §6 (`LAW-§6-SIMULACION`)
   is even reachable on `good.pdf` (it is not). Never let "5 verdicts unchanged" stand in for correctness.
4. A **negative-control specimen** (`decoy-*-rightleak`) that MUST flip its consuming rule to `InsufficientData`.
5. C1.5's drift guard (`GeometricPlausibilityCoverageTests`) auto-extends once fields are in the calibration
   table with clean (no-`geometryDefect`) specimens.

**Calibration bar (unchanged from C1, do not shop it down):** train/holdout split, `min(clean) ≥ 0.8 ∧
max(ambiguous) < 0.8 ∧ margin ≥ 0.15`, per-profile (dummievec + realbanamex, not pooled), ±3pt perturbation.

## STOP-risk assessment (both) — conditional GO

**Not a hard STOP.** On the one real fixture, rank-adjacency (gap≈1) and competition-count (=1) both cleanly
admit the legit pick, and a decoy is detectable by one signal or the other. **But** no adversarial specimen
exists yet, and the C1.6 addendum is a live precedent that an un-spiked "looks clean" claim was later found
WRONG. So: **run the C2.0b spike per field-slice, require margin ≥ 0.15, arm only on GO; if a slice cannot
separate, ship it dark permanently** (own killswitch off) — exactly the Tasa/Cat posture. `PagoMinimo` is the
most likely permanent-dark candidate (vacuous on real demo + hardest calibration).

## Revised story map (supersedes the §"Proposed story map" above)

- **C2.0a** generator: clean far-right + decoy specimens for the CL-21/CL-22 operands FIRST (non-vacuous slice),
  then `PagoMinimo` clean+decoy. God's-eye manifest `geometryDefect` + `confidenceExpectations`.
- **C2.0b** separation spike (MAKE-OR-BREAK, per slice): train/holdout, margin ≥0.15, ±3pt, STOP if no separation.
- **C2.k** add the field-scoped killswitch `EmitHeaderMoneyGeometricConfidence` (default OFF) — Finding 2.
- **C2.1** `HeaderMoneyFieldGeometricSignals`/`ScoreHeaderMoneyField` + calibration; wire the **CL-21/CL-22
  operands first** (`SaldoCargosRegulares`, `PagoParaNoGenerarIntereses`); emit dark. Unit + calibration tests.
- **C2.2** arm the CL-21/CL-22 slice: rule-unit (non-vacuous) + calibration + armed demo-diff (CL-22 Pass/Fail
  is real here) → flip the killswitch for this slice.
- **C2.3** extend to `SaldoDeudorTotal`/`CreditoDisponible`/`SaldoCargosAMeses` (CL-24/CL-25); calibrate-then-arm.
- **C2.4** `PagoMinimo` slice — spike-gated; arm ONLY if C2.0b separates; else permanent-dark with a logged caveat.
- **C2.5** extend C1.5 architecture-enforcement drift-guard to the new scored fields.
- **C2.C** Cl42/Cl49 date-window guard-gap — evaluate; log honestly if inert (owner-authorized but skeptical).
- Adversarial review each increment (false-abstain / verdict-flip hunt).

## Log
- 2026-07-17 — Scoping opened from a two-spike framing pass. Spike 1 refuted (and flagged as dangerous) the
  "harden C1 Tasa/Cat single-bank gap" lever. Spike 2 surfaced the recompute-operand delta (6 guarded-but-
  unscored fields, all non-vacuous on the real demo). Orchestrator re-verified the two load-bearing claims
  (§6 inline PagoMinimo guard; scored-vs-guarded delta; real-demo extraction). Recommended A→B, ship-dark,
  spike-gated. Owner decision pending.
