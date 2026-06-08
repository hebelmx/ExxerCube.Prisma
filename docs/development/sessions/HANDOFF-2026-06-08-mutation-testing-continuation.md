# Handoff — Mutation testing continuation (2026-06-08)

**For:** the next agent picking up mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (the canonical guide — setup, per-unit results,
cross-repo guideline). This handoff is the *roadmap*; that doc is the *reference*.
**Branch:** `Kt2` (all work below is committed **and pushed** to `origin/Kt2`). Build green, suites green.

This complements (does not supersede) `HANDOFF-2026-06-08-audit-verification-mutation.md`.

---

## 1. What's done (5 units hardened, survivor-driven)

Each unit got a `*MutationKillingTests.cs` next to its existing tests, driven by parsing the Stryker HTML
report and writing **exact-value** assertions. Per-unit detail + residual analysis is in the guide.

| Unit | File | Score | Tests added |
|---|---|---|---|
| AdaptiveTxtFieldExtractor | `02 Infrastructure/Infrastructure.Extraction.Txt` | 24.9% → **82.1%** | 88 |
| EnhancedFieldMergeStrategy | `02 Infrastructure/Infrastructure.Extraction.Adaptive` | 66.2% → **87.2%** | 18 |
| FieldMatcherService<T> | `02 Infrastructure/Infrastructure.Classification` | 30.7% → **61.4%** | 16 |
| MatchingPolicyService | `02 Infrastructure/Infrastructure.Classification` | 41.1% → **56.8%** | 13 |
| NameMatchingPolicy | `02 Infrastructure/Infrastructure.Classification` | 3.1% → **36.9%** | 12 |

Stryker configs exist at (each `mutate`-scoped to the hardened file(s)):
- `08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt/stryker-config.json`
- `08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Adaptive/stryker-config.json`
- `08 Tests/02 Infrastructure/Tests.Infrastructure.Classification/stryker-config.json` (mutates all 3
  Classification matching files)

## 2. RESERVED for its own dedicated session — do NOT bundle

Mutation testing surfaced **two real conflict-detection defects**. They are behavior changes (not test work),
they are related, and they are self-contained. **Fix them together in a single focused session**, separate
from any test-hardening, to avoid context overload and scope creep. Full writeups (root cause, impact,
suggested fixes, guardrails) are ready:

1. **`docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`**
   — `FieldMatcherService.CollectAdditional` ignores its `origin` param (Xml/PdfOcr branches identical), so
   the XML-vs-OCR additional-field conflict detection never fires.
2. **`docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md`** (HIGH)
   — `NameMatchingPolicy` scores every value against itself (`ScorePair(x,x)=1.0`), so it always reports a
   perfect match and never a conflict; the fuzzy/alias/accent logic (~70% of the file) is inert.

**Why a separate session:** both touch reconciliation/identity semantics; #1 may ripple into `ExtractedFields`
and every `IFieldExtractor<T>`. After fixing, re-run Stryker on the affected files — the ~75 currently-dead
mutants (FieldMatcher merge block + NameMatchingPolicy fuzzy/alias) become killable, which will lift those
three Classification units well above their current ceilings. The existing mutation-killing tests are written
to **keep passing after the fix** (they assert only fix-safe behavior); you add cross-value tests then.

Minor (no session needed, fold into the above or a cleanup): `MatchingPolicyService.GetConflictThreshold(string)`
is an unused private method — safe to delete.

## 3. Next units to harden (clear path, pick any — independent)

Follow the exact same loop used for the 5 done units (see §5). Best remaining **deterministic, fast**
candidates (avoid live-OCR / Docker / Playwright / dormant-Python suites):

- **DOCX strategies** (Adaptive project, diversifies away from Classification) — each has a `*LiskovTests`
  with the same contract-vs-exact-value gap:
  `02 Infrastructure/Infrastructure.Extraction.Adaptive/Strategies/{StructuredDocxStrategy, TableBasedDocxStrategy,
  SearchExtractionStrategy, ComplementExtractionStrategy, ContextualDocxStrategy}.cs`
  and `AdaptiveDocxExtractor.cs`. Expected to give clean, high-ceiling wins (no known dead code).
- **Export** — `02 Infrastructure/Infrastructure.Export/SiroXmlExporter.cs` (the SIRO-XML deliverable,
  deterministic) and `ExcelLayoutGenerator.cs` / `CriterionMapperService.cs`. Note: it has **no dedicated
  SiroXmlExporter test yet** — you'll add the test project scaffolding + tests. Avoid `DigitalPdfSigner`
  (crypto) and `PdfRequirementSummarizerService` (PDF rendering) — slower / less deterministic.
- **Classification (remaining)** — `LegalDirectiveClassifierService.cs` (dictionary-based, has 3 test files,
  big but deterministic), `FileClassifierService.cs`. Avoid `ExpedienteClasifierService` / `SemanticAnalyzer*`
  (Ollama/HTTP).
- **Imaging** — `Infrastructure.Imaging` (deterministic filters/quality) if you want pure-math targets.

## 4. Optional: CI mutation gate (only once a baseline policy is agreed)

Five units now have stable baselines. To gate regressions without slowing PRs:
- Set `thresholds.break` per config above a *conservative* floor (e.g. a few points below each unit's current
  score) — NOT the headline number, because the equivalent-mutant floors differ per unit.
- Run diff-based on PRs: `dotnet stryker --since` (only mutates changed files); reserve full per-unit runs for
  nightly. Keep `StrykerCompat=true` and `test-runner: mtp` (both mandatory — see guide).
- Don't gate the two units with open findings (FieldMatcher/NameMatching) at their *current* score as if it
  were "good"; their real target is post-fix.

## 5. The loop (how every unit above was done)

1. `dotnet tool restore` once. Confirm the target test project is green:
   `dotnet test <test-project.csproj>`.
2. Create/extend `stryker-config.json` next to the test project: set `project`, `test-projects`,
   `"test-runner": "mtp"`, and `mutate: ["**/<File>.cs"]`. (Strict JSON — no unknown keys, not even comments.)
3. Baseline: from the test-project dir, `StrykerCompat=true dotnet stryker`. **Both** settings are mandatory
   (test-runner=mtp or you get a structural 0%; StrykerCompat=true to flatten this repo's custom
   `bin/<project>/<config>/<tfm>` output layout).
4. Parse survivors from the HTML report — the data is JSON in a `<script>` (`app.report = {...}`); use
   `json.JSONDecoder().raw_decode` (there is trailing JS after the object). Group by line/method.
5. Prioritize **NoCoverage** (counts against the score; score = `(Killed+Timeout)/(Killed+Timeout+Survived+NoCoverage)`),
   then weak (`ShouldNotBeNull`-only) survivors. Write **exact-value** tests. Keep tests deterministic
   (TestContext.Current.CancellationToken; Shouldly; NSubstitute; no Moq/FluentAssertions).
6. Re-run; confirm the gain. Classify the residual honestly (equivalent mutants: correlated guards, log
   strings, `First`→`FirstOrDefault` on non-empty, dead code). **If a cluster is unkillable because the code
   is dead/broken, that's a finding — write it up in `docs/qa/findings/` and don't contort tests to chase it.**
7. Commit in small chunks (test+config separately from docs) with the score delta in the subject; update
   `docs/qa/test-plans/mutation-testing.md` and push.

## 6. Pointers
- Guide / per-unit detail: `docs/qa/test-plans/mutation-testing.md`
- Findings: `docs/qa/findings/2026-06-08-*.md`
- Testing-stack constraints (xunit.v3.mtp-v2 + MTP 2.1.0): `CLAUDE.md` → "Testing stack"
