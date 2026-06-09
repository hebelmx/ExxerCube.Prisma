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

### Units 6–10: the five Adaptive-DOCX strategies (2026-06-08)

The five `IAdaptiveDocxStrategy` implementations in `Infrastructure.Extraction.Adaptive/Strategies/`
(`TableBased`, `StructuredDocx`, `ContextualDocx`, `SearchExtraction`, `ComplementExtraction`), driven by the
same `Tests.Infrastructure.Extraction.Adaptive` project. Each had a `*LiskovTests` that verifies the
interface contract on one rich sample document but with **loose assertions** — `if (AdditionalFields.ContainsKey(...))`
guards, `ShouldContain`, `ShouldBeGreaterThan(0)`, confidence *ranges* (`>= 70/85`) — so the field mappings,
the confidence ladders, the currency-normalization switches, the per-branch `hasAnyData` bookkeeping and the
cleanup regexes were never pinned to exact values. The project's `stryker-config.json` now mutates all six
hardened files (the five strategies + `EnhancedFieldMergeStrategy`). Adaptive project **126 → 282 tests**, all green.

| Unit | New tests | Killed (Δ) | Outcome |
|---|---|---|---|
| TableBasedDocxStrategy | 26 | 99 → 157 | 63.19% → **96.32%**, 0 survivors |
| StructuredDocxStrategy | 25 | 101 → 132 | every observable mutant killed |
| ContextualDocxStrategy | 29 | 41 → 103 | 84.62% → **96.45%**, 0 survivors |
| SearchExtractionStrategy | 29 | 54 → 138 | every observable mutant killed |
| ComplementExtractionStrategy | 29 | 49 → 120 | 93.58% → **97.25%**, 0 survivors |

What the new tests pin (per unit, exact-value): the confidence ladders at their exact rungs/boundaries
(pipe-count 95/85/60/0 for TableBased; label-count 90/75/50/0 for StructuredDocx; keyword-count 80/70/50/0
for ContextualDocx; and the `(keywordCount, extractionScore)` **2-tuple** switches 75/65/50/0 and 85/75/60/0
for Search/Complement — each crafted to sit on its boundary), the `CanExtract` thresholds (whitespace + the
`>= 2` keyword boundary), the shared currency switch (`M.N.`/`pesos` → MXN, USD, EUR prefix, default MXN), the
`amount > 0` zero-rejection (Search), single-core-field non-null results + the no-data null return, the
Causa/Accion cleanup regexes, and the exact extended-field/account values (NumeroOficio, AutoridadNombre, RFC,
CLABE incl. standalone-no-keyword, Banco) the Liskov tests only checked behind `ContainsKey` guards.

