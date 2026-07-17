# TRACKER — Veriqan C2: geometric confidence for recompute-operand money fields

**Branch:** `Liv` · **Design of record:** `SCOPING-veriqan-c2-recompute-operand-confidence.md`
**Owner ruling (2026-07-17):** appetite **A→B→C (full)**; dark-first + calibration-gate; **mini-party first** on
the `PagoMinimo` far-right-fallback risk. One epic, supervised orchestrator loop.

## The lever (verified ground truth)
Extend C1's `GeometricPlausibilityScorer` to the six guarded-but-unscored recompute-operand fields
(`PagoMinimo`, `SaldoDeudorTotal`, `CreditoDisponible`, `SaldoCargosRegulares`, `SaldoCargosAMeses`,
`PagoParaNoGenerarIntereses`). All six extract on the real demo `good.pdf` → **non-vacuous**, unlike Tasa/Cat.
Reuses the C1.6 mechanism + `EmitGeometricConfidence` killswitch + C1.4/C1.6 armed-verdict-diff gate.
No rule changes.

## Test floor to hold (bare `net10.0/` DLL, run the DLL directly — NOT `dotnet test <csproj>`)
Extraction **448** · Validation **532** · Orchestration **184** · Application **158** (all 0-fail, armed).
Any drop to N−k green is a silent regression, not a pass.

## Status

> **REORDERED after mini-party + owner ruling (2026-07-17).** (1) `PagoMinimo` is VACUOUS on the real demo
> (§6 blocked by the `Tasa` NotExtracted gate) → **lead with the CL-21/CL-22 recompute operands**
> (non-vacuous, demo-provable), demote `PagoMinimo` to a synthetic-only spike-gated slice (arm only if its
> C2.0b spike separates; else permanent-dark). (2) `EmitGeometricConfidence` already defaults `true` (C1.7).
> **Owner ruled: NO new killswitch — reuse the armed flag, but PROVE the C2.0b separation margin (≥0.15)
> BEFORE merging each slice's production wiring** (wiring = arming; there is no ship-dark grace period). So the
> hard gate each slice: specimens (C2.0a) → spike GO (C2.0b) → THEN wire + commit (C2.1+). No spike GO, no merge.

| ID | Story | Status | Notes |
|----|-------|--------|-------|
| C2.party | Mini-party (architect+QA) → design of record | ✅ DONE | Signal set resolved (rank-adjacency + competition-count); 2 reshaping findings; conditional-GO |
| C2.confirm | Owner confirm: reorder + killswitch mechanism | ✅ RULED | Reorder YES; reuse armed flag + prove-margin-before-merge |
| C2.0a | Generator: clean + decoy specimens — CL-21/22 operands FIRST (`SaldoCargosRegulares`, `PagoParaNoGenerarIntereses`), then PagoMinimo | IN PROGRESS | Corpus has none; delegated |
| C2.0b | Separation spike (MAKE-OR-BREAK, per slice): train/holdout, margin ≥0.15, ±3pt. STOP if no separation. | TODO | **HARD GATE: must GO before C2.1 merge** |
| C2.1 | `HeaderMoneyFieldGeometricSignals`/`ScoreHeaderMoneyField` + calibration; wire CL-21/22 operands; emit (goes LIVE — armed flag) | TODO | Only after C2.0b GO; non-vacuous slice |
| C2.2 | Verify CL-21/22 slice: rule-unit (non-vacuous) + calibration + armed demo-diff (CL-22 Pass/Fail is real) | TODO | No separate flag-flip — wiring is arming |
| C2.3 | Extend to `SaldoDeudorTotal`/`CreditoDisponible`/`SaldoCargosAMeses` (CL-24/25); calibrate-then-arm | TODO | |
| C2.4 | `PagoMinimo` slice — spike-gated; arm ONLY if C2.0b separates; else permanent-dark + logged caveat | TODO | Vacuous on real demo |
| C2.5 | Extend C1.5 architecture-enforcement drift-guard to new scored fields | TODO | |
| C2.C | Cl42/Cl49 date-window guard-gap — evaluate; log honestly if inert (do not build for scope's sake) | TODO | Slice C (weak; owner authorized but skeptical) |
| review | Adversarial review each increment (false-abstain / verdict-flip hunt) | ONGOING | anti-drift gate |

## Cardinal risk
False-abstain: geometric score under-rates a CLEAN pick → working GREEN/RED → false InsufficientData.
`PagoMinimo` is the sharpest case (legit value in far-right unconstrained fallback column). Mitigation =
calibrate-then-arm-dark discipline; STOP at C2.0b if no clean separation.

## Log
- 2026-07-17 — Epic scoped from two framing spikes (SCOPING doc). Owner ruled A→B→C + mini-party first.
  Mini-party launched.
