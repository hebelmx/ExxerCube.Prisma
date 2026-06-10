# Handoff — Mutation testing: §2.5 Metrics/FileStorage COMPLETE (Units 34–36) (2026-06-09)

**For:** the next agent continuing mutation-testing work on ExxerCube.Prisma.
**Read first:** `docs/qa/test-plans/mutation-testing.md` (canonical guide — Units 34–36 are this session) and
`docs/qa/test-plans/mutation-testing-roadmap.md` (the tick-box plan; §2.5 now ✅, §2.6 is next).
**Supersedes** `HANDOFF-2026-06-09-mutation-extraction-base-COMPLETE.md` for the "next units" question.
**Branch:** `Kt2`. Build green; FileStorage **32/32**, Metrics **40/40** green.

---

## 1. What this session did — Units 34–36

| Unit | File (project) | Result | Tests |
|---|---|---|---|
| 34 | `SafeFileNamerService.cs` (`Infrastructure.FileStorage`) | Killed 51 / Survived 18 / Timeout 1, **0 killable survivors** | 4 → 21 |
| 35 | `FileMoverService.cs` (`Infrastructure.FileStorage`) | bundled (project 10 → **32 green**), **0 killable survivors** | 3 → 11 |
| 36 | `ProcessingMetricsService.cs` (`Infrastructure.Metrics`) | Killed 88 / Survived 34 / Timeout 3, **0 killable survivors** | 18 → **40 green** |

Two new `stryker-config.json` files (one per test project), both `test-runner: mtp`, `coverage-analysis: perTest`.
Commit `66e8e9e` (tests + configs), then this doc-set. **✅ §2.5 Metrics/FileStorage COMPLETE.**

## 2. Key lessons added this session (full detail in the guide, Units 34–36)

1. **Shouldly string `ShouldContain`/`ShouldNotContain` are case-INSENSITIVE by default.** To assert casing
   (e.g. that `ToUpperInvariant` ran) use `value.Contains(x, StringComparison.Ordinal).ShouldBeFalse()`, not
   `ShouldNotContain("mixedCase")` (which trips on the upper-cased text).
2. **`coverage-analysis: off` is a one-shot killability oracle, not a reporting mode (under MTP).** It proved
   `FileMoverService` L35's `ArgumentException` throw is killable (the empty-path test kills it there) — it
   survives `perTest` only as **exception-path attribution noise**. But the off-run *also* mis-reported other
   mutants (flipped `counter++` from Timeout-killed to Survived; phantom survivors on `CreateDirectory`/the
   truncation block). **Keep the committed config on `perTest`; use off only to answer "is X killable?".**
3. **Reflection makes timer callbacks and validation state deterministic.** Invoke the private `AggregateMetrics`
   `Timer` callback directly; for `ValidatePerformanceAsync`, **enqueue events into the private
   `_processingEvents` `ConcurrentQueue`** and set the private-setter `CurrentStatistics` property — this builds
   an exact throughput + statistics snapshot *without* a 100-doc loop, so each threshold branch can be isolated
   to a single failing requirement (killing the per-branch `IsMeetingRequirements = false` flips). Kill
   `>`/`<` thresholds at the exact boundary (avg-time 30, success-rate 0.99 both *meet*).
4. **Kill `Average→Min/Max` only where the inputs are deterministic** (confidence: two events 0.9/0.5 → mean
   0.7). On real `Stopwatch`-elapsed processing time it is **floor** (non-deterministic). Float means need a
   tolerance: `ShouldBe(0.7f, 0.0001f)`.
5. **Dispose semantics are testable:** after `Dispose()`, the disposed `SemaphoreSlim` makes
   `StartProcessingAsync` throw `ObjectDisposedException` — kills `Dispose(true)`→`Dispose(false)`/removal, the
   `_metricsLock.Dispose()` removal, and the `!_disposed && disposing` negate mutants. The `&&`→`||`,
   `SuppressFinalize`, timer-dispose, and `_disposed = true` mutants are the Dispose-pattern equivalent floor.
