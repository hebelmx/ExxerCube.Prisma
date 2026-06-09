# Handoff — Mutation testing: §2.4 Extraction base continued (Units 28–30) (2026-06-09)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (canonical guide — setup, per-unit results, all lessons;
Units 28–30 are this session) and `docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan).
**Supersedes** `HANDOFF-2026-06-09-mutation-extraction-base.md` (Units 25–27) for the "next units" question.
**Branch:** `Kt2` — all work this session **committed AND pushed** (`origin/Kt2` at `db26878`). Build green,
`Tests.Infrastructure.Extraction` **312/312** green.

---

## 1. What this session did — Units 28–30 (all in `Infrastructure.Extraction`, driven by `Tests.Infrastructure.Extraction`)

| Unit | File(s) | Result | Tests |
|---|---|---|---|
| 28 | `Matching/MexicanNameFuzzyMatcher.cs` | Killed 0→75 (57.69% headline), **0 killable survivors** | +58 |
| 29 | `Teseract/XmlExpedienteParser.cs` + `Teseract/XmlMetadataExtractor.cs` | **15.03% → 93.32%** (parser Killed 69→462, extractor 12→41), **0 killable survivors** | +43 |
| 30 | `DocumentComparisonService.cs` + `AdditionalFieldsReconciler.cs` | 92.79% combined (Killed 73 / 30), **0 killable survivors** | +33 |

The Extraction-base `stryker-config.json` now mutates **9 files** (XmlFieldExtractor, TextSanitizer,
OcrSanitizationService, FileTypeIdentifierService, MexicanNameFuzzyMatcher, XmlExpedienteParser,
XmlMetadataExtractor, DocumentComparisonService, AdditionalFieldsReconciler). `coverage-analysis` is `perTest`.

Commits (range `3084f4f..db26878`, all on `origin/Kt2`): `4399dc2`/`ed4bdf2` (U28), `4142a46`/`a2d73b5` (U29),
`17df663`/`db26878` (U30) — test+config first, then docs.

## 2. Resolved this session: the §2.4 Strategies/ duplication question

The base `Ocr.Strategies/{ComplementExtractionStrategy,SearchExtractionStrategy,StructuredDocxStrategy}.cs` are
**DEAD CODE — skip them.** They implement `IDocxExtractionStrategy`/`IComplementDocxExtractionStrategy`, which have
**zero production consumers or DI registrations** (only the flaky `Tests.Infrastructure.Extraction.Teseract` suite
references them). The LIVE copies are `Extraction.Adaptive/Strategies/*` (`IAdaptiveDocxStrategy`, wired in DI,
hardened Units 6–10). Cleanup = delete the 3 dead base files + their dead interfaces (out of scope for hardening).

## 3. Key lessons added this session (full detail in the guide, Units 28–30)

1. **A class dominated by `static readonly` lookup tables shows a low Stryker headline that is mostly the
   static-initializer limitation, not weak tests** (Unit 28). The two name `HashSet`s + six `Regex` static fields
   in MexicanNameFuzzyMatcher gave 42 "survivors" under BOTH perTest and coverage-off. **Confirm by hand-editing
   one table entry and watching a test fail** (`"cristian"→""` failed a test), then report "0 killable survivors".
2. **Stryker's `Boolean → true` mutant on an `if`-condition is an MTP artifact when the same line's `Negate
   expression` mutant IS killed** (Unit 28). Verify by forcing the condition always-true by hand with a
   *non-constant* always-true expression (a literal `if(true)` won't compile — CS0162 under warnings-as-errors).
3. **`result.GetMetadata<T,TMeta>()` turns *attached* scoring metadata into a killable surface** (Unit 29). When a
   parser logs/attaches an analysis object rather than returning it, read it back through the IndQuestResults
   metadata extension — that's what unlocked XmlExpedienteParser's ~150-line `BuildExtractionMetadata` counter block.
4. **Build exhaustive "every-name" fixtures programmatically** (Unit 29). A `StringBuilder` loop over the known-field
   list (plain AND `Cnbv_`) asserting `AdditionalFields.Count == 0` kills a whole local lookup table at once.
5. **perTest attribution noise is SEVERE on large parser suites** (Unit 29). At ~290 tests, ~30 string/equality
   "survivors" appeared on lines the tests clearly assert, and **the set shrank run-to-run** (95→93 on identical
   code). **PROVEN killable** by manually applying 3 of them (element-name strings + a hasData logical) → 11 tests
   failed. Trust the Killed delta + a manual spot-check; do not chase rotating string survivors.
6. **Equivalent-mutant patterns** (Unit 30): a `continue` after adding a brand-new dict key is equivalent when the
   fall-through re-reaches the same state; a `IsNullOrWhiteSpace(v) ? "" : v.Trim()` ternary is only killable with a
   **null** input (the always-false mutant becomes `null.Trim()` → NRE) — add an explicit null case.

## 4. Where mutation testing stands (rough estimate)

- **~38 files hardened across 7 projects**, all with **0 killable survivors**.
- **Infrastructure deterministic surface ≈ 90% complete:** Extraction.Txt, Extraction.Adaptive, Classification
  (deterministic), Export base, Export.Adaptive, Imaging are **done**; Extraction base is nearly done (only the
  Docx/Pdf metadata extractors remain).
- **Overall ≈ 65% of the worthy deterministic business-logic surface** (denominator ≈ 55–58 files by the
  roadmap's count). The largest *unsurveyed* remainder is **§2.6 Core/Application** (~11 `*Service.cs`, of which
  ~6–8 look pure/worthy) — that's the chunk that most moves the number next.

## 5. Next units (priority order) — §2.4 finish, then §2.5, then survey §2.6

1. **Finish §2.4 Extraction base — the Docx/Pdf metadata extractors.** ⚠️ **Determinism check FIRST:**
   - `Teseract/DocxMetadataExtractor.cs` (400), `Teseract/DocxFieldExtractor.cs` (210),
     `Analysis/DocxStructureAnalyzer.cs` (157) — OpenXml parsing is deterministic (same family as the
     ClosedXML/OpenXml round-trips in Units 15/19); these should be safe. Build the .docx bytes in-test via
     `DocumentFormat.OpenXml` (already a GlobalUsing in this test project).
   - `Teseract/PdfMetadataExtractor.cs` (618) — **verify it doesn't pull OCR/PDF-render deps** before mutating; if
     it does, it's ⛔ (non-deterministic, like the other OCR files). If it's pure byte/string parsing, harden it.
   - `Teseract/CompositeMetadataExtractor.cs` (64, pure delegator) — takes the **concrete**
     `XmlMetadataExtractor`/`DocxMetadataExtractor`/`PdfMetadataExtractor` (not interfaces), so do it **after** the
     three above; drive real collaborators and assert a collaborator-specific side effect (same approach as Unit 16
     CompositeResponseExporter).
2. **§2.5 Metrics / FileStorage** (small, deterministic): `Infrastructure.Metrics/ProcessingMetricsService.cs`,
   `Infrastructure.FileStorage/SafeFileNamerService.cs` (pure naming — easy), `FileMoverService.cs`. ⛔ skip the
   `FileSystemDownloadStorageAdapter` / options DTOs.
3. **§2.6 Core/Application** — **survey first** (`01 Core/Application`, ~11 `*Service.cs`): pick the pure ones
   (e.g. `DecisionLogicService`, `ConfigurationValidationService`, `SLATrackingService`, `AuditReportingService`,
   `FieldMatchingService`, `MetadataExtractionService`); skip the I/O orchestrators (`DocumentIngestionService`,
   `FileDownloadService`, `HealthCheckService`). Each needs a per-test-project `stryker-config.json`.
4. Optional: `Infrastructure.Classification/SemanticAnalyzerService.cs` (deterministic Levenshtein + in-repo
   dictionary, NOT Ollama; already driven by existing dictionary/text-comparer tests).

⛔ **Avoid (non-deterministic / out of scope):** `TesseractOcrExecutor`, `GotOcr2OcrExecutor`,
`OcrProcessingService`, `PdfOcrFieldExtractor`, `PdfToImageConverter`, `OcrSessionRepository`,
`BulkProcessingService`; native EmguCV/OpenCv/PIL filters; DB/Testcontainers; UI/Playwright; legacy events.

## 6. STILL DEFERRED to end-of-campaign (unchanged)

1. **Unit 24 mutation-verify re-run** — the 3 Imaging `*FilterSelectionStrategy.cs` have green value-exact tests
   but the confirming Stryker run was deferred (scope to those 3 pure-managed files, run, confirm 0 survivors).
2. **Native-mutation Windows popups permanent fix** — mutating EmguCV `CvInvoke.*` crashes mutant processes → WER
   dialogs. Suppress WER UI / scope native files into their own run / run headless.

## 7. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
Also minor: `MatchingPolicyService.GetConflictThreshold(string)` unused private — safe to delete.

## 8. The per-unit loop (reminder)

1. Pick a pure/deterministic file; read it + existing tests for gaps. Confirm the right test project drives it
   (some classes are only covered by the flaky Teseract suite → "dark" in the deterministic project; test fresh).
2. For large files, run a **scoped baseline** first (set `mutate` to just that file) to target precisely.
3. Write **exact-value** mutation-killing tests (Shouldly; `TestContext.Current.CancellationToken`; NSubstitute;
   no Moq/FluentAssertions). For orchestrators, mock the dependency interface; read attached metadata via
   `result.GetMetadata<…>()`.
4. Build + run the test project green (running the whole small project is simplest; the `|`-OR filter-query is
   finicky under MTP).
5. Run scoped: `cd <test-project-dir> && StrykerCompat=true dotnet stryker`. Stryker runs are ~10 min — launch in
   background.
6. Parse `StrykerOutput/<ts>/reports/mutation-report.html` (`app.report = {...}`; `json.JSONDecoder().raw_decode`).
   **Trust "Killed delta + 0 killable survivors", not the headline %.** Classify residuals: Serilog / dead-defensive
   / equivalent / static-init floor / **perTest noise** (prove the last by hand-applying the mutation → tests fail).
7. **Re-add ALL hardened files to the config's `mutate` list** before committing. Commit test+config (Killed delta
   in subject) separately from the docs. Update `mutation-testing.md` + roadmap + memory; push.

## 9. Pointers

- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 28–30 this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Prior handoff (Units 25–27): `docs/development/sessions/HANDOFF-2026-06-09-mutation-extraction-base.md`
- Imaging handoff (still holds the two deferred items): `HANDOFF-2026-06-09-mutation-imaging-done.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0; `test-runner: mtp` + `StrykerCompat=true` mandatory):
  `CLAUDE.md` → "Testing stack"
