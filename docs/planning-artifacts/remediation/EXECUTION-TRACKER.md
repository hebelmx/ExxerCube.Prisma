# Remediation Execution — Tracker (Wave 0 critical-few)

**Anchor (intended-solution doc):** `PRISMA-REMEDIATION-EPICS.md` + `VERIQAN-REMEDIATION-EPICS.md` (per-story subagent briefs + ACs are the spec the diff must not drift from).
**Approved:** 2026-06-19 by owner — execute the unified Wave-0 critical few (7 stories), Prisma first. All four planning forks hold.
**Branch:** `Liv` · **Mode:** EXECUTION (orchestrator + isolated subagents; verify every result from ground truth).

## Handoff (top — update on each context clear)
**WAVE-1 SESSION 2026-06-19b DONE + ADVERSARIALLY REVIEWED + 3 MAJORS REMEDIATED + pushed. Branch `Liv`, HEAD=6e429a05. Owner steer = "resume Wave-1 draining".**
**6 stories landed this session:** S7 (f15d4f10 footer-band pagination), S13+CosmeticS2 (c9640ef9 IVA→ILegalToleranceProvider.IvaRate), S16 (e9fc1287 GetOrAdd+retry dedup), CosmeticS1 (422155cd VerdictSignal doc), S4 (dd9a3b7a FR-12 pHash CatalogImagePresenceRule + gated extraction-stage render→hash).
- **S4 nuance:** first pass delivered the rule but NOTHING populated StatementModel.PagePerceptualHashes (rule永abstained — non-functional). Orchestrator caught it from ground truth (grep: only written in a test) and re-delegated the render-population. Now wired + gated behind ctor flag `enableCatalogImageHashing` (default OFF — all-page PDFium render is costly; enable once a real catalog-image bundle + corpus-calibrated Hamming threshold land, VERIQAN-E5/CPA-2). PDFium verified rendering on-box (3/3 non-zero hashes).
- **ADVERSARIAL REVIEW DONE (2 skeptics: plan-completion-reviewer + qa-correctness) → 3 Majors FIXED (81c9e328 extractor, 6e429a05 persistence):**
  (1) **S7** footer band was a HARD filter → a 'N de M' label just above the 10% band was silently lost → CL-31 abstain → false-PASS. Now footer is a PREFERENCE with page-wide last-match FALLBACK (recall never worse than pre-S7). +boundary test (Y~12%).
  (2) **S4** SKBitmap bytes reinterpreted as Bgra32 assuming PDFium=Bgra8888; RGBA platforms → channel swap → false-RED on every image once live. Now normalized to Bgra8888 (zero-copy on Windows) before LoadPixelData.
  (3) **S16** EF dup-retry re-queried the SAME poisoned context → identity map could return the uncommitted NEW entity (wrong GUID) → idempotency broken on real SQL (InMemory test blind to it). Now ChangeTracker.Clear()+AsNoTracking() re-query; honest WithFailure on non-constraint DbUpdateException. +non-constraint test.
