# Phase-2 Evidence Bundle — MANIFEST

**Generated:** 2026-06-20  
**Environment:** DESKTOP-FB2ES22 / Windows 11 Pro / .NET 10.0.301 / Docker 29.5.3  
**Branch:** Liv  
**Evidence root:** `docs/qa/harness/runs/phase2-evidence/`

> This manifest lists every artifact collected during the phase-2 evidence run.
> No PASS/FAIL verdicts are rendered here. All outcomes are stated as factual observations.

---

## Build Context

| Project | Build Result | Warnings | Errors | Duration |
|---------|-------------|----------|--------|----------|
| Tests.AllRealWireE2E | SUCCESS | 4 (MSB3026 Defender retries — OCR dll lock, recovered) | 0 | 5m 42s |
| QaHarness.Tests | SUCCESS | 0 | 0 | 2m 02s |
| Web.UI | SUCCESS | 0 | 0 | 1m 36s |

> Note: All three projects initially failed in parallel due to Microsoft Defender Antivirus (PID 6656) locking
> `ExxerCube.Prisma.Infrastructure.Extraction.Ocr.dll` during simultaneous compilation. The E2E build
> recovered via MSBuild retry (4 retries). The harness and Web UI builds were retried sequentially after
> the E2E build completed and the lock was released. This is a transient environment issue, not a product
> code defect.

---

## TASK 1 — Capstone Full-Pipeline E2E

| Item | Value |
|------|-------|
| Test | `MaxFidelityGateFullPipelineE2ETests.RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit` |
| Outcome | FAILED (exit code 2) |
| Duration | 8m 40s |
| Failure point | Step 1 — Playwright login to SIARA simulator |
| Failure assertion | `(await adapter.WaitForSelectorAsync("a[href$='.pdf']", 90000, ct)).IsSuccess should be True but was False` |
| Failure message | "the simulator dashboard should render at least one PDF document link within 90s" |
| Source location | `MaxFidelityGateE2EBase.cs` line 297 |

**Infrastructure observations from the run log:**
- Docker connected successfully: Docker Desktop 29.5.3, WSL2 kernel, 31.18 GB RAM
- SQL Server Testcontainer provisioned and became ready (containers ac81e1f5, eeb68c022167)
- SIARA simulator was started by the test (or already running on localhost:5001)
- Playwright login reached the authenticated dashboard (`#arrivalRateSlider` selector found — login succeeded)
- Dashboard did NOT render `a[href$='.pdf']` link within 90 seconds — 0 PDF document links appeared
- Test retried login 3 times (retry logic in `LoginAndCaptureStorageStateAsync`) before failing
- `cases.json` in `Prisma/Deployments/Siara.Simulator/app/cases.json` contains 500 case IDs (non-empty)
- No pipeline artifacts were produced (failure occurred before ingestion began)

**No pipeline artifacts harvested** (test never reached ingestion/OCR/export stages).

### Artifacts

| File | Size | Description |
|------|------|-------------|
| `e2e/build-output.txt` | 11,882 B | Full `dotnet build` console output for the E2E project — SUCCESS with 4 MSB3026 warnings |
| `e2e/e2e-run.log` | 5,683 B | Full `dotnet test` console output including Testcontainers spin-up log and failure stack trace |
| `e2e/e2e-capstone.trx` | 6,110 B | TRX test result file: 1 test, 0 passed, 1 failed |
| `e2e/artifacts/` | — | Directory created; no pipeline artifacts harvested (test failed before pipeline execution) |

---

## TASK 2 — Harness Integration Tests

| Item | Value |
|------|-------|
| Filter | `/*/*/HarnessIntegrationTests/*` |
| Tests run | 2 |
| Passed | 2 |
| Failed | 0 |
| Duration | 4m 04s |
| Exit code | 0 |

**Tests executed:**
1. `HarnessFastE2E_HealthCheckWithStubHttpClient_WritesMarkdownReport` — 8s — Passed
2. `HarnessIntegration_ProvisionBootHealthCheck_CompletesWithReport` — 3m 17s — Passed (boots real Web UI via WebApplicationFactory, provisions SQL container, runs HealthCheckWorkflow, writes report)

