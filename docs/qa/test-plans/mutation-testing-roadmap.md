# Mutation Testing — Roadmap & Checklist (for the next agents)

**Companion to** `docs/qa/test-plans/mutation-testing.md` (the *reference*: setup, the two mandatory settings,
per-unit results, all lessons). **This file is the *plan*: the goal, the denominator, and a tick-box path.**

**Last updated:** 2026-06-09 (after Units 21–24: Imaging §2.3 COMPLETE; Unit 24 mutation-verify deferred).
**Branch:** `Kt2`.

---

## 0. The goal (and what "done" is *not*)

> **Mutation-harden the deterministic business-logic surface (~55–70 files) to ≥ 85 % killed with
> 0 *killable* survivors, and fix the defects mutation surfaces along the way.**

- **Not** 100 %: every unit has a 5–15 % *equivalent-mutant floor* (Serilog statements, unreachable `catch`
  blocks, `First`→`FirstOrDefault` on never-empty collections, correlated guards, dead branches). Report
  **"Killed delta + 0 killable survivors,"** never chase the headline %.
- **Not** everywhere: do **not** mutate non-deterministic code (OCR/Tesseract, dormant Python/VLM, Browser
  automation, Database/Testcontainers, UI/Playwright, worker orchestration). Mutants there are ambiguous.

**Where we are:** ~29 files hardened across **6 projects** (Extraction.Txt, Extraction.Adaptive, Classification,
Export base, Export.Adaptive, and the now-complete **Imaging**). That's roughly **45 % of the worthy surface** —
high quality where done, breadth is the remaining work. Estimate ~5–7 more focused sessions at ~1–2 units each.
(One small caveat: Unit 24's three filter-selection strategy files have green exact-value tests but their Stryker
kill-count was deferred — see §2.3.)

---

## 1. Status at a glance

| Project | Mutation status |
|---|---|
| `Infrastructure.Extraction.Txt` | ✅ done (AdaptiveTxtFieldExtractor 82 %) |
| `Infrastructure.Extraction.Adaptive` | ✅ **complete** (7 files: 5 strategies + merge + orchestrator, 96–97 %) |
| `Infrastructure.Classification` (deterministic) | ✅ **complete** (5 files; LegalDirective 96 %, FileClassifier 89 %, the 3 matching policies 37–61 %*) |
| `Infrastructure.Export` (base) | ✅ **complete** (Units 14–16: SiroXmlExporter, ExcelLayoutGenerator, CriterionMapperService, CompositeResponseExporter — all 0 killable survivors) |
| `Infrastructure.Export.Adaptive` | ✅ **complete** (Units 17–20: TemplateFieldMapper, SchemaEvolutionDetector, AdaptiveExporter, AdaptiveResponseExporterAdapter — all 0 killable survivors) |
| `Infrastructure.Imaging` | ✅ **complete** (Units 21–24: Levenshtein, FeatureNormalizer, Polynomial(Model/Analyzer/Trained), 3 filter-selection strategies — Unit 24 mutation-verify deferred but tests green). Native filters/analyzers excluded. |
| `Infrastructure.Extraction` (base) | ⬜ TODO — several deterministic extractors (see §2.4) ⬅ **next** |
| `Infrastructure.Metrics` / `FileStorage` | ⬜ TODO — small (see §2.5) |
| `01 Core` Application services | ⬜ TODO — broadens beyond Infrastructure (see §2.6) |
| OCR / Python / Browser / Database / UI / Events(legacy) | ⛔ excluded by design (non-deterministic / dormant / legacy) |

\* The two low matching-policy scores are capped by **real bugs**, not weak tests — see §3.

---

## 2. The checklist (priority order — tick as you go)

For **each** unit follow the loop in §4. Each box = one commit (`test(qa): … (Killed X -> Y, 0 survivors)`).

### 2.1 Export — regulatory, highest value ⬅ start here
> `Tests.Infrastructure.Export` **already exists** (no scaffolding needed) but today only tests
> `DigitalPdfSigner`. Add a `stryker-config.json` next to it.

- [x] `SiroXmlExporter.cs` — ✅ **done (2026-06-08): 0 coverage → 86.22 %, Killed 0→169, 0 killable survivors,
  26 tests, Export project 26→52 green.** Was the SIRO-XML deliverable with ZERO coverage. See guide Unit 14.
- [x] `ExcelLayoutGenerator.cs` — ✅ **done (2026-06-08): 0 coverage → 0 killable survivors (combined Export run
  84.74 %), 17 tests, project 52→69 green.** ClosedXML round-trip to assert cells/styles. See guide Unit 15.
