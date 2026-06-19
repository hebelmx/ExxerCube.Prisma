# RC1 — Validation / Computation Cluster (Veriqan VEC E11 "computation moat")

**Date:** 2026-06-18 · **Branch:** `Liv` · **Phase:** RC.1 (requirement → evidence trace)
**Auditor:** Claude Code (read-only; no production code modified)
**Scope:** FR-6, FR-7, FR-8, NFR-8, and the E11 recompute rules
(§20-WATERFALL / §19-INTERES / §6-SIMULACION / §8-INDICADORES / §16-OTRASLINEAS),
plus the tolerance + confidence-guard infrastructure that underpins them.

> **Bar (from Brief §7):** end-to-end evidence on **real input**, NOT a green unit test.
> A green rule test against a hand-built `StatementModel` is **not** readiness evidence.
> Believe wiring + file:line, never comments/prose.

---

## 0. Method note — what "Built-state class" means here

| Class | Meaning in this cluster |
|-------|-------------------------|
| **Real+Wired+E2E** | Computation is real, DI-wired into the engine, AND proven on a real/representative statement end-to-end. |
| **Real-unwired** | Real computation + DI-wired, but never exercised on real input (only hand-built models). |
| **Partial** | Real logic present but with a structural deferral (carry-forward, missing extractor branch, uncalibrated threshold) that materially limits it. |
| **Stub** | Placeholder; returns a fixed/degenerate answer. |
| **Missing** | Not implemented. |
| **Unknown** | Could not establish from wiring. |

> **Cluster-wide ceiling:** because **zero real CONDUSEF statements exist** in the corpus
> (RC0b §2.3 — only 3 `KnownSynthetic` non-compliant fixtures, no `KnownGood`/`KnownBroken`),
> **no rule in this cluster can reach `Real+Wired+E2E`.** The best attainable class for a
> correct, wired computation is **Real-unwired** (real + wired, but unproven on real data).
> This is the single dominating finding and it is not a code defect — it is a data/corpus gap.

---

## 1. Requirement → Evidence Trace

