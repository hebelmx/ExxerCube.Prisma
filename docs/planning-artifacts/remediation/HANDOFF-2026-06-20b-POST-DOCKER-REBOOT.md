# HANDOFF — Post-Docker-Reboot Verification Runbook (2026-06-20b)

> **Context for the next session:** Docker was **repaired** on this box but **a reboot is
> required** before the engine is live. Once rebooted, the large backlog of *code-complete but
> Docker/SQL/Playwright-gated* deferrals across the whole remediation effort becomes runnable.
> This document is the prioritized runbook to **close them**. Until reboot, nothing here can run.

**Branch:** `Liv` · **HEAD at handoff:** `7b61fd49` · **Tree:** clean, pushed to `origin/Liv`.
**Durable state / full history:** `docs/planning-artifacts/remediation/EXECUTION-TRACKER.md` (top handoff block).

---

## 0. Status snapshot (what is DONE)

- **Wave-0** (critical-few 7 + entire tail 11) — DONE + adversarially reviewed.
- **Wave-1 Veriqan-E2** epic — COMPLETE (16/16 + cosmetics) + reviewed.
- **Wave-1 Prisma-E2** non-gated trio (Sentinel classify / ProcessId audit / Tesseract deadlock) — DONE + reviewed.
- **Wave-1 Prisma-E5** cleanup epic (S1–S5) — DONE + adversarially reviewed + 4 review-fixes — **this session**.
- **Prisma non-gated Wave-1 work is essentially DRAINED.** Everything remaining is Docker/SQL/Playwright-gated, business/ops/legal-gated, or Veriqan-E3+ (issue #17).

## Environment ground truth (carry forward)
- **Reboot pending** to activate the repaired Docker. Verify with `docker info` / `docker ps` first thing.
- dotnet 10.0.301 at `C:\Program Files\dotnet\dotnet.exe`. **Build single projects** — the `.sln` is slow on E:.
- **MTP test runner:** do NOT pass `--nologo` to `dotnet test` (→ "Zero tests ran"). global.json opts into MTP.
- git via the Bash tool works this session (also GitHubDesktop mingw / PowerShell). **Never** cygwin git (mangles line endings).
- Ignore everything under `.claude/worktrees/**` (stale agent-worktree copies; gitignored).
- A stale worktree dir `.claude/worktrees/agent-aec4ef1721bf1635f/` exists on disk — harmless (gitignored), can be removed if desired.

---

## 1. PRIORITY A — Re-confirm the Testcontainers SQL suites are green (fast win, proves Docker)

These were reported green on a SQL-capable box but not on this one. Run them first as a Docker smoke test:

| Suite | csproj | Expected |
|---|---|---|
| System storage IT | `08 Tests/05 System/.../Tests.System.Storage` | 39/39 (shared assembly-fixture SQL container) |
| Infrastructure DB IT | `08 Tests/02 Infrastructure/Tests.Infrastructure.Database/...csproj` | 110/110 (incl. SLAEnforcer SQL tests) |
| Veriqan persistence IT | `08 Tests/05 System/Veriqan.Infrastructure.Persistence.IntegrationTests` | green (was code-complete, never run on box) |

> Pattern reminder: each write-heavy test class provisions its own DB on the shared container via
> `SqlServerContainerFixture.CreateIsolatedDatabaseAsync(name)`. Do NOT reintroduce a shared mutable DB.

If any fail, that is a **real finding** (they were only ever asserted from a CI claim) — triage before moving on.

---

## 2. PRIORITY B — Run the max-fidelity E2E gates (the headline 3-process proof)

These are the deferred end-to-end assertions from the Prisma-E2 trio + earlier waves. They need
**Testcontainers SQL + native Tesseract + (for the UI legs) Playwright**.

1. **`MaxFidelityGateFullPipelineE2ETests`** (`08 Tests/06 E2E/Tests.AllRealWireE2E/`) — the full
   Downloader→Extractor→Reconciliator pipeline on the SIARA simulator. Confirm it runs green end-to-end.
2. **`MaxFidelityGatePartialCaseE2ETests`** — the OCR scenario that S4 (Tesseract deadlock fix) **re-enabled**
   (`runAthenaPipeline:true`). This is the regression proof for the singleton-engine deadlock fix. Must pass
   without hanging (the whole point of the fix).
3. **PRISMA-E2-S5 ProcessId audit assertion** — after the gate runs, the End-to-end evidence AC is:
   `SELECT COUNT(*) FROM AuditRecords WHERE ProcessId IS NULL` **returns 0**. The assertion was added to the
   gate; confirm it passes against real SQL.
4. Real-OCR suite `Extraction.Teseract` — **159/159** expected (154 OCR + 4 deadlock-regression + 1 DI same-instance).
   Already verified on-box this session, but re-run alongside to confirm no native-Tesseract surprise under load.

---

## 3. PRIORITY C — FU1: close the broadcaster→DOM bridge AND verify it (owner deferred to a verifiable box)

**Why deferred:** the re-enabled `SignalREventBroadcaster` (Web.UI) pushes domain events via Ember's
generic `SendToAllAsync` wire method name, but `Dashboard.razor` (`On<DashboardMetrics>("MetricsUpdated")`)
and `SlaDashboard.razor` (`On<string>("SLAStatusUpdated")` / `On("SLAEscalated")`) listen for **typed**
names that only `ProcessingHub`'s typed methods emit. So server push + hub connections work, but **no DOM
element updates from a DomainEvent**. Both adversarial reviewers confirmed this independently.

**Now do-able because Playwright + SQL will be available.** Pick an option, implement, then PROVE it with
the already-written (currently `[Skip]`-ped) live-update Playwright test
`Tests.UI/SignalR/SignalRBroadcasterPlaywrightTests.cs::Dashboard_ReceivesLiveMetricsUpdate_WhenBroadcasterEmitsDomainEvent`:

- **Option (a) — Razor handler (keeps broadcaster transport-agnostic):** in `Dashboard.razor`/`SlaDashboard.razor`
  add a `hubConnection.On<DomainEvent>("<ember-wire-name>", _ => InvokeAsync(<existing reload>))` handler.
  REQUIRES first confirming the exact client method-name `ExxerHub<T>.SendToAllAsync` emits (inspect the
  `IndFusion.Ember` package / its tests — Orion tests reference `"ReceiveMessage"`).
- **Option (b) — broadcaster→typed hub calls:** in `SignalREventBroadcaster.BroadcastEventAsync`, after
  resolving the hub, branch on `domainEvent` type and call the matching `ProcessingHub` typed method
  (`UpdateMetrics` / `UpdateSLAStatus` / `NotifySLAEscalation`). Razor untouched, but couples the broadcaster
  to the concrete hub + needs a per-event-type → payload mapping (e.g. which event carries `DashboardMetrics`).

**DoD:** the live-update test is un-skipped and GREEN on the Playwright box; `Dashboard`/`SlaDashboard`
visibly update without a page reload after one document is processed. Tracked as **EXECUTION-TRACKER task FU1 / task #8**.

---

## 4. PRIORITY D — `docker build` + `docker-compose` (deployability ACs, code-complete)

All of these have the artifacts written; they only ever lacked a Docker daemon:

- **Prisma Dockerfiles** (PRISMA-E1-S1): 4 hosts — Orion.Worker, Athena.Worker, Reconciliator.Worker, Web.UI.
  Run `docker build` per image; confirm each builds + the array-form ENTRYPOINT.
- **`docker-compose.dev.yml`** (PRISMA-E1-S5): 6 services (3 workers + Web.UI + SIARA Simulator + SQL + Seq).
  `docker compose -f docker-compose.dev.yml up`; confirm inter-service network + shared storage volume; confirm
  worker→Ember-hub TCP connectivity (not `localhost`).
- **Veriqan Dockerfile + compose** (VERIQAN-E1-S3): `docker build` + compose up.
- **Live OTLP/Seq delivery:** with compose up, confirm Serilog Seq sink + OTLP traces actually arrive (the
  `%SEQ_URL%`/OTLP env-expansion fix from Wave-0 tail was build/boot-verified but never confirmed delivering).

---

## 5. PRIORITY E — PRISMA-E2-S8: CI Docker image build/publish/push (now runnable)

`quality-gates.yml` (`Prisma/Code/Src/CSharp/.github/workflows/`). The brief (PRISMA-REMEDIATION-EPICS.md
"### PRISMA-E2-S8") adds a `publish` job: `dotnet publish` → build 4 Docker images → scan → push to registry
on release-branch pushes, tagged with git SHA. Now that Docker works, this can be authored AND smoke-tested
locally (build+tag the 4 images; the actual registry push stays CI-gated on credentials). Closes D24.

This is the **last remaining non-business-gated Prisma-E2 story.**

---

## 6. PRIORITY F — Remaining Playwright/UI suites (full green numbers)

With Playwright browsers + SQL present, run and record true numbers:
- **`Tests.UI`** — full suite. This session saw 23 passed / 1 skipped (FU1) / **2 NavigationSmoke env-gated
  fails** (no SQL → `/health` 503). With SQL+Playwright these should be **green** (CLAUDE.md's historical 21/21
  predates the +new SignalR tests). The new `SignalRHubEndpointTests::ProcessingHub_NegotiateEndpoint_Returns200_WhenHubIsMapped`
  early-returns without SQL — confirm it actually asserts 200 now.
- **`BrowserAutomation.E2E`** (was 18/18), **`Tests.EndToEnd`** (was 29/29), **`Tests.UI`** Playwright legs.

---

## 7. Still GATED (NOT unblocked by Docker — do not attempt)

- **PRISMA-E3** security epic — business/ops/security-review-gated (asymmetric JWT keys, PII AES-GCM, Vault,
  alerting, mTLS, CNBV CUB/ISO trajectory). All specced with BLOCKED markers.
- **PRISMA-GATED** live SIARA ingestion — **legal-gated** (ADR-010 P1 counsel sign-off) + **corpus-gated**
  (quality-model retrain, SIRO `.xsd` from Banamex).
- **PRISMA-E4-S1** PersonIdentityResolver DB persistence — buildable-now engineering (NOT gated); a candidate
  for a future session if the owner wants more Prisma engineering. Its Testcontainers IT would now be runnable.
- **PRISMA-E5-S6** semantic/NLP classification — business-gated.
- **Veriqan-E3+** (persistence/audit/security/E13 productization) — E13 ROADMAP-gated on **issue #17** (buyer
  discovery, OPEN); corpus calibration thresholds UNCALIBRATED pending a real good+broken corpus (×2 corpora).

## 8. Three non-engineering critical-path unlocks (still open)
issue **#17** (Veriqan buyer/E13), **ADR-010 P1** legal counsel (live SIARA), **real corpus ×2** (Prisma quality
model + Veriqan calibration).

---

## Suggested order for the post-reboot session
1. `docker info` → confirm daemon live.
2. **§1** Testcontainers SQL smoke (fast; proves Docker + catches any real IT regression).
3. **§2** max-fidelity E2E gates (the headline proof; closes the Prisma-E2 trio's deferred ACs).
4. **§3** FU1 (owner explicitly wants this on a verifiable box) — implement + prove with the skipped test.
5. **§4–§6** deployability (docker build/compose, S8 CI, full Playwright numbers) as time allows.
6. Re-run an adversarial review at the boundary; update the tracker + memory; push.
