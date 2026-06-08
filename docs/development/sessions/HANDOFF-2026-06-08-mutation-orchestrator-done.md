# Handoff — Mutation testing: Adaptive-DOCX project COMPLETE, what's next (2026-06-08)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
the timeout-inflation lesson, cross-repo guideline). This handoff is the *roadmap*; that doc is the *reference*.
**Branch:** `Kt2` — all work below is committed **and pushed** to `origin/Kt2` (through `ee3b54e`).
Build green, Adaptive test project green (**313/313**).

**Supersedes** `HANDOFF-2026-06-08-mutation-docx-strategies.md` for the "next units" question. The reserved
findings (§4 below) and the per-unit loop are unchanged — re-read them in the guide and the
`HANDOFF-2026-06-08-mutation-testing-continuation.md` §5.

---

## 1. What this session did — Unit 11: the AdaptiveDocxExtractor orchestrator

Hardened `AdaptiveDocxExtractor.cs` (`Infrastructure.Extraction.Adaptive/`) — the BestStrategy / MergeAll /
Complement coordinator over the five (already-hardened) `IAdaptiveDocxStrategy` implementations. **This finishes
the Adaptive-DOCX project**: orchestrator + 5 strategies + `EnhancedFieldMergeStrategy` are all hardened, and the
project's `stryker-config.json` now mutates all **seven** files.

Added `AdaptiveDocxExtractorMutationKillingTests.cs` (**31 tests; Adaptive project 282 → 313 green**), driven by
**NSubstitute mocks of `IAdaptiveDocxStrategy`** (the Liskov tests used the *real* strategies, so merge/complement
internals, the selection ladder, the confidence ordering, and both `catch` blocks were unpinned).

```
AdaptiveDocxExtractor.cs: Killed 28 -> 80 · Survived 0 -> 0 · NoCoverage 25 -> 0 · Timeout 77 -> 50
```

Commits: `f919230` (test+config), `ee3b54e` (guide doc). Per-unit detail in the guide under **"Unit 11"**.

## 2. Two lessons from this unit (both reusable on any orchestrator-style unit)

1. **Covering a `catch (OperationCanceledException)` block.** The `*LiskovTests` cancellation tests pass an
   **already-cancelled** token, which trips `ThrowIfCancellationRequested()` **before** the `try` — so the catch
   is never entered (it shows up as `NoCoverage`). The only way in is a **mock dependency that throws OCE from
   *inside* the try, with a *live* token**. The generic `catch (Exception)` → null / → fallback is reached the
   same way with a *plain* exception. Watch which inner method swallows vs rethrows: here the inner
   `GetStrategyConfidencesAsync` swallows generic exceptions (returns all-zero) but rethrows OCE, so to hit the
   *outer* generic catch you must throw from `strategy.ExtractAsync`, not `GetConfidenceAsync`.

2. **Timeout inflation is NOT just a regex problem.** Units 6–10 hit it via catastrophic regex backtracking; this
   non-regex unit hit it via **MTP per-mutant session overhead** on the large (313-test) suite. ~58 mutants landed
   in `Timeout` (counts as detected) → headline read **100%**, but parsing the Timeout bucket exposed **functional
   mutants that only "passed" via timeout** (the Causa/Accion `&&` merge guards, the `ComplementFields` core `??`,
   the copy-existing `Montos`/`Fechas` `Add` statements — each only equivalent *given the weak first-pass tests*).
   **ALWAYS parse the Timeout bucket and classify every entry as log/equivalent vs functional.** Strengthen the
   functional ones to deterministic kills (both-set core fields for the `&&`; a mirror existing/new split for the
   `??`; an existing-only collection item so removing the copy line changes the count). Report the **Killed delta +
   "0 killable survivors"**, never the headline %.

## 3. Next units to harden (pick any — independent; same per-unit loop)

In rough priority / cleanliness order:

1. **Export** — `02 Infrastructure/Infrastructure.Export/SiroXmlExporter.cs` (the SIRO-XML deliverable,
   deterministic), `ExcelLayoutGenerator.cs`, `CriterionMapperService.cs`. Note: **no dedicated test project yet** —
   you scaffold `Tests.Infrastructure.Export` (mirror an existing test csproj for the `xunit.v3.mtp-v2` + MTP 2.1.0
   package set, NSubstitute/Shouldly global usings) + tests + a new `stryker-config.json`. Avoid `DigitalPdfSigner`
   (crypto) and `PdfRequirementSummarizerService` (PDF rendering) — slower / less deterministic. **Highest value:
   SIRO XML is a regulatory deliverable.**
2. **Classification (remaining)** — `LegalDirectiveClassifierService.cs` (dictionary-based, 3 test files, big but
   deterministic), `FileClassifierService.cs`. Avoid `ExpedienteClasifierService` / `SemanticAnalyzer*`
   (Ollama/HTTP). The Classification `stryker-config.json` already exists (mutates the 3 matching files) — extend
   its mutate list.
3. **Imaging** — `Infrastructure.Imaging` (deterministic filters/quality) for pure-math targets.

The per-unit loop is unchanged — **§5 of `HANDOFF-2026-06-08-mutation-testing-continuation.md`**. Mechanics that
saved time: scope `mutate` to **one file** for the baseline; parse survivors from the HTML report
(`app.report = {...}`, `json.JSONDecoder().raw_decode`); **also classify the Timeout bucket** (log/equivalent vs
functional); write exact-value tests; **prefer NSubstitute mocks of the dependency interface** when the unit is an
orchestrator/coordinator (it isolates the unit's own logic far better than driving it through real collaborators);
re-run; then re-add all hardened files to the config's mutate list before committing. Commit test+config per unit
with the `Killed` delta in the subject; update the guide; push.

## 4. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged. Two real defects surfaced by mutation testing; fix them **together in a dedicated session**, separate
from test-hardening:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)

Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is an unused private method — safe to delete.

## 5. Minor behavioural findings still pinned as-is (from Units 6–10, not fixed)
- Date-only documents return null in several strategies (the `Fecha`/date case sets neither `hasAnyData` nor a
  guard-counted collection).
- `StructuredDocxStrategy.ExtractMexicanNames` assigns captured groups **positionally** for both name patterns, so
  the `PATERNO MATERNO Nombre` pattern mislabels the parts.
- The Accion `(?:identificada|con|en).*$` cleanup matches the substring `en` *inside* words like
  "aseguram**ien**to", truncating legitimate values.

## 6. CI mutation gate (still optional)
Eleven units now have stable baselines. If/when a gate is wanted: `dotnet stryker --since` (diff-based) on PRs,
full per-unit runs nightly, `thresholds.break` a few points below each unit's stable (Killed-based) floor — **not**
the noisy headline % for units prone to timeout inflation. Keep `StrykerCompat=true` + `test-runner: mtp`. Don't
gate the two units with open findings at their current score.

## 7. Pointers
- Guide / per-unit detail / both timeout lessons: `docs/qa/test-plans/mutation-testing.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, reserved §2, CI §4: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