- **Verified integrated by orchestrator (re-run, ground truth):** Validation.Tests 479/479, Extraction.Tests 145/145, Orchestration.Tests 71/71, Application.Tests 119/119. (warnings-as-errors ⇒ green = production 0/0.)
- **Concurrent-edit collision (S13+S16 both edited Section6PaymentSimulationRule.cs) merged coherently — VERIFIED by integrated build+test.** S16 also fixed a CS1574 S13's new doc introduced.
- **Calibration-report.md regenerates a fresh TIMESTAMP on every Orchestration.Tests run — pure churn; reverted, do NOT re-commit. (repo-hygiene smell: tracked generated artifact.)**
- **DEFERRED MINORS (logged, not fixed — none are false-verdict risks now):** (a) S7 10% threshold still wants real-corpus calibration → E5 (mitigated by fallback). (b) S13 `IvaRate=0` from a MISCONFIGURED provider → Section16 false-RED; both shipping providers return 0.16 — add a `>0` guard as cheap hardening if revisited. (c) S4 partial page-hash list (a matching page that fails to render → false-absent) → E5; gated OFF. (d) S4 true E2E round-trip test (rendered hash → rule PASS) → E5/corpus.
- **VERIQAN-E2 STATUS: 11/16 + both cosmetics DONE.** Done: S1,S2,S3,S4,S7,S8,S12,S13,S14,S15,S16. **Remaining E2 = 5, ALL PdfPig serial-chain (one per round): S5 (CL-35 Aptos font + FontUsage.IsEmbedded), S6 (CL-34 card image-fallback/page-1 propagation), S9 (America/Mexico_City timezone), S10 (es-MX number format), S11 (CL-48 blank-page glyph filter + 2cm gap).**
- **NEXT:** drain E2 PdfPig tail S5→S6→S9→S10→S11 one-per-round (all touch PdfPigStatementFieldExtractor.cs — never parallel with each other; CAN parallel with a Prisma-tree story, Prisma ⟂ Veriqan). Then Prisma: E2 S3/S5/S8/S4 + E5 S1-S5. Run adversarial review at the E2-epic-completion boundary.
**WAVE-1 (prior) — owner steer 2026-06-19 = "both tracks in parallel". 9 stories DONE + 2 Major reworks, all ground-truth-verified + adversarially reviewed + remediated. Was HEAD=323ff4a5.**
- **DONE (Veriqan-E2 cardinal/correctness, 7):** S1 text-density abstain guard (60fe635d), S2 §20 saldo-a-favor sign guard (d0ab2485; commit subject mislabels it "S1" — it IS S2/#2), S3 §16 column-count guard (8df83b01), S8 TimeProvider→**reworked** to anchor on PeriodCutDate (aa998bdd + fix), S12 VerdictAggregator unknown→abstain (dcc3ec73), S14 marked-PDF rotation→**reworked** swapped-90/270 fix (1adfcbc9 + fix), S15 dup-alert AlertSentAt+concurrency-token (5e1eea27).
- **DONE (Prisma-E2 buildable-now, 2):** S2 production runbook (62ba0c2a; corrected migration-ownership: --migrate-only does ONLY PrismaDbContext per worker), S9 ADR-013 accept in-memory metrics for MVP (95161817).
- **ADVERSARIAL REVIEW (2 skeptics vs epics briefs) → 2 confirmed Majors REWORKED + pushed (323ff4a5):** (1) S14 — the 90° and 270° coordinate transforms were SWAPPED, placing highlights outside the MediaBox on A4; old 270° test bounds-check (<=842) masked it. Re-derived all 4 + added 36-case bounds-invariant property test (Reporting.Tests 67/67). (2) S8 — TimeProvider.System default still drifted across year boundary on reprocess (#36 NOT closed); now anchors year-repair to statement PeriodCutDate year, TimeProvider fallback; cross-clock determinism test (Extraction.Tests 140/140). Minors documented not reworked: dup-alert is DELIBERATE at-least-once (duplicate RED email benign; never drop a RED) — comment added; VerdictAggregator GREEN-with-InsufficientData is by-design (test asserts PassCount=0).
- **Wave-1 test gates (all re-verified independently from ground truth):** Validation.Tests 473/473, Application.Tests 117/117, Reporting.Tests 67/67, Orchestration.Tests 69/69, Extraction.Tests 140/140, Persistence builds 0/0.
- **PdfPig serial-chain discipline:** `PdfPigStatementFieldExtractor.cs` is the shared bottleneck (7 E2 stories touch it: S5/S6/S7/S8/S9/S10/S11). Advance ONE per round; parallelize only disjoint-project stories. `Directory.Packages.props` (S4 needs pHash NuGet) is also a serialize-point. Concurrent-edit collision DID occur this session (S8+S15 both touched JobVerdict.cs/AlertContext.cs) but merged coherently — VERIFIED by full integration build, not assumed. Watch for it.
- **Docker-gated DEFERRALS this wave:** S15 Testcontainers dup-alert IT (code-complete, unit-mocked). Prisma-E2 S1/S6/S7 + the E2E verification of S4/S5 are Docker/real-TCP-gated → not startable on this box.
- **NEXT Wave-1 REMAINING:** Veriqan-E2 S4(pHash,L,NuGet) S5(CL-35 Aptos) S6(CL-34 card) S7(CL-31 pagination) S9(timezone) S10(es-MX num) S11(CL-48 blank) S13(IVA→config) S16(dedup); Veriqan-COSMETIC S1/S2; Prisma-E2 S3(Sentinel trace) S5(ProcessId audit code) S8(CI docker) S4(Tesseract deadlock — needs OCR infra); Prisma-E5 cleanup S1-S5. Order: keep draining the PdfPig chain one-per-round + disjoint parallel.


**WAVE-0 TAIL — PRISMA TRACK COMPLETE + ADVERSARIALLY REVIEWED + REMEDIATED 2026-06-19.** Prisma W0 tail = PRISMA-E1 S6/S2/S7/S5 all done on `Liv`. HEAD=a250d72d.
Prisma-tail commits: 27b78e0c (S6 Serilog sinks — needed worker UseSerilog wire-up, brief's "config-only" premise was wrong), 56621a6e (S2 --migrate-only runner; workers only own PrismaDbContext, not 3 contexts), 03ae2a87 (S7 dashboard counts; Interlocked thread-safety fix), 9a36444e (S5 docker-compose.dev.yml 6-svc), a250d72d (S6/S7 REVIEW FIXES).
**Adversarial review (plan-completion-reviewer) caught 2 Majors → FIXED in a250d72d:** (1) `${SEQ_URL}`/`${OTLP_ENDPOINT}` are NOT interpolated by ASP.NET Core config — Serilog Seq sink crashed all 4 hosts at boot on unset var; fix = `%SEQ_URL%` (Serilog env-expansion) + OTLP Endpoint=null+env-override + per-host SEQ_URL default guard. (2) S7 endpoint test only asserted >=0; added `==3`-after-record endpoint test to Athena+Orion Worker.Tests. Verified: Athena.Worker.Tests 25/25, Reconciliator.Worker.Tests 8/8, Orion.Worker.Tests 24/25 (1 pre-existing env-gated SignalR `IngestionHubWireTests` timing fail — host boots fine), Web.UI 0/0.
**VERIQAN W0 TAIL — COMPLETE + ADVERSARIALLY REVIEWED + REMEDIATED 2026-06-19.** All 7 done on `Liv`:
V-S7 health (fe936377), V-S8 OTel/Serilog+spans (6fbba6f5), V-S10 extraction-coverage floor U2 (8cb842b5), V-S6 bundle move+guide (046f6905), V-S11 streaming batch + poison-PDF guards (d2dd7a82; orchestrator pre-commit fix removed a [ThreadStatic] anti-pattern), V-S3 Dockerfile/compose (7ae1bda4), V-S9 runbook (8b61d015).
**Owner ruling (V-S7) 2026-06-19:** Veriqan in-memory persistence = LIVE but NOT READY (/health/ready 503 when no durable DB) — resolves the S2-vs-S7 spec conflict, closes U4. In-memory still runs for dev (liveness 200).
**Veriqan-track adversarial review (plan-completion-reviewer) → 1 Major + 1 Minor FIXED (commit AFTER 8b61d015):** (Major) `Veriqan:BatchProcessor:MaxConcurrency` was dead config — BatchProcessor always used per-call BatchOptions (default 4); fixed so a non-default per-call value overrides, else the configured global drives consumer count. (Minor) runbook wrongly said ReprocessService is invoked by the pipeline — corrected (DI-registered only, not wired). Nits (stale tracker) addressed here.
**Wave-0 tail test gates (all re-verified independently):** Veriqan.Orchestration.Tests 63/63, Veriqan.Infrastructure.Extraction.Tests 137/137, Veriqan.Worker.HealthChecks.Tests 9/9, Veriqan.Application.Tests 115/115, ReferenceData.Tests 38/38, Veriqan.Worker builds 0/0. Prisma: Athena.Worker.Tests 25/25, Orion.Worker.Tests 24/25 (1 pre-existing env-gated SignalR fail), Reconciliator.Worker.Tests 8/8, dashboard HealthChecks 17/17 + 22/22, Web.UI 0/0.
**Docker-gated DEFERRALS (no Docker on box):** Prisma docker-compose.dev up + P-S2 Testcontainers migrate test + V-S3 docker build/compose up + Veriqan Testcontainers persistence ITs — all code-complete, run on a Docker/CI box to close. Live OTLP/Seq delivery also Docker-deferred.
**ENTIRE WAVE-0 TAIL (Prisma 4 + Veriqan 7 = 11 stories) DONE + both tracks reviewed. NEXT = owner steer: Wave 1 (cardinal/ops) per SPRINT-PLAN, or pause.**
**Lesson:** config-only stories that touch logging sinks MUST be verified by RUNNING a host (build ≠ boot) — `${}` vs `%%` and boot-crash were invisible to `dotnet build`.

---
### (prior) WAVE-0 CRITICAL-FEW COMPLETE + ADVERSARIALLY REVIEWED 2026-06-19 — 7/7 stories + 1 review fix, all committed, pushed, ground-truth-verified. HEAD=697b4fc8 on `Liv`.
Commits: 523737f1 (P-S3), 7160806a (V-S2), ebc7d644 (P-S4), 3957b3c6 (V-S1), ded95284 (P-S1), 61dd6371 (V-S5), 4ded1760 (V-S4), 697b4fc8 (review-fix #1).
**Adversarial review (plan-completion-reviewer):** confirmed ctor blast-radius safe (no prod path fails to resolve VerificationPipeline), re-verified 54/54 + 8/8. Found 1 Major → FIXED (697b4fc8: BLOCKED-from-binder verdicts now persisted; tests 56/56). Minors: #2/#3 no-action (out-of-scope simulator URLs / Docker-gated), #4 logged as follow-up (task: WAF test for Web.UI /health/ready 503 — wiring confirmed by code review, low priority).
**Deferred (no Docker on box):** P-S1 `docker build`, V-S5 Testcontainers `VerdictPersistenceIntegrationTests` — run on a Docker/CI box to fully close.
**NEXT options for owner:** (a) Wave-0 TAIL (PRISMA-E1 S2 auto-migration / S5 compose / S6 log sinks / S7 dashboard metrics; VERIQAN-E1 S3 Dockerfile+compose / S6–S11), (b) advance to Wave 1, or (c) pause. Awaiting owner steer.

### (prior) EXECUTION IN PROGRESS — 4 of 7 Owner approved Wave-0 critical few. Two parallel sequential tracks. Docker NOT available → Prisma-S1 `docker build` + Veriqan-S5 Testcontainers ACs are CODE-COMPLETE-but-E2E-DEFERRED.
- **DONE+committed+pushed+verified:** PRISMA-E1-S3 (523737f1), VERIQAN-E1-S2 (7160806a), PRISMA-E1-S4 (ebc7d644), VERIQAN-E1-S1 (3957b3c6). HEAD=3957b3c6 on `Liv`.
- **REMAINING:** PRISMA-E1-S1 (Dockerfiles), VERIQAN-E1-S5 (persist), then VERIQAN-E1-S4 (report/notify).
- **ENV INCIDENT (worked around):** the **Bash tool is DOWN** at harness level — `C:\Program Files\Git\cmd\git.exe` is missing (broken Git-for-Windows install), which also breaks Git-Bash's shell. **Workarounds in use:** (1) git via mingw git at `C:\Users\Abel Briones\AppData\Local\GitHubDesktop\app-3.5.4\resources\app\git\cmd\git.exe` driven from PowerShell — verified it sees a CLEAN tree (cygwin git at `C:\cygwin64\bin\git.exe` does NOT — it mangles line endings, do NOT use it for commits). (2) `dotnet` via PowerShell (`C:\Program Files\dotnet\dotnet.exe`, 10.0.301). (3) Grep/Glob/Read/Edit tools for search/edit (Bash-independent).

## Environment ground truth (verified 2026-06-19)
- Docker: **NOT running/available** → Testcontainers + `docker build` ACs cannot execute here.
- dotnet 10.0.301. Build/filesystem slow (E:). Build single projects, not the `.sln`.
- Confirmed paths:
  - Veriqan.Worker: `04 Services/Veriqan.Worker/` · csproj `ExxerCube.Prisma.Veriqan.Worker.csproj` · `Program.cs`
  - VerificationPipeline: `03 Orchestration/Veriqan.Orchestration/Pipeline/VerificationPipeline.cs`
  - Veriqan.Orchestration.Tests: `08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/`
  - Veriqan persistence ITs: `08 Tests/05 System/Veriqan.Infrastructure.Persistence.IntegrationTests/`
  - Reconciliator: `04 Services/Reconciliator/Prisma.Reconciliator.Worker/`
  - Web.UI: `07 UI/UI/ExxerCube.Prisma.Web.UI/`
  - Athena.Worker / Orion.Worker confirmed under `04 Services/Athena|Orion/`

## Conflict map (why ordering is what it is)
- Veriqan `S2` ∩ `S5` → both edit `VeriqanOrchestrationExtensions.cs` ⇒ sequential.
- Veriqan `S1` ∩ `S2` → both edit Worker `Program.cs` ⇒ sequential.
- Veriqan `S5` ∩ `S4` → both edit `VerificationPipeline.cs::ProcessAsync` ⇒ sequential (persist=stage7 first, then report=8/notify=9).
- Prisma `S1` ∩ `S4` → both build Web.UI + Reconciliator ⇒ sequential.
- Prisma tree ⟂ Veriqan tree ⇒ the two tracks run in parallel.

## Execution order
**Track Prisma (sequential):** P-S3 (config) → P-S4 (health probes) → P-S1 (Dockerfiles)
**Track Veriqan (sequential):** V-S2 (appsettings+validation) → V-S1 (POST /verify) → V-S5 (persist stage) → V-S4 (report/notify stages)

## Story status
| Story | Track | Status | Verified by | Notes |
|-------|-------|--------|-------------|-------|
| PRISMA-E1-S3 config externalization | Prisma | ✅ DONE 523737f1 | git grep 0 + Web.UI build 0/0 | |
| PRISMA-E1-S4 real health probes | Prisma | ✅ DONE ebc7d644 | Reconciliator.Worker.Tests 8/8 (verified) | new HealthChecks proj mirrors Athena/Orion; 2 Tests.UI NavigationSmoke fail = env-gated (no SQL/Playwright), not regression |
| PRISMA-E1-S1 Dockerfiles + CI | Prisma | in_progress | dotnet build hosts (PS) | docker build = DEFERRED (no docker) |
| VERIQAN-E1-S2 appsettings+validation | Veriqan | ✅ DONE 7160806a | Orchestration.Tests 43/43 (verified) | |
| VERIQAN-E1-S1 POST /verify | Veriqan | ✅ DONE 3957b3c6 | Orchestration.Tests 45/45 (verified) | |
| VERIQAN-E1-S5 persist stage | Veriqan | in_progress | build (PS) | Testcontainers test = DEFERRED (no docker) |
| VERIQAN-E1-S4 report/notify stages | Veriqan | pending | build + NSubstitute tests | |

## Cadence
- Verify each story from ground truth (single-project `dotnet build` + relevant test) before marking done.
- Commit per cohesive story; push to `Liv`.
- Adversarial review at the track-completion boundary (skeptic pass vs the epics briefs) before declaring Wave-0 critical-few done.