**Report file written by test 2:** The TRX confirms the integration test completed. The harness writes its run report to `docs/qa/harness/runs/phase2-evidence/` (the prior `report-20260620-200430.md` is from a prior session run; the integration test's report from this session was written to the same directory but the harness run ID differs — see `report-20260620-200430.md` for the report format reference).

### Artifacts

| File | Size | Description |
|------|------|-------------|
| `harness-integration/build-output.txt` | 5,759 B | Full `dotnet build` console output — SUCCESS, 0 warnings, 0 errors, 2m 02s |
| `harness-integration/run.log` | 629 B | `dotnet test` summary — 2/2 passed in 4m 04s |
| `harness-integration/harness-integration.trx` | 4,878 B | TRX test result file: 2 tests, 2 passed, 0 failed |

---

## TASK 3 — Web UI Live Evidence

| Item | Value |
|------|-------|
| Build | SUCCESS (0 warnings, 0 errors, 1m 36s) |
| Launch | SUCCESS — `dotnet run --no-build` |
| Listening URL | `http://localhost:5172` |
| Boot behaviour | DB seed failed (expected — `Server=DESKTOP-FB2ES22\SQL2022` hardcoded, not available); app continued |
| `GET /` | HTTP 200 |
| `GET /health` | HTTP 503 — body: "Unhealthy" (SQL readiness check fails without DB) |

**Screenshot results (all 8 screens navigated and captured via .NET Playwright CLI / Chromium 1223):**

| File | Size | Observation |
|------|------|-------------|
| `webui/screen-home.png` | 101,062 B | Home page — large rendered content (MudBlazor app shell) |
| `webui/screen-login.png` | 66,016 B | Login page — full ASP.NET Core Identity login form rendered |
| `webui/screen-dashboard.png` | 78,134 B | Dashboard page — rendered with MudBlazor layout (unauthenticated redirect or dashboard shell) |
| `webui/screen-processing.png` | 4,254 B | Processing page — small file size indicates redirect to login (auth-protected route) |
| `webui/screen-manual-review.png` | 4,254 B | Manual Review page — same small size, redirect to login |
| `webui/screen-export.png` | 4,254 B | Export page — same small size, redirect to login |
| `webui/screen-audit.png` | 4,254 B | Audit page — same small size, redirect to login |
| `webui/screen-sla-dashboard.png` | 4,254 B | SLA Dashboard page — same small size, redirect to login |

> The 4,254-byte screens are consistent with a login redirect page (identical byte count across 5 routes
> suggests the same redirect HTML is served for all auth-protected routes). The 3 larger screenshots
> (home, login, dashboard) rendered distinct content.

### Artifacts

| File | Size | Description |
|------|------|-------------|
| `webui/build-output.txt` | 3,629 B | Full `dotnet build` console output — SUCCESS, 0 warnings, 0 errors |
| `webui/http-probes.txt` | 714 B | HTTP status codes for `GET /` (200) and `GET /health` (503) with explanatory notes |
| `webui/webui-run.log` | 0 B | `dotnet run` stdout log file (output captured via background task monitor instead) |
| `webui/screenshot-run.log` | 1,622 B | Playwright CLI screenshot session log — all 8 screens navigated |
| `webui/screenshot.mjs` | 2,319 B | Playwright Node.js screenshot script (not used — playwright npm not installed; .NET CLI used instead) |
| `webui/screen-home.png` | 101,062 B | Screenshot of home page (HTTP 200, full MudBlazor render) |
| `webui/screen-login.png` | 66,016 B | Screenshot of login page |
| `webui/screen-dashboard.png` | 78,134 B | Screenshot of /dashboard |
| `webui/screen-processing.png` | 4,254 B | Screenshot of /processing (auth redirect) |
| `webui/screen-manual-review.png` | 4,254 B | Screenshot of /review (auth redirect) |
| `webui/screen-export.png` | 4,254 B | Screenshot of /export (auth redirect) |
| `webui/screen-audit.png` | 4,254 B | Screenshot of /audit (auth redirect) |
| `webui/screen-sla-dashboard.png` | 4,254 B | Screenshot of /sla (auth redirect) |

---

## Prior-Session Artifacts (pre-existing)

| File | Size | Description |
|------|------|-------------|
| `report-20260620-200430.md` | 5,044 B | Harness run report from prior session (Docker unavailable; 5/6 workflows aborted) |
| `report-20260620-200430.json` | 6,669 B | Harness run report JSON from prior session |

---

## Environment Notes

- **Defender lock (CS2012):** Microsoft Defender Antivirus (PID 6656) locked `Extraction.Ocr.dll` and `Orion.Worker.dll` during parallel builds. The E2E build recovered via MSBuild retry (MSB3026, 4 retries). The other two builds failed on first attempt and were retried sequentially after the lock released. This is a known issue on this machine with the slow E: drive and real-time AV scanning.
- **SIARA simulator `cases.json`:** The file is populated with 500 case IDs. The simulator started and served the login page successfully. The Playwright session reached the authenticated dashboard. However, the document list (`a[href$='.pdf']`) did not appear within 90 seconds. This may indicate the simulator's Blazor InteractiveServer circuit did not render the document list (possible: the `bulk_generated_documents_all_formats/` path is not wired in `appsettings.json` for this run context, or the corpus scanning loop had not completed within 90s).
- **Web UI DB:** `appsettings.json` hardcodes `Server=DESKTOP-FB2ES22\SQL2022` — not available in this environment. The app boots gracefully with degraded health (`/health` = 503). Per CLAUDE.md this is expected behaviour.
