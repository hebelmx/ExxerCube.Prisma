# Handoff — Mutation testing: Classification deterministic units COMPLETE, what's next (2026-06-08)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
the timeout/attribution-noise lessons, cross-repo guideline). This handoff is the *roadmap*; that doc is the *reference*.
**Branch:** `Kt2` — all work below is committed (push pending at time of writing; see §6). Build green,
Classification test project green (**258/258**).

**Supersedes** `HANDOFF-2026-06-08-mutation-orchestrator-done.md` for the "next units" question. The reserved
conflict-detection findings (§4) are unchanged.

---

## 1. What this session did — Units 12 & 13: the two deterministic Classification classifiers

Two units, both in `Infrastructure.Classification`, driven by `Tests.Infrastructure.Classification`. The
project's `stryker-config.json` now mutates **all five** deterministic files (the 3 matching policies from the
prior session + these two). **This finishes the deterministic Classification surface** — the only Classification
files left unmutated are the Ollama/HTTP ones (`ExpedienteClasifierService`, `SemanticAnalyzer*`), which are
deliberately out of scope (non-deterministic).

| Unit | File | Score | Tests added |
|---|---|---|---|
| 12 | `LegalDirectiveClassifierService.cs` | 79.68% → **96.02%** | 56 |
| 13 | `FileClassifierService.cs` | 38.79% → **88.79%** | 35 |

- **Unit 12** (commit `c5be87b` test+config, `4d7a68f` doc): keyword/regex legal-directive classifier. Killed
  170→241, 0 killable survivors. Residual 10 = the three `catch(Exception)` blocks + `CalculateConfidence`'s
  dead `if (matches==0)` guard.
- **Unit 13** (commit `1f74070` test+config, `ee76b58` doc): rule-based regulatory Level1/Level2 classifier.
  Killed 45→103, 0 killable survivors. Surfaced a **dead-branch finding** (the `Min(85)` middle confidence tier
  is unreachable: discrete scores {10,70,90} only ever differ by {0,20,60,80}, never [40,60)).

## 2. Two reusable lessons from Unit 12 (both already folded into the guide)

1. **perTest attribution noise flaps the headline hard.** Identical test sets gave 0 / 5 / 7 / 18 "survivors"
   across re-runs of the *same* code — almost all **Serilog statement/string mutants** (equivalent floor)
   flip-flopping between Killed/Survived/Timeout because perTest coverage attribution is unstable on a large,
   slow suite. **Trust the `Killed` delta + "0 killable survivors" from a clean run**, never a single headline %.
2. **A one-off `coverage-analysis: "off"` cross-check separates real gaps from noise — with two caveats.**
   Temporarily set `coverage-analysis` to `"off"` (then revert to `perTest`): it runs every test against every
   mutant, so attribution luck is gone. It exposed genuine gaps Unit 12's noisy runs hid (the Classify-path
   wiring calls; a test contaminated by `"ordena"` embedding the substring `"ORDEN"`). **Caveats:** (a) it
   **over-counts** survivors via a Stryker **static-initializer limitation** — `static readonly string[]` keyword
   arrays survive in coverage-off's shared process (the array initializes once; the per-mutant switch never
   re-runs the initializer) but **perTest kills them**; (b) it is slower. Use it to *find* gaps, report the score
   from `perTest`.

## 3. Next units to harden (pick any — independent; same per-unit loop)

The Classification deterministic surface is done. In rough priority / cleanliness order:

1. **Export** — `02 Infrastructure/Infrastructure.Export/SiroXmlExporter.cs` (the SIRO-XML regulatory
   deliverable, deterministic), `ExcelLayoutGenerator.cs`, `CriterionMapperService.cs`. **No dedicated test
   project yet** — scaffold `Tests.Infrastructure.Export` (mirror an existing test csproj for the
   `xunit.v3.mtp-v2` + MTP 2.1.0 package set + NSubstitute/Shouldly global usings) + tests + a new
   `stryker-config.json`. Avoid `DigitalPdfSigner` (crypto) and `PdfRequirementSummarizerService` (PDF
   rendering). **Highest value: SIRO XML is a regulatory deliverable.**
2. **Imaging** — `Infrastructure.Imaging` (deterministic filters/quality) for pure-math targets.
3. **Classification — one possible extra:** `SemanticAnalyzerService.cs` is actually deterministic (it uses the
   `LevenshteinTextComparer` + the in-repo `ClassificationDictionary`, no Ollama) and already has the
   `LegalDirectiveClassifierDictionaryTests` + `TextComparerFindBestMatchTests` driving it. It could be hardened
   without new infra. (Note: despite its name, `LegalDirectiveClassifierDictionaryTests` tests
   `SemanticAnalyzerService`, not `LegalDirectiveClassifierService`.) Lower priority than Export.

The per-unit loop is unchanged — **§5 of `HANDOFF-2026-06-08-mutation-testing-continuation.md`**. Time-savers
that worked this session: scope `mutate` to **one file** for the baseline; parse survivors/NoCoverage/**and the
Timeout bucket** from the HTML report (`app.report = {...}`, `json.JSONDecoder().raw_decode`); write exact-value
tests; for a noisy/large suite do one `coverage-analysis: off` cross-check to find real gaps (then revert);
re-run; **re-add all hardened files to the config's `mutate` list before committing**; commit test+config per
unit with the `Killed` delta in the subject; update the guide; push.

## 4. STILL RESERVED — two conflict-detection findings (own session, do NOT bundle)

Unchanged from prior handoffs. Two real defects surfaced by mutation testing on the **other three**
Classification files (FieldMatcher/MatchingPolicy/NameMatching); fix them **together in a dedicated session**:
1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)

Also minor: `MatchingPolicyService.GetConflictThreshold(string)` is an unused private method — safe to delete.

## 5. Minor behavioural findings pinned as-is this session (not fixed)
- **FileClassifier dead middle confidence tier** (Unit 13): `Min(85, maxScore)` branch is unreachable because
  the discrete category scores {10,70,90} never yield a top-two difference in [40,60). Documented in the test
  file; fix = continuous scoring or collapse the tier. Minor.
- (Carried over from Units 6–11 — DOCX strategy date-only nulls, positional name mislabel, the `en`-substring
  Accion cleanup — see the orchestrator handoff §5.)

## 6. Status / push
- Commits this session on `Kt2`: `c5be87b`, `4d7a68f` (Unit 12), `1f74070`, `ee76b58` (Unit 13), plus this
  handoff. **If not already pushed, `git push origin Kt2`.**
- Solution/build green; Classification project 258/258 green.

## 7. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md`
- Reserved findings: `docs/qa/findings/2026-06-08-*.md`
- Per-unit loop, reserved §2, CI §4: `docs/development/sessions/HANDOFF-2026-06-08-mutation-testing-continuation.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
