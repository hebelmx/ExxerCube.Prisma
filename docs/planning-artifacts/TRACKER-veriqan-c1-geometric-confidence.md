# TRACKER — Veriqan C1: geometric extraction-confidence

**Branch:** `Liv` · **Started:** 2026-07-16 · **Design of record:** `SCOPING-veriqan-c1-geometric-extraction-confidence.md` (§"C1 intended-solution design")
**Orchestrator scope (owner-confirmed 2026-07-16):** Full C1 epic (C1.0a → C1.5) in one supervised loop. The **C1.0b spike is a hard internal gate**: if `margin < 0.15` or any clean field `< 0.8`, HALT and surface to owner — never lower the floor / shop the threshold.

## The lever (one line)
Money/rate fields are text-layer (no digit noise); the real misread is GEOMETRIC (wrong token/column/row). Emit a geometric-plausibility confidence from the positional extractor; the already-wired 0.8 guard (15 rules) converts a low-plausibility pick into honest `InsufficientData`. **No rule changes.**

## Load-bearing design facts (party, do not re-derive)
- **`sin IVA` sibling/order-marker is THE load-bearing signal** for the CAT/TASA swap. Token-competition COUNT is structurally inert for the swap (`pctTokens.Count` always 2) — it only catches a spurious *extra* decoy token.
- **MULTIPLICATIVE penalty** formula (any single red flag drags <0.8 alone), NOT weighted sum.
- Constants in static internal `FieldCalibrationTable`, NOT tenant config. 0.8 threshold untouched.
- Gap-distance signal (#4) is **CUT**. Column signal (#3) is **binary dual-pass disagreement**, not continuous distance.
- Corpus has ZERO adversarial specimens today → generator (C1.0a) is a **prerequisite**, not the final increment.

## Baseline test counts (post-C1.0b — new floor for "bit-identical" gates)
Extraction **370** (366 +4 golden roundtrip cases) · Validation **532** · Orchestration **165** (147 +4 C1.0a, +14 C1.0b spike) · Application **158**. Run via bare `net10.0/` DLL (freshest output dir), not `Debug/net10.0/`.

## Post-C1.2 test counts (new floor)
Extraction **398** (370 +28: 12 `GeometricPlausibilityScorerTests` unit + 16 `GeometricPlausibilityCalibrationTests` production-wiring calibration) · Validation **532** (unchanged) · Orchestration **165** (unchanged) · Application **158** (unchanged). Solution build 0/0.

## ✅ C1.0b SPIKE VERDICT = GO (settled mechanism for C1.2 to inherit)
Margin **0.35** (min clean 1.000 − max ambiguous 0.650 ≥ 0.15), proven on a train/holdout split, robust to ±3pt jitter (50 trials). **Signals (multiplicative, independent):** #1 **sibling/order-marker rank-adjacency** — is `sin`+`IVA` the two words immediately after the CAT-picked token in Left-order, before the TASA pick? (LOAD-BEARING; catches the marker-displaced swap). #2 **competition count >2** (decoy only; inert for the swap). **Constants:** `SiblingAbsentPenalty=0.55`, `CompetitionExcessPenalty=0.65`. **Settled: penalty-factor, NOT gate** (decoy-percent's `band:low` ground truth rules out a gate). **C1.2 MUST use rank/ordinal adjacency, not edge-distance** (edge-gap ~2.2pt < ±3pt jitter → memorized/fragile). Prototype: `Calibration/GeometricPlausibilityScorerPrototype.cs` + `SpikeBandLocator.cs` + `C1_0b_SeparationSpikeTests.cs` (14 tests).
**⚠️ CARRY-FORWARD for C1.3:** the lever catches *marker-displaced* swaps (`s-c1-swap-displaced`), NOT the geometrically-invisible central-marker swap (`s-c1-swap`) — an inherent, documented blind spot. C1.3's negative control MUST use **`s-c1-swap-displaced`** (which flips → InsufficientData when armed); `s-c1-swap` must NOT be expected to flip.

## Status
| # | Story | Status | Verify gate |
|---|---|---|---|
| C1.0a | Generator adversarial specimens + manifest ground truth | ✅ done (`f37a60b2`) | `s-c1-swap` proven confident-WRONG through live pipeline (CL-10 flips Pass→confident Fail) — VERIFIED |
| C1.0b | Separation spike (MAKE-OR-BREAK) | ✅ GO (`7deb4fb7`) | margin 0.35, train/holdout blind, ±3pt robust — VERIFIED (read prototype + reran 14 tests) |
| C1.1 | `ExtractedField.Found` confidence overload | ✅ done (`4af169e5`) | bit-identical green — VERIFIED |
| C1.2 | `GeometricPlausibilityScorer` + signals in `ExtractTasaAndCat` (DARK) | ✅ done | scorer unit (28 new) + calibration green; flag-off byte-identical — VERIFIED |
| C1.3 | Arm Tasa/Cat via verdict-diff harness | ☐ blocked by C1.2 | 5 demo verdicts identical off/on; `s-c1-swap` 6th case flips → InsufficientData |
| C1.4 | Extend to `ScanResumenColumn` (11 money fields) | ☐ blocked by C1.3 | decoy-resumen + realbanamex decoy calibrated-then-armed |
| C1.5 | Architecture-enforcement test | ☐ blocked by C1.4 | every scored field ↔ calibration entry ↔ specimen; Confidence≥0.8 sweep |

## ⚠️ CRITICAL FINDING from C1.0a (gates C1.0b design) — SEPARABILITY
The `s-c1-swap` fixture as built = `"27.36% sin IVA 28.86%"` (numbers transposed, `sin IVA` kept BETWEEN the two `%` tokens — same structure as baseline `"28.86% sin IVA 27.36%"`). This **proves the confident-wrong VERDICT exists** (CL-10 flips), BUT the two layouts are **geometrically identical** — `sin IVA` is equidistant, token count is 2, columns align — so **NO geometric signal separates them.** The party's load-bearing signal (sibling `sin IVA`) can only catch a swap if the marker TRACKS the true CAT (i.e. a realistic swap displaces `sin IVA` off the mis-picked leftmost token). 
**⇒ C1.0b's FIRST job:** decide whether a *realistic* CAT/TASA swap displaces the `sin IVA` marker (per the party's signal intent — `sin IVA` legally qualifies CAT). If yes, build/calibrate a marker-displaced swap variant (e.g. `"27.36% 28.86% sin IVA"`) that is BOTH faithful to real Banamex layout AND geometrically separable (margin≥0.15). If the only realistic swap is geometrically invisible → **STOP + escalate to owner** (lever not viable via geometry alone). Do NOT shop for a passing fixture; justify layout from the realbanamex reference. Keep the current central-`sin IVA` `s-c1-swap` as a negative control ("geometrically-invisible swaps exist").
Deferred by C1.0a: `decoy-resumen-amount` (belongs in C1.4).

