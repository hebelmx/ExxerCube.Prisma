# Handoff — Mutation testing: Imaging §2.3 COMPLETE (Units 21–24), what's next (2026-06-09)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (canonical guide — setup, per-unit results, all lessons;
Units 21–24 are this session) and `docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan).
**Supersedes** `HANDOFF-2026-06-08-mutation-export-adaptive-done.md` for the "next units" question.
**Branch:** `Kt2` — Units 21–24 are **committed** (`97abde1`, `d244048`, `c0bd467`, `e170ed6`) + this docs commit.
Build green, Imaging test project green (**180/180**). ⚠️ Commits are **local — `git push` not yet done.**

---

## 1. What this session did — Units 21–24: Imaging (roadmap §2.3 COMPLETE)

Eight files in `02 Infrastructure/Infrastructure.Imaging`, driven by the already-existing
`Tests.Infrastructure.Imaging` (added a `stryker-config.json`). Project **44 → 180 green**.

| Unit | File(s) | Result | Tests |
|---|---|---|---|
| 21 | `LevenshteinTextComparer.cs` | 0 cov → Killed 0→151, 0 killable survivors | +40 |
| 22 | `FeatureNormalizer` / `PolynomialModel` / `TrainedPolynomialModel` | Killed +19 (10/50/29), 0 killable | +29 |
| 23 | `PolynomialImageQualityAnalyzer.cs` | 0 cov → Killed 0→54, 0 killable (true 77.78 %) | +14 |
| 24 | `Analytical`/`Default`/`Polynomial` `FilterSelectionStrategy.cs` | **tests green; Stryker verify DEFERRED** | +53 |

Native-bound code was skipped as planned: `AdaptiveEnhancementFilter`, `PolynomialEnhancementFilter`,
`EmguCvImageQualityAnalyzer`, `OpenCvAdvancedEnhancementFilter`, `PilSimpleEnhancementFilter`, `NoOp*`/`Stub*`.

## 2. ⏳ The one loose end — finish Unit 24's mutation verification FIRST

Unit 24's three filter-selection strategy files have **53 exact-value tests, all green (180/180)**, but the
Stryker mutation run that confirms "Killed delta + 0 killable survivors" was **deferred** (see §3). They are pure,
managed, deterministic, value-exact by construction — the verification is expected to be clean. To finish:

```bash
cd "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Imaging"
# temporarily scope the mutate list to ONLY the three strategy files (managed → no native crash popups):
#   "**/AnalyticalFilterSelectionStrategy.cs", "**/DefaultFilterSelectionStrategy.cs", "**/PolynomialFilterSelectionStrategy.cs"
StrykerCompat=true dotnet stryker
# parse survivors from StrykerOutput/<ts>/reports/mutation-report.html (app.report = {...}; raw_decode)
# then RESTORE the full 8-file mutate list and update the guide/roadmap with the verified counts.
```
Expected residual = the equivalent floor only: the boundary `<`/`<=` threshold mutants where the crafted metrics
don't sit exactly on a threshold, the OpenCv `Blur<1500` RefineConfig branch (dead — OpenCv only comes from Q1
which requires `Blur>1500`), the stub-model odd-`bilateralD` `++`/clamp (midpoint 9 is already odd), and Serilog.

## 3. ⚠️ Environment gotcha that interrupted this session — native-mutation popups

Mutating native-bound code (`PolynomialImageQualityAnalyzer`'s `CvInvoke.*` calls in Unit 23) **crashes the
mutant process**, which triggers **Windows Error Reporting dialogs** ("dotnet was launched with bad parameters"),
~5–6 per run. **Results are unaffected** (Stryker counts a crashed mutant as killed), but the popups are
disruptive on an interactive desktop. Mitigations for next time:
- **Scope native files into their own run**, separate from the pure managed files.
- Prefer running Stryker **headless / in CI**, or after disabling WER dialogs for `dotnet.exe`.
- The pure managed files (everything except `PolynomialImageQualityAnalyzer`) do **not** cause popups.

## 4. Lessons added to the guide this session (Units 21–24)

1. **Native-heavy suites inflate the headline via timeout-as-killed.** Unit 23 scoped run read **94.44 % with 56
   timeouts**; `additional-timeout: 30000` collapsed them to the **true 77.78 %** (54 killed, 12 survived = floor).
   Give native suites timeout headroom (the config now carries `additional-timeout: 30000`) or parse the Timeout
   bucket before trusting any score.
2. **The Laplacian is high-pass → its sum over any interior feature is 0**, which makes `ComputeVariance`'s
   mean-subtraction / mean-scaling mutants **equivalent** for every cleanly-predictable image. A real
   "equivalent-for-realistic-inputs" floor; reaching them needs a border-clipped feature (native-fragile blur).
3. **Pin native-extraction output with clean structured images.** `half-width 0|v` (50×50) →
   `Blur=v²/25, Contrast=v/2, Noise=v/25, Edge=0.02` — exact, version-stable (Emgu pinned 4.12), and enough to
   kill the whole deterministic post-processing surface through the public API.
4. **Substituted `IOptionsMonitor` + hand-built coefficients** make a hot-reloadable model fully deterministic
   and let `Received(1)` kill the ctor's `OnChange` subscription statement.
5. **Reflection/midpoint stub models are deterministic** — `PolynomialModel.CreateStub` (empty coefficients) →
   range midpoint, so `PolynomialFilterSelectionStrategy`'s predicted params are constants you can assert exactly.

## 5. Next units to harden — §2.4 Extraction (base)

`Tests.Infrastructure.Extraction` exists. **First check for duplication** (roadmap §2.4): `ComplementExtractionStrategy.cs`,
`SearchExtractionStrategy.cs`, `StructuredDocxStrategy.cs` appear here AND (already hardened) under
`Extraction.Adaptive/Strategies/` — confirm whether the base copies are live or dead before testing. Quick wins:
`XmlFieldExtractor.cs` (already has 16 tests), `OcrSanitizationService.cs`/`TextSanitizer.cs` (pure string),
`FileTypeIdentifierService.cs`, `MexicanNameFuzzyMatcher.cs`. ⛔ avoid OCR/render/DB-coupled extractors.

## 6. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from prior handoffs:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is unused private — safe to delete.

## 7. Status / push
- Commits this session on `Kt2`: `97abde1` (U21), `d244048` (U22), `c0bd467` (U23), `e170ed6` (U24), + this docs commit.
- Solution/build green; Imaging test project **180/180** green. **~29 files hardened across 6 projects** (Txt,
  Adaptive, Classification, Export, Export.Adaptive, **Imaging**) ≈ 45 % of the worthy deterministic surface.
- ⚠️ **`git push` not yet done** — push these commits at session start.

## 8. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 21–24 are this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
