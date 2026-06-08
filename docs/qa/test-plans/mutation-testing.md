# Mutation Testing (Stryker.NET) — setup, pilot, and how to expand

**Added:** 2026-06-07b. **Tool:** Stryker.NET **4.14.2**, pinned as a repo-local tool in
`.config/dotnet-tools.json` (run `dotnet tool restore` once after cloning).

## Why this only works now
Stryker.NET could not drive **xUnit v3 + Microsoft.Testing.Platform (MTP)** test projects until the
4.x line added MTP support (~March 2026). This repo's test stack is `xunit.v3.mtp-v2` + MTP 2.1.0 (see
CLAUDE.md "Testing stack"), so mutation testing was previously unavailable. Stryker 4.14.2 supports it.

## What it is
Mutation testing measures **test-suite effectiveness**, not coverage. Stryker injects small faults
("mutants") into production code (flip `>` to `>=`, `&&` to `||`, drop a statement, etc.) and re-runs the
tests. A mutant **killed** = a test failed (good — the suite caught the fault). A mutant **survived** =
all tests still passed (a blind spot — code a test should pin but doesn't). The **mutation score** =
killed / (total non-error mutants).

## Pilot (intentionally tiny — proves the toolchain)
Scoped to one deterministic, well-tested unit so the first run is fast and the MTP compatibility is proven:

- **Config:** `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt/stryker-config.json`
- **Under test:** `Infrastructure.Extraction.Txt` → `AdaptiveTxtFieldExtractor.cs`
- **Driven by:** `Tests.Infrastructure.Extraction.Txt` (35 tests, deterministic — no OCR/DB/network)

Run it:
```bash
dotnet tool restore   # once
cd "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt"
dotnet stryker
# HTML report -> ./StrykerOutput/<timestamp>/reports/mutation-report.html
```

### Pilot result (2026-06-07b) — ✅ WORKING, real mutation score

```
Coverage capture complete: 272 mutations covered, 0 static
26 mutants NoCoverage · Killed 64 · Survived 167 · Timeout 0
Final mutation score: 24.90 %   (1m10s)
```

The toolchain runs end-to-end and produces a **real, trustworthy score** on the xunit.v3 + MTP +
custom-artifacts stack. 24.90% was a meaningful first baseline: the 35 passing tests pinned only ~¼ of
`AdaptiveTxtFieldExtractor`'s behavior — the 167 survivors were concrete targets for stronger assertions.

### Survivor-driven hardening (2026-06-08) — ✅ 24.90% → 82.10%

The 167 survivors were parsed out of the HTML report and grouped by extractor method. The bulk were
**ten extraction helpers with no value-asserting coverage** (Email, Telefono, CodigoPostal, Direccion,
FundamentoLegal, AutoridadEspecifica, FechaPublicacion, DiasPlazo, TieneAseguramiento, NombreSolicitante),
plus weak (`ShouldNotBeNull`-only) assertions on the `ExtractFieldByName` alias switch, the produced
`FieldValue` metadata, the authority-priority ladder, and the labeled-NumeroOficio / OCR-fuzzy-expediente
branches. Added `AdaptiveTxtFieldExtractorMutationKillingTests.cs` (88 tests, every one pinning an **exact**
value so a string/equality/boolean/block mutation is observable).

```
35 (orig) + 88 (new) = 123 tests, all green
296 mutations covered · Killed 211 · Survived 40 · NoCoverage 6 · Timeout 0
Final mutation score: 82.10 %   (~2m)
```

The remaining **40 survivors are the equivalent-mutant floor** and were deliberately left:
- `match.Success || match.Groups.Count > 1` (`&&`→`||`) and `Count > 1`→`>= 1` — a capture-group match
  always has `Groups.Count ≥ 2`, and `Success`/`Count>1` are perfectly correlated, so these don't change
  behavior (≈22 mutants).
- Serilog format-string / statement mutations and the inner "not found" failure message that the public
  method discards (it re-wraps with its own message); plus the defensive `catch` blocks (NoCoverage) that
  can't be entered without forcing a `Regex` exception.
- `source.OcrConfidence ?? 0.8f` "remove-left" — the confidence is discarded in the `ExtractFieldsAsync`
  output path (`ExtractedFields` stores strings, not `FieldValue`); it is pinned where it *is* observable,
  via `ExtractFieldAsync`.
- `RegexOptions.Multiline | IgnoreCase` (`|`→`&`) where neither flag changes matching for that pattern
  (no `^`/`$`; case handled by explicit alternations).

Killing those would require log-output inspection or contrived inputs that don't reflect real OCR text.
**82.10% is the honest ceiling for this unit** given the equivalent mutants.

**Two settings are MANDATORY here (this is the whole story — both were the cause of an earlier 0% run):**

1. **`"test-runner": "mtp"` in `stryker-config.json`.** Without it Stryker defaults to the **VSTest bridge**,
   which against MTP-native test projects (xunit.v3 / `xunit.v3.mtp-v2`) runs tests but **cannot observe
   kills** → a uniform **0% score that is always broken**. This is non-negotiable for any MTP project.
2. **Output-path accommodation for this repo's custom layout.** `Directory.Build.props` has a guarded,
   OFF-by-default block: when `StrykerCompat=true` (env var, surfaced to MSBuild), `OutputPath` and
   `IntermediateOutputPath` flatten to drop the `<Configuration>` segment, because this repo uses the
   non-standard `bin\<project>\<config>\<tfm>` layout and Stryker reconstructs `bin\<project>\<tfm>`
   (and `obj\…\ref\`). Run mutation testing with it set:
   ```bash
   StrykerCompat=true dotnet stryker   # from the test project dir
   ```
   Repos that use the **default** `bin/<config>/<tfm>` layout (e.g. IndFusion.Ember) need **only** setting #1.

### Second unit: EnhancedFieldMergeStrategy (2026-06-08) — ✅ 66.22% → 87.16%

Widened the same survivor-driven approach to a second deterministic unit in the Adaptive-DOCX project
(`Infrastructure.Extraction.Adaptive` → `EnhancedFieldMergeStrategy.cs`, driven by the 126-test
`Tests.Infrastructure.Extraction.Adaptive`). Config: a second `stryker-config.json` next to that test
project with `mutate: ["**/EnhancedFieldMergeStrategy.cs"]`.

Baseline survivor analysis (Killed 61 / Survived 10 / Timeout 37 / **NoCoverage 40**) showed the existing
`*LiskovTests` verify the `IFieldMergeStrategy` contract on happy paths but **only ever exercise an
Expediente conflict**, never assert conflict details, and never merge `AdditionalFields` or feed duplicate
`Montos`/`Fechas`. Added `EnhancedFieldMergeStrategyMutationKillingTests.cs` (18 tests; 126+18=144 green)
pinning: per-field conflict details (`ResolvedValue` / `ResolutionStrategy` strings `"First non-null value"`
and `"Primary source preference"` / exact `ConflictingValues`) for both overloads, no-conflict-on-identical-
values, the `MergedFieldNames` bookkeeping, secondary-fill branches, and the collection-dedup rules
(`AdditionalFields` first-key-wins, `Montos` unique-by-`(Currency,Value)`, `Fechas` exact-dedup).

```
296 mutations covered · Killed 61 -> 124 · Survived 10 -> 7 · NoCoverage 40 -> 12 · Timeout 37 -> 5
Final mutation score: 66.22 % -> 87.16 %
```

Two notes for future expansion:
- **Score formula (confirmed here):** `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`.
  `NoCoverage` counts against you (so adding *any* covering test helps); `Timeout` counts as detected.
- **Timeouts were runner overhead, not hangs:** the 37 baseline timeouts (spread across logging / `SourceCount++`
  lines that can't loop) collapsed to 5 once the NoCoverage lines got real tests — they were MTP per-mutant
  session overhead tripping a short timeout, not infinite loops. Adding coverage stabilizes them.

The residual 7 survivors + 12 NoCoverage are the equivalent-mutant floor: redundant
`ThrowIfCancellationRequested` checkpoints (removing the first is masked by the second), the defensive
`catch (OperationCanceledException)` / `catch (Exception)` logging blocks (unreachable without a contrived
mid-merge cancellation — the token check throws *before* the `try`), and Serilog statements.

### Third unit: FieldMatcherService<T> (2026-06-08) — ✅ 30.71% → 61.43% (+ a dead-code finding)

Third deterministic unit, in `Infrastructure.Classification` (`FieldMatcherService.cs`, driven by the
126-test `Tests.Infrastructure.Classification`; config mutates only that file). The existing
`FieldMatcherServiceTests` cover the public happy paths but only with a single **DOCX** source type, never
populate `AdditionalFields`, never exercise `AccionSolicitada`, and assert the unified record / completeness
shallowly. Added `FieldMatcherServiceMutationKillingTests.cs` (16 tests; 126+16=142 green) pinning: the
`AdditionalFields` merge pipeline (non-empty merged, whitespace skipped, `"Origin"` key excluded); the
`GetSourceType`/`ToOrigin` switches for DOCX/PDF/XML/unknown source types (via `FieldMatcherService<PdfSource>`
etc. + two-agreeing-sources so the policy preserves `SourceType`+`Origin`); `AccionSolicitada` + alias;
`OverallAgreement` = **average** (not min) of per-field agreements; `GenerateUnifiedRecord` actually applying
core fields; and `ValidateCompleteness` null/empty/multi-missing branches.

```
Killed 43 -> 84 · Survived 29 -> 32 · NoCoverage 68 -> 22 · Timeout 0 -> 2
Final mutation score: 30.71 % -> 61.43 %
```

**Why the ceiling is lower than the other two units (and a real finding):** ~35 of the ~54 residual mutants
live in **dead-equivalent code** — `CollectAdditional` is called once with `FieldOrigin.Xml` and once with
`FieldOrigin.PdfOcr`, but **both branches do identical work** (the origin param only gates excluding the
`"Origin"` key, which both branches exclude the same way). So `xmlFields` and `ocrFields` are always
identical, which makes `MergeAdditionalFields`' XML-vs-OCR conflict detection (and `Normalize`, used only
there) **unreachable** — no mutant in it can be killed through the public API. **The XML-vs-OCR additional-
field conflict detection does not actually work.** This is a genuine code smell surfaced by mutation testing;
fixing it (make `CollectAdditional` filter by the field's real origin) is a separate change, out of scope for
this test-hardening pass. **Full finding + suggested fixes (for a dedicated session):**
`docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`. The other ~19 residual are Serilog/`catch`-block statements and equivalent
comparison mutants (`Count > 0` → `>= 0` where the collection is only populated when non-empty).

### Fourth unit: MatchingPolicyService (2026-06-08) — ✅ 41.05% → 56.76%

Fourth deterministic unit, also in `Infrastructure.Classification` (`MatchingPolicyService.cs` — the
value-selection / agreement / conflict policy that `FieldMatcherService` delegates to). The project's
`stryker-config.json` now mutates **both** `FieldMatcherService.cs` and `MatchingPolicyService.cs`. Added
`MatchingPolicyServiceMutationKillingTests.cs` (13 tests; the project is now 155 green) pinning: the
all-blank `"NONE"` sentinel result, `finalConfidence = agreementLevel * average(confidences)` (kills the
`Average`→`Min` and `*`→`/` mutants), the null / all-blank / single-value edges of
`CalculateAgreementLevelAsync` and `HasConflictAsync`, the custom-vs-default `HasConflictAsync` threshold
ternary (incl. a service whose `ConflictThreshold` is non-default so the default branch is observable), and
source-type resolution (options-level fallback to highest confidence + per-field-rule priority override).

```
MatchingPolicyService.cs: Killed 39 -> 54 · score 41.05 % -> 56.76 %
```

Residual is the equivalent/dead/defensive floor: `bestGroup.First()` → `FirstOrDefault()` on always-non-empty
groups; the `hasConflict` formula `Count > 1 && best < total` (its two clauses are logically equivalent, so
`&&`/`||`/`>=`/`<=` variants don't change the result); the `?? string.Empty` group-key fallback (unreachable
because values are pre-filtered non-blank); the `catch` blocks; and **`ApplySourcePriority`'s reordering,
which can only change tie-break order, not the selected value** — so its `OrderBy`/ternary mutants are
unobservable through the result. Minor finding: **`GetConflictThreshold(string)` is an unused private method**
(`HasConflictAsync` reads `_options.ConflictThreshold` directly) — dead code; safe to delete.

### Fifth unit: NameMatchingPolicy (2026-06-08) — ✅ 3.08% → 36.92% (+ a high-severity finding)

Fifth deterministic unit, the third in `Infrastructure.Classification` (`NameMatchingPolicy.cs` — fuzzy,
alias-aware person/authority-name matching; the policy `FieldMatcherService` routes `*NOMBRE*` fields to).
It had **no direct tests at all** (baseline 3.08%). The project's `stryker-config.json` now mutates all three
matching files. Added `NameMatchingPolicyMutationKillingTests.cs` (12 tests; project now 167 green).

```
NameMatchingPolicy.cs: Killed 2 -> 24 · score 3.08 % -> 36.92 %
```

**The low ceiling is itself the finding (high severity).** `SelectBestValueAsync` and
`CalculateAgreementLevelAsync` both score every value **against itself**, and `ScorePair(x, x) == 1.0`, so the
maximum score is always 1.0: the winner is always the first value, the score/agreement is always 1.0, and a
conflict is **never** reported — for any inputs. That makes the fuzzy / alias / accent-normalization logic
(~70% of the file: `Normalize`, `ScorePair`'s fuzzy+alias branches, `IsAlias`, `BuildAliasMap`) **unobservable
through the public API**, so those mutants can't be killed until the defect is fixed. The fuzzy name matching
does not actually function. **Full finding + suggested fix:**
`docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md`.

The 12 tests deliberately pin only behavior that stays correct after a fix (input guards, result
construction, identical-name → 1.0 / no-conflict, the `Math.Max` aggregation, `HasConflict` threshold
comparison) — they will keep passing once the diagonal is excluded, and be joined then by real cross-name
tests.

> **Two open conflict-detection findings now exist** (this one and the FieldMatcher `CollectAdditional` one).
> Both are paths that silently never fire; consider tackling them together in a dedicated session.

## Cross-repo guideline (for restoring mutation testing org-wide)
Confirmed by diffing this repo against the known-working **IndFusion.Ember** setup (same Stryker 4.14.2,
same MTP `global.json`):

| Concern | Required | Notes |
|---|---|---|
| Stryker version | `dotnet-stryker` **4.x** (4.14.2 verified) | 4.x is the first line supporting xUnit v3 + MTP (~2026-03). Pin it in `.config/dotnet-tools.json`. |
| **Test runner** | **`"test-runner": "mtp"`** | MANDATORY for MTP/xunit.v3 projects. Omitting it = silent 0% via the VSTest bridge. The single most important setting. |
| Coverage analysis | `"perTest"` works once test-runner is `mtp` | With the VSTest default, coverage capture *fails* ("coverage capture failed"); with `mtp` it succeeds. |
| Config location | next to the **project under test** (Ember) or the **test project** (this repo) | Both work; set `project` + `test-projects` to disambiguate. |
| Output layout | default `bin/<config>/<tfm>` → nothing needed | Custom/redirected `OutputPath` (this repo) needs the `StrykerCompat` flatten above, or Stryker can't find the binaries / `ref` assemblies. |
| Strict config | no unknown keys (not even `_comment`) | Stryker validates `stryker-config.json` strictly and aborts on extras. |

## How to expand
Mutation runs are **expensive** (each mutant re-runs the suite), so scale deliberately:

1. **Widen within a project** — drop the `mutate` filter (or add globs) to cover the whole project, not
   just one file.
2. **Add more projects** — give each a `stryker-config.json` next to its test project. Best candidates are
   **deterministic, fast** suites: Domain, Application, Classification, Export(.Adaptive), Extraction(.Txt/
   .Adaptive/XML), Imaging. **Avoid** mutating code whose tests depend on live OCR (`Extraction.Teseract`,
   `System.Ocr.Pipeline` — flaky), Docker (`System.Storage`, `Infrastructure.Database`), Playwright
   (`Tests.UI`, `BrowserAutomation.E2E`), or the dormant Python/VLM paths — non-determinism and slowness
   make mutants ambiguous.
3. **CI:** run with `--since` (diff-based) so PRs only mutate changed files; reserve full runs for nightly.
   Set `thresholds.break` above 0 once a baseline is agreed to fail the build on regressions.

## Notes / gotchas
- `stryker-config.json` is **strictly validated** — unknown keys (even `_comment`) fail the run. Keep
  prose in this doc, not the config.
- Stryker auto-detects the project under test from the test project's references; `project` +
  `test-projects` in the config disambiguate when there are several.
- Output (`StrykerOutput/`) is build-artifact noise — it is git-ignored at the repo level; don't commit it.
