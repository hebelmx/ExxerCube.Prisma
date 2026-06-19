# Veriqan — Corpus Calibration Harness (Intended-Solution Design)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Status:** design (anchor doc — implementation tracked separately)
**Owner ruling (2026-06-18):** build a corpus calibration harness as the next ungated move while E13
stays roadmap-gated on issue #17. This document is the **intended-solution spec**: all delegated
implementation and all adversarial review must be checked against *this*, not against agent prose.

---

## 1. Why this exists (the carry-forward it closes)

Every E11/E12 threshold is currently **synthetic-only / spec-derived, not calibrated against real data**
(see `ORCHESTRATION-HANDOFF-2026-06-18e-E13.md` §3). The 3 PRP2 "Dummie VEC" fixtures are synthetic and
all known-good. There is **no real CONDUSEF-statement corpus**, and acquiring one (known-good +
deliberately-broken FORM/typography specimens) is the single highest-value next step.

The harness does **not** invent a corpus and does **not** auto-tune thresholds. It builds the **turnkey
machinery** so that *when* a real labelled corpus lands, calibration is a config-and-run operation, not a
code project:
- a **labelling schema** (manifest) describing each specimen and its expected verdicts,
- an **evaluation harness** that runs the real pipeline over the labelled corpus and scores findings
  against the labels (confusion matrix per CheckId + false-Fail guard),
- a **threshold-evidence report** that surfaces the measured-quantity distributions (split good vs broken)
  so a human can choose each threshold *with evidence*, and
- **recommendations only** — never auto-applied threshold edits (the cardinal rule forbids a preventive
  gate from ever false-Failing; a threshold change is a legal/compliance decision).

## 2. Cardinal rule (inherited, non-negotiable)

A preventive gate must **never false-Fail**. The harness's primary regression value is exactly this: it
proves the shipped rules produce **no new Fail** on a known-good statement. Any harness assertion or
recommendation that could push a rule toward false-Failing is a design violation.

## 3. Placement & non-invasiveness (hard constraint)

- The harness is **purely additive**. It MUST NOT modify any file under
  `01 Core/Veriqan.*`, `02 Infrastructure/Veriqan.*`, `03 Orchestration/Veriqan.Orchestration`, or any
  existing rule/domain/extractor. Zero production-code edits. (If a genuine read-only seam is missing,
  surface it — do not edit a rule to expose internals.)
- It lives **inside the existing test project** `08 Tests/03 Orchestration/Veriqan.Orchestration.Tests`
  (namespace `ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration`). That project already references
  Orchestration + all five infra adapters + ReferenceData and already has the working xunit.v3.mtp-v2 /
  MTP 2.1.0 setup. **No new `.csproj`, no `.sln` edits, no package changes.**
- It reuses the proven end-to-end spine `VerificationPipelineEndToEndTests` (same DI graph, same
  `IVerificationPipeline.ProcessAsync`).

## 4. The pipeline spine (already exists — reuse verbatim)

```
StatementSubmission(pdfBytes, fileName, StatementContextKey)
  → IVerificationPipeline.ProcessAsync(submission, ct)   // Result<VerificationOutcome>
      VerificationOutcome.Summary.Signal  : VerdictSignal { Green, Red, Blocked }
      VerificationOutcome.Findings        : IReadOnlyList<RuleFinding>
        RuleFinding { CheckId, Verdict (Pass|Fail|InsufficientData), DofNumeral,
                      Severity, Expected?, Observed?, ToleranceApplied?, Locator? }
```

DI graph to stand up (copied from `VerificationPipelineEndToEndTests`): `AddVeriqanIngestion/Binding/
Verdict` + `AddVeriqanExtraction/Validation/Visual/Reporting` + `AddVeriqanInMemoryPersistence` +
`VeriqanMetrics` singleton + `IVerificationPipeline` + `TimeProvider.System`, with a fake
`IVecReferenceDataProvider` returning the **per-specimen** bundle built from manifest fields.

Each specimen binds only if its reference bundle resolves the product token the extractor reads from the
PDF. The manifest therefore carries the minimum bundle fields (institution, period, product id + token +
aliases, ordinary rate, credit line, annual commission, required font family, banking-year days).

## 5. Components to build (all under `.../Calibration/`)

### 5.1 Labelling schema (POCOs + JSON)
- `SpecimenLabel` enum: `KnownGood`, `KnownBroken`, **`KnownSynthetic`**.
  - **`KnownGood`** = a statement believed CONDUSEF-compliant; it must pass all rules modulo
    `AllowedFails` that are *purely omitted-optional-reference-data* gaps. **Do not label a statement
    `KnownGood` if it genuinely violates a format/typography/structure rule.**
  - **`KnownSynthetic`** = a synthetic/placeholder fixture that is NOT a compliant statement and is
    known to violate format rules; used to exercise the pipeline, never as compliance evidence. Treated
    like KnownGood for the no-new-Fail guard (no intended defects) but reported distinctly and never
    described as "good."
  - **`KnownBroken`** = a deliberately-defective specimen carrying `IntendedDefects` a rule MUST catch.
