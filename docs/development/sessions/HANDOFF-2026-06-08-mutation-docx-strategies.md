# Handoff — Mutation testing: DOCX strategies done, what's next (2026-06-08)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
the timeout-inflation lesson, cross-repo guideline). This handoff is the *roadmap*; that doc is the *reference*.
**Branch:** `Kt2` — all work below is committed **and pushed** to `origin/Kt2` (through `6f21807`).
Build green, Adaptive test project green (282/282).

**Supersedes** `HANDOFF-2026-06-08-mutation-testing-continuation.md` for the "next units" question; that doc's
§2 (reserved findings) and §4 (CI gate) and §5 (the loop) are **still authoritative** — re-read them.

---

## 1. What this session did (5 Adaptive-DOCX strategy units)

Hardened all five `IAdaptiveDocxStrategy` implementations in
`02 Infrastructure/Infrastructure.Extraction.Adaptive/Strategies/`, driven by
`Tests.Infrastructure.Extraction.Adaptive`. Each got a `*MutationKillingTests.cs` next to its existing
`*LiskovTests` (which were loose: `if (ContainsKey)` guards, `ShouldContain`, confidence *ranges*).
**Adaptive project 126 → 282 tests, all green.** The project's `stryker-config.json` now mutates all six
hardened files (the five strategies + `EnhancedFieldMergeStrategy`).

| Unit | New tests | Killed (Δ) | Outcome |
|---|---|---|---|
| TableBasedDocxStrategy | 26 | 99 → 157 | 63.2% → 96.3%, 0 survivors |
| StructuredDocxStrategy | 25 | 101 → 132 | all observable mutants killed |
| ContextualDocxStrategy | 29 | 41 → 103 | 84.6% → 96.5%, 0 survivors |
| SearchExtractionStrategy | 29 | 54 → 138 | all observable mutants killed |
| ComplementExtractionStrategy | 29 | 49 → 120 | 93.6% → 97.3%, 0 survivors |

Commits: `e33a65d`..`6f21807` (5 test+config commits + 1 guide-doc commit). Per-unit detail in the guide
under **"Units 6–10"**.

## 2. ⚠️ The lesson that will bite you on regex-heavy units — TIMEOUT-INFLATED BASELINES

Stryker counts a **`Timeout` as killed** (a hanging mutant is "detected"). Mutated regex patterns backtrack
catastrophically and time out, so on a regex-heavy unit **dozens-to-hundreds of mutants land in `Timeout` and
inflate the headline score, masking real survivors.** Observed this session: StructuredDocx baseline **94.97%
with 92 timeouts** → after adding coverage the timeouts collapsed and the true survivors appeared; the score
then read **67–86% across *identical* re-runs** purely on timeout count. Search baseline had 129 timeouts,
Complement 155.

**So, for any regex-heavy unit:**
- **Do not trust a high baseline** — it's probably timeout masking. Add exact-value coverage and re-run; the
  timeouts collapse and the genuine survivors surface to pin.
- **The headline % is noisy** (swings ~30 points run-to-run). Report the **stable** signal: the `Killed`
  count delta and **"0 killable survivors / residual is the equivalent floor."** That's what the commit
  messages do — copy that framing.
- The equivalent floor is consistent: Serilog format-strings, `match.Success && match.Groups.Count > 1`
  capture-guards (failed match has `Count==1`), redundant alternate regex patterns (the first subsumes the
  rest), `\d{18}` CLABE validation, post-`break` re-match no-ops, the two defensive `catch` blocks, and a
  perTest coverage-attribution artifact on the currency ternary.

## 3. Next units to harden (pick any — independent; same §5 loop)

In rough priority / cleanliness order:

1. **`AdaptiveDocxExtractor.cs`** (`Infrastructure.Extraction.Adaptive/`) — the **orchestrator** that selects
   among the five (now-hardened) strategies. Listed in the prior handoff's §3 alongside the strategies but
   **not yet done**. Natural next step; finishes the Adaptive-DOCX project. Driven by
   `AdaptiveDocxExtractorLiskovTests` + `AdaptiveDocxExtractionIntegrationTests`. Add it to the project's
   `stryker-config.json` mutate list.
2. **Export** — `02 Infrastructure/Infrastructure.Export/SiroXmlExporter.cs` (the SIRO-XML deliverable,
   deterministic), `ExcelLayoutGenerator.cs`, `CriterionMapperService.cs`. Note: **no dedicated test project
   yet** — you scaffold `Tests.Infrastructure.Export` (mirror an existing test csproj for the xunit.v3.mtp-v2
   + MTP package set) + tests + a new `stryker-config.json`. Avoid `DigitalPdfSigner` (crypto) and
   `PdfRequirementSummarizerService` (PDF rendering) — slower / less deterministic.
3. **Classification (remaining)** — `LegalDirectiveClassifierService.cs` (dictionary-based, 3 test files,
   big but deterministic), `FileClassifierService.cs`. Avoid `ExpedienteClasifierService` /
   `SemanticAnalyzer*` (Ollama/HTTP). The Classification `stryker-config.json` already exists (mutates the 3
   matching files) — extend its mutate list.
4. **Imaging** — `Infrastructure.Imaging` (deterministic filters/quality) for pure-math targets.

The loop is unchanged — **§5 of `HANDOFF-2026-06-08-mutation-testing-continuation.md`**. Mechanics that saved
time this session: scope `mutate` to **one file** for the baseline run, parse survivors from the HTML report
(`app.report = {...}`, `json.JSONDecoder().raw_decode`), classify log/capture-guard/redundant-pattern as the
equivalent floor up front, write exact-value tests for the rest, then re-add all hardened files to the config's
mutate list before committing. Commit test+config per unit with the `Killed` delta in the subject; update the
guide; push.

## 4. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from the prior handoff §2. Two real defects surfaced by mutation testing; fix them **together in a
dedicated session**, separate from test-hardening:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)

Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is an unused private method — safe to delete.

## 5. Minor behavioural findings surfaced this session (pinned as-is, not fixed)

Documented in the test comments + the guide; decide later whether any deserve a fix:
- **Date-only documents return null** in several strategies — the `Fecha`/date case sets neither `hasAnyData`
  nor a guard-counted collection, so a document with only dates hits the "no data extracted" guard.
- **`StructuredDocxStrategy.ExtractMexicanNames`** assigns captured groups **positionally**
  (`nombre=Groups[1]`, `paterno=Groups[2]`, `materno=Groups[3]`) for *both* name patterns, so pattern[1]
  (`PATERNO MATERNO Nombre`) mislabels the parts.
- **The Accion `(?:identificada|con|en).*$` cleanup** (Complement) matches the substring `en` *inside* words
  like "aseguram**ien**to", truncating legitimate values.

## 6. CI mutation gate (still optional — §4 of prior handoff)

Ten units now have stable baselines. If/when a gate is wanted: `dotnet stryker --since` (diff-based) on PRs,
full per-unit runs nightly, `thresholds.break` a few points below each unit's *stable* (Killed-based) floor —
**not** the noisy headline % for the regex-heavy units. Keep `StrykerCompat=true` + `test-runner: mtp`.
Don't gate the two units with open findings at their current score.

## 7. Pointers
- Guide / per-unit detail / timeout lesson: `docs/qa/test-plans/mutation-testing.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Prior handoff (loop §5, reserved §2, CI §4): `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