## Key file map
- Generator: `scripts/veriqan-corpus/synth_gen.py` → `Prisma/Fixtures/PRP2/synthetic/corpus-manifest.json`
- Extractor: `02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs` (`ExtractTasaAndCat` ~1265-1287, `ScanResumenColumn` ~1438-1512)
- Domain type: `01 Core/Veriqan.Domain/Extraction/ExtractedField.cs` (`.Found` hardcodes 1.0 @ :82)
- Calibration harness (C#): `08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/` (`CorpusSchema.cs`, `CalibrationDriverTests.cs`)
- Demo verdict E2E: `VecChecklistDemoE2ETests`

## Adversarial-review cadence
After C1.0a, after C1.0b (the gate itself is the review), after C1.3, after C1.5. Hunt: false-abstain (Lens C) + any verdict flipping the wrong way.

## Log
- 2026-07-16 — Tracker opened; tasks #1–#7 created. Scope confirmed = full C1 with C1.0b as hard internal gate. Starting C1.0a + C1.1 in parallel.
- 2026-07-16 — C1.1 done (`4af169e5`, bit-identical green). C1.0a done (`f37a60b2`, swap→confident-wrong CL-10 verified) + surfaced the SEPARABILITY finding. C1.0b spike = **GO**, margin 0.35, verified from ground truth (read prototype/harness, reran 14 tests). Make-or-break gate PASSED → proceeding to C1.2. Carry-forward: C1.3 negative control = `s-c1-swap-displaced`.
- 2026-07-16 — C1.2 done. Lifted the spike mechanism verbatim into production: `Confidence/GeometricPlausibilityScorer.cs` (new file, `02 Infrastructure/Veriqan.Infrastructure.Extraction/`) — `GeometricToken`/`GeometricSignals`/`FieldCalibration`/`FieldCalibrationTable.TasaCat` (SiblingAbsentPenalty=0.55, CompetitionExcessPenalty=0.65) + `GeometricPlausibilityScorer.Score`/`IsSiblingAdjacentToPick` (rank-based, matches C1.0b's perturbation-proofed version). Wired into `ExtractTasaAndCat` (now takes `emitGeometricConfidence`); score is ALWAYS computed, only the `ExtractedField.Found` overload choice is gated. Ship-dark switch = new `emitGeometricConfidence` ctor param on `PdfPigStatementFieldExtractor`, default `false` (mirrors the existing `enableCatalogImageHashing` opt-in pattern) — NOT tenant config, NOT flipped on. 28 new tests (12 unit `GeometricPlausibilityScorerTests` + 16 `GeometricPlausibilityCalibrationTests`, the latter running the REAL extractor end-to-end with the flag armed over the synthetic corpus — production-wiring analogue of the C1.0b spike, not a duplicated band-locator). Verdict: margin unchanged from spike (min clean 1.0 − max ambiguous 0.65 = 0.35 ≥ 0.15). Flag-off byte-identical proven by an explicit `[Theory]` asserting Confidence==1.0 on all 6 fixtures (clean AND ambiguous) with the flag off. All 4 gates green: Extraction 398 (370+28), Validation 532, Orchestration 165, Application 158 — unchanged where expected. Solution build 0/0. Next: C1.3 (arm via verdict-diff harness, negative control = `s-c1-swap-displaced`).
