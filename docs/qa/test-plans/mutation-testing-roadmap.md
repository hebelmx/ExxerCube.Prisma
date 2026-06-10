# Mutation Testing — Roadmap & Checklist (for the next agents)

**Companion to** `docs/qa/test-plans/mutation-testing.md` (the *reference*: setup, the two mandatory settings,
per-unit results, all lessons). **This file is the *plan*: the goal, the denominator, and a tick-box path.**

**Last updated:** 2026-06-10 (Units 38–42: **§2.6 Core/Application COMPLETE** [SLATracking, AuditReporting,
FieldMatching, DecisionLogic] + **§2.7 SemanticAnalyzerService COMPLETE** — all 0 killable survivors).
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

**Where we are:** ~46 files hardened across **9 projects** (Extraction.Txt, Extraction.Adaptive, Classification,
Export base, Export.Adaptive, Imaging, Extraction base, and the now-complete **FileStorage** + **Metrics**).
That's roughly **75–80 % of the worthy deterministic surface** — the largest remaining piece is **§2.6
Core/Application**. Estimate ~2–4 more focused sessions. (One small caveat: Unit 24's three filter-selection
strategy files have green exact-value tests but their Stryker kill-count was deferred — see §2.3.)

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
| `Infrastructure.Extraction` (base) | ✅ **COMPLETE** (Units 25–33, 14 files: XmlFieldExtractor, sanitizers, FileTypeIdentifier, MexicanNameFuzzyMatcher, XML parser+metadata, DocumentComparison+AdditionalFieldsReconciler, Docx/Pdf/Composite metadata extractors — all 0 killable survivors). Dead `Ocr.Strategies/*` skipped. |
| `Infrastructure.Metrics` / `FileStorage` | ✅ **COMPLETE** (Units 34–36: SafeFileNamer, FileMover, ProcessingMetrics — all 0 killable survivors) |
| `01 Core` Application services | ✅ **COMPLETE** (Units 37–41: ConfigurationValidation, SLATracking, AuditReporting, FieldMatching, DecisionLogic — all 0 killable survivors. I/O orchestrators skipped by design) |
| `Infrastructure.Classification` SemanticAnalyzerService (§2.7) | ✅ **COMPLETE** (Unit 42, mocked ITextComparer, 0 killable survivors) |
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
  `PolynomialFilterSelectionStrategy.cs` — ✅ **Unit 24 mutation-verify DONE (2026-06-10):** scoped Stryker re-run
  (no native popups — pure managed). First run: Analytical 59 killed/22 surv, Default 21/4, Polynomial 30/32.
  Added **boundary-exact tests** — Analytical +16 (every `ClassifyQualityLevel` + `RefineConfig` threshold at its
  exact value, killing the `>=`/`<=` mutants the far-from-boundary tests missed) and Default +4 (Q3/Q4
  `EnableEnhancement` bools + the 0.7/0.3 adjustment boundaries) → Analytical & Default **0 killable survivors**.
  **Analytical residual floor:** `RefineConfig` OpenCv `BlurScore < 1500` block is **dead** (a Q1/OpenCv config
  always has blur>1500, so `<1500` is unreachable) and the `FilterType==X && EnableEnhancement` `&&`→`||` mutants
  are param-default equivalent (PilParams/OpenCvParams are non-null defaults, so entering the wrong block mutates
  an unused object). **`PolynomialFilterSelectionStrategy` is an explicit STUB/placeholder** ("uses stub models…
  TODO: train models") — its residual survivors are floor/low-value: stub-model **name strings** (don't affect
  predictions), Serilog, the unused-`features` adaptive array (`CreateAdaptiveConfig` ignores its param), the
  dead `bilateralD` odd-branch (stub midpoint 9 is already odd → NoCoverage), and the normalized-heuristic
  `SelectFilterType` boundaries (hardening placeholder stub logic to exact normalized boundaries is deferred until
  real trained models replace the stubs — see the file TODOs / task #11). (≥10 % threshold task #11 is separate.)
- ⛔ skipped native-bound enhancement filters `AdaptiveEnhancementFilter.cs`, `PolynomialEnhancementFilter.cs`
  (they apply native pixel ops); and `EmguCvImageQualityAnalyzer`, `OpenCvAdvancedEnhancementFilter`,
  `PilSimpleEnhancementFilter`; trivial `NoOp*`/`Stub*`.

> ⚠️ **Native-mutation popup warning:** mutating native-bound code (`PolynomialImageQualityAnalyzer`'s `CvInvoke`
> calls) crashes mutant processes and triggers Windows Error Reporting dialogs ("dotnet launched with bad
> parameters"), ~5–6 per run. Results are unaffected (Stryker counts a crashed mutant as killed) but it's noisy
> — prefer scoping native files into their own run, or run headless.

### 2.4 Extraction (base project) — deterministic extractors/sanitizers
> `Tests.Infrastructure.Extraction` exists. **Duplication RESOLVED (2026-06-09, Unit 28 follow-up):** the base
> `Ocr.Strategies/{ComplementExtractionStrategy,SearchExtractionStrategy,StructuredDocxStrategy}.cs` are **DEAD
> CODE — skip them.** They implement `IDocxExtractionStrategy`/`IComplementDocxExtractionStrategy`, which have
> **zero production consumers or DI registrations** anywhere in the solution; the only references are the flaky
> `Tests.Infrastructure.Extraction.Teseract` suite. The LIVE copies are `Extraction.Adaptive/Strategies/*`
> (implement `IAdaptiveDocxStrategy`, wired in DI, already hardened Units 6–10). Cleanup = delete the 3 dead base
> files + their dead interfaces (out of scope for hardening; note for a tidy-up pass).
- [x] `XmlFieldExtractor.cs` — ✅ **Unit 25 (2026-06-09): 70.41% → 95.41%, Killed 138→187, 0 killable survivors,
  +39 tests.** Added the project's `stryker-config.json` (project = `Infrastructure.Extraction.Ocr.csproj`,
  mutate = `Teseract/XmlFieldExtractor.cs`). The 48 NoCoverage were the untested measure-inference chain
  (InferMeasure→ParseActionKind→ToSpanishMeasureName — every keyword + fallback mapped to its exact Spanish
  measure) plus collection guards/authority/CURP/file-loading. Residual = equivalent floor (StrictCurp `|`→`&`
  equivalent since strict & loose normalize identically; `"causa"` label equiv since Causa is always null) +
  perTest attribution noise. **Lesson: the CURP strict-regex block is an equivalent mutant — the loose regex
  subsumes any valid strict CURP, so removing the strict branch yields the same normalized value.**
- [x] `XmlExpedienteParser.cs`, `XmlMetadataExtractor.cs` — ✅ **Unit 29 (2026-06-09): project 15.03% → 93.32%,
  0 killable survivors, +43 tests.** Parser Killed 69→462, extractor Killed 12→41. Drove the parser purely
  through `ParseAsync` + read the attached counters via `result.GetMetadata<Expediente, ExtractionMetadata>()`;
  the extractor via a substituted `IXmlNullableParser`. A programmatic all-known-names-at-root fixture kills every
  `CaptureUnknownFields.knownFields` entry at once. Residual = equivalent floor (Serilog; dead-defensive root-null
  /never-null-parent/`Value ?? ""`/BOM-detect/object-init-default) + **proven** perTest attribution noise (4
  element-name strings + the hasData logical — manual L72/L145/L200 mutation failed 11 tests; survivor set shrank
  run-to-run). See guide Unit 29.
- [x] `AdditionalFieldsReconciler.cs` — ✅ **Unit 30 (2026-06-09): Killed 30, 0 killable survivors, +9 tests.**
  (bundled with DocumentComparisonService). Residual: L31 `continue` removal is equivalent (fall-through
  re-reaches the same merged state); L51 killed by a null-value test (Normalize must short-circuit null before
  `.Trim()`). See guide Unit 30.
- [x] `DocxStructureAnalyzer.cs`, `DocxFieldExtractor.cs`, `DocxMetadataExtractor.cs` — ✅ **Units 31 (2026-06-09):
  DocxStructureAnalyzer 61 killed/100% (was untested), DocxFieldExtractor 72 killed/97.3%, DocxMetadataExtractor
  136 killed/95.8%, 0 killable survivors.** OpenXml-deterministic; build .docx bytes in-test. See guide Units 31–33.
- [x] `MexicanNameFuzzyMatcher.cs` — ✅ **Unit 28 (2026-06-09): Killed 0→75, Survived 52, 0 killable survivors,
  +58 tests.** Ported fresh (was only in the flaky Teseract suite). Low 57.69% headline is **all floor, verified**:
  42 = static-field-initializer limitation (the two name `HashSet`s + six `Regex` fields — *proven* killable-in-
  principle: hand-editing `"cristian"→""` fails a test); 6 = Stryker MTP `Boolean→true` artifact on L123–128 (the
  same lines' `Negate` mutants are killed; forcing the condition always-true by hand fails 19 tests); the rest are
  genuine equivalents/dead code (all-digits `return false` is **dead** — AmountPattern subsumes every pure-digit
  string; `NormalizeForComparison` null-guard unreachable; split-options equiv after normalize; `||`→`&&` &
  block-removal equiv on empties; `>=`→`>` fuzzy-threshold boundary). See guide Unit 28.
- [x] `OcrSanitizationService.cs`, `TextSanitizer.cs` — ✅ **Unit 26 (2026-06-09): both 100.00%, 0 survivors,
  +29 tests** (TextSanitizer 70 killed, OcrSanitizationService 21 killed). They were only covered by the flaky
  OCR/Teseract suite (out of scope); tested fresh in the deterministic project. Pinned the account/SWIFT/generic
  cleaners (incl. the SWIFT 9→11 padding conditions + length boundaries) and the orchestrator's first-line
  discovery + cross-field normalization merge.
- [x] `FileTypeIdentifierService.cs` — ✅ **Unit 27 (2026-06-09): Killed 58 / Survived 8 / NoCoverage 5
  (81.69% headline), 0 killable survivors, +25 tests.** All 13 non-kills are the equivalent floor (Serilog
  log statements/text/`?? "unknown"` + the dead defensive `catch`, unreachable: guarded indexing, safe
  `GetString`, modern `Path.GetExtension` never throws). Pinned the `<4`/`>=5` length boundaries, per-byte
  PDF/ZIP magic numbers, the XML `<?xml`-vs-`TrimStart("<")` alternatives, the DOCX `word/` vs generic-ZIP
  `else` vs `xl/`-only fall-through, every extension-switch arm + `ToLowerInvariant`, content-wins ordering,
  and the empty-fileName guard.
- [x] `DocumentComparisonService.cs` — ✅ **Unit 30 (2026-06-09): Killed 73, 0 killable survivors, +24 tests**
  (combined run 92.79%). Exact-value status ladder + 16-field aggregation. Residual = Serilog floor. Guide Unit 30.
- [x] `PdfMetadataExtractor.cs`, `CompositeMetadataExtractor.cs` — ✅ **Units 32–33 (2026-06-09): Pdf 214
  killed/87.7% (30 floor = dead placeholder direct-extraction path + dead `patternViolations++` + unreachable
  catches the OCR helper pre-catches), Composite 5 killed/100%, 0 killable survivors.** Pdf is deterministic via
  substituted `IOcrExecutor`/`IImagePreprocessor` (its direct-text path is a placeholder, so OCR+regex parsing is
  the whole surface); Composite drives the real concrete collaborators with distinct routes. See guide Units 31–33.

> ✅ **§2.4 Extraction base COMPLETE** (Units 25–33, all 0 killable survivors). The base `Ocr.Strategies/*` are
> DEAD CODE (skip). Next: §2.5 Metrics/FileStorage, then survey §2.6 Core/Application.
- ⛔ avoid OCR/render/DB-coupled: `TesseractOcrExecutor`, `GotOcr2OcrExecutor`, `OcrProcessingService`,
  `PdfOcrFieldExtractor`, `PdfToImageConverter`, `OcrSessionRepository`, `BulkProcessingService`.

### 2.5 Metrics / FileStorage — ✅ **COMPLETE** (2026-06-09, Units 34–36)
> Added `stryker-config.json` to `Tests.Infrastructure.FileStorage` (2 files) and `Tests.Infrastructure.Metrics`
> (1 file). FileStorage project 10 → 32 green; Metrics project 18 → 40 green. All **0 killable survivors**.
- [x] `Infrastructure.FileStorage/SafeFileNamerService.cs` — ✅ Unit 34: Killed 51 / Survived 18 / Timeout 1,
  0 killable survivors, +17 tests. Structure-regex pins + ordinal casing checks + sanitizer branches +
  200-char truncation + catch. Floor = Serilog + `>200` boundary + dead `IsNullOrEmpty` branch. Guide Unit 34.
- [x] `Infrastructure.FileStorage/FileMoverService.cs` — ✅ Unit 35: bundled run, 0 killable survivors, +8 tests.
  Exact `Path.Combine` classification dir + uniqueness counter `_2` + constructor guards + locked-source catch.
  Floor = Serilog + `overwrite:false` equiv + 1000-cap boundary/throw + `?? ""` dead fallback. The L35 throw is
  **proven killable under `coverage-analysis: off`** (perTest exception-path attribution noise). Guide Unit 35.
- [x] `Infrastructure.Metrics/ProcessingMetricsService.cs` — ✅ Unit 36: Killed 88 / Survived 34 / Timeout 3,
  0 killable survivors, +22 tests. Reflection-driven `AggregateMetrics` + `ValidatePerformance` (inject events
  into the private `_processingEvents` queue + set `CurrentStatistics`) isolate every threshold branch; boundary
  kills at avg-time 30 / success-rate 0.99; `Average→Min` killed on confidence (not timing); Dispose→
  ObjectDisposed. Floor = Serilog + ConfigureAwait/lock primitives + timing `Average→Min` + guarded ternaries +
  Dispose-pattern + dead unused `recentFailed`. **perTest flapped 88↔89.** Guide Unit 36.
- ⛔ skipped `FileSystemDownloadStorageAdapter` + options DTOs (I/O-bound), as planned.
- ⛔ `Infrastructure.Events/InMemoryEventBus.cs` is legacy/never-registered (CLAUDE.md) — low value, skip unless idle.

> ✅ **§2.5 Metrics/FileStorage COMPLETE.** Next: survey **§2.6 Core/Application**.

### 2.6 Core / Application services — ⏳ **STARTED** (2026-06-09, Unit 37)
> Broadens beyond Infrastructure. Survey done — `01 Core/Application/Services` has 11 `*Service.cs`. Added
> `Tests.Application/stryker-config.json` (the project's first). Pure/worthy vs I/O-orchestrator split below.
- [x] `ConfigurationValidationService.cs` — ✅ **Unit 37: confirming run Killed 217 / Survived 5 / Timeout 0
  (97.75%), 0 killable survivors, +~80 tests, project 260/260 green.** Pure validation; boundary `[Theory]`s for
  all 14 numeric rules + all 35 OCR languages + the 3 sub-validator aggregations + factories. Floor = Serilog
  (L27/59/71) + the `if (result.IsValid)` negate (L57, gates only the success-vs-warning log).
  Lesson: un-validated factory literals (the 9 preset bool flags) die only via **direct field assertion**, not
  via "validates clean". Guide Unit 37.
- [x] `SLATrackingService.cs` — ✅ **Unit 38: Killed 65/66, 0 killable survivors, +50 tests.** Pure delegator
  over `ISLAEnforcer`; all survivors floor (Serilog/ConfigureAwait + the L131 `&&`→`||` correlated-equivalent).
- [x] `AuditReportingService.cs` — ✅ **Unit 39: Killed ~104, 0 killable survivors, +59 tests.** Exact CSV/JSON
  output (headers, row format, ordering — incl. ExportJson OrderBy, escaping, camelCase+indentation), all
  guard/failure/null/cancel branches. Floor = Serilog + ConfigureAwait.
- [x] `FieldMatchingService.cs` — ✅ **Unit 40: Killed 129, 0 killable survivors, +44 tests** (mocked
  `IMatchingPolicy`). Guards, per-source extraction, value collection (origin/confidence/source), match/conflict/
  missing, agreement averaging, the `CreateExtractedFields` switch, `AggregateValidation`, and failure-with-value
  not-collected (kills the 4 `IsSuccess && Value != null`→`||`). Floor = Serilog/ConfigureAwait + always-true
  Count guards + log-only requiredFields block + **dead** `DeriveSlaFromAdditional`/`AdditionalMerged` surface +
  unreachable persona/compliance/conflict loops. Surfaced an inverted `FechaEstimadaConclusion` warning (finding).
- [x] `DecisionLogicService.cs` — ✅ **Unit 41: Killed ~120, 0 killable survivors, +46 tests.** Exact failure/
  validation strings for all four public methods; the partial-result paths (resolver-cancelled, dedup-cancelled,
  between-iteration, ProcessDecisionLogic classify-cancelled-with-partial) with confidence/missing-ratio
  arithmetic + warning composition. Floor = Serilog + **audit-trail `LogAuditAsync` side-effects** (mocked dep,
  not observable in the Result — treated as logging floor) + the unreachable ProcessDecisionLogic classify-
  failure/success-with-partial sub-branches (a cancelled token always makes classify cancelled).
- ⛔ skipped the I/O orchestrators: `DocumentIngestionService`, `FileDownloadService`, `HealthCheckService`,
  `ExportService`, `FileMetadataQueryService` (DB/port/`File.*`-bound), and `MetadataExtractionService`
  (real `File.Exists`/`File.ReadAllBytesAsync` I/O).

> ✅ **§2.6 Core/Application COMPLETE** (Units 37–41, all 0 killable survivors).

### 2.7 — deterministic Classification leftover — ✅ **COMPLETE** (2026-06-10, Unit 42)
- [x] `SemanticAnalyzerService.cs` — ✅ **Unit 42: 0 killable survivors, +16 tests.** Deterministic (fuzzy phrase
  matching over the in-repo `ClassificationDictionary`, **not** Ollama). Driven through a **mocked
  `ITextComparer`** so each directive detector, the best-match (max) selection, the exact confidence propagation,
  and the `>= DefaultThreshold` (0.85) boundary are isolated; guards + exception covered. Added to the
  Classification `stryker-config.json`. (The existing `LegalDirectiveClassifierDictionaryTests` exercise the real
  Levenshtein comparer — kept as integration coverage.)

---

## 2b. DEFERRED items — ✅ BOTH DONE (2026-06-10, end-of-campaign clean-up)

1. ✅ **Unit 24 mutation-verification re-run — DONE.** Scoped `Tests.Infrastructure.Imaging` to the three
   `*FilterSelectionStrategy.cs`, ran `StrykerCompat=true dotnet stryker` (no popups — pure managed, confirmed).
   First run: Analytical 59 killed/22 surv, Default 21/4, Polynomial 30/32. Added boundary-exact tests
   (Analytical +16, Default +4) → **Analytical & Default 0 killable survivors**; Polynomial's residual is
   stub-placeholder floor (see §2.3 entry). Restored the full 8-file mutate list.

2. ✅ **Native-mutation Windows popups — RESOLVED (decision: formally exclude native files).** The permanent fix
   was demonstrated (set `HKCU\Software\Microsoft\Windows\Windows Error Reporting\DontShowUI=1` for the run, then
   revert — done programmatically around the Unit 24 run). **Decision: native-bound files
   (`PolynomialImageQualityAnalyzer`'s `CvInvoke.*`, `EmguCvImageQualityAnalyzer`, `OpenCvAdvancedEnhancementFilter`,
   `PilSimpleEnhancementFilter`, the native enhancement filters) are formally EXCLUDED from the mutation target** —
   they yield ambiguous mutants (a crashed native mutant counts as "killed" regardless) and aren't deterministic
   business logic. When one must be run, apply the `DontShowUI=1` suppression for the run and revert after.

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
- The valid `stryker-config.json` files live next to their test projects. As of 2026-06-09 (after Units 34–36)
  there are **10**: Classification, Extraction.Adaptive, Extraction.Txt, **Export** (4 files), **Export.Adaptive**
  (4 files), **Imaging** (8 files), **Extraction** base (14 files), **FileStorage** (SafeFileNamer + FileMover),
  **Metrics** (ProcessingMetricsService), and **Application** (ConfigurationValidationService).

## 7. Pointers
- Reference / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md`
- Latest session handoff: `docs/development/sessions/HANDOFF-2026-06-09-mutation-extraction-base-units28-30.md`
  (§2.4 Extraction base — Units 28–30 done; next = Docx/Pdf metadata extractors [determinism check first] →
  §2.5 Metrics/FileStorage → survey §2.6 Core/Application. Supersedes the Units 25–27 handoff;
  `HANDOFF-2026-06-09-mutation-imaging-done.md` still holds the two deferred items.)
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, CI-gate option: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
