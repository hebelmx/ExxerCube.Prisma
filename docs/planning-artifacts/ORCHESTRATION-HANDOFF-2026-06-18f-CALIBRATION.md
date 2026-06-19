# Veriqan VEC — Continuation Handoff (Corpus Calibration Harness landed; E13 still gated)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Supersedes:** `ORCHESTRATION-HANDOFF-2026-06-18e-E13.md`
(which remains accurate for the E1–E12 done-state). The entire **technical build roadmap (E1–E12) is
COMPLETE**; **E13 stays ROADMAP-GATED on issue #17** (economic-buyer discovery — still OPEN). This
session added the **corpus calibration harness** (the ungated prep the owner chose) as a purely additive
test/tooling deliverable.

---

## 1. What landed this session — the calibration harness

Owner ruling (2026-06-18): with E13 gated, build the **corpus calibration harness** — the turnkey
machinery so that *when* a real labelled CONDUSEF corpus lands, calibration is config-and-run, not a code
project. **Purely additive: ZERO production-assembly edits** (`git diff` against all `01/02/03` Veriqan
production projects is empty). It lives inside the existing test project
`08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/` and reuses the proven
`VerificationPipelineEndToEndTests` spine (`IVerificationPipeline.ProcessAsync` → `VerificationOutcome`).

**Components (all under `.../Calibration/`):**
- `CorpusSchema.cs` — labelling schema: `SpecimenLabel {KnownGood, KnownSynthetic, KnownBroken}`,
  `CorpusSpecimen` (bind-bundle params + `IntendedDefects` + two suppression buckets: `AllowedFails`
  (ref-data gaps) and `KnownFixtureDefects` (genuine PDF non-compliance)), `CorpusManifest`.
- `CalibrationHarness.cs` — runs the pipeline per specimen (fresh DI graph + per-specimen fake bundle),
  aggregates per-CheckId Pass/Fail/InsufficientData + DetectionRate/FalsePositiveRate, computes
  `NewFails = Fails \ (AllowedFails ∪ KnownFixtureDefects ∪ IntendedDefects)`, and read-only
  threshold-evidence probes (TYPO-MINSIZE pt, SEC-SIZECAP §17=25%/§21·§28=33% page-fraction,
  ADS-PLACEMENT §12 chars, currency residuals).
- `CalibrationReportRenderer.cs` — Markdown report: honesty banner, label breakdown, per-CheckId matrix,
  two distinct suppression-bucket columns, threshold-evidence tables, **Proposed-NOT-applied**
  recommendations.
- `CalibrationDriverTests.cs` — `[Fact] Calibration_SyntheticCorpus_NoNewFalseFails`: resolves the
  manifest via repo-root walk (no hardcoded path), `Assert.Skip` when absent, asserts Result success +
  **empty NewFails per KnownGood/KnownSynthetic** (cardinal-rule regression guard) + findings>0 + report
  written. Deterministic.
- `Prisma/Fixtures/PRP2/corpus-manifest.json` — seed: the 3 Dummie VEC PDFs (reused, not copied).
- `docs/qa/calibration/README.md` — schema + threshold→CheckId→DOF calibration map + how-to-add-specimen.
- `docs/qa/calibration/calibration-report.md` — generated artifact (regenerated each test run).
- `docs/planning-artifacts/CORPUS-CALIBRATION-HARNESS-DESIGN-2026-06-18.md` — the intended-solution anchor.

**Green footprint:** `Veriqan.Orchestration.Tests` **37/37** (36 prior + 1 driver), build 0/0.
**Adversarially reviewed** (1 skeptic, vs the design doc) → 3 findings remediated (below).

## 2. The big honest finding (acted on) — the synthetic fixtures are NOT compliant

The harness immediately revealed that the 3 PRP2 "Dummie VEC" fixtures **genuinely violate many CONDUSEF
format rules** — they are simplified placeholders, not compliant statements:
- `LAW-TYPO-MINSIZE` — body text at **5.04 pt** (word `'11'`, page 1; floor is 8 pt). Real breach.
- `LAW-SEC-PRESENCE` — **~9 mandatory sections missing** (§2,3,4,5,9,10,14,15[,24]).
- `LAW-SEC-ORDER-GAP` — sections out of legal order.
- `CL-35` non-Aptos fonts; `CL-31` pagination mismatch; `CL-34` card-number gaps; `CL-28` text overlap.
- `LAW-§17-LEGENDS / §26-NOTAS / §27-GLOSARIO` — verbatim mandatory legends absent (similarity 0.3–0.75).