| Requirement-ID | Intent (one line) | Built-state class | Evidence (file:line / test / none) | Gap to E2E-readiness |
|---|---|---|---|---|
| **FR-6** (intra-statement arithmetic CL-10, 18..26) | Recompute resumen/nivel-de-uso identities within configurable tolerance; each Finding records the tolerance applied. | **Real-unwired** | Real formulas: `Cl21PagoParaNoGenerarInteresesRule.cs:140-147` (7-term identity), `Cl10CatRule.cs:140-147` (CAT formula + pct-pt tolerance), `Cl24/Cl25/Cl18/Cl19/Cl20` sum/identity rules; all resolve tolerance + record `ToleranceApplied`. Wired via Scrutor scan (`VeriqanValidationServiceCollectionExtensions.cs:53-57`). Tested 468/468 on hand-built `StatementModel` (RC0b §3.2). | Never run on a real statement; thresholds spec-derived not measured; correctness of column/field mapping vs a real PDF unproven. |
| **FR-7** (cross-period CL-17, 36..41) | Compare against Prior Statement values; absent prior → InsufficientData. | **Real-unwired** | `Cl17AdeudoPeriodoAnteriorRule.cs:119-128` abstains cleanly when `ctx.PriorStatement is null` or prior `PagoParaNoGenerarIntereses` null → InsufficientData (no false FAIL). Real comparison `:131-137`. | Prior-statement binding never exercised on a real two-period pair; no corpus with a real prior statement. |
| **FR-8** (configurable tolerance bands) | Every numeric check reads tolerance from config; changing it moves the pass/fail boundary **without code change**; tolerance recorded on each Finding. | **Partial** | Mechanism is real but the override channel is the **E9 TenantProfile** (`ResolvedTenantProfile.GetEffectiveTolerance` `:123-126`), NOT bundle `toleranceConfig`. Rules call `GetEffectiveTolerance(CheckId, legalDefault)` (e.g. `Cl21:83`). In the Worker `TenantProfile = LegalBaseline()` (single fixed profile, RC0b §AddVeriqanVerdict) whose `EffectiveTolerances` map is empty → **every rule always gets the legal default**. The PRD's bundle-`toleranceConfig`-drives-pass/fail intent (FR-8) is **not** the wired path. | The literal FR-8 acceptance ("changing `currencyToleranceMxn` in the bundle changes the boundary") is **not** demonstrable in the shipped Worker — only a per-tenant overlay (E13-gated) can move it. Spec→build drift on the *channel*. |
| **NFR-8** (abstain-safety / InsufficientData not false-FAIL) | Computation rules verify from the statement's own figures; low-confidence input → InsufficientData, never FAIL; false FAIL on a known-good statement is a design violation. | **Real-unwired** | Pattern is real and uniform: `ConfidenceGuard.BelowThreshold` (`ConfidenceGuard.cs:53-57`) gates every field; every rule returns `InsufficientData(...)` on missing/low-confidence (e.g. `Cl21:114-137`, `Section19:189-197`, `Section20:117-132`, `Section6:195-217`). Verdict aggregator **never** escalates InsufficientData to RED (`VerdictAggregator.cs:92-99, 121-130` — GREEN when all Pass/InsufficientData). Engine isolates rule exceptions → InsufficientData, batch continues (`VecValidationEngine.cs:102-115`). | **Proven only on crafted inputs.** The cardinal rule ("never false-block on a known-good statement") has **no real known-good statement** to test against — there are zero `KnownGood` specimens (RC0b §2.3). The property is *designed-in and unit-true*, not *empirically demonstrated*. |
| **§20-WATERFALL** (FR-31 / E11.2) | 7-column payment-distribution identity within typed tolerance; missing/low-conf cell → InsufficientData. | **Real-unwired** | Real identity `Section20PaymentDistributionRule.cs:143-151` (`|pagos| ≈ Σcols − saldoAFavor`); 7-cell confidence+kind+parse guards `:117-132`; dual verdict `:154-180`. Extractor exists: `PdfPigStatementFieldExtractor.ExtractSection20Table` (`:3680-3749`). Tested `Section20WaterfallRuleTests.cs` on hand-built tables. | Extractor never run against a real §20 table; column-order assumption (saldoAFavor at [6], signs) uncalibrated; no real §20 fixture. |
| **§19-INTERES** (FR-30 / E11.3) | Per-row `monto ≈ saldo×(tasa/360)×días`; Ordinarios rate = §10 tasa; low-conf rows → InsufficientData. | **Real-unwired** | Real recompute `Section19InterestPerRowRule.cs:216-217`; rate normalization with an explicit **ambiguous-zone abstain** `(1.0,1.5]` → null → InsufficientData `:403-442`; §10 cross-check `:300-344` skips (abstains) when either side absent/low-conf. Extractor `ExtractSection19Table` (`:3498-3588`). | Rate-scale heuristic (% vs fraction) is unvalidated against real cells; no real §19 fixture; the ambiguous-zone width is a judgement call. |
| **§6-SIMULACION** (FR-29 / E11.4) | Re-run Banxico Circular 13/2011 revolving-balance recursion for k∈{1,2,5}; computed months+interest match printed §6 within tolerance. | **Partial** | Recursion engine is real and pure (`PaymentSimulation.Run` `:398-436`): per-month interest, IVA 0.16 hard-coded constant `:94`, non-amortising guard + 600-iter cap `:405-433`. Rule guards + comparison `:236-338`. **BUT** the §6 extractor (`ExtractSection6Table:3904-3914`) returns `NotFound` whenever §6 absent, and **§6 is absent in all 3 fixtures** — so on every input available today the rule returns InsufficientData and **the recursion never executes**. (The §6 rule's own doc-comment `:26-33` is **stale**: it claims "no §6 extractor was built," but `ExtractSection6Table` exists and is wired at `:3377`. The real blocker is the absence of a §6-bearing specimen + calibration, not the extractor's absence.) | Carry-forward: §6 extractor is UNCALIBRATED (`:3885` "no §6 PDF fixture exists") and has never seen a present §6 table; IVA=16% hard-coded; ±1-month tolerance is engineering judgement. The whole moat-defining recursion is **dark on all real input**. |
| **§8-INDICADORES** (FR-32 / E11.5) | Three 12-month indicators present + non-negative; missing → FAIL; unreadable → InsufficientData. | **Real-unwired** (presence/shape only) | `Section8AnnualCostIndicatorsRule.cs`: presence + non-negativity (`:239-252`), confident-empty → FAIL (`:199-220`), low-conf empty → abstain (`:201-207`), `SectionNotFound`/`Indeterminate` → abstain (`:145-156`). **Explicitly NOT a recompute** — coherence check deliberately omitted to avoid false-FAIL (`:63-68`); registered tolerance is unused ("present for future use only"). Extractor `ExtractSection8Table:3393-3483`. | It is a structural/shape check, not a computation (acceptable per its own design); coherence-with-period-figures (the FR-32 "where derivable" clause) is **not** built. No real §8 fixture. |
| **§16-OTRASLINEAS** (FR-33 / E11.6) | Per-row interest-vs-rate/días + IVA-vs-interest reconcile; totals tie to §19; absent → N/A; low-conf → InsufficientData. | **Partial** | Real per-row arithmetic: Check-1 interest (`:269`), Check-2 IVA×0.16 (`:291`), Check-3 §19 totals-tie cross-check (`:385-441`). Conditional-absent → **Pass/N-A** (`:167-177`). Extractor `ExtractSection16Table:3762-3823`. | **Self-declared corpus-gated**: the code itself says column-mapping accuracy is unverified — "no real §16 fixture available to calibrate column positions" (`:83-86`, `:355-356`, `:364-365`). The 9-column index map (`:98-102`) is a guess until a real §16 statement lands. |
| **Tolerance infra** (FR-8 backing / AR-6) | Tolerances are typed, range-bounded **data** (not code constants) with a legal default + permitted [Min,Max]. | **Partial** | `Tolerance` record enforces Min≤Default≤Max + `Resolve` rejects out-of-range overrides (`Tolerance.cs:54-112`). `DefaultLegalToleranceProvider.cs` registers all cluster checks (`:98-131`). **BUT** the values are **in-code constants**, and the ceilings are **explicitly flagged `⚠️ ESTIMATED`** (`:64-92`) — "engineering estimates derived from the rounding math … should be confirmed by a legal/compliance review." Defaults (0.50 MXN etc.) are **spec-derived from the Acuerdo rounding math, NOT measured against a corpus.** | Not "data" in the AR-6 sense yet (no external config/store drives them in the Worker — SQL path exists but Worker has no connection string, RC0b §Persist). Ceilings uncalibrated + legally unconfirmed. |
| **ConfidenceGuard** (NFR-8 backing) | Field below confidence threshold → abstain. | **Real-unwired** | `ConfidenceGuard.cs:53-57` (`< threshold`), boundary `>=` passes; default 0.8 (`TenantProfile.LegalMinFieldConfidenceDefault`). Used uniformly across the cluster. Tested `ConfidenceGuardTests.cs`. | The 0.8 threshold is a fixed default, never tuned against a real extractor's confidence distribution; per-rule confidence calibration is corpus-gated. |
| **VecValidationEngine** (NFR-5/NFR-6) | Deterministic ordering; one rule's failure/exception never halts the batch; stamps DOF numeral. | **Real-unwired** | `VecValidationEngine.cs:69-122`: try/catch per rule → InsufficientData + continue (`:102-115`); deterministic CheckId sort (`:125-126`); DOF numeral stamped centrally (`:119`). | Determinism/isolation never stress-tested on a real malformed-PDF batch at volume (RC0b §9). |

