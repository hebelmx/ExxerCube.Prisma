# Remediation Execution — Tracker (Wave 0 critical-few)

**Anchor (intended-solution doc):** `PRISMA-REMEDIATION-EPICS.md` + `VERIQAN-REMEDIATION-EPICS.md` (per-story subagent briefs + ACs are the spec the diff must not drift from).
**Approved:** 2026-06-19 by owner — execute the unified Wave-0 critical few (7 stories), Prisma first. All four planning forks hold.
**Branch:** `Liv` · **Mode:** EXECUTION (orchestrator + isolated subagents; verify every result from ground truth).

## Handoff (top — update on each context clear)
**WAVE-0 CRITICAL-FEW COMPLETE + ADVERSARIALLY REVIEWED 2026-06-19 — 7/7 stories + 1 review fix, all committed, pushed, ground-truth-verified.** HEAD=697b4fc8 on `Liv`.
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