- `CorpusSpecimen`:
  - `FileName` (relative to the corpus dir), `Label`, `Description`/`Notes`.
  - `Bundle` params needed to bind: `Institution`, `PeriodLabel`, `PeriodStart`, `PeriodEnd`,
    `ProductId`, `ProductName`, `ProductToken`, `Aliases[]`, `AnnualOrdinaryRate`, `CreditLine`,
    `AnnualCommissionMxn`, `RequiredFontFamily`, `BankingYearDays`. (Mirror `BuildFakeBundle`.)
  - `IntendedDefects`: `IntendedDefect[]` — for KnownBroken only — each `{ CheckId, ExpectedVerdict =
    Fail, DefectNote }` describing the deliberately-injected violation the rule MUST catch.
  - **`AllowedFails`**: `{CheckId, Reason}[]` — Fails permitted ONLY because *optional reference data was
    omitted from the synthetic bundle* (e.g. faked RFC/rate/period). These are NOT statement defects.
  - **`KnownFixtureDefects`**: `{CheckId, Reason}[]` — Fails that are *genuine non-compliance properties
    of the fixture PDF itself* (sub-floor typography, non-Aptos font, missing mandatory sections, absent
    verbatim legends, pagination/overlap). Kept in a SEPARATE bucket so a green test can NEVER be read as
    "this statement is compliant." This split is the honesty fix from the 2026-06-18 adversarial review.
  - **No-new-Fail guard:** any `Fail` CheckId NOT in `AllowedFails ∪ KnownFixtureDefects ∪
    IntendedDefects` is a **regression / new false-Fail** → the driver test fails.
- `CorpusManifest`: `{ CorpusDir, Specimen[] }`, deserialized from `corpus-manifest.json`
  (System.Text.Json, source-gen optional). Located at `Prisma/Fixtures/PRP2/corpus-manifest.json`,
  reusing the 3 existing synthetic PDFs — **no PDF copying**.
- Seed manifest: the 3 Dummie VEC fixtures as **`KnownSynthetic`** (they are NOT compliant — they fail
  typography/structure/legend rules), with the two suppression buckets split per the classification
  above and captured from a real run (see §7). Zero `KnownBroken` specimens initially — the report must
  say so loudly AND must make the non-compliance of the synthetic fixtures unmistakable.

### 5.2 Calibration harness engine (`CalibrationHarness`)
- Input: a `CorpusManifest` + an `IVerificationPipeline` factory (build the DI graph once; swap the fake
  provider's bundle per specimen, e.g. provider returns a bundle keyed by the submission's ContextKey).
- For each specimen: read PDF bytes → `ProcessAsync` → collect `(Signal, Findings)`.
  - Missing PDF file → record `SpecimenSkipped` (do not throw; mirror the e2e graceful-skip).
- Aggregate into a `CalibrationResult`:
  - Per **CheckId** across the corpus: counts of `Pass` / `Fail` / `InsufficientData`.
  - Per **KnownGood** specimen: `NewFails` = `Fail` CheckIds ∉ `AllowedFails` → **must be empty** (the
    cardinal-rule guard).
  - Per **KnownBroken** specimen: for each `IntendedDefect`, did the named CheckId actually `Fail`?
    → `Detected` (TP) / `Missed` (FN). And any `Fail` on a *different* KnownGood-shared check that the
    defect did not intend = candidate false-positive (note it).
  - Corpus-level per-CheckId: **DetectionRate** (TP/(TP+FN) over specimens that intend it) and
    **FalsePositiveRate** (KnownGood specimens where it newly Failed / KnownGood count).
- **Threshold-evidence probe** — for the calibratable CheckIds, tabulate the measured quantity split by
  label. Source the quantity **read-only**, preferring the `StatementModel` the harness already holds (it
  can call `ExtractFullAsync` once per specimen and inspect `TypographySamples`, `Sections` geometry,
  section text lengths) and/or parse `RuleFinding.Observed`. Probes (initial set):
  - `LAW-TYPO-MINSIZE`: distribution of real-word body `PointSize`, and the located fecha-límite size.
  - `LAW-SEC-SIZECAP`: per-target-section page-fraction (§17, §21, §28).
  - `LAW-ADS-PLACEMENT`: §12 character length.
  - `LAW-§19-INTERES` / currency rules: recompute residual (|reported − computed|) where Observed exposes it.
  - Each probe row: `{ CheckId, Specimen, Label, MeasuredValue, CurrentThreshold, Margin }`.