---

## 2. Deferral-Debt Inventory

Every `InsufficientData`-by-design, uncalibrated, carry-forward, or estimated item found in this cluster:

| # | Item | Kind | Location | Impact |
|---|------|------|----------|--------|
| D1 | **All tolerance ceilings `⚠️ ESTIMATED`** (CurrencyMxn max 1.00, Points max 2.00, ExchangeRate [0.05,0.20], RewardsPesos max 2.00) | Uncalibrated + legally-unconfirmed | `DefaultLegalToleranceProvider.cs:64-92` | Out-of-range tenant overrides are rejected against an *unconfirmed* ceiling; legal review pending. |
| D2 | **Tolerance defaults spec-derived, not measured** (0.50 MXN etc. from Acuerdo rounding math) | Uncalibrated | `DefaultLegalToleranceProvider.cs:61-65` | The pass/fail boundary on every arithmetic rule rests on an unverified assumption about real rounding behaviour. |
| D3 | **§6 recursion is dark on all real input** — §6 extractor UNCALIBRATED, §6 absent in every fixture | Carry-forward + corpus-gated | `PdfPigStatementFieldExtractor.cs:3885,3904-3914`; `Section6PaymentSimulationRule.cs:26-33` (stale doc) | The moat-defining recursion has **never executed** on a present §6 table; effectively unproven. |
| D4 | **§16 column-mapping accuracy corpus-gated** (9-col index map is a guess) | Corpus-gated (self-declared) | `Section16OtherCreditLinesRule.cs:83-86,355-356,364-365` | Could mis-map columns on a real §16 → false-FAIL or false-PASS; unverifiable today. |
| D5 | **§19 rate %-vs-fraction normalization heuristic** + ambiguous-zone `(1.0,1.5]`→abstain | Uncalibrated heuristic | `Section19InterestPerRowRule.cs:403-442` | A real cell landing in the ambiguous zone abstains; threshold boundaries unvalidated against real rate prints. |
| D6 | **§8 coherence-with-period-figures not built** (presence/non-neg only; tolerance registered but unused) | Deliberately deferred | `Section8AnnualCostIndicatorsRule.cs:63-68` | FR-32 "coherent within tolerance where derivable" clause is unmet. |
| D7 | **IVA = 16% hard-coded** in §6 and §16 | Hard-coded constant | `Section6PaymentSimulationRule.cs:94`; `Section16OtherCreditLinesRule.cs:104` | Period-specific or future IVA change silently wrong until manually updated. |
| D8 | **§6 ±1-month tolerance** is engineering judgement | Uncalibrated | `Section6PaymentSimulationRule.cs:107` | Boundary-rounding tolerance unvalidated against real §6 columns. |
| D9 | **MinFieldConfidence 0.8 default** never tuned to a real extractor's confidence distribution | Uncalibrated | `TenantProfile.LegalMinFieldConfidenceDefault`; used cluster-wide | Too high → over-abstain (low coverage); too low → false-FAIL/PASS. Unknown which, with no corpus. |
| D10 | **FR-8 bundle-`toleranceConfig` channel not wired** — only TenantProfile overlay (E13-gated) can move tolerances | Spec→build drift | rules use `GetEffectiveTolerance`; Worker uses fixed `LegalBaseline()` | The literal FR-8 acceptance is not demonstrable in the shipped composition. |
| D11 | **CL-17 cross-period never exercised on a real prior/current pair** | Corpus-gated | `Cl17AdeudoPeriodoAnteriorRule.cs` | Prior-statement binding path unproven end-to-end. |

