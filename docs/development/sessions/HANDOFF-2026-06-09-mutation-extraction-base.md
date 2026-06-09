# Handoff — Mutation testing: §2.4 Extraction base progressing (Units 25–27) (2026-06-09)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (canonical guide — setup, per-unit results, all
lessons; Units 25–27 are this session) and `docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan).
**Supersedes** `HANDOFF-2026-06-09-mutation-imaging-done.md` for the "next units" question (Imaging §2.3 is done;
we're now inside §2.4 Extraction base).
**Branch:** `Kt2` — all work this session **committed AND pushed** (`origin/Kt2` at `3084f4f`). Build green,
`Tests.Infrastructure.Extraction` **187/187** green.

---

## 1. What this session did — Units 25–27: §2.4 Extraction base (in progress)

All three live in `02 Infrastructure/Infrastructure.Extraction` (assembly
`ExxerCube.Prisma.Infrastructure.Extraction.Ocr`), driven by the `Tests.Infrastructure.Extraction` project.
A `stryker-config.json` was created there and now mutates **4 files**.

| Unit | File(s) | Result | Tests |
|---|---|---|---|
| 25 | `Teseract/XmlFieldExtractor.cs` | 70.41% → **95.41%**, Killed 138→187, 0 killable survivors | (prior) |
| 26 | `TextSanitizer.cs` + `OcrSanitizationService.cs` | **both 100.00%**, 0 survivors / 0 NoCoverage (70 + 21 killed) | +29 |
| 27 | `Teseract/FileTypeIdentifierService.cs` | Killed 58 / Surv 8 / NoCov 5 (81.69% headline), **0 killable survivors** | +25 |

Commits: `f1886bc`/`1648601` (Unit 26 tests+docs), `aa98eed`/`3084f4f` (Unit 27 tests+docs). All on
`origin/Kt2`. Range this session: `f1babeb..3084f4f`.

## 2. Key findings / lessons added this session

1. **Check which test project actually drives a class before assuming coverage** (Unit 26). `TextSanitizer` was
   covered *only* by the flaky `Tests.Infrastructure.Extraction.Teseract` suite (out of scope for mutation runs),
   so it was **dark** in the deterministic `Tests.Infrastructure.Extraction`. Tested fresh there → 100%.
2. **A `try/catch` that wraps only non-throwing operations is an uncoverable block** (Unit 27).
   `FileTypeIdentifierService`'s `catch` can't be reached from the public API: guarded indexing (`length < 4`),
   bounded `Encoding.UTF8.GetString(..Math.Min..)`, and **modern `Path.GetExtension` no longer throws on invalid
   chars**. Don't contort the SUT to reach it — log it as the equivalent floor (its 5 NoCoverage + the Serilog
   statement/text/`?? "unknown"` mutants are all the floor).
3. **`xl/`-only ZIP is a subtle third path** in `FileTypeIdentifierService`: `Contains("word/") ||
   Contains("xl/")` true but inner `Contains("word/")` false → returns **neither Docx nor Zip** (falls to null).
   Worth an explicit test so a mutant can't collapse it to Docx or Zip.
4. **A clean 100% is achievable on small pure string-logic units** (Unit 26) — no equivalent floor at all when
   there's no logging and no dead defensive code.

## 3. ⚠️ Carried-over loose end — `MexicanNameFuzzyMatcher` needs test-porting first

It's the obvious next §2.4 candidate (pure fuzzy matching, rich surface) **but its existing tests live in
`Tests.Infrastructure.Extraction.Teseract`**, a *different* project from the stryker-driven
`Tests.Infrastructure.Extraction`. Adding it to the mutate list as-is → **all NoCoverage**. Port/rewrite its
mutation-killing tests into `Tests.Infrastructure.Extraction` first (same pattern as the others). Its surface:
`IsMatch` (null/whitespace guard, non-name-field exact-match short-circuit, non-Mexican normalized-exact branch,
fuzzy `>= 85` threshold), `IsNameField` (the 6 regex patterns, special-char `$`/`/` exclusion, all-digits, the
`>= 0.80` letter-ratio), `IsLikelyMexicanName` (accent/ñ detector, given/surname sets, `ez`/`es` endings),
`GetSimilarityScore`, `NormalizeForComparison`/`RemoveDiacritics`.

## 4. Next units to harden — §2.4 Extraction base (remaining)

Still in `Tests.Infrastructure.Extraction`, pure/deterministic, no native/popup risk:
- `MexicanNameFuzzyMatcher.cs` — **port tests first** (see §3).
- `PdfMetadataExtractor.cs`, `DocxMetadataExtractor.cs`, `CompositeMetadataExtractor.cs`, `DocxFieldExtractor.cs`,
  `DocxStructureAnalyzer.cs` — **first resolve the Strategies/ duplication question** (roadmap §2.4):
  `ComplementExtractionStrategy.cs`, `SearchExtractionStrategy.cs`, `StructuredDocxStrategy.cs` appear here AND
  (already hardened) under `Extraction.Adaptive/Strategies/` — confirm live vs dead before testing.
- `DocumentComparisonService.cs`, `AdditionalFieldsReconciler.cs`, `XmlExpedienteParser.cs`, `XmlMetadataExtractor.cs`.
- ⛔ **avoid** OCR/render/DB-coupled: `TesseractOcrExecutor`, `GotOcr2OcrExecutor`, `OcrProcessingService`,
  `PdfOcrFieldExtractor`, `PdfToImageConverter`, `OcrSessionRepository`, `BulkProcessingService`.

After §2.4: §2.5 Metrics/FileStorage, then §2.6 Core (roadmap).

## 5. ⏳ STILL DEFERRED to end-of-campaign (roadmap §2b) — unchanged

1. **Unit 24 mutation verification** — the three Imaging `*FilterSelectionStrategy.cs` files have 53 green tests
   but the confirming Stryker run was deferred (pure managed → no popups; scope the Imaging mutate list to just
   those 3, run, confirm 0 killable survivors, restore full list). Details:
   `HANDOFF-2026-06-09-mutation-imaging-done.md` §2.
2. **Native-mutation Windows popups permanent fix** — mutating EmguCV `CvInvoke.*` crashes mutant processes →
   WER dialogs (~5–6/run). Results unaffected (crash = killed). Fix: suppress WER UI / `SetErrorMode`, or scope
   native files into their own run, or run headless/CI.

## 6. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from prior handoffs:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is unused private — safe to delete.

## 7. Status / numbers

- **~33 files hardened across 7 projects** (Txt, Adaptive, Classification, Export, Export.Adaptive, Imaging,
  **Extraction base**) ≈ 45%+ of the worthy deterministic surface.
- Build green; `Tests.Infrastructure.Extraction` **187/187** green.
- All commits **pushed** to `origin/Kt2` (`3084f4f`).

## 8. The per-unit loop (reminder)

1. Pick a pure/deterministic file; read it + its existing tests for gaps.
2. Write exact-value mutation-killing tests (boundaries, per-operand, branch alternatives, ordering, guards).
3. Build + run the test project (`dotnet test <proj>.csproj`) — confirm green. (Filter-query is finicky under
   MTP; running the whole small project is simplest.)
4. Add the file to that project's `stryker-config.json` mutate list. Run scoped:
   `cd <test-project-dir> && StrykerCompat=true dotnet stryker --mutate "**/<File>.cs"`.
5. Parse `StrykerOutput/<ts>/reports/mutation-report.html` (`app.report = {...}`; `json.JSONDecoder().raw_decode`)
   for Survived/NoCoverage. **Trust "Killed delta + 0 killable survivors", not the headline %** (timeout
   inflation + perTest noise). Classify residuals as equivalent floor (Serilog, dead catch, correlated guards).
6. Commit tests+config, then docs; update guide + roadmap + memory; push.

## 9. Pointers

- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 25–27 are this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Prior handoff (Imaging §2.3 + the two deferred items): `docs/development/sessions/HANDOFF-2026-06-09-mutation-imaging-done.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0; `test-runner: mtp` + `StrykerCompat=true` are
  mandatory): `CLAUDE.md` → "Testing stack"