- [x] `CriterionMapperService.cs` — ✅ **done (2026-06-08): 14 killed, 0 killable survivors, 9 tests.** Unit 16.
- [x] `CompositeResponseExporter.cs` — ✅ **done (2026-06-08): 3 killed, 0 survivors, 2 delegation tests.** Unit 16.
- ⛔ avoid `DigitalPdfSigner.cs` (crypto), `PdfRequirementSummarizerService.cs` (PDF rendering).

> ✅ **§2.1 Export base project COMPLETE** (Units 14–16: SiroXmlExporter, ExcelLayoutGenerator,
> CriterionMapperService, CompositeResponseExporter — all 0 killable survivors). Next: §2.2 Export.Adaptive,
> then §2.3 Imaging.

### 2.2 Export.Adaptive — ✅ **COMPLETE** (2026-06-08, Units 17–20)
> `Tests.Infrastructure.Export.Adaptive` exists; added a `stryker-config.json` (all 4 files). Project 75 → 173 green.
- [x] `TemplateFieldMapper.cs` — ✅ Unit 17: 44.89% → **85.23%**, Killed 79→150, 0 killable survivors, +45 tests.
- [x] `SchemaEvolutionDetector.cs` — ✅ Unit 18: 53.55% → **84.15%**, Killed 97→154, 0 killable survivors, +22 tests.
- [x] `AdaptiveExporter.cs` — ✅ Unit 19: 47.86% → **82.05%**, Killed 56→96, 0 killable survivors, +25 tests
  (Excel/XML/DOCX round-trip read-back, caching via mock repo, error paths via throwing mocks).
- [x] `AdaptiveResponseExporterAdapter.cs` — ✅ Unit 20: 0 coverage → Killed 0→8, 0 killable survivors, +6 tests.
  Headline 28.57% is the small-file floor (logging-dominated delegator).
- ⛔ skipped EF artifacts (`InitialCreate*`, `TemplateDbContext*`, `*ModelSnapshot`), `TemplateRepository`,
  `TemplateSeeder` (DB-bound) — as planned.

### 2.3 Imaging — deterministic math — ✅ **COMPLETE** (2026-06-09, Units 21–24)
> `Tests.Infrastructure.Imaging` exists; added a `stryker-config.json` (8 files, `additional-timeout: 30000`).
> Project **44 → 180 green**.
- [x] `LevenshteinTextComparer.cs` — ✅ Unit 21: 0 coverage → Killed 0→151, 0 killable survivors, +40 tests.
- [x] `FeatureNormalizer.cs` / `PolynomialModel.cs` / `TrainedPolynomialModel.cs` — ✅ Unit 22: Killed +19
  (10/50/29), 0 killable survivors, +29 tests. (FeatureNormalizer floor = `- min` where every min is 0.0;
  TrainedPolynomialModel driven via a substituted `IOptionsMonitor` with hand-built coefficients.)
- [x] `PolynomialImageQualityAnalyzer.cs` — ✅ Unit 23: 0 coverage → Killed 0→54, 0 killable survivors, +14 tests.
  Hybrid (deterministic post-processing around native EmguCV extraction); pinned exactly via clean structured
  images (`half-width 0|v` → Blur=v²/25, Contrast=v/2, Noise=v/25, Edge=0.02). **Two lessons:** (a) this
  native-heavy suite *inflated* the headline via timeout-as-killed (scoped run read 94.44 % with 56 timeouts;
  with `additional-timeout: 30000` the true picture is 77.78 %, 54 killed, 12 survived — all floor). (b) the
  Laplacian is high-pass, so its sum over any **interior** feature is 0 → the `ComputeVariance` mean-subtraction
  / mean-scaling mutants are **equivalent** for every cleanly-predictable image (reaching them needs a
  border-clipped feature whose blur is border-mode-dependent / native-fragile).
