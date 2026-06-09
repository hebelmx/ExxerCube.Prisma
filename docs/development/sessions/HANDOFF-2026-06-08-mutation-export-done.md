# Handoff — Mutation testing: Export base project COMPLETE, what's next (2026-06-08)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
all lessons) and `docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan). This handoff is the
*session-specific roadmap*; those two docs are the *reference* + *plan*.
**Branch:** `Kt2` — all work below is **committed and PUSHED** (`061bb5f..65197b2`). Build green,
Export test project green (**80/80**).

**Supersedes** `HANDOFF-2026-06-08-mutation-classification-done.md` for the "next units" question. The reserved
conflict-detection findings (§4) are unchanged.

---

## 1. What this session did — Units 14–16: the Export base project (roadmap §2.1)

Four deterministic files in `02 Infrastructure/Infrastructure.Export`, driven by the **already-existing**
`Tests.Infrastructure.Export` project. Added a `stryker-config.json` next to that test project; it now mutates
**all four** hardened files. **This finishes the Export base project** — only `DigitalPdfSigner` (crypto) and
`PdfRequirementSummarizerService` (PDF rendering) are deliberately left out.

> ⚠️ **Correction to the prior handoff:** it said Export "has **no dedicated test project yet** — scaffold
> `Tests.Infrastructure.Export`." That was **wrong** — the project already existed (it just only tested
> `DigitalPdfSigner` and had no Stryker config). No scaffolding was needed. (Same stale "scaffold Export" note
> appears in older handoffs — ignore it.)

| Unit | File | Result | Tests added |
|---|---|---|---|
| 14 | `SiroXmlExporter.cs` | 0 coverage → **86.22%**, Killed 0→169 | 26 |
| 15 | `ExcelLayoutGenerator.cs` | 0 coverage → 0 killable (combined run 84.74%) | 17 |
| 16 | `CriterionMapperService.cs` | 14 killed, 0 killable | 9 |
| 16 | `CompositeResponseExporter.cs` | 3 killed, **0 survivors** (full) | 2 |

**All four reached 0 killable survivors.** Export test project **26 → 80 green**. Commits: `061bb5f`/`634bfdd`
(Unit 14), `9896278`/`29811be` (Unit 15), `c650d3d`/`65197b2` (Unit 16) — test+config and doc paired per unit.

## 2. Four reusable lessons from this session (all already folded into the guide, Units 14–16)

1. **Reach a `catch (OperationCanceledException) when (token.IsCancellationRequested)` by cancelling
   mid-write, not before.** An already-cancelled token trips the pre-`try` guard, so the filter is never
   entered. Use a custom `Stream` whose `Write`/`WriteAsync` **cancels its own `CancellationTokenSource` and
   throws OCE** (with a *live* token passed in). A sibling stream that throws `IOException` on write reaches the
   generic `catch`. (Used in SiroXmlExporter **and** ExcelLayoutGenerator — note ClosedXML `SaveAs` writes
   **synchronously** inside the `Task.Run`, so override the *sync* `Write` there.)
2. **A "String mutation → empty" survivor can be a `string.Join` *separator*, not the whole message.**
   SiroXmlExporter L327's `WithFailure($"...: {Join("; ", errors)}")` survived **even under `coverage-analysis:
   "off"`** despite a test asserting the literal — because the blanked string was the `"; "` separator; the
   content still passed. The empty-content-model schema raises **exactly two** errors, so asserting the joined
   `"; "` is present pins it. **A throwaway diagnostic test that prints `result.Error` is the fastest way to see
   which literal a string mutant actually hit** (delete it after).
3. **Round-trip a binary writer's artifact to pin its mutants.** ExcelLayoutGenerator uses ClosedXML, so read
   the produced `.xlsx` back with `new XLWorkbook(stream)` and assert exact cells/styles. Numbers come back
   **typed** → use `GetValue<int>()` (not `GetString()`) for numeric cells; compare colors by **ARGB**
   (`...BackgroundColor.Color.ToArgb() == XLColor.LightGray.Color.ToArgb()` — named-color reference equality
   won't survive a round-trip, ARGB does). `AdjustToContents()` column auto-fit **is** killable because ClosedXML
   persists the explicit width on save (assert `Column(1).Width > 12`).
4. **A pure delegator is cheap to fully cover — no mocking.** CompositeResponseExporter's ctor takes **concrete**
   types (`SiroXmlExporter`/`DigitalPdfSigner`), so drive the **real** collaborators and assert a
   collaborator-specific side effect (a valid `<SiroResponse>` in the stream; the signer's certificate error).
   With no operators/literals/booleans of its own, all 3 of its mutants die.

**Floor note for small files:** SiroXmlExporter is large enough to read ~86%, but ExcelLayoutGenerator (53
testable mutants) and CriterionMapperService are **small and logging-dominated**, so their headline % looks low
even at **0 killable survivors**. The residual everywhere is the standard floor: Serilog statements/strings,
`ConfigureAwait(false)` booleans, XmlWriter auto-close/auto-declaration, ClosedXML cosmetics, and unreachable
defensive `catch` blocks. **Report the `Killed` delta + "0 killable survivors", not the %.**

## 3. Next units to harden — recommendation: **start a fresh session**

The next chunk (roadmap **§2.2 Export.Adaptive**) is a **different project** with its **own test project** — a
natural clean boundary. Everything needed to take off cold is already durable (this handoff + the guide Units
14–16 + the roadmap §2.2 checklist + the auto-memory `mutation-testing-stryker.md`). A fresh session re-warms
instantly and sheds this session's transient Stryker/polling output.

### §2.2 Export.Adaptive — the recommended next target (test project EXISTS)
- **Prod:** `02 Infrastructure/Infrastructure.Export.Adaptive/` → `AdaptiveExporter.cs`,
  `SchemaEvolutionDetector.cs`, `TemplateFieldMapper.cs`, `AdaptiveResponseExporterAdapter.cs`.
- **Tests:** `08 Tests/02 Infrastructure/Tests.Infrastructure.Export.Adaptive/` **already exists** with
  `AdaptiveExporterTests`, `SchemaEvolutionDetectorTests`, `TemplateFieldMapperTests`, `TemplateRepositoryTests`
  — so you **extend** loose contract tests (like the DOCX-strategy units), you don't scaffold. **It has no
  `stryker-config.json` yet — add one** (copy the Export one; `project` =
  `ExxerCube.Prisma.Infrastructure.Export.Adaptive.csproj`, `test-projects` = the Adaptive test csproj).
- ⛔ **Skip the DB-bound / EF artifacts:** `TemplateRepository.cs`, `TemplateSeeder.cs`, the `Data/` EF context +
  `InitialCreate*`/`*ModelSnapshot`, and `template_migration.sql`. Mutating DB-coupled code gives ambiguous
  mutants. `TemplateRepositoryTests` may be Testcontainers-backed — leave repo mutation alone.

### Then §2.3 Imaging (deterministic math — `Tests.Infrastructure.Imaging` exists)
`PolynomialImageQualityAnalyzer`, the filter-selection strategies, `FeatureNormalizer`/`PolynomialModel`,
`LevenshteinTextComparer` (pure, high-value, also used by Classification). ⛔ avoid native-bound
`EmguCvImageQualityAnalyzer`/`OpenCv*`/`PilSimple*`. See roadmap §2.3 for the full list and the deferred
analytical-filter ≥10% threshold (task #11).

**The per-unit loop is unchanged** — roadmap §4 (condensed) / `HANDOFF-2026-06-08-mutation-testing-continuation.md`
§5 (full). Time-savers that worked again this session: scope `mutate` to **one file** for the baseline; parse
Survived/NoCoverage/**and the Timeout bucket** from the HTML report (`app.report = {...}`,
`json.JSONDecoder().raw_decode`); write exact-value tests; for a noisy/large suite do one `coverage-analysis:
off` cross-check to find real gaps (then revert); re-run; **re-add ALL hardened files to the config's `mutate`
list before committing**; commit test+config per unit with the `Killed` delta in the subject; update the guide +
roadmap; push.

## 4. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from prior handoffs. Two real defects surfaced by mutation testing on the Classification
matching policies; fix them **together in a dedicated session**:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)

Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is an unused private method — safe to delete.

## 5. Minor behavioural findings pinned as-is (not fixed)
- **No new code defects surfaced this session** — the Export files are clean. The only Export "dead code" found
  is the unreachable defensive `_siroSchemaSet == null` guard **inside** `SiroXmlExporter.ValidateXmlSchema`
  (only ever called when non-null) and the OCE-catch in `CriterionMapperService` (no token-aware operation runs
  inside its `try`). Both are harmless defensive equivalents — pinned as floor, no fix needed.
- (Carried over: Classification FileClassifier dead `Min(85)` tier; the DOCX-strategy date-only nulls / positional
  name mislabel / `en`-substring Accion cleanup — see the classification + orchestrator handoffs §5.)

## 6. Status / push
- Commits this session on `Kt2`, **pushed** to origin: `061bb5f`, `634bfdd`, `9896278`, `29811be`, `c650d3d`,
  `65197b2`, plus this handoff.
- Solution/build green; Export test project **80/80** green. 17 files hardened across 4 projects (Txt, Adaptive,
  Classification, **Export base — now complete**) ≈ 30% of the worthy deterministic surface.

## 7. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 14–16 are this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, CI-gate option: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
