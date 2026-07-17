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
| C2.0a | Generator: clean + decoy specimens — CL-21/22 operands (`SaldoCargosRegulares`, `PagoParaNoGenerarIntereses`) | ✅ DONE `98540e38` | 6 specimens (2 clean + 4 decoy, both profiles), verified vs LIVE extractor. Extraction 456/456. Decoy mis-picks: Saldo 500091/480033, Pago 7654/9871 |
| C2.0b | Separation spike (MAKE-OR-BREAK): prototype HeaderMoney scorer on real PdfPig tokens; train(dummievec)/holdout(realbanamex), margin ≥0.15, ±3pt. | ✅ **GO** | Both fields separate: clean=1.0, decoy=0.55×0.65=**0.3575**, **margin 0.64**, blind holdout, 50-trial ±3pt robust. Constants: RankNotAdjacent 0.55 · CompetingAmount 0.65 · RankAdjThreshold 1. **CAVEAT (honest): 2 signals perfectly CORRELATED on this corpus** (rightmost-wins ⇒ every misread-decoy is both rank-far AND competing) — separation proven, independence NOT. Orch 207/0. Also fixed a C2.0a honesty-test regression (corpus 21→27; 3 realbanamex excluded as Product-OCR). **GATE PASSED → C2.1 unblocked.** |
| C2.rev | Adversarial review of C2.0a/C2.0b (2 lenses) | ✅ DONE | Honesty lens: SOUND (empirically proven realbanamex→Product-OCR; no god's-eye swap; counts execute-verified). Spike lens: narrow GO reproduced + prototype/production fidelity CONFIRMED, but **2 MAJOR findings reshape C2.1** (below). |
| **C2.1** | Promote prototype → production `HeaderMoneyFieldGeometricSignals`/`ScoreHeaderMoneyField` + `FieldCalibrationTable.HeaderMoney`; wire `ExtractNivelDeUsoField` + `ExtractPagoParaNoGenerarIntereses`; emit goes **LIVE** (armed flag) | TODO — **now carries review acceptance criteria** | Only after the AC below. Non-vacuous slice. **DO NOT wire the prototype verbatim.** |

### C2.1 acceptance criteria (from adversarial review — MUST hold before arming)
- **AC1 (Finding #1, MAJOR/blocking): fix the footnote-digit false-abstain.** A clean pick with a harmless extra digit in the band (documented real shape `PdfPigStatementFieldExtractor.cs:1115` `"Pago para no generar intereses 2 $32,446.69"`) currently scores 0.3575 (= a decoy) though the value is CORRECT. Fix: exclude footnote-marker digits from candidacy/rank (e.g. bare small-integer tokens, or a value-magnitude/`$`-required filter, or align candidacy to what the prod extractor itself would pick), so signal #2 counts only candidates that could actually change the rightmost pick. **Add a `clean-*-footnote` specimen (correct value + stray digit) that MUST score ≥0.8**, and re-run the spike before arming.
- **AC2 (Finding, QA lens): tighten decoy `expectedStatus`.** On arming, the 4 decoy specimens' misread fields must move `Extracted` → an abstain/low-confidence status in their manifests, or the honesty suite passes them for the wrong reason (same lifecycle as C1.4/C1.6).
- **AC3 (Finding #4): close the `GetBand` fidelity gap** — the production scorer must use the literal prod band helpers (`GetBand`/`GroupIntoBands`, `:2622-2654`), not the spike's simplified per-word filter.
- **AC4 (Finding #2): log the wrong-row blind spot** — a decoy that REPLACES the true value (sole candidate, rank-1, wrong value) scores 1.0 confident-wrong; out of scope for this cross-column-leak slice but must be recorded as a residual limitation (no silent caps).
- **AC5 (Findings #3/#5): reconsider signal #2's net value** before inheriting it (it produces AC1's false positive), and disclose that constants are C1.0b-reuse, not corpus-derived.
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
