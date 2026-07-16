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

## Status
| # | Story | Status | Verify gate |
|---|---|---|---|
| C1.0a | Generator adversarial specimens + manifest ground truth | ☐ | `s-c1-swap` exists + proven confident-WRONG verdict through live pipeline |
| C1.0b | Separation spike (MAKE-OR-BREAK) | ☐ blocked by C1.0a | train/holdout, min(clean)≥0.8, max(ambiguous)<0.8, margin≥0.15, ±3pt perturbation |
| C1.1 | `ExtractedField.Found` confidence overload | ☐ (parallel w/ C1.0a) | Extraction 366 / Validation 532 / Orchestration 147 / Application 158 — BIT-IDENTICAL |
| C1.2 | `GeometricPlausibilityScorer` + signals in `ExtractTasaAndCat` (DARK) | ☐ blocked by C1.0b, C1.1 | scorer unit + calibration green; flag-off byte-identical |
| C1.3 | Arm Tasa/Cat via verdict-diff harness | ☐ blocked by C1.2 | 5 demo verdicts identical off/on; `s-c1-swap` 6th case flips → InsufficientData |
| C1.4 | Extend to `ScanResumenColumn` (11 money fields) | ☐ blocked by C1.3 | decoy-resumen + realbanamex decoy calibrated-then-armed |
| C1.5 | Architecture-enforcement test | ☐ blocked by C1.4 | every scored field ↔ calibration entry ↔ specimen; Confidence≥0.8 sweep |

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