- [x] `AnalyticalFilterSelectionStrategy.cs` / `DefaultFilterSelectionStrategy.cs` /
  `PolynomialFilterSelectionStrategy.cs` — ⏳ Unit 24: +53 exact-value tests, **180/180 green**, but the Stryker
  **mutation-verification run is DEFERRED** (the Unit 23 native-EmguCV run triggered Windows crash popups; the
  run was stopped before re-confirming these pure managed files). Tests are value-exact by construction; re-run
  Stryker scoped to the three `*FilterSelectionStrategy.cs` to confirm Killed delta + 0 survivors. (`Polynomial…`
  uses stub models → deterministic midpoint predictions; the ≥10 % threshold task #11 is a separate concern.)
- ⛔ skipped native-bound enhancement filters `AdaptiveEnhancementFilter.cs`, `PolynomialEnhancementFilter.cs`
  (they apply native pixel ops); and `EmguCvImageQualityAnalyzer`, `OpenCvAdvancedEnhancementFilter`,
  `PilSimpleEnhancementFilter`; trivial `NoOp*`/`Stub*`.

> ⚠️ **Native-mutation popup warning:** mutating native-bound code (`PolynomialImageQualityAnalyzer`'s `CvInvoke`
> calls) crashes mutant processes and triggers Windows Error Reporting dialogs ("dotnet launched with bad
> parameters"), ~5–6 per run. Results are unaffected (Stryker counts a crashed mutant as killed) but it's noisy
> — prefer scoping native files into their own run, or run headless.

### 2.4 Extraction (base project) — deterministic extractors/sanitizers
> `Tests.Infrastructure.Extraction` exists. **First check for duplication:** `ComplementExtractionStrategy.cs`,
> `SearchExtractionStrategy.cs`, `StructuredDocxStrategy.cs` appear here AND (already hardened) under
> `Extraction.Adaptive/Strategies/` — confirm whether the base copies are live or dead before testing.
- [ ] `XmlFieldExtractor.cs` (already has 16 tests from a prior session — add a mutation config, likely a quick win)
- [ ] `XmlExpedienteParser.cs`, `XmlMetadataExtractor.cs`
- [ ] `AdditionalFieldsReconciler.cs`
- [ ] `DocxStructureAnalyzer.cs`, `DocxFieldExtractor.cs`, `DocxMetadataExtractor.cs`
- [ ] `MexicanNameFuzzyMatcher.cs`
- [ ] `OcrSanitizationService.cs`, `TextSanitizer.cs` (pure string cleanup)
- [ ] `FileTypeIdentifierService.cs`
- [ ] `PdfMetadataExtractor.cs`, `CompositeMetadataExtractor.cs`, `DocumentComparisonService.cs`
- ⛔ avoid OCR/render/DB-coupled: `TesseractOcrExecutor`, `GotOcr2OcrExecutor`, `OcrProcessingService`,
  `PdfOcrFieldExtractor`, `PdfToImageConverter`, `OcrSessionRepository`, `BulkProcessingService`.

### 2.5 Metrics / FileStorage — small, deterministic
- [ ] `Infrastructure.Metrics/ProcessingMetricsService.cs`
- [ ] `Infrastructure.FileStorage/SafeFileNamerService.cs` (pure naming logic — easy)
- [ ] `Infrastructure.FileStorage/FileMoverService.cs`
- ⛔ `Infrastructure.Events/InMemoryEventBus.cs` is legacy/never-registered (CLAUDE.md) — low value, skip unless idle.

### 2.6 Core / Application services
> Broadens beyond Infrastructure. Survey `01 Core/Application` for concrete `*Service.cs` with real logic
> (skip interfaces, DTOs, events, validators that just guard nulls). Add per-test-project configs.
- [ ] (survey first, then list) — e.g. fusion/reconciliation/pipeline-coordination services that are pure.

### 2.7 Optional — deterministic Classification leftover
- [ ] `SemanticAnalyzerService.cs` — deterministic (Levenshtein + in-repo `ClassificationDictionary`, **not**
  Ollama); already driven by `LegalDirectiveClassifierDictionaryTests` + `TextComparerFindBestMatchTests`.
  (Reminder: that "DictionaryTests" file tests *this* class, not `LegalDirectiveClassifierService`.)

---

## 2b. DEFERRED — batch these at the END of the mutation campaign (owner decision, 2026-06-09)

Two operational loose ends are **deliberately deferred to a single clean-up pass at the end of the whole
mutation effort**, rather than interrupting unit-by-unit progress:

1. **Unit 24 mutation-verification re-run.** The three Imaging filter-selection strategy files
   (`AnalyticalFilterSelectionStrategy`, `DefaultFilterSelectionStrategy`, `PolynomialFilterSelectionStrategy`)
   have green, value-exact tests (180/180) but their Stryker kill-count was not captured. Finish by scoping the
   `Tests.Infrastructure.Imaging` mutate list to **only** the three `*FilterSelectionStrategy.cs` files
   (pure managed → no native popups), running `StrykerCompat=true dotnet stryker`, confirming Killed delta + 0
   killable survivors, then restoring the full 8-file mutate list and recording the numbers.

2. **Native-mutation Windows popups (root cause + permanent fix).** Mutating native-bound code (EmguCV
   `CvInvoke.*` in `PolynomialImageQualityAnalyzer`, and any future Emgu/OpenCv/PIL file) crashes the mutant
   process → Windows Error Reporting dialogs ("dotnet was launched with bad parameters"), ~5–6 per run. Results
   are unaffected (a crashed mutant counts as killed) but it's disruptive on an interactive desktop. **Interim
   workaround (use now):** keep native files in their own scoped run, or run Stryker headless/CI. **Permanent fix
   (do at end of campaign):** disable WER UI for the mutation run — e.g. set `HKCU\Software\Microsoft\Windows\
   Windows Error Reporting\DontShowUI=1` (or `Disabled=1`) for the run, or launch Stryker in a session/job that
   suppresses crash dialogs (`SetErrorMode`/`SEM_NOGPFAULTERRORBOX`), then revert. Decide whether to keep mutating
   native files at all or formally exclude them (they yield ambiguous mutants anyway — see the ⛔ list in §2.3).

---

## 3. RESERVED — bug-fix session (do NOT bundle with test hardening)

Two real conflict-detection defects mutation testing surfaced in the matching policies. Fixing them lifts
FieldMatcher (61 %) and NameMatching (37 %) by unblocking ~75 currently-dead mutants. Do them **together**:
- [ ] `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
- [ ] `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
- [ ] minor: delete unused private `MatchingPolicyService.GetConflictThreshold(string)`.
After fixing, re-run Stryker on those files and add the cross-value tests the existing tests were written to allow.

---

## 4. The per-unit loop (condensed — full version in the continuation handoff §5)

1. `dotnet tool restore` (once). Confirm the test project is green: `dotnet test <test.csproj>`.
2. Create/extend `stryker-config.json` next to the test project: `project`, `test-projects`,
   **`"test-runner": "mtp"`**, `"coverage-analysis": "perTest"`, `mutate: ["**/<File>.cs"]` (scope to ONE file
   for the baseline). Strict JSON — **no unknown keys, not even comments**.
3. Baseline from the test-project dir: **`StrykerCompat=true dotnet stryker`** (both settings mandatory — see guide).
4. Parse the HTML report: `app.report = {...}` JSON in a `<script>`; `json.JSONDecoder().raw_decode` (trailing JS).
   Bucket by **Survived / NoCoverage / Timeout**, group by line.
5. Write **exact-value** tests (Shouldly; `TestContext.Current.CancellationToken`; NSubstitute; no Moq/FluentAssertions).
   Prioritize NoCoverage (counts against score) then weak survivors. For orchestrators, **mock the dependency interface**.
6. Re-run; classify residual honestly. **If a cluster is unkillable because the code is dead/broken → that's a
   FINDING** (`docs/qa/findings/`), don't contort tests.
7. **Re-add ALL hardened files to the config's `mutate` list** before committing. Commit test+config (Killed delta
   in subject) separately from the guide doc update. Update `mutation-testing.md` + this checklist. Push.

---

## 5. Lessons that will save you time (full detail in the guide)

- **Score formula:** `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`. NoCoverage counts against
  you; Timeout counts as detected.
- **Timeout inflation:** Stryker counts `Timeout` as killed. On regex-heavy *and* large suites, mutants land in
  Timeout and inflate the headline. **Always parse the Timeout bucket** for functional mutants that only "passed"
  via timeout; convert them to deterministic kills.
- **perTest attribution noise:** on large/slow suites the headline flaps run-to-run (identical code gave
  0/5/7/18 "survivors" — almost all equivalent Serilog mutants). **Trust the Killed delta from a clean run.**
- **`coverage-analysis: "off"` cross-check:** temporarily set it (then revert to `perTest`) to find real gaps vs
  noise — it runs every test on every mutant. **Caveats:** it *over-counts* survivors because `static readonly`
  array initializers can't be mutated in its shared process (perTest kills them); and it's slower. Find gaps with
  it, report the score from perTest.
- **Covering a `catch (OperationCanceledException)`:** an already-cancelled token trips the pre-`try` guard, so
  the catch is never entered. Throw OCE from a mock dependency *inside* the try with a *live* token.
- **Contaminated test inputs:** Spanish keywords embed substrings (`"ordena"` contains `"ORDEN"`;
  `"DESBLOQUEO"` contains `"BLOQUEO"`; `"alcance del oficio"` triggers Alcance). Craft inputs that isolate ONE term.
- **Dead branches are findings:** e.g. FileClassifier's `Min(85)` middle confidence tier is unreachable because
  discrete scores {10,70,90} only ever differ by {0,20,60,80}. Pin as floor, note it, move on.

---

## 6. Housekeeping
- ✅ Deleted the stale orphaned root config `Prisma/Code/Src/CSharp/stryker-config.json` (camelCase keys,
  `testRunner: dotnet`, non-existent `testProjects` path — drift from when mutation was broken).
- The valid `stryker-config.json` files live next to their test projects. As of 2026-06-08 (after Units 17–20)
  there are **5**: Classification, Extraction.Adaptive, Extraction.Txt, **Export** (4 files), and
  **Export.Adaptive** (mutates all 4 hardened Adaptive files).

## 7. Pointers
- Reference / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md`
- Latest session handoff: `docs/development/sessions/HANDOFF-2026-06-09-mutation-imaging-done.md`
  (Imaging §2.3 complete — Units 21–24; Unit 24 mutation-verify deferred; next = §2.4 Extraction base)
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, CI-gate option: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