- Everything returns via `Result<T>`; no exceptions for control flow; `CancellationToken` honored.

### 5.3 Report renderer (`CalibrationReportRenderer`) + driver test
- Renders a Markdown report:
  1. **Honesty banner** — corpus size, #KnownGood, #KnownBroken; if `KnownBroken == 0`: a bold notice
     that **thresholds remain UNCALIBRATED — detection power is unmeasured until deliberately-broken
     specimens are added**, and that all distributions below are one-sided (good-only).
  2. **Per-CheckId verdict matrix** across the corpus (Pass/Fail/InsufficientData counts + Detection /
     FalsePositive rates where defined).
  3. **Threshold-evidence tables** (one per probe) with the current threshold + the good-vs-broken
     measured distribution + the separating margin (or "good-only, no separation measurable yet").
  4. **Recommendations** — explicitly flagged *Proposed, NOT applied*; empty/▢ until a corpus with both
     classes exists. Never an instruction to loosen a gate below legal floor.
- Output: write to `docs/qa/calibration/calibration-report.md` (repo-tracked, reviewable). Harness locates
  repo root by walking up from `AppContext.BaseDirectory` to the dir containing `Prisma/Fixtures` (or the
  `.sln`); fall back to skip-write if not found (never throw).
- **Driver `[Fact]` `Calibration_SyntheticCorpus_NoNewFalseFails`**:
  - Skips gracefully if the corpus dir / manifest is absent (CI may lack the binary fixtures).
  - Runs the harness over the seed manifest.
  - Asserts: (a) `Result` success; (b) every KnownGood specimen has **empty `NewFails`** (cardinal-rule
    regression guard); (c) at least one finding per specimen (proves rules ran); (d) report written.
  - Must be **deterministic** (rules are deterministic; same PDFs → same findings). No flakiness.

### 5.4 Docs + carry-forward
- `docs/qa/calibration/README.md`: the schema, the **threshold → CheckId → DOF-numeral calibration map**
  (the full inventory: 8/10pt floors + 0.25 ε; §12 700/805; ¼/⅓ page + 0.02 ε; 7-phrase ads allowlist;
  §19 rate 1.5/1.0 + 0.0005 ε; currency 0.50 MXN bands + the ⚠️ estimated ranges), how to add a
  known-good and a deliberately-broken specimen, and the turnkey run command.
- Update memory (`veriqan-vec-progress.md`, `MEMORY.md`) and write a short continuation handoff.

## 6. Out of scope (explicitly)
- No new `.csproj`/`.sln`/package changes. No console exe (documented future option).
- No edits to any rule, domain type, extractor, or DI extension in production assemblies.
- No auto-application of any threshold change. No fabricated "broken" specimens to inflate coverage
  (a synthetic broken specimen may be added **later, clearly labelled synthetic**, only to exercise the
  detection path — never presented as real calibration evidence).
- No network, no real corpus acquisition (that is a human/business data task).

## 7. Verification (ground-truth gates the orchestrator runs)
- `dotnet build` of `Veriqan.Orchestration.Tests` (and its deps) → **0 errors / 0 warnings**
  (TreatWarningsAsErrors is on).
- `dotnet test` of `Veriqan.Orchestration.Tests` → all prior tests still green **and** the new driver
  test green. (Plain `dotnet test <csproj>`, no `--nologo`.)
- Inspect `git diff`: confirm **only** files under the test project's `Calibration/` folder, the
  `corpus-manifest.json`, and `docs/qa/calibration/**` changed — **no production assembly touched**.
- Read the emitted `calibration-report.md` and confirm the honesty banner correctly reports
  "0 KnownBroken → thresholds uncalibrated".
- Capture `AllowedFails`: run once, read the actual Fail CheckIds per synthetic specimen from the report,
  encode them into the manifest as the golden set, re-run → green. Document each AllowedFail's reason
  (almost all = omitted optional reference data, NOT a typography/geometry defect).

## 8. Story breakdown (tracker)
- **C.1** Schema POCOs + JSON manifest + seed (3 synthetic KnownGood) + `AllowedFails` capture.
- **C.2** `CalibrationHarness` engine (run pipeline per specimen, aggregate, threshold probes).
- **C.3** `CalibrationReportRenderer` + driver `[Fact]` + emit report artifact.
- **C.4** Docs (`README.md` + calibration map) + memory + handoff.
- **Review** adversarial gate against this doc (cardinal-rule + non-invasiveness + honesty banner).

C.1→C.3 are interdependent and all touch the one test project → **serialize on a single dev agent** (the
recurring "parallel agents on the same project RACE" gotcha). C.4 + review follow.