6. **perTest flaps run-to-run** on these larger/lock-heavy suites (88↔89 killed, phantom survivors on the
   `WaitAsync` lines). Trust **Killed delta + 0 killable survivors**, never the headline % (70 %).

## 3. Where mutation testing stands

- **~46 files hardened across 9 projects**, all **0 killable survivors**. Roughly **75–80 %** of the worthy
  deterministic surface. Largest remaining: **§2.6 Core/Application**.
- 9 `stryker-config.json` files (one per hardened test project).

## 4. Next units (priority order) — §2.6 Core/Application

1. **Survey first** (`01 Core/Application`, ~11 `*Service.cs`): pick the **pure/deterministic** ones —
   `DecisionLogicService`, `ConfigurationValidationService`, `SLATrackingService`, `AuditReportingService`,
   `FieldMatchingService`, `MetadataExtractionService`. ⛔ skip the I/O orchestrators
   (`DocumentIngestionService`, `FileDownloadService`, `HealthCheckService`).
2. Each needs its own `stryker-config.json` next to its test project (the per-test-project config is the easiest
   step to forget — `git status` first to confirm `Kt2` is clean, then add the config).
3. Optional: `Infrastructure.Classification/SemanticAnalyzerService.cs` (deterministic Levenshtein + in-repo
   dictionary, NOT Ollama).

⛔ **Avoid (non-deterministic / out of scope):** OCR/Tesseract/VLM, Browser automation, DB/Testcontainers,
UI/Playwright, worker orchestration, legacy events, native EmguCV/OpenCv/PIL.

## 5. STILL DEFERRED to end-of-campaign (unchanged)

1. **Unit 24 mutation-verify re-run** — the 3 Imaging `*FilterSelectionStrategy.cs` have green value-exact tests
   but the confirming Stryker run was deferred.
2. **Native-mutation Windows popups permanent fix** (EmguCV `CvInvoke.*` crashes → WER dialogs).

## 6. STILL RESERVED — own session, do NOT bundle

1. `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`
2. `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH)
Minor dead-code notes (floor, not defects): `MatchingPolicyService.GetConflictThreshold(string)` unused private;
`patternViolations++` unreachable in the Docx/Pdf metadata extractors; **`recentFailed` in
`ProcessingMetricsService.UpdateCurrentStatistics` is computed but never used** (new this session).

## 7. The per-unit loop (reminder)

1. Pick a pure/deterministic file; confirm the right test project drives it; read existing tests for gaps.
2. For large files, scoped baseline first (`mutate` = just those files).
3. Write **exact-value** tests (Shouldly — remember string contains is case-insensitive;
   `TestContext.Current.CancellationToken`; NSubstitute; no Moq/FluentAssertions). Reflection is fair for
   private timer callbacks / injecting deterministic state.
4. Build + run the test project green.
5. Scoped run: `cd <test-project-dir> && StrykerCompat=true dotnet stryker` (background ~5–10 min).
6. Parse `StrykerOutput/<ts>/reports/mutation-report.html` (`app.report = {...}`; `json.JSONDecoder().raw_decode`).
   Cover **reachable** NoCoverage error-branches; classify the rest as floor. Trust Killed delta + 0 killable
   survivors. Use a one-shot `coverage-analysis: off` run only to *prove* a suspicious survivor is killable
   (then revert to `perTest`).
7. **Re-add ALL hardened files to the config's `mutate` list** before committing. Commit test+config separately
   from docs. Update `mutation-testing.md` + roadmap + memory; push.

## 8. Pointers
- Guide / per-unit detail / all lessons: `docs/qa/test-plans/mutation-testing.md` (Units 34–36 this session)
- Plan / tick-box checklist: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Prior handoff (§2.4 Units 31–33): `docs/development/sessions/HANDOFF-2026-06-09-mutation-extraction-base-COMPLETE.md`
- Testing-stack constraints (`test-runner: mtp` + `StrykerCompat=true` mandatory): `CLAUDE.md` → "Testing stack"
