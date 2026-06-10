# Handoff — Mutation testing: §2.4 Extraction base COMPLETE (Units 31–33) (2026-06-09)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (canonical guide — Units 31–33 are this session) and
`docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan; §2.4 now ✅, §2.5 is next).
**Supersedes** `HANDOFF-2026-06-09-mutation-extraction-base-units28-30.md` for the "next units" question.
**Branch:** `Kt2`. Build green, `Tests.Infrastructure.Extraction` **405/405** green.

---

## 1. What this session did — Units 31–33 (all in `Infrastructure.Extraction`, driven by `Tests.Infrastructure.Extraction`)

| Unit | File(s) | Result | Tests |
|---|---|---|---|
| 31 | `Analysis/DocxStructureAnalyzer.cs` + `Teseract/DocxFieldExtractor.cs` + `Teseract/DocxMetadataExtractor.cs` | 61+72+136 killed (100% / 97.3% / 95.8%), **0 killable survivors** | ~70 |
| 32 | `Teseract/PdfMetadataExtractor.cs` | 214 killed (87.7%), **0 killable survivors** | ~17 |
| 33 | `Teseract/CompositeMetadataExtractor.cs` | 5 killed (100%), **0 killable survivors** | 4 |

Combined scoped run (the 5 new files): **Killed 488, Survived 0, Timeout 0, 38 NoCoverage (all floor)**.
The Extraction-base `stryker-config.json` now mutates **14 files** (the 9 from Units 25–30 + these 5). `coverage-analysis`
is `perTest`. Commit: tests+config in one (`Killed 0 -> 488`), then this doc-set.

**✅ §2.4 Extraction base is COMPLETE** (Units 25–33). The base `Ocr.Strategies/*` are DEAD CODE — skip.

## 2. Key lessons added this session (full detail in the guide, Units 31–33)

1. **Dates are culture-parsed** (`DateTime.TryParse`) — use ISO `yyyy-MM-dd` + `DD/DD` values that read identically
   in every locale (`12/12/2025`, `12-12-2025`); isolate each of the three date patterns with a value only it matches.
2. **`[^\n\r]+`/`[^\n]+` captures run to end-of-text.** OpenXml joins paragraph `<w:t>` with single spaces (no
   newlines) → make a Causa/Acción value the **trailing** text for an exact capture; for Pdf, OCR text is a raw
   string so real `\n` works.
3. **`patternViolations++` is a dead branch in all three extractors** — the loose RFC scan's captured value always
   passes the anchored `^…$` re-test, and `ExtractAreaDescripcion` only emits catalogued areas or `""`. Floor.
4. **PdfMetadataExtractor's direct-text path is a placeholder** (`TryExtractTextFromPdfAsync` → `Success("")`), so
   `usedDirectExtraction` is always false → the `BuildExtractionMetadata` `else` block, the `Length < 50` right
   operand, and the generic/OCE catches (the OCR helper pre-catches) are **dead**. That's most of Pdf's 30 floor.
   The genuinely reachable error branches (OCR-yields-empty, preprocess/ocr `Success(null!)`, `ExtractTextAsync`
   OCR-failure) were covered after a first pass. `Result<T>.Success(null!)` is accepted by IndQuestResults.
5. **perTest NoCoverage on demonstrably-covered lines** (Pdf L58/L174 prefix string) — large-suite (405 tests)
   attribution noise; the failure tests provably hit those lines. Strengthened the assertion to pin the literal
   prefix; did **not** re-run Stryker for two string mutants (no-chasing rule).
6. **Reachable defensive error-branches are easy to miss on first pass.** Build small fixtures for them:
   valid-package-missing-Body (docx with `MainDocumentPart.Document` but no `Body` → `InvalidOperationException`),
   no-MainDocumentPart (`WordprocessingDocument.Create` without `AddMainDocumentPart` → opens with null part),
   empty `new Table()` (round-trips, hits `rows.Count == 0`), null `fieldDefinitions`/`fieldName` (→ catch block).

## 3. Where mutation testing stands

- **~43 files hardened across 7 projects**, all **0 killable survivors**.
- **Infrastructure deterministic surface ≈ complete:** Extraction.Txt, Extraction.Adaptive, Classification
  (deterministic), Export base, Export.Adaptive, Imaging, and now **Extraction base (§2.4)** are all done.
- **Overall ≈ 70% of the worthy deterministic business-logic surface.** Largest unsurveyed remainder is
  **§2.6 Core/Application** (~11 `*Service.cs`, ~6–8 pure/worthy).

## 4. Next units (priority order) — §2.5, then survey §2.6

1. **§2.5 Metrics / FileStorage** (small, deterministic): `Infrastructure.Metrics/ProcessingMetricsService.cs`,
   `Infrastructure.FileStorage/SafeFileNamerService.cs` (pure naming — easy), `FileMoverService.cs`. ⛔ skip the
   `FileSystemDownloadStorageAdapter` / options DTOs. Each needs a `stryker-config.json` next to its test project.
2. **§2.6 Core/Application** — **survey first** (`01 Core/Application`, ~11 `*Service.cs`): pick the pure ones
   (`DecisionLogicService`, `ConfigurationValidationService`, `SLATrackingService`, `AuditReportingService`,
   `FieldMatchingService`, `MetadataExtractionService`); skip the I/O orchestrators (`DocumentIngestionService`,
   `FileDownloadService`, `HealthCheckService`).
3. Optional: `Infrastructure.Classification/SemanticAnalyzerService.cs` (deterministic Levenshtein + in-repo
   dictionary, NOT Ollama).

⛔ **Avoid (non-deterministic / out of scope):** `TesseractOcrExecutor`, `GotOcr2OcrExecutor`, `OcrProcessingService`,
`PdfOcrFieldExtractor`, `PdfToImageConverter`, `OcrSessionRepository`, `BulkProcessingService`; native
EmguCV/OpenCv/PIL filters; DB/Testcontainers; UI/Playwright; legacy events.

## 5. STILL DEFERRED to end-of-campaign (unchanged)

1. **Unit 24 mutation-verify re-run** — the 3 Imaging `*FilterSelectionStrategy.cs` have green value-exact tests
   but the confirming Stryker run was deferred.
2. **Native-mutation Windows popups permanent fix** — mutating EmguCV `CvInvoke.*` crashes mutant processes → WER
   dialogs. Suppress WER UI / scope native files / run headless.

## 6. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
Minor: `MatchingPolicyService.GetConflictThreshold(string)` unused private — safe to delete.
Plus a new minor dead-branch note: `patternViolations++` is unreachable in the Docx/Pdf metadata extractors
(documented as floor, not a defect — extraction still produces the right counters).

## 7. The per-unit loop (reminder)

1. Pick a pure/deterministic file; read it + existing tests for gaps. Confirm the right test project drives it.
2. For large files, run a **scoped baseline** first (set `mutate` to just those files).
3. Write **exact-value** tests (Shouldly; `TestContext.Current.CancellationToken`; NSubstitute; no Moq/FluentAssertions).
4. Build + run the test project green.
5. Run scoped: `cd <test-project-dir> && StrykerCompat=true dotnet stryker` (~10–22 min; launch in background).
6. Parse `StrykerOutput/<ts>/reports/mutation-report.html` (`app.report = {...}`; `json.JSONDecoder().raw_decode`).
   **Trust "Killed delta + 0 killable survivors", not the headline %.** Classify NoCoverage: cover the **reachable**
   error-branches; classify the rest as floor (Serilog / dead-defensive / equivalent / dead-branch / perTest noise).
7. **Re-add ALL hardened files to the config's `mutate` list** before committing. Commit test+config (Killed delta
   in subject) separately from the docs. Update `mutation-testing.md` + roadmap + memory; push.

## 8. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 31–33 this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Prior handoff (Units 28–30): `docs/development/sessions/HANDOFF-2026-06-09-mutation-extraction-base-units28-30.md`
- Imaging handoff (still holds the two deferred items): `HANDOFF-2026-06-09-mutation-imaging-done.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0; `test-runner: mtp` + `StrykerCompat=true` mandatory):
  `CLAUDE.md` → "Testing stack"
