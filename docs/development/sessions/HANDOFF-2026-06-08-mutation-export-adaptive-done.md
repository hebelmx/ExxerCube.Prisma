# Handoff — Mutation testing: Export.Adaptive §2.2 COMPLETE, what's next (2026-06-08)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
all lessons; Units 17–20 are this session) and `docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box
plan). This handoff is the *session-specific roadmap*; those two docs are the *reference* + *plan*.
**Branch:** `Kt2` — all work below is **committed** (`d2fe6e6..f9086fa`, + this doc commit). Build green,
Export.Adaptive test project green (**173/173**).

**Supersedes** `HANDOFF-2026-06-08-mutation-export-done.md` for the "next units" question. The reserved
conflict-detection findings (§4) are unchanged.

---

## 1. What this session did — Units 17–20: Export.Adaptive (roadmap §2.2)

Four deterministic files in `02 Infrastructure/Infrastructure.Export.Adaptive`, driven by the **already-existing**
`Tests.Infrastructure.Export.Adaptive` project (added a `stryker-config.json`; it now mutates **all four**
hardened files). **This finishes Export.Adaptive** — the EF/DB artifacts (`TemplateRepository`, `TemplateSeeder`,
`TemplateDbContext*`, `InitialCreate*`, `*ModelSnapshot`) were skipped as planned.

| Unit | File | Result | Tests |
|---|---|---|---|
| 17 | `TemplateFieldMapper.cs` | 44.89% → **85.23%**, Killed 79→150 | +45 |
| 18 | `SchemaEvolutionDetector.cs` | 53.55% → **84.15%**, Killed 97→154 | +22 |
| 19 | `AdaptiveExporter.cs` | 47.86% → **82.05%**, Killed 56→96 | +25 |
| 20 | `AdaptiveResponseExporterAdapter.cs` | 0 coverage → Killed 0→8 (28.57% = small-file floor) | +6 |

**All four reached 0 killable survivors.** Export.Adaptive test project **75 → 173 green**. Commits (test+config,
one per unit): `d2fe6e6` (17), `d6165e1` (18), `20ffc68` (19), `f9086fa` (20). The config's `mutate` list grew
each commit and now lists all four files.

## 2. Five reusable lessons from this session (all folded into the guide, Units 17–20)

1. **To kill a wrapped `throw new ArgumentException("…")` inside a caught block, assert the INNER text**, not the
   outer wrapper. `"Transformation error: {ex.Message}"` survives a blanked inner literal if you only assert
   `"Transformation error"` — assert `"Invalid transformation format"` (the thrown literal) instead.
2. **Switch case labels are equivalent when the explicit arm == the lowercased default.** `GetDataTypeName`'s
   `"decimal" => "decimal"` is unkillable (the `_ => Name.ToLowerInvariant()` default produces the same string);
   only labels whose value *differs* (`"Int64" => "long"`) are killable. Don't chase the equivalent ones.
3. **Correlated guards make whole helpers equivalent.** `ComputeLevenshteinDistance` runs **only after** the
   substring-containment check short-circuits, so its empty-string guards and first-column deletion baseline
   (`matrix[len1,0]=len1`) are unreachable — every input where they'd matter is a substring relationship caught
   upstream. Hand-verify by working the DP, then pin as floor.
4. **Round-trip binary artifacts to pin generation mutants.** Read the produced xlsx back with ClosedXML, XML
   with `XDocument`, docx with OpenXml (`WordprocessingDocument.Open`), and assert exact cell/element/paragraph
   positions + ordering + text. Loose `Length > 0` assertions leave all of it dark. **Caveat:** OpenXml saves
   parts on `Dispose`, so an explicit `Document.Save()` is an **equivalent** mutant.
5. **Make caching observable with a mock dependency.** A substituted repo turns cache hits/misses into call
   counts: `Received(1)` after two reads kills `TryAdd`; `Received(2)` across a `ClearCache` kills the clear; a
   per-type-key vs constant-key test kills the cache-key literal. And **throwing mocks** (`.Returns<T>(_ => throw …)`)
   reach per-method exception catches to pin their return messages.

**Floor note (unchanged):** report **`Killed` delta + "0 killable survivors", not the %**. Small/logging-heavy
files (the adapter especially) read low — the standard floor is Serilog statements/strings, `ConfigureAwait(false)`
booleans, `await Task.CompletedTask` no-ops, `?? fallback` right-operands (left always non-null), unreachable
defensive catches, and internal error messages swallowed by the caller.

## 3. Next units to harden — recommendation: **start a fresh session**

The next chunk (roadmap **§2.3 Imaging**) is a **different project** with its **own test project** — a natural
clean boundary. A fresh session re-warms instantly and sheds this session's transient Stryker/polling output.

### §2.3 Imaging — the recommended next target (`Tests.Infrastructure.Imaging` EXISTS)
- **Prod:** `02 Infrastructure/Infrastructure.Imaging/` — deterministic math:
  `PolynomialImageQualityAnalyzer.cs` (production analyzer), `AnalyticalFilterSelectionStrategy.cs`
  (note the deferred ≥10% threshold task #11 — see GAP matrix), `DefaultFilterSelectionStrategy.cs` /
  `PolynomialFilterSelectionStrategy.cs`, `FeatureNormalizer.cs`, `PolynomialModel.cs`, `TrainedPolynomialModel.cs`,
  `AdaptiveEnhancementFilter.cs`, `PolynomialEnhancementFilter.cs`, and `LevenshteinTextComparer.cs` (pure string
  distance, also used by Classification — high-value, easy).
- **Add a `stryker-config.json`** next to `Tests.Infrastructure.Imaging` (copy the Export.Adaptive one; set
  `project` = the Imaging prod csproj, `test-projects` = the Imaging test csproj).
- ⛔ **avoid native-bound:** `EmguCvImageQualityAnalyzer`, `OpenCvAdvancedEnhancementFilter`,
  `PilSimpleEnhancementFilter`; trivial `NoOp*`/`Stub*`. Mutating native-bound code gives ambiguous mutants.

### Then §2.4 Extraction (base) — deterministic extractors/sanitizers (see roadmap §2.4; check for duplication first).

**The per-unit loop is unchanged** — roadmap §4 (condensed). Time-savers that worked again this session: scope
`mutate` to **one file** for the baseline; parse Survived/NoCoverage/**Timeout** from the HTML report
(`app.report = {...}`, `json.JSONDecoder().raw_decode`); write exact-value tests; re-run; **re-add ALL hardened
files to the config's `mutate` list before committing**; commit test+config per unit with the `Killed` delta in
the subject; update the guide + roadmap; push.

## 4. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from prior handoffs. Two real defects surfaced by mutation testing on the Classification matching
policies; fix them **together in a dedicated session**:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)

Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is an unused private method — safe to delete.

## 5. Minor behavioural findings pinned as-is (not fixed)
- **No new code defects surfaced this session** — the Export.Adaptive files are clean. The only "dead code" found
  is equivalent-mutant floor (unreachable Levenshtein guards behind the containment check; the redundant OpenXml
  `Document.Save()`; the `await Task.CompletedTask` async-placeholders). All harmless — pinned as floor, no fix.
- (Carried over: the reserved Classification conflict-detection bugs in §4; FileClassifier dead `Min(85)` tier;
  the DOCX-strategy date-only nulls / positional name mislabel / `en`-substring Accion cleanup.)

## 6. Status / push
- Commits this session on `Kt2`: `d2fe6e6`, `d6165e1`, `20ffc68`, `f9086fa`, plus the docs/handoff commit.
- Solution/build green; Export.Adaptive test project **173/173** green. **21 files hardened across 5 projects**
  (Txt, Adaptive-DOCX, Classification, Export base, **Export.Adaptive — now complete**) ≈ 35% of the worthy
  deterministic surface.
- ⚠️ **Verify `git push`** at session start — confirm these commits reached origin.

## 7. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 17–20 are this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, CI-gate option: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