**The dominant lesson — timeout-inflated baselines (READ THIS before trusting a regex-heavy unit's score).**
StructuredDocx/Search/Complement (and to a lesser degree the others) are **regex-heavy**, and Stryker counts a
**`Timeout` as killed** (a mutation that hangs the suite is "detected"). When a mutated regex pattern triggers
catastrophic backtracking it times out — so on a cold/high-variance run dozens-to-hundreds of mutants land in
`Timeout` and inflate the headline score. Examples observed: StructuredDocx **baseline 94.97% with 92 timeouts**
that, once real coverage was added, collapsed to ~11–22 timeouts and exposed the true survivors (the score then
read 67–86% **across identical re-runs** purely on timeout count); Search baseline 129 timeouts; Complement 155.
**Consequences for the loop:**
- **Do not trust a high baseline on a regex-heavy unit** — it is probably timeout masking. Add coverage and
  re-run; the timeouts collapse and the real survivors appear.
- **The headline % is noisy** for these units (swings ~30 points run-to-run). Report the **stable** signal
  instead: the `Killed` count delta and **"0 (killable) survivors / residual is the equivalent floor."**
- Adding exact-value coverage is still the right move — it converts ambiguous timeouts into deterministic
  kills and surfaces the genuine survivors to pin.

**Residual / equivalent floor (consistent across all five):** Serilog format-string statements; the
`match.Success && match.Groups.Count > 1` capture-group guards (a *failed* `Regex.Match` has `Groups.Count == 1`
and a *successful* capture has `> 1`, so the comparison can never change behaviour); the **redundant alternate
regex patterns** — every extractor tries several patterns where the first (optional-prefix / overlapping
char-class) subsumes the rest, so the later patterns are unreachable; the `\d{18}` CLABE `== 18 && All(IsDigit)`
validation (the regex guarantees both); post-`break` re-match no-ops; the two defensive `catch` blocks
(cancellation rethrows *before* the `try`; a `Regex` exception can't be forced with valid text); and one perTest
coverage-attribution artifact on the currency ternary. None are killable through the public API.

**Minor findings surfaced (not fixed — behaviour pinned as-is):** (1) the `Fecha`/date case in several strategies
sets neither `hasAnyData` nor a guard-counted collection, so a *date-only* document returns null. (2)
`StructuredDocxStrategy.ExtractMexicanNames` assigns captured groups **positionally** (`nombre=Groups[1]`,
`paterno=Groups[2]`, `materno=Groups[3]`) for *both* name patterns, so pattern[1]
(`PATERNO MATERNO Nombre`) mislabels the parts. (3) the Accion `(?:identificada|con|en).*$` cleanup matches the
substring `en` **inside** words like "aseguram**ien**to", truncating legitimate values. All three are documented
in the test comments.

### Unit 11: AdaptiveDocxExtractor — the orchestrator (2026-06-08)

The `AdaptiveDocxExtractor` (`Infrastructure.Extraction.Adaptive/AdaptiveDocxExtractor.cs`) that **selects among /
merges / complements** the five (now-hardened) strategies — finishing the Adaptive-DOCX project. Driven by the same
`Tests.Infrastructure.Extraction.Adaptive`; the project's `stryker-config.json` now mutates **all seven** files
(orchestrator + the six strategy/merge files). The existing `AdaptiveDocxExtractorLiskovTests` exercise the
orchestrator through the *real* strategies on a couple of happy-path documents, so the merge/complement internals,
the strategy-selection ladder, the confidence ordering, and the two `catch` blocks were never pinned to exact
values. Added `AdaptiveDocxExtractorMutationKillingTests.cs` (**31 tests; Adaptive project 282 → 313 green**),
driving the orchestrator with **NSubstitute mocks of `IAdaptiveDocxStrategy`** so per-strategy confidence/extraction
results are controlled precisely and the orchestrator's own logic is observable in isolation.

```
AdaptiveDocxExtractor.cs: Killed 28 -> 80 · Survived 0 -> 0 · NoCoverage 25 -> 0 · Timeout 77 -> 50
```

What the tests pin (exact-value): the `ExtractionMode` dispatch (BestStrategy ignores `existingFields` vs Complement
preserves them; invalid mode → `null` via the generic catch); the `OrderByDescending` confidence sort + exact
per-strategy values/names; the empty-input all-zero confidences; `BestStrategy` highest-confidence selection +
`Confidence == 0` → null guard; `MergeAll`'s `Confidence > 0` capable filter, all-null → null guard, the first-non-null
core-field merge, `AdditionalFields` first-key-wins, and `Montos` unique-by-`(Currency,Value)` / `Fechas` dedup; and
the `Complement` preserve-then-fill core `??` logic plus the copy-existing + add-unique collection rules.

**Cancellation-coverage lesson:** the `*LiskovTests` cancellation tests pass an **already-cancelled** token, which
trips `ThrowIfCancellationRequested()` **before** the `try` — so neither `catch (OperationCanceledException)` block is
ever entered (both were `NoCoverage`). A mock strategy that throws the `OperationCanceledException` from **inside** the
try (with a *live* token) is the only way to reach and pin the rethrows. Likewise the generic `catch` → `null` /
→ all-zero are reached via a strategy that throws a *plain* exception (from `ExtractAsync` for the orchestrator catch;
from `GetConfidenceAsync` for the confidences catch).

**Timeout-noise lesson (the non-regex variant of Units 6–10):** this unit has **no regex**, yet ~58 mutants still
landed in `Timeout` — here from **MTP per-mutant session overhead** on the large (313-test) suite, not catastrophic
backtracking. `Timeout` counts as detected, so the headline read **100%** — but parsing the `Timeout` bucket exposed a
handful of **functional** mutants the first test pass did *not genuinely* kill (they only "passed" via timeout): the
Causa/Accion `&&` merge guards, the `ComplementFields` core `??`, and the copy-existing `Montos`/`Fechas` `Add`
statements. Strengthened those to **deterministic kills** (both-set core fields for the `&&`; a mirror existing/new
split for the `??`; an existing-only `Monto`/`Fecha` so removing the copy line changes the count) — Killed 72 → 80,
Timeout 58 → 50. **So trust the Killed delta + "0 killable survivors", not the 100% headline.**

**Residual / equivalent floor:** Serilog `LogDebug`/`LogError`/`LogInformation` statement+string mutants and the
`string.Join` log interpolation (L111); the discarded `ArgumentOutOfRangeException` message (invalid mode is swallowed
by the generic catch → `null`, so the message is never observable); the L53 `ThrowIfCancellationRequested()` (removing
it is **backstopped** by the identical check at L91 inside `GetStrategyConfidencesAsync`); and `First` /
`FirstOrDefault` / `Single` / `Last`-equivalent Linq on guaranteed-single (unique-name) matches. None killable through
the public API. (The `Montos` `Any`/`==` dedup mutants at L268/L321 *are* killed by the precise count/value
assertions — they merely re-appear in `Timeout` run-to-run on this large suite.)

### Unit 12: LegalDirectiveClassifierService (2026-06-08) — ✅ 79.68% → 96.02%

The keyword/regex legal-directive classifier in `Infrastructure.Classification`
(`LegalDirectiveClassifierService.cs` — the fourth Classification file; config now mutates all four). The
existing `LegalDirectiveClassifierServiceTests` / `...EdgeCaseTests` (23 tests) drove the happy paths with
loose assertions (`ShouldContain`, `Confidence > 60`) and only ever exercised `MapToComplianceActionAsync`
with a **Block** directive — so the confidence ladder, the precedence ladder, `DetectDocumentRelationType`
(never tested at all), the amount/account heuristics, product-type detection, the `Circular` instrument
pattern, and every `ApplyEdgeCaseValidation` warning were unpinned. (Note: `LegalDirectiveClassifierDictionaryTests`
despite its name tests `SemanticAnalyzerService`, not this class.) Added `LegalDirectiveClassifierServiceMutationKillingTests.cs`
(**56 tests; Classification project 167 → 223 green**).

```
LegalDirectiveClassifierService.cs: Killed 170 -> 241 · Survived 6 -> 0 · Timeout 30 -> 0 · NoCoverage 45 -> 10
Final mutation score (clean perTest run): 79.68 % -> 96.02 %
```

What the tests pin (exact-value): `CalculateConfidence` = `60 + matches*10` at 70/80/90 and the `Math.Min(100,..)`
cap; the `DetermineActionTypeWithPrecedence` order (Unblock>Block>Transfer>Document>Information>Unknown) + the
per-type confidence selection (incl. the Information/Unknown=30 branches); all four `DocumentRelationType`
outcomes + the AMPLIA/ACLARA synonyms; the amount pattern-priority ($ > monto > pesos > plain-formatted), the
`digitsOnly.Length >= 10` account-skip heuristic (with a 9-digit decimal sitting just under the boundary to pin
**both** `digitsOnly` `Replace` calls) and the same/cross-priority tie-breaks; product-type TARJETA>CUENTA and
the null case; `DetectLegalInstruments` Circular + exact counts; and every `ApplyEdgeCaseValidation` warning
(Transfer→CLABE, Unblock→prior-reference, Block→missing-target, low-confidence `< 70` boundary) on **both** the
MapTo and the `ClassifyDirectivesAsync` code paths.

**Two lessons reinforced on this unit:**

1. **The headline % flaps hard run-to-run (perTest attribution noise on a large/slow suite), even with no
   regex.** Identical test sets produced 0, 5, 7, and 18 "survivors" across re-runs — almost all of them
   **Serilog statement/string mutants** (equivalent floor) flip-flopping between Killed/Survived/Timeout because
   perTest coverage attribution is unstable here. Trust the **Killed delta + "0 killable survivors"** from a
   clean run, not any single headline.

2. **A one-off `coverage-analysis: "off"` cross-check is worth running to separate real gaps from noise — but
   read it with two caveats.** Running it (temporarily, then revert to `perTest`) runs every test against every
   mutant, so attribution luck is removed. It surfaced **three genuine gaps** the noisy perTest runs hid: the
   `ExtractActionDetails`/`ApplyEdgeCaseValidation` **calls inside the `ClassifyDirectivesAsync` branches**
   (only the MapTo path was asserted), and a **contaminated test** — `"ordena"` embeds the substring `"ORDEN"`,
   one of the four prior-reference terms, so a "single reference" test stayed true via a second term and the
   `||`→`&&` mutants survived (fixed with a 4-term Theory using sentences free of `ordena`). **Caveats:** (a)
   coverage-off **over-counts** survivors via a Stryker **static-initializer limitation** — the `*Keywords`
   `string[]` field mutants (L296–300, 23 of them) survive in coverage-off's shared test process because the
   static array is initialized once and the per-mutant switch never re-runs the initializer; **perTest kills
   them**. (b) it is slower. So use coverage-off to *find* gaps, but report the score from `perTest`.

**Residual / equivalent floor (10 NoCoverage in the clean run):** the three `catch (Exception)` blocks
(`LogError` + the `$"…{ex.Message}"` failure message in all three public methods) — unreachable because valid
string input through `ToUpperInvariant`/`Regex` can't throw; and `CalculateConfidence`'s `if (matches == 0) return 0;`
— **dead through the public API** because every call site is gated behind the matching `Contains*Directive(text)`,
so `matches >= 1` always. Also equivalent (when they appear): the `bestMatch == null || …` first clause (when
`bestMatch` is null the `i < bestPatternPriority` term is always true since the priority is still `int.MaxValue`),
and the Serilog mutants above.

### Unit 13: FileClassifierService (2026-06-08) — ✅ 38.79% → 88.79%

The rule-based regulatory classifier in `Infrastructure.Classification` (`FileClassifierService.cs` — the
fifth and last Classification file; config now mutates all five). Fully deterministic: each Level-1 category
gets a 90/70/10 score from keyword presence, a confidence is derived from the score spread, and a Level-2
subcategory is picked by priority. The existing `FileClassifierServiceTests` (8 tests) asserted only
`Level1`/`Level2` with `Score > 70` and never touched the Informacion/Transferencia/OperacionesIlicitas
categories, the 70/10 rungs, the confidence ladder, the `/AS` shortcut, the `string.Join` separator, or the
Level-2 order. Added `FileClassifierServiceMutationKillingTests.cs` (**35 tests; Classification project 223 → 258 green**).

```
FileClassifierService.cs: Killed 45 -> 103 · Survived 56 -> 7 · NoCoverage 15 -> 6 · Timeout 0
Final mutation score: 38.79 % -> 88.79 %
```

What the tests pin (exact-value): every Level-1 high keyword → score **90** (a Theory with the keyword in
`AreaDescripcion`, so each row also pins the L41 `?? string.Empty` source) asserting `Confidence == 90` as a
sentinel (any stray keyword mutation that lights a second category collapses the score-spread and drops
confidence below 90); every secondary keyword → **70**; the no-keyword document → **all six scores = 10** (pins
the six `else` blocks — a removed block leaves the score at its `0` default); the `/AS` expediente shortcut
(Aseguramiento + Especial); the **space-join** multi-word keyword (`["OPERACIONES","ILICITAS"]` → matches only
when joined with a space, killing the `string.Join("", …)` mutant); each Level-2 keyword + the
Especial>Judicial>Hacendario priority + the null default; and the confidence ladder's `Min(100, max)`
high-clarity branch and `Min(70, average)` close-scores branch (which kills the `Average()`→`Min()`,
`Min()`→`Max()`, and difference-arithmetic mutants).

**Dead-branch finding (minor — pinned as floor, not fixed):** the **middle confidence tier**
(`scoreDifference >= 40` → `Min(85, maxScore)`, L228/L230) is **unreachable**. Because every category resolves
to one of three discrete scores {10, 70, 90}, the difference between the top score and the next is *always* one
of {0, 20, 60, 80} — never in [40, 60). So that `else if` body never executes (its `Min(85)` is `NoCoverage`)
and, at the boundary `diff == 60`, `>=60` and `>60` both cap at 70, making the L224 `>=`→`>` mutant equivalent
too. Documented in the test file; a fix (continuous scoring, or collapsing the tier) is out of scope for test
hardening.

**Residual / equivalent floor:** the dead middle tier above (L224/L228/L230); the Serilog `LogDebug` statements
(L37/L66); the `catch (Exception)` block (L71/L72 — unreachable, `ExtractedMetadata` access can't throw);
`OrderByDescending(...).First()`→`FirstOrDefault()` (L250 — the scores dictionary always has six entries, never
empty); and the `?? string.Empty` fallback strings (L41/L42 — only reached when `Expediente` is null, and the
fallback value is never a keyword, so the mutation is equivalent). **0 killable survivors.**

### Unit 14: SiroXmlExporter (2026-06-08) — ✅ 0 coverage → 86.22% (first Export unit)

The **SIRO-XML regulatory deliverable** in `Infrastructure.Export` (`SiroXmlExporter.cs`) — roadmap
**§2.1, top priority** because it had **ZERO test coverage** (the Export test project only tested
`DigitalPdfSigner`). First unit in the Export project; added a `stryker-config.json` next to
`Tests.Infrastructure.Export` (`mutate: ["**/SiroXmlExporter.cs"]`). Added `SiroXmlExporterTests.cs`
(**26 tests; Export project 26 → 52 green**).

```
SiroXmlExporter.cs: Killed 0 -> 169 · Survived 24 · NoCoverage 3 · Timeout 0
Final mutation score: 86.22 %
```

What the tests pin (exact-value): the pre-start cancellation guard; every input guard message
(null metadata / null stream / non-writable stream) and every `ValidateMetadata` message (null Expediente,
blank NumeroExpediente, blank NumeroOficio); the XML declaration, root element name and the
`http://siro.regulatory.namespace` namespace (parsed via `XDocument` local-name queries, robust to the
child `xmlns`); all required elements with exact values; the `yyyy-MM-dd` FechaPublicacion format; every
optional top-level element on **both** the present-when-set and absent-when-blank sides; the
SolicitudPartes/SolicitudEspecificas wrappers, the per-parte optional fields (present/absent), and
**multi-element sibling rendering** (two partes / two especificas + SolicitudEspecificas-is-root-child,
which kill the per-`WriteEndElement` removals — with a single element the removal is masked by
`WriteEndDocument` auto-close); the indentation settings (line count + `"\n  <"`); the schema branch
both ways — an empty `XmlSchemaSet` still succeeds (no Error-severity events), and an empty-content-model
schema fails with the `"SIRO schema validation failed: …"` message; and the Exception / OCE catch paths.

**Two lessons reinforced:**

1. **Reach the OCE catch by cancelling mid-write, not before.** An already-cancelled token trips the
   pre-`try` guard, so the `catch (OperationCanceledException) when (token.IsCancellationRequested)` filter
   is never entered. A custom `Stream` whose `WriteAsync` **cancels its own `CancellationTokenSource` and
   throws OCE** (with a *live* token passed in) is what reaches it; a separate stream that throws `IOException`
   on write reaches the generic catch.
2. **A "String mutation → \"\"" survivor can be a `Join` *separator*, not the whole message.** L327's
   `Result.WithFailure($"SIRO schema validation failed: {string.Join("; ", errors)}")` survived even under
   `coverage-analysis: "off"` (every test vs every mutant) despite a test asserting the literal — because the
   mutated string was the **`"; "` separator**, not the message; the content (and "SIRO schema validation
   failed" / "Position") still passed. The empty-content-model schema raises **exactly two** errors, so
   asserting the joined `"; "` is present pins it. (A throwaway diagnostic test printing the real `result.Error`
   is the fastest way to see which literal a string mutant actually hit.)

**Residual / equivalent floor (24 Survived + 3 NoCoverage):** Serilog statement/string/`?? "Unknown"`
mutants (the `Log*` calls); the `ConfigureAwait(false)` booleans + the `FlushAsync` no-op on a
`MemoryStream`; `WriteStartDocument()` removal (XmlWriter auto-emits the declaration when
`OmitXmlDeclaration=false`, so the decl is present with or without it); the **trailing**
`WriteEndElement`(SolicitudEspecificas/SiroResponse) + `WriteEndDocument` removals (the writer auto-closes
the remaining open elements on `Flush`/`Dispose`, so the output is byte-identical); the unreachable defensive
`if (_siroSchemaSet == null) return Success` **inside** `ValidateXmlSchema` (only called when non-null); and
the defensive `catch` in `ValidateXmlSchema` (can't force a validation exception with valid input). **0
killable survivors.**

### Unit 15: ExcelLayoutGenerator (2026-06-08) — ✅ 0 coverage → 0 killable survivors (second Export unit)

The **SIRO Excel-registration layout (FR18)** in `Infrastructure.Export` (`ExcelLayoutGenerator.cs`) —
roadmap **§2.1**, no dedicated tests before. The project's `stryker-config.json` now mutates **both** Export
files (`SiroXmlExporter` + `ExcelLayoutGenerator`). Added `ExcelLayoutGeneratorTests.cs` (**17 tests; Export
project 52 → 69 green**). The generator uses **ClosedXML** (`XLWorkbook`), so the tests **round-trip the
produced `.xlsx` back through `XLWorkbook`** and assert exact cell values.

```
ExcelLayoutGenerator.cs (combined run): Killed 40 · Survived 11 · Timeout 2 · NoCoverage 0
Combined Export run (both files): Killed 209 / Survived 35 / Timeout 2 / NoCoverage 3 = 84.74 %
```

What the tests pin (exact-value): the worksheet name (`"SIRO Registration"`); all **12 header labels** at
their exact columns; the **bold + light-gray header styling** (`Cell(1,1).Style.Font.Bold` true and
`Fill.BackgroundColor.Color.ToArgb()` == `XLColor.LightGray`'s ARGB — round-trips reliably); every core data
cell at its column (text via `GetString()`, numbers via `GetValue<int>()`); the `yyyy-MM-dd` FechaPublicacion
string; the first-party RFC + the composed `"Nombre Paterno Materno"` name **including** the null-surname
`Trim()` case (→ `"Carlos"`) and the empty-RFC `?? string.Empty` fallback; the **party-row gate** (an empty
`SolicitudPartes` list leaves the RFC/name cells blank and still **succeeds** — the `Count > 0` → `>= 0`
mutant would do `SolicitudPartes[0]` on an empty list and throw, so asserting success kills it); **column
auto-fit** (`Column(1).Width > 12` — `AdjustToContents()` widens the "NumeroExpediente" column well past the
~8.43 default, and ClosedXML persists the explicit width on save, so the statement-removal mutant is killed);
every validation message; and the Exception / OperationCanceledException catch paths (the OCE filter reached
via a `Stream` whose **synchronous** `Write` cancels its own CTS and throws OCE — ClosedXML `SaveAs` writes
synchronously inside the `Task.Run`).

**Lesson — round-trip the artifact to pin a binary writer's output.** For ClosedXML/OpenXML (and any binary
exporter) the cell-name / cell-value / style-flag mutants are only observable by **reading the produced file
back** with the same library; string-in-the-bytes greps are brittle. Numbers come back typed, so use
`GetValue<int>()` (not `GetString()`) for numeric cells, and compare colors by **ARGB** (`XLColor.LightGray`
as a named color may not survive a round-trip by reference equality, but its ARGB does).

**Residual / equivalent floor (all 13 non-killed):** Serilog statement/string/`?? "Unknown"` mutants on the
`Log*` calls (12 of them — two landed in `Timeout` on this run, which Stryker counts as detected; the rest
Survived) and the single `ConfigureAwait(false)` boolean. The file is small (53 testable mutants) and
logging-dominated, so the headline reads lower than the larger units even though **0 killable survivors**
remain — report the Killed delta + "0 killable survivors", not the %.

### Unit 16: CriterionMapperService + CompositeResponseExporter (2026-06-08) — ✅ Export §2.1 complete

The last two deterministic `Infrastructure.Export` units; the project's `stryker-config.json` now mutates
**all four** hardened Export files. Export test project **69 → 80 green**.

- **`CriterionMapperService`** (maps `List<ComplianceRequirement>` → the SIRO criteria dictionary).
  `CriterionMapperServiceTests.cs` (9 tests). Pins the cancellation/null guards; the `"Criterion_{RequerimientoId}"`
  key; the nested field map (`RequerimientoId`/`Descripcion`/`Tipo`/`EsObligatorio`, incl. the `false` case so
  the bool isn't coerced); the summary entries (`TotalRequirements` == count, `MappedAt` matched against the
  `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$` ISO-UTC regex since the value is `DateTime.UtcNow`); and the **generic
  catch** via a **null list element** (`requirement.RequerimientoId` → NRE → failure, not thrown). Stryker:
  **14 killed; 9 non-killed are all floor** — Serilog statements/strings, the `ConfigureAwait(false)` boolean,
  and the **unreachable OCE-catch logging** (NoCoverage — the `try` body has no token-aware operation, so an
  `OperationCanceledException` with a cancelled token can never originate inside it; the entity properties are
  non-virtual, so it can't be injected either). **0 killable survivors.**

- **`CompositeResponseExporter`** (a pure delegator — two methods forwarding to `SiroXmlExporter` /
  `DigitalPdfSigner`). `CompositeResponseExporterTests.cs` (2 tests) construct it with the **real** exporters
  (its ctor takes concrete types, not interfaces) and prove `ExportSiroXmlAsync` delegates to the XML exporter
  (a valid `<SiroResponse>` lands in the stream) and `ExportSignedPdfAsync` delegates to the PDF signer (its
  certificate failure surfaces). Stryker: **3 killed, 0 survivors** — a delegator has almost no mutable surface.

**Lesson — a pure delegator is cheap to fully cover.** No need to mock its concrete collaborators: drive the
real ones and assert a collaborator-specific side effect (the XML shape; the signer's certificate error). With
no operators/literals/booleans of its own, every one of its handful of mutants dies.

### Units 17–20: Export.Adaptive — §2.2 COMPLETE (2026-06-08)

The four deterministic files in `02 Infrastructure/Infrastructure.Export.Adaptive`, driven by the
already-existing `Tests.Infrastructure.Export.Adaptive` (added a `stryker-config.json`). Project **75 → 173 green**.
EF/DB artifacts (`TemplateRepository`, `TemplateSeeder`, `TemplateDbContext*`, `InitialCreate*`, `*ModelSnapshot`)
were skipped as planned.

- **Unit 17 — `TemplateFieldMapper`** (44.89% → **85.23%**, Killed 79→150, +45 tests). The richest file:
  reflection-based field extraction, value formatting, a pipe-chained transformation mini-language, and a
  string-rule validator. `TemplateFieldMapperMutationTests.cs` covers every transformation
  (ToUpper/ToLower/Trim/Substring/Replace/PadLeft/PadRight + chaining) and every validation rule
  (Regex/Range/MinLength/MaxLength/EmailAddress/Required) with **boundary** cases (value length == min/max,
  numeric == range edge — all must *pass*), `FormatValue` branches, the required/optional × default/null matrix,
  and `DisplayOrder` last-write-wins. **Two lessons:** (1) to kill a wrapped `throw new ArgumentException("…")`
  inside a caught block, **assert the inner exception text** (`"Invalid transformation format"`), not just the
  outer `"Transformation error"` wrapper — the wrapper survives a blanked inner literal. (2) The case labels in
  `GetDataTypeName`-style switches are **equivalent** when the explicit arm equals the lowercased default
  (`"decimal" => "decimal"`); only labels whose mapping *differs* from `Name.ToLowerInvariant()` (e.g.
  `"Int64" => "long"`) are killable.

- **Unit 18 — `SchemaEvolutionDetector`** (53.55% → **84.15%**, Killed 97→154, +22 tests). Reflection schema-drift
  + a public `CalculateSimilarity` (substring-containment ratio **or** Levenshtein, over a prefix/suffix-stripping
  normalizer). `SchemaEvolutionDetectorMutationTests.cs` pins exact similarity values (a `[Theory]` per
  prefix/suffix; hand-computed Levenshtein for `cat`/`car`=0.67, `cats`/`car`=0.5), data-type/nullability mapping,
  nested-path recursion **and** collection-non-recursion, severity, and the **equal-score rename tie-break**
  (assert the first-encountered candidate wins → kills `>` → `>=`). **Lesson — correlated guards make whole
  helpers equivalent:** `ComputeLevenshteinDistance` is only reached *after* the containment check
  short-circuits, so its empty-string guards and first-column deletion-baseline init (`matrix[len1,0]=len1`) are
  **unreachable** — any input where they'd matter is a substring relationship caught upstream. Pin as floor.

- **Unit 19 — `AdaptiveExporter`** (47.86% → **82.05%**, Killed 56→96, +25 tests across 3 classes). Orchestrator +
  Excel/XML/DOCX byte generation. **Round-trip the binary artifacts** (`AdaptiveExporterMutationTests`): read the
  xlsx back with ClosedXML, the XML with `XDocument`, the docx with OpenXml, and assert exact cell/element/paragraph
  positions + `DisplayOrder` ordering + the `"Label: value"` docx text — the existing `Length > 0` assertions left
  all of this dark, and XML/DOCX paths were **never exercised** (only Excel). A **mock-repository** class makes the
  template cache observable as call counts (`Received(1)` after two reads kills `TryAdd`; `Received(2)` across a
  `ClearTemplateCache` kills the clear; per-type keys vs a constant key). A **throwing-mock** class reaches the
  `ExportWithVersion` mapping-failure path and every per-method exception catch. **Lesson:** OpenXml's
  `WordprocessingDocument` saves all parts on `Dispose`, so the explicit `mainPart.Document.Save()` is an
  **equivalent** mutant (the round-trip still sees the paragraphs).

- **Unit 20 — `AdaptiveResponseExporterAdapter`** (0 coverage → Killed 0→8, +6 tests). A tiny IResponseExporter →
  IAdaptiveExporter delegator. Headline **28.57%** is the **small-file floor** — the file is logging-dominated.
  All functional mutants die (both ctor guards, the `"XML"` template type via `Received`, error propagation
  asserting `Error` survives the `?? "Unknown error"` fallback, the null-bytes guard, the exact stream write via
  **byte-equality** on the `MemoryStream`, the PDF stub message). The 20 residual are pure floor: Serilog `Log*`
  statements (NullLogger), the `?? "Unknown"` expediente value *inside* those log args, `ConfigureAwait(false)`
  booleans, and `FlushAsync` on a MemoryStream (a no-op once `WriteAsync` has written). **0 killable survivors.**

> ✅ **§2.2 Export.Adaptive COMPLETE.** Next: §2.3 Imaging (`Tests.Infrastructure.Imaging` exists) — deterministic
> math (`PolynomialImageQualityAnalyzer`, filter strategies, `FeatureNormalizer`/`PolynomialModel`,
> `LevenshteinTextComparer`); avoid native-bound Emgu/OpenCv/PIL.

### Units 21–24: Imaging — §2.3 COMPLETE (2026-06-09)

Eight files in `02 Infrastructure/Infrastructure.Imaging`, driven by `Tests.Infrastructure.Imaging` (added a
`stryker-config.json`, `additional-timeout: 30000`). Project **44 → 180 green**. Native enhancement filters and
the Emgu/OpenCv/PIL analyzers were skipped as native-bound.

- **Unit 21 — `LevenshteinTextComparer`** (0 coverage → Killed 0→151, 0 killable survivors, +40 tests). Exact-value
  tests for all five methods: edit distance (incl. the DP first-row/first-column init baselines), fuzzy ratio
  (both-empty=100 vs one-empty=0; SequenceMatcher half-match=50), similarity, the weighted quality score (with a
  low-special-char-ratio case that makes the `specialCharRatio` division observable and word-length boundary
  words for the `>=2 && <=15` bounds), and `FindBestMatch` (exact/mid-text/below-threshold, the duplicate-phrase
  tie-break, the `>=`-threshold boundary, and a leading-words match exercising `FindSubstringIndex`'s
  `currentPos < 0` offset guard). Floor: Serilog log-arg truncations; guards backstopped by the `maxLength==0` /
  both-empty / phrase-length downstream returns; the dead `bestStartIndex`; single-occurrence `FindSubstringIndex`
  (IndexOf-from-0 redundant with the cumulative offset).

- **Unit 22 — `FeatureNormalizer` / `PolynomialModel` / `TrainedPolynomialModel`** (Killed +19 = 10/50/29, 0
  killable survivors, +29 tests). FeatureNormalizer floor is the `- min`→`+ min` arithmetic (every empirical min
  is `0.0` → equivalent). PolynomialModel: empty-model midpoint, basis expansion at every degree, the
  length-mismatch throw, clamp; lone equivalent = `i < features.Length`→`<=` on the interaction outer loop (inner
  `j=i+1` runs zero times at `i==length`). TrainedPolynomialModel was driven via a **substituted
  `IOptionsMonitor`** with hand-built coefficients so the StandardScaler `(x-mean)/scale`, every degree-2
  polynomial term, the dot-product, and both clamp directions yield exact values, plus the ctor's `OnChange`
  hot-reload subscription via `Received(1)`.

- **Unit 23 — `PolynomialImageQualityAnalyzer`** (0 coverage → Killed 0→54, 0 killable survivors, +14 tests). A
  **hybrid**: deterministic post-processing wrapped around native EmguCV feature extraction. The trick is that
  clean structured images give **exactly deterministic** features — `half-width 0 | v` over 50×50 →
  `Blur=v²/25, Contrast=v/2, Noise=v/25, Edge=0.02` — so `ComputeVariance/StdDev/MeanAbsolute/CountNonZero`, the
  `DetermineQualityLevel` 5-band ladder, the `[0,1]` normalization formulas (sub-clamp **and** upper-clamp), and
  the diagnostics map are all pinned to exact values via `ExtractFeatures`/`AnalyzeAsync`.
  **Three lessons:**
  1. **Native-heavy suites inflate the headline via timeout-as-killed.** A scoped run read **94.44 % with 56
     timeouts**; adding `additional-timeout: 30000` collapsed them and revealed the **true 77.78 %** (54 killed,
     12 survived — all floor). *Always* give native suites timeout headroom (or parse the Timeout bucket) before
     trusting the score.
  2. **The Laplacian is high-pass, so its sum over any interior feature is 0** (a single bright pixel: `-400 +
     4·100 = 0`). With mean 0, `ComputeVariance`'s `sum += val`→`-=` (sign-independent through `mean²`),
     `sum/N`→`*N` (0 either way), and `- mean²`→`+ mean²` (adds 0) are **all equivalent** for every cleanly
     predictable image; killing them needs a border-clipped feature whose blur is border-mode-dependent
     (native-fragile). A legitimate "equivalent-for-realistic-inputs" floor.
  3. **Mutating native code pops Windows crash dialogs.** `CvInvoke` mutants crash the mutant process → ~5–6
     "dotnet launched with bad parameters" Error-Reporting popups per run. Harmless to results (counted killed)
     but noisy — scope native files into their own run.

- **Unit 24 — the three filter-selection strategies** (`Analytical`/`Default`/`Polynomial`, +53 exact-value
  tests, **180/180 green**). Pins both public switches on each (all arms + default), the threshold/heuristic
  classifiers (Analytical's `ClassifyQualityLevel` ladder + `&&` short-circuits via `SelectFilter`; Polynomial's
  `SelectFilterType` None/OpenCv/PIL incl. the `||` clause), the `RefineConfig`/adjustment parameter math (exact
  contrast factors, median 5/7, the `Min`→`Max`-killing clamp), and — for the stub-model `Polynomial` strategy —
  the deterministic midpoint predictions (PIL CF 1.75 / median `round(4)`→`5`; OpenCv DenoiseH 10.5, etc.).
  ⏳ **The Stryker mutation-verification run for these three (pure, managed) files is DEFERRED** — the Unit 23
  native run's crash popups led to stopping before re-confirming; the tests are value-exact by construction, so
  re-running Stryker scoped to the three `*FilterSelectionStrategy.cs` files should confirm Killed delta + 0
  survivors.

> ✅ **§2.3 Imaging COMPLETE** (Unit 24 verify pending). Next: §2.4 Extraction (base) — deterministic
> extractors/sanitizers (check for duplication with `Extraction.Adaptive` first).

### Unit 25: XmlFieldExtractor (Infrastructure.Extraction.Ocr) — §2.4 started (2026-06-09)

First unit in the Extraction base project (`ExxerCube.Prisma.Infrastructure.Extraction.Ocr`, the classes under
`Teseract/`). Added the project's `stryker-config.json` next to `Tests.Infrastructure.Extraction`
(`mutate: ["**/Teseract/XmlFieldExtractor.cs"]`). **70.41% → 95.41%, Killed 138→187, +39 tests** (39 new +
the existing 16). Baseline had **48 NoCoverage** — almost entirely the **measure-inference chain**
(`InferMeasure → ParseActionKind → ToSpanishMeasureName`) which the existing tests never reached. The new
`XmlFieldExtractorMutationTests` pin every measure outcome (a `[Theory]` over the InferMeasure keyword branches
AND the ParseActionKind fallback, each mapped to its exact Spanish name), the authority/area keys, the
CuentasRaw/RfcList present-vs-absent guards (`>0`→`>=0`), both RFC sources + the whitespace skip, the
subdivision accent/space normalization (one InlineData per accent so each `Replace` is observable), the loose
vs strict CURP paths, `ExtractFieldAsync` routing/aliases/failure, and file-based loading via a real temp file.

**Two lessons:**
1. **A more-specific regex guarded by a broader fallback is an equivalent mutant.** The `StrictCurpRegex` block
   is unkillable: the `LooseCurpRegex` matches any valid strict CURP and `NormalizeCurp` maps both to the same
   18-char uppercase value, so removing the strict branch (or flipping its `RegexOptions |`→`&`) yields identical
   output. Pin via the loose path, mark the strict block as floor.
2. **perTest attribution noise is loud on this larger suite.** Across re-runs the headline flapped 94.39–95.41%
   and a rotating handful of *deterministically-covered* string-key/regex-pattern mutants (the RfcList key, the
   loose-regex pattern) drifted between Killed and Survived. Trust the **Killed delta (+49) and "residual is
   equivalent floor"**, not any single run's survivor list. (Equivalent floor here: the strict-regex block above;
   the `"causa"` switch label — `fields.Causa` is always null so the default lookup gives the same not-found
   failure; the defensive no-root / dead `?? "Extraction failed"` / unreachable `"Desconocido"` default; and the
   `NormalizeCurp` blank-guard + `>18` truncation the regex caps make unreachable.)

### Unit 26: TextSanitizer + OcrSanitizationService (Infrastructure.Extraction.Ocr) — §2.4 (2026-06-09)

Two pure OCR-cleanup classes; added both to the Extraction-base `stryker-config` mutate list. **Both reached
100.00% (0 survivors / 0 NoCoverage), Killed: TextSanitizer 70, OcrSanitizationService 21; +29 tests.**
`TextSanitizer` was previously covered *only* by the flaky OCR/Teseract suite (out of scope for mutation runs),
so it was effectively dark in the deterministic project — a reminder to **check which test project actually
drives a class before assuming it's covered**. The tests pin the account/SWIFT/generic cleaners (the
label-strip-vs-digit-change normalization distinction, the `[6,20]` length-suspect boundaries via a Theory at
5/6/20/21, and the SWIFT `9→11` `PadRight('X')` padding with its full `normalized && len==9 && !hadWhitespace`
guard set) and the orchestrator's first-CUENTA/first-SWIFT line discovery + the cross-field merge (a normalized
SWIFT bumps a clean account) with its `Count == 0` no-double-add guard. A clean 100% is achievable on small pure
string-logic units with no equivalent floor.

### Unit 27: FileTypeIdentifierService (Infrastructure.Extraction.Ocr) — §2.4 (2026-06-09)

Content-based file-type detection (magic numbers + extension fallback); added to the Extraction-base
`stryker-config` mutate list. **Killed 58 / Survived 8 / NoCoverage 5 (81.69% headline), 0 killable survivors;
+25 tests.** All 13 non-kills are the documented equivalent floor — Serilog `LogWarning`/`LogDebug`/`LogError`
statement + message-text + `?? "unknown"` mutants (L43/L47/L52), and the **dead defensive `catch` block**
(L52-53), which is unreachable from the public API: `IdentifyByContent` guards its indexing (`length < 4`) and
its `Encoding.UTF8.GetString(...Math.Min(...))` can't overrun, and modern `Path.GetExtension` no longer throws
on invalid chars, so nothing inside the `try` can throw. The tests pin the real surface: the `< 4` and XML
`>= 5` length boundaries (exact-4-byte PDF id'd vs 3-byte fallback; exact-5-byte `<?xml` vs 4-byte `<?xm`
below boundary), per-byte PDF/ZIP magic-number discrimination (one-byte-wrong Theories), the XML
`StartsWith("<?xml")` vs `TrimStart().StartsWith("<")` alternatives, the DOCX `word/` return vs generic-ZIP
`else` (Zip) vs `xl/`-only fall-through-to-null (a subtle third path that returns neither Docx nor Zip),
both-markers-prefers-Docx, every extension-switch arm incl. `ToLowerInvariant` (`.PDF`), content-wins-over-a-
conflicting-extension ordering, and the `!IsNullOrEmpty(fileName)` empty-string guard. **Lesson: a defensive
`try/catch` wrapping only non-throwing operations is an uncoverable block — don't contort the SUT to reach it;
log it as the floor.**

### Unit 28: MexicanNameFuzzyMatcher (Infrastructure.Extraction.Ocr) — §2.4 (2026-06-09)

Fuzzy name matcher (FuzzySharp `Fuzz.Ratio`, 85% threshold) that exact-matches non-name fields and only
fuzzy-matches Mexican/Spanish names. Ported fresh into the deterministic `Tests.Infrastructure.Extraction`
(it was previously covered only by the flaky `Tests.Infrastructure.Extraction.Teseract` suite, out of scope).
Added to the Extraction-base `stryker-config` mutate list. **Killed 75 / Survived 52 / Timeout 0 (57.69%
headline), 0 killable survivors; +58 tests.** The low headline is **entirely** a lookup-table + tooling floor —
verified, not assumed:

- **42 survivors (L24–44) are the static-field-initializer limitation.** The two `static readonly HashSet`
  name tables (`SpanishGivenNames`/`SpanishSurnames`) and the six `static readonly Regex` pattern fields:
  Stryker's per-element string mutants survive because the static initializer runs **once** in the reused MTP
  test host, so the mutation switch can't be re-evaluated per mutant. **Proven killable-in-principle:** manually
  editing `"cristian" → ""` in the set makes `IsMatch_MexicanViaNameSetOnly_ReturnsTrue` **fail**. Not a test gap.
- **6 survivors (L123–128) are a Stryker MTP *boolean-literal* artifact.** On each `if (XxxPattern.IsMatch(..))
  return false;` the **`Negate expression`** mutant on the same line **is killed** by the IsNameField positives,
  yet the runtime-identical **`Boolean → true`** mutant survives. **Proven artifact:** forcing the L123 condition
  always-true by hand fails **19** tests. (`if (true)` itself won't even compile here — CS0162 unreachable-code
  under TreatWarningsAsErrors — which is likely why Stryker's literal-true mutant is mis-handled.)
- **The rest are genuine equivalent mutants / dead code:** `L143` (`return false` inside the all-digits guard) is
  **dead code** — `AmountPattern` (`^\$?\s*[\d,]+\.?\d*$`) matches *every* pure-digit string first, so the
  all-digits branch is unreachable (minor finding, like FileClassifier's `Min(85)` tier); `L189`
  (`return string.Empty` guard in `NormalizeForComparison`) is an unreachable defensive guard (all call sites
  pre-guard null/whitespace); `L156` `|`→`&` on `StringSplitOptions` is equivalent because `NormalizeForComparison`
  already collapses+trims whitespace so the split options never matter; `L59`/`L97` `||`→`&&` and `L98` block-removal
  are equivalent because empty/whitespace inputs fall through to `string.Equals`/`Fuzz.Ratio` of empty strings →
  `false`/`0` anyway; `L86` `>=`→`>` is the fuzzy-threshold boundary (can't be deterministically pinned to a pair
  scoring *exactly* 85 across FuzzySharp versions).

The 58 tests pin the real surface: the null/whitespace guards; the **non-name exact-ordinal** path (identical RFCs
match, near-miss RFC/account/expediente/amount/date don't); the **non-Mexican normalized-exact** path
(Smith/smith match, Smith/Smyth & John/Jon don't); the **both-Mexican fuzzy** path with above- and below-threshold
pairs; the `||`-routing operators (a name vs a `/`-bearing string stays exact; González/Gonzalex stays
normalized-exact); `IsLikelyMexicanName` isolated per branch with **near-but-different** pairs so the fuzzy result
(true) flips to normalized-exact (false) when the branch is removed — accent-only (Cárdenas/Cárdenaz),
leading-accent-at-index-0 (Ávila/Ávilas, kills `>=`→`>` on `IndexOfAny`), ñ-only (Peña/Peñas), name-set-only
(Cristian/Christian), ez/es-ending-only (Gutierrez/Gutierres); `GetSimilarityScore` exact-100s that pin
diacritic-removal, lower-casing, whitespace-collapse and Trim, plus a space-vs-no-space `< 100`; `IsNameField`'s
`$`/`/` exclusion at high letter ratio, the `0.80` boundary (`abcd.` = exactly 0.80 true, `abc..` = 0.60 false,
kills the cast/`/`→`*`/`0.80`→`0` mutants), the `All`→`Any` all-digits mutant (`Juan2`), and `MatchThreshold`.

**Lessons added this unit:** (1) **A class dominated by `static readonly` lookup tables will show a low Stryker
headline that is mostly the static-initializer limitation, not weak tests** — confirm by manually editing one table
entry and watching a test fail, then report "0 killable survivors". (2) **Stryker's `Boolean → true` mutant on an
`if`-condition is an MTP artifact when the same line's `Negate expression` mutant is killed** — verify by forcing
the condition always-true by hand (a non-constant always-true expression avoids the CS0162 that a literal `true`
triggers under warnings-as-errors). (3) Re-confirmed: `coverage-analysis: "off"` does **not** rescue
static-initializer or boolean-literal survivors here (same 52), so don't chase them — prove killability by hand.

### Unit 29: XmlExpedienteParser + XmlMetadataExtractor (Infrastructure.Extraction.Ocr) — §2.4 (2026-06-09)

The two pure XML parsers (`XmlExpedienteParser` is the big one — 528 lines of CNBV/PRP1 element mapping +
metadata scoring; `XmlMetadataExtractor` is the `IMetadataExtractor` adapter over it). Both already had a few
happy-path tests in the deterministic project. Added to the Extraction-base `stryker-config` mutate list (now
7 files). **Project headline 15.03% → 93.32%; parser Killed 69 → 462, extractor Killed 12 → 41; +43 tests;
0 killable survivors.**

Approach: drive the parser **only through the public `ParseAsync`** and read the attached scoring counters via
`result.GetMetadata<Expediente, ExtractionMetadata>()` (the IndQuestResults metadata extension) — this makes the
whole ~150-line `BuildExtractionMetadata` counting surface killable with exact-value assertions
(`TotalFieldsExtracted`/`RegexMatches`/`CatalogValidations`/`PatternViolations` on full / minimal / zero-id /
invalid-RFC / per-catalog-value / fecha-boundary fixtures). The extractor is driven via a substituted
`IXmlNullableParser<Expediente>`. **One efficient fixture trick:** a *programmatically built* XML placing every
`knownFields` name (plain AND `Cnbv_`-prefixed) as a root element with a value, then asserting
`AdditionalFields.Count == 0`, kills **every** `CaptureUnknownFields.knownFields` string in one shot (blanking any
entry would make that name "unknown" → captured → Count > 0). Other pins: GetElementValue `Cnbv_` fallback /
`xsi:nil="true"` / empty-to-null; every parse default (`int/bool/DateTime.TryParse` fallbacks, `OficioYear`'s
`DateTime.Now.Year` default, the `?? string.Empty` arms, `&& tieneAseg`); `ExtractLawMandatedFields` hasData
combinations + per-field null-when-blank ternaries; UTF-8 BOM handling; multiple `SolicitudPartes`.

**Residual = 0 killable.** Genuine equivalent floor: Serilog statements/strings; dead-defensive code (the
`root == null` failure return is unreachable — `XDocument.Load` throws on a rootless doc first; the
`GetElementValue` `parent == null` guard is never hit — all call sites pass non-null; `element.Value ?? string.Empty`
— `XElement.Value` is never null; the `StreamReader` BOM-detect flag — the BOM is handled either way; the
`new ExtractionMetadata { Source = SourceType.XML_HandFilled }` object-initializer — `Source`'s property default is
the *same* value, so removing the initializer is equivalent). The unused `curpPattern`/`datePattern` regexes in
`BuildExtractionMetadata` are dead (only `rfcPattern` is used) — minor finding, pinned as floor.

**Lessons:** (1) **`result.GetMetadata<T, TMeta>()` turns "attached" scoring metadata into a killable surface** —
when a parser logs/attaches an analysis object rather than returning it, read it back through the Result metadata
extension instead of writing the counters off as a Serilog-only floor. (2) **Build exhaustive "every-name" fixtures
programmatically** — a `StringBuilder` loop over the known-field list kills a whole local lookup-table
(`knownFields`) far more cheaply and completely than hand-written XML. (3) **perTest attribution noise is severe on
large parser suites** — at ~290 tests the run reported ~30 string/equality "survivors" on lines the tests clearly
assert, and the set *shrank run-to-run* (95→93 survivors on identical code). **Proven noise, not real:** manually
applying three of them (an element-name string at L72/L145, the `!IsNullOrWhiteSpace(areaDescripcion)` → `(x!=null)`
hasData mutant at L200) **failed 11 tests**. Trust the Killed delta + a manual spot-check; don't chase the rotating
string survivors.

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

> **Next-agent roadmap & checklist:** `docs/qa/test-plans/mutation-testing-roadmap.md` — the goal, the
> deterministic-surface denominator, a tick-box path (Export → Imaging → Extraction base → Metrics/FileStorage
> → Core), the reserved bug-fix session, and the condensed loop + lessons. (Older roadmaps:
> `HANDOFF-2026-06-08-mutation-classification-done.md`, `…-continuation.md`.)

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