**Remediation (adversarial-review fix):** relabelled the 3 specimens `KnownGood → KnownSynthetic`, and
**split the suppression** so genuine non-compliance is NOT laundered into a green test:
- `AllowedFails` (ref-data/bundle-approximation only): **CL-42** (all 3) + **CL-18, CL-52, CL-53** (spec 02).
- `KnownFixtureDefects` (genuine PDF non-compliance): everything else above.
The report banner now reads **"KnownGood: 0 · KnownSynthetic: 3 (non-compliant placeholders — NOT
compliance evidence) · KnownBroken: 0"**, and the per-specimen table tags each Fail to its exact bucket.
Two other review fixes: §17 SIZECAP probe now prints the correct **25%** cap (was 33%); driver test
resolves the manifest relatively + uses `Assert.Skip`.

## 3. What this is worth + what it still cannot do (honest limits)
- The harness is a **turnkey regression guard + evidence generator**, not a calibrator yet. With **0
  KnownBroken specimens** every threshold remains **UNCALIBRATED** — detection power (true-positive rate)
  is unmeasured; all distributions are good-only. The report says this loudly.
- The cardinal-rule guard catches a **net-new** Fail on a known specimen; it does not measure drift inside
  the already-suppressed set (inherent to a snapshot guard; mitigated by the honest bucket split).
- It does **not** auto-apply any threshold change (legal/compliance decision; forbidden by the cardinal
  rule that a preventive gate must never false-Fail).

## 4. Highest-value next moves (unchanged priority order)
1. **Acquire a real CONDUSEF-statement corpus** — known-good AND deliberately-broken (one broken specimen
   per calibratable rule). This is THE lever: it makes the harness actually calibrate (E11 §6/§16 extractor
   X-ranges, §20 saldo-a-favor sign, E12 8/10pt + §12 + ¼/⅓ + ads thresholds), and turns the good-only
   distributions into separating margins. Drop PDFs in `Prisma/Fixtures/PRP2/`, add manifest rows
   (`label`, `bundle`, `intendedDefects`/`knownFixtureDefects`), run the calibration test, read the report.
2. **(Optional) Fix the synthetic generator** so the Dummie VEC fixtures are actually compliant (≥8pt text,
   Aptos fonts, all 28 sections, verbatim legends) — or accept them as `KnownSynthetic` placeholders. See
   the P0/P1/P2 table in `docs/qa/calibration/README.md`.
3. **E13** only once issue #17 (economic buyer) resolves — stories 13.1–13.4 in `epics.md` §13.

## 5. Orchestration mechanics (heeded, reconfirmed)
- Verify every chunk from ground truth (`dotnet build`/`dotnet test` + `git status`/diff); subagents
  misreport counts. `dotnet test <csproj>` plain — never `--nologo` (MTP → "Zero tests ran").
- The harness is in the ONE test project that already references Orchestration + all infra + the working
  xunit.v3.mtp-v2 / MTP 2.1.0 setup → no new `.csproj`, no `.sln`/package churn, no MTP fragility.
- E: filesystem SLOW (builds ~2–4 min, test ~17 s). Prefer `git ls-files`. Be patient.
- Tracker for this session: TaskCreate/Update IDs 1–6 (C.1–C.4 + review + R1 remediation).

## 6. Authoritative artifacts
`docs/planning-artifacts/`: this handoff, `CORPUS-CALIBRATION-HARNESS-DESIGN-2026-06-18.md` (design anchor),
`ORCHESTRATION-HANDOFF-2026-06-18e-E13.md` (E1–E12 done-state), epics.md, architecture.md.
`docs/qa/calibration/`: README (schema + calibration map) + calibration-report.md (generated).
Memory `veriqan-vec-progress.md` + `MEMORY.md` current.
