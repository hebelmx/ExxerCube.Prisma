# Handoff — Mutation-testing campaign COMPLETE (§2.6 Core/Application + §2.7) (2026-06-10)

**For:** whoever picks up mutation testing (or audits it) next.
**Branch:** `Kt2`. **Status: the marathon is COMPLETE** — the deterministic business-logic surface is
mutation-hardened end-to-end. What remains are *non-deterministic* areas excluded by design and two small
operational loose ends (below).

**Read first:** `docs/qa/test-plans/mutation-testing.md` (the reference — Units 38–42 are this session) and
`docs/qa/test-plans/mutation-testing-roadmap.md` (§2.6 and §2.7 now ✅).

---

## 1. What this session did — Units 38–42

| Unit | File (project) | Tests | Result |
|---|---|---|---|
| 38 | `SLATrackingService` (`01 Core/Application`) | +50 | Killed 65/66, **0 killable survivors** |
| 39 | `AuditReportingService` (`01 Core/Application`) | +59 | Killed ~104, **0 killable survivors** |
| 40 | `FieldMatchingService` (`01 Core/Application`) | +44 | Killed 129, **0 killable survivors** |
| 41 | `DecisionLogicService` (`01 Core/Application`) | +46 | Killed ~120, **0 killable survivors** |
| 42 | `SemanticAnalyzerService` (`02 Infrastructure/Infrastructure.Classification`, §2.7) | +16 | **0 killable survivors** |

A **combined confirming run** over all 5 hardened Application files read **Killed 612 / Survived 245**
(ConfigurationValidation 215, SLATracking 65, AuditReporting 103, FieldMatching 129, DecisionLogic 100); a scoped
DecisionLogic+Audit reconfirm verified the gap fixes. Every remaining survivor is the **equivalent/dead/floor**
class (Serilog, `ConfigureAwait`, audit-trail side-effects, unreachable branches) — detailed per-unit in the guide.

Commits on `Kt2`: `0896eed` (Units 38–41 tests + config), `c5f72a2` (combined-run gap fixes + §2.7 + finding),
then the doc/config-restore commit (this handoff set).

## 2. The §2.6/§2.7 split that was used

**Hardened (pure/deterministic, mock-drivable):** ConfigurationValidation, SLATracking, AuditReporting,
FieldMatching, DecisionLogic, SemanticAnalyzer.
**Skipped (I/O orchestrators, out of scope):** `DocumentIngestionService`, `FileDownloadService`,
`HealthCheckService`, `ExportService`, `FileMetadataQueryService`, and **`MetadataExtractionService`** (real
`File.Exists` / `File.ReadAllBytesAsync`).

## 3. New lessons (full detail in the guide, Units 38–42)

1. **`IsSuccess` is STRICT in this `IndQuestResults` build** — `Result.Success(null)` → `IsSuccess == false`, yet
   it's neither a failure nor cancelled. Assert null-value success paths via `IsFailure==false` + `Value==null`.
   This also makes several `IsSuccess && Value != null` → `||` mutants **correlated-equivalent** after the prior
   failure/cancel guards (only failure-with-value can tell them apart).
2. **Exact-LINE, not substring, for trailing CSV/JSON fields** — a trailing-field mutation hides behind a
   substring assert; use `Lines(text).ShouldContain("…exact row…")`.
3. **Repeat the boundary/order test for EVERY method** — `< startDate` and `OrderBy` exist in all four Audit
   methods; the first pass missed ExportCsv/Json `<`→`<=` and ExportJson's OrderBy.
4. **Mock the policy/comparer to isolate branches** — FieldMatching via `IMatchingPolicy`, SemanticAnalyzer via
   `ITextComparer` (`FindBestMatch("<exact phrase>",…)` per dictionary). Capture args (`Arg.Is`) to pin
   origin/confidence/source.
5. **Failure-with-value (`WithFailure(err, value)`) is the only way to kill `IsSuccess && Value!=null`→`||`.**
6. **Trigger DecisionLogic partial-cancellation paths** by making the resolver mock RETURN `Cancelled` (not throw)
   or cancel a CTS from inside the resolver's first call; only the **Result-visible** outputs (warnings/confidence/
   ratio) are killable — audit-trail `LogAuditAsync` side-effects to a mocked dep are floor, and a cancelled token
   makes the downstream classify cancelled, so classify-failure/success-with-partial sub-branches are dead.
7. **SmartEnum serialization:** classification JSON serializes `r.Stage` (the raw `EnumModel` object) as nested
   properties; export JSON uses `.ToString()` = `displayName ?? Name` (e.g. `"Decision Logic"` with a space).

## 4. Findings surfaced (NOT fixed here — pinned-as-is / documented)

- **NEW:** `FieldMatchingService` `FechaEstimadaConclusion` validation warning is **inverted** (warns when present,
  silent when missing). `docs/qa/findings/2026-06-09-fieldmatching-fechaestimada-warning-inverted.md`.
- **Still reserved (own session, do NOT bundle):**
  `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`,
  `docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md` (HIGH).
- Minor dead-code (floor): `MatchingPolicyService.GetConflictThreshold(string)` unused;
  `ProcessingMetricsService.UpdateCurrentStatistics` `recentFailed` unused; `patternViolations++` unreachable in
  the Docx/Pdf metadata extractors.

## 5. Deferred items — BOTH DONE this session

1. ✅ **Unit 24 Imaging filter-selection mutation-verify — DONE.** Scoped re-run (pure managed → no popups):
   Analytical 59 killed/22, Default 21/4, Polynomial 30/32. Added boundary-exact tests (Analytical +16, Default
   +4) → Analytical & Default **0 killable survivors**. `PolynomialFilterSelectionStrategy` is an explicit
   stub/placeholder ("uses stub models … TODO: train models") — its residual is floor (stub-model name strings,
   Serilog, unused-`features` adaptive array, dead `bilateralD` odd-branch) plus the normalized-heuristic
   `SelectFilterType` boundaries, deferred until real trained models replace the stubs.
2. ✅ **Native-WER-popup — RESOLVED.** Decision: **native-bound files are formally excluded** from the mutation
   target (ambiguous mutants — a crashed native mutant counts as killed regardless; not deterministic logic). The
   permanent fix is demonstrated: set `HKCU\Software\Microsoft\Windows\Windows Error Reporting\DontShowUI=1` for
   the run and revert after (done programmatically around the Unit 24 run; the pure-managed re-run produced no
   popups, confirming the scoping approach).

## 6. What is intentionally NOT mutation-tested (by design)

OCR/Tesseract/VLM, dormant Python/CSnakes, Browser automation, Database/Testcontainers, UI/Playwright, worker
orchestration, legacy `InMemoryEventBus`, native EmguCV/OpenCv/PIL, and the I/O orchestrators in §2. Mutants there
are non-deterministic or ambiguous.

## 7. Config state

Each hardened test project has its own `stryker-config.json` with **`test-runner: mtp`** + run with
**`StrykerCompat=true dotnet stryker`** (both mandatory — see `CLAUDE.md` → Testing stack). The Application config
mutates all 5 Application files; the Classification config now includes `SemanticAnalyzerService.cs`.

## 8. Pointers
- Reference + every per-unit lesson: `docs/qa/test-plans/mutation-testing.md`
- Plan / tick-box: `docs/qa/test-plans/mutation-testing-roadmap.md`
- Prior handoff (§2.5): `docs/development/sessions/HANDOFF-2026-06-09-mutation-metrics-filestorage-COMPLETE.md`
- Memory: `mutation-testing-stryker.md`