**Deferral-debt count: 11.**

---

## 3. Biggest readiness gaps (≤5)

1. **No real corpus → the entire cluster is capped at `Real-unwired`.** Every computation is unit-true against hand-built `StatementModel`s and **never against a real CONDUSEF statement** (zero `KnownGood`, zero `KnownBroken` — RC0b §2.3). The "468/468 green" headline proves the formulas compute as the test author specified; it proves **nothing** about behaviour on a real bank statement. This is the dominating gap.
2. **The §6 moat is dark.** §6-SIMULACION is the headline differentiator (Banxico recursion), yet on **every available input** it returns InsufficientData because no fixture contains a §6 table and the §6 extractor is uncalibrated (D3). The recursion engine has never run inside the pipeline on a present §6.
3. **Tolerance thresholds are spec-derived, not measured, and ceilings are flagged ESTIMATED + legally-unconfirmed** (D1, D2). The pass/fail boundary of *every* arithmetic rule is an unverified assumption — exactly the "uncalibrated against real data" risk the brief warns about.
4. **§16 column mapping is a self-declared guess** (D4). On a real §16 the 9-column index map could be wrong, and the code *itself* admits it cannot be verified without a corpus — a latent false-FAIL/false-PASS source on a conditional section.
5. **FR-8 channel drift** (D10): the PRD's "change the bundle tolerance config, move the boundary, no code change" is not the wired path; the only override mechanism is the E13-gated per-tenant overlay, and the Worker pins a single empty `LegalBaseline()` profile.

---

## 4. Per-class tally (this cluster)

| Class | Count | Items |
|-------|-------|-------|
| **Real+Wired+E2E** | **0** | (impossible without a real corpus) |
| **Real-unwired** | **7** | FR-6, FR-7, NFR-8, §20-WATERFALL, §19-INTERES, §8-INDICADORES, ConfidenceGuard/Engine |
| **Partial** | **4** | FR-8, §6-SIMULACION, §16-OTRASLINEAS, Tolerance-infra |
| **Stub** | **0** | — |
| **Missing** | **0** | — |
| **Unknown** | **0** | — |

> Net: the computation code is **genuinely real** — these are honest arithmetic identities with
> uniform, well-engineered abstain-safety, not shape-checks dressed up as computation. The gap is
> **not** "the moat isn't built"; it is "**the moat has never touched real water.**" Readiness is
> blocked by the corpus/calibration data-acquisition task (corpus-gated), plus one spec→build
> channel drift (FR-8, technical) and one carry-forward (§6 extractor calibration).

---

## 5. Adversarial notes (for RC.2 hand-off)

- **The §6 rule doc-comment is provably stale** — it asserts the §6 extractor "has NOT yet been built," but `ExtractSection6Table` exists and is wired (`PdfPigStatementFieldExtractor.cs:3377,3904`). *Believe the wiring, not the prose.* The true state is "extractor exists but is uncalibrated and never fed a present §6."
- **"468/468 green" must not be cited as readiness.** Substrate is 100% hand-built `StatementModel` (`Section6SimulationRuleTests.cs:27` says outright there are "no real PDF fixtures with a §6 table"). Tautological to its fixtures.
- **The cardinal abstain-safety property is designed-in but empirically unproven** — there is no known-good real statement to confirm "never false-blocks." A skeptic should treat NFR-8 as *asserted*, not *demonstrated*.
- **FR-8 acceptance text is not satisfiable in the shipped Worker.** Worth refuting directly in RC.2.
