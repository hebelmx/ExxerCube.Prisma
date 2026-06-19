# VERIQAN VEC — Remediation Epics and Stories

**Subsystem:** Veriqan VEC (compliance-validation engine for CONDUSEF credit-card statements)
**Source matrix:** `RC4-VERIQAN-READINESS-MATRIX.md` (2026-06-18, branch `Liv`)
**Gap counts (at matrix date):**
- Blocks-production: 28 rows (#1–#28), of which **#16 is RESOLVED** (commit d0d9ef65) — **27 open**
- Degrades: 33 rows (#29–#61)
- Cosmetic: 3 rows (#62–#64)
- Unknown-Unknowns surfaced: 18 (U1–U18); U2 has no numbered row — planned as a dedicated story here

**Ordering note:** Veriqan is planned SECOND, after Prisma MVP (per locked orchestrator decision, REMEDIATION-ORCHESTRATION-KICKOFF.md §1). This file is the Veriqan half; its wave numbering mirrors RC6 (W0–W6) so the cross-cutting `SPRINT-PLAN.md` can reference these epics without renumbering.

**Executor model:** every story is written as a self-contained **subagent brief** — the orchestrator dispatches one agent per story; the agent reads the named files, makes the named change, runs the named test project, and reports DoD met or not. Stories do NOT cross-project boundaries (each targets a single focused change).

**Abstain-safety discipline (applies to all stories):** `InsufficientData` never escalates to RED. Any story touching a rule or the verdict aggregator must preserve this invariant. Do not trade false-passes for false-fails.

---

## Non-Engineering Critical-Path Unlocks (reference — not stories)

Before the corpus-gated and business-gated items below can unblock, the following non-engineering decisions must be driven in parallel:

| ID | Gate | What it unblocks |
|----|------|-----------------|
| CPA-1 | **E13 buyer gate** — drive GitHub issue #17 to a decision | VERIQAN-E5 (productization) and items tagged `E13-gated` |
| CPA-2 | **Real CONDUSEF corpus acquisition** — identify bank-relationship owner; agree NDA; acquire labelled KnownGood + KnownBroken specimens; load into calibration harness | VERIQAN-E5 (calibration) and items tagged `corpus-gated` |

These are not engineering tasks. They must be driven by the business/product owner concurrently with W0–W3 engineering.

---

## Epic Summary

| ID | Title | Wave(s) | Gate | Gap IDs Covered |
|----|-------|---------|------|-----------------|
| VERIQAN-E1 | Minimum Deployable Composition | W0 | buildable-now | #8, #9, #10, #11, #12, #13, #14, #23, #24, #25, #28 + U2 |
| VERIQAN-E2 | Cardinal-Rule and Correctness Fixes | W1 | buildable-now (§20/§16 open corpus-verify gate) | #1, #2, #3, #4, #5, #6, #7, #36, #37, #38, #41, #42, #44, #45, #46, #48 + U2 |
| VERIQAN-E3 | Persistence, Durability, and Audit | W2 | buildable-now | #15, #19, #47, #50, #56 |
| VERIQAN-E4 | Security Hardening | W3 | mixed (buildable-now + business-gated) | #17, #18, #20, #21, #22, #26, #51, #52, #53, #54 |
| VERIQAN-E5 | Corpus Calibration and Productization | W4 / W6 | corpus-gated + E13-gated | #2(verify), #3(verify), #27, #29, #30, #31, #32, #33, #34, #35, #40, #43, #49, #54, #57, #64 |
| VERIQAN-COSMETIC | Doc and Code Cosmetics | W1 (parallel) | buildable-now | #62, #63 |

**Degrades not in the above epics that ARE assigned:** #36, #37, #38, #39, #41, #42, #44, #45, #46, #48 → all in E2; #50, #56, #47 → E3; #58, #59, #60, #61 → see E1/E2 stories; #55 → E1-S7.

Full gap-to-story coverage table at end of this document.

---

## VERIQAN-E1 — Minimum Deployable Composition

**Goal:** enable a single real CONDUSEF statement to enter the containerised Worker, traverse ingest → extract → bind → validate → verdict → persist → report → notify, with all results durable in SQL and the Worker observable.

**Wave:** W0 (RC6 §Wave 0 items 0.1–0.8)

**Gate:** `buildable-now` — zero dependency on corpus or business decision

**Gaps covered:** #8, #9, #10, #11, #12, #13, #14, #23, #24, #25, #28 and unknown-unknowns U2 (minimum-extraction-coverage floor), U3 (orphaned report/notify), U4 (silent-config trap), U6 (no reference-bundle provenance)

---

### VERIQAN-E1-S1 — Add POST /verify HTTP endpoint to Worker

**Traceability:** Closes #8, #12. RC6 W0.2.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add a minimal `POST /verify` HTTP endpoint to `Veriqan.Worker/Program.cs` that accepts a `StatementSubmission` JSON body and calls `IVerificationPipeline.ProcessAsync`. Return 202 Accepted with the job GUID. Wire `IBatchProcessor` for batch submissions via `POST /batch`.

Context:
- `Veriqan.Worker/Program.cs` is currently 21 lines exposing only `/health` and `/health/live`. `IBatchProcessor` is DI-registered (`VeriqanOrchestrationExtensions.cs:92`) but never invoked.
- `IVerificationPipeline.ProcessAsync` is the single-statement entry point. `IBatchProcessor.ProcessBatchAsync` is the batch entry point.
- The Worker's `.csproj` is at `Prisma/Code/Src/CSharp/04 Services/Veriqan/Veriqan.Worker/` (locate exact path with `git ls-files`).
- No auth is required in this story (auth is VERIQAN-E4-S1). The endpoint must be reachable unauthenticated for local dev; document in a `TODO(E4)` comment.
- Follow Result<T> pattern: if `ProcessAsync` returns failure, map to 422 Unprocessable with error body.

Constraints:
- Result<T> + CancellationToken (default) on every async method.
- Build only the Worker project, not the full solution.
- ITDD: write one integration test in `Veriqan.Orchestration.Tests` that POSTs to a `WebApplicationFactory`-hosted Worker and asserts HTTP 202.
- No `--nologo` or extra dotnet flags.

Definition of Done:
- `POST /verify` returns 202 with a job GUID for a valid `StatementSubmission`.
- `POST /batch` returns 202 for a valid `BatchRequest`.
- One new integration test in `Veriqan.Orchestration.Tests` (or equivalent test project) is green.
- `dotnet build <Worker.csproj>` exits 0/0 (errors/warnings).

End-to-end evidence AC: a `StatementSubmission` POSTed to the running Worker kicks off `IVerificationPipeline.ProcessAsync` (verified by the integration test observing the pipeline invoked via a test double or by asserting a `JobVerdict` row exists after the call).

**Effort:** S

---

### VERIQAN-E1-S2 — Author appsettings.json and startup validation

**Traceability:** Closes #10, #28 (config half). RC6 W0.3. Addresses U4 (silent-config trap).
**Gate:** `buildable-now`

**Subagent brief**

Task: Author `Veriqan.Worker/appsettings.json` (and a parallel `appsettings.Production.json.template`) documenting all required keys with correct defaults. Add startup validation that warns loudly when critical keys are absent.

Context:
- `VeriqanOrchestrationExtensions.cs:73–85` shows `ConnectionStrings:VeriqanDb` absent → silent in-memory fallback.
- `CsvReferenceDataOptions.cs:14` defaults to empty string → every real bind → BLOCKED.
- `SmtpOptions.cs:17,19,38` defaults to `localhost:25` / `EnableSsl=false`.
- Currently no `appsettings.json` exists in the Worker directory (verified: `git ls-files | grep -i appsettings` shows nothing for Veriqan.Worker).
- Required keys at minimum: `ConnectionStrings:VeriqanDb`, `Veriqan:CsvReferenceData:RootDirectory`, `Veriqan:Smtp:Host`, `Veriqan:Smtp:Port`, `Veriqan:Smtp:EnableSsl`, `Veriqan:LegalBaseline:EncryptionKey`.

Constraints:
- Production template must NOT contain real secrets — use `<REPLACE_ME>` placeholders.
- Startup validation: use `IOptions<T>` with `[Required]` or an `IHostedService` early-exit that logs a structured Serilog `Warning` ("VeriqanDb connection string absent — persistence will be IN-MEMORY") and returns `Result.WithFailure` on the first pipeline call (do not crash the process, but emit a WARN on startup and every pipeline invocation while unconfigured).
- Add one unit test verifying the startup validator fires when `VeriqanDb` is absent.

Definition of Done:
- `Veriqan.Worker/appsettings.json` committed with all documented keys and safe defaults.
- `Veriqan.Worker/appsettings.Production.json.template` committed with `<REPLACE_ME>` placeholders and a comment block documenting each key's purpose.
- Startup validator fires a Serilog `Warning` when `VeriqanDb` is absent.
- Unit test for startup validation is green.
- `dotnet build <Worker.csproj>` 0/0.

End-to-end evidence AC: a Worker started without `VeriqanDb` logs a structured warning and returns a non-2xx from `POST /verify` instead of silently accepting and discarding data.

**Effort:** S

---

### VERIQAN-E1-S3 — Author Dockerfile and docker-compose for Veriqan.Worker

**Traceability:** Closes #11. RC6 W0.1.
**Gate:** `buildable-now`

**Subagent brief**

Task: Author a multi-stage `Dockerfile` for `Veriqan.Worker` and a `docker-compose.yml` for local development.

Context:
- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime); build image: `mcr.microsoft.com/dotnet/sdk:10.0`.
- Build artifacts go to `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\` (Directory.Build.props). The Dockerfile must use `dotnet publish` with `--output /app/publish`.
- SQL Server connection string and CSV root directory must be ENV vars (not baked into the image).
- `docker-compose.yml` must include: `veriqan-worker` service, `sqlserver` service (mcr.microsoft.com/mssql/server:2022-latest), a shared `veriqan-data` volume for the CSV reference bundle, and correct port mapping (8080 for HTTP).
- Pattern: mirror the Prisma Dockerfile style if one exists, otherwise follow standard .NET 10 multi-stage convention.

Constraints:
- The Dockerfile must copy only the Worker project output, not the full solution tree.
- ENV var names must match the keys documented in VERIQAN-E1-S2's `appsettings.Production.json.template`.
- A `.dockerignore` excluding `bin/`, `obj/`, and `TestAssets/` is required.

Definition of Done:
- `Dockerfile` and `docker-compose.yml` committed under `Prisma/Code/Src/CSharp/04 Services/Veriqan/Veriqan.Worker/` (or the repo's established docker artifact location).
- `docker build` succeeds on the target machine (record the command and expected output in the commit message).
- `docker-compose up` starts without errors (verified locally).
- `dotnet build <Worker.csproj>` 0/0.

End-to-end evidence AC: `docker-compose up` produces a running `veriqan-worker` container that responds to `GET /health` with `{"status":"Healthy"}`.

**Effort:** S

---

### VERIQAN-E1-S4 — Wire report (marked PDF) and notify (alert email) stages into VerificationPipeline

**Traceability:** Closes #9. RC6 W0.4. Addresses U3 (orphaned report/notify).
**Gate:** `buildable-now`

**Subagent brief**

Task: Add pipeline stages 8 (Report) and 9 (Notify) to `VerificationPipeline.ProcessAsync` after the verdict stage. Stage 8 calls `IMarkedPdfGenerator.GenerateAsync` on RED or BLOCKED outcomes. Stage 9 calls `IVecAlertService.SendRedAlertAsync` on RED outcomes.

Context:
- `VerificationPipeline.cs:341` currently returns `VerificationOutcome` without calling either adapter.
- `IMarkedPdfGenerator` and `IVecAlertService` are already DI-wired and unit-tested — they are orphaned adapters.
- File: `Veriqan.Infrastructure/` — locate exact paths with `git ls-files | grep -i VerificationPipeline`.
- Stage 8 must run even when Stage 9 will be skipped (GREEN verdict). Stage 9 must only fire on RED.
- Both stages must be wrapped in try/catch that captures failures as non-fatal `Finding`s (type: `ReportGenerationFailure` / `NotificationFailure`) appended to the outcome — they must not cause the pipeline to return a failure `Result`; they are best-effort post-processing. The verdict itself is already determined.
- The duplicate-alert guard (VERIQAN-E2-S12 / gap #46) is a separate story; add a `// TODO(E2-S12)` comment at the send call site.

Constraints:
- Result<T> + CancellationToken.
- ITDD: add or extend a test in `Veriqan.Orchestration.Tests` that verifies `IMarkedPdfGenerator.GenerateAsync` is called exactly once for a RED outcome, and `IVecAlertService.SendRedAlertAsync` is called exactly once for a RED outcome.
- Use NSubstitute for mocks in tests (not Moq).

Definition of Done:
- `VerificationPipeline.cs` stage 8 and stage 9 added.
- Tests verifying stage 8 and stage 9 invocation are green in `Veriqan.Orchestration.Tests`.
- `dotnet build <Orchestration project>` 0/0.

End-to-end evidence AC: when a real statement produces a RED verdict via the `POST /verify` endpoint, a marked PDF is generated and an email alert is dispatched (confirmed by integration test using test doubles for the SMTP and PDF output sinks).

**Effort:** S

---

### VERIQAN-E1-S5 — Add Persist stage to VerificationPipeline (JobVerdict + Findings)

**Traceability:** Closes #14. RC6 W0.5.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add a Persist stage (stage 7, between verdict and report) to `VerificationPipeline.ProcessAsync` that writes `JobVerdict` and all `Finding` rows to the database. Wire `AddVeriqanDisposition` into `AddVeriqan`.

Context:
- `VerificationPipeline.cs:341` returns the outcome without writing any rows.
- `EfDispositionRepository.cs` exists but is orphaned — never called by the pipeline.
- Tables/migrations for `JobVerdict` and `Finding` exist (`AddDispositionAudit` migration `20260618001142.cs:14–47`).
- `DispositionService` / `AddVeriqanDisposition` registration exists; confirm the exact extension method name by reading `VeriqanOrchestrationExtensions.cs`.
- Persist stage must be the first post-verdict action so findings are durable before report/notify.
- On persist failure, the pipeline must return `Result.WithFailure` (persist is non-optional — we must not silently discard a verdict).

Constraints:
- Result<T> + CancellationToken on the repository calls.
- ITDD: add a Testcontainers-based integration test in the persistence test project (the one already using `SqlServerContainerFixture`) that verifies a `JobVerdict` row and at least one `Finding` row are written after a successful pipeline run.
- One `Finding` assertion per rule outcome type (at least GREEN + RED).

Definition of Done:
- `VerificationPipeline.cs` persist stage added (before report stage).
- `AddVeriqanDisposition` wired in `AddVeriqan`.
- Testcontainers integration test is green.
- `dotnet build` of the persistence project 0/0.

End-to-end evidence AC: a `POST /verify` call with a valid PDF results in at least one `JobVerdict` row and `N` `Finding` rows in the SQL database (proven by the Testcontainers integration test querying the database).

**Effort:** M

---

### VERIQAN-E1-S6 — Move demo reference bundle to deployable directory; document bundle-authoring process

**Traceability:** Closes #13, #28 (data half). RC6 W0.6. Addresses U6 (no reference-bundle provenance).
**Gate:** `buildable-now` (engineering half) + `business-gated` (bank must author real rates/products CSVs — see note)

**Subagent brief**

Task (engineering half, buildable now): Move the `Demo_Bank_(Iqubica)` CSV bundle from the test project (`Veriqan.Infrastructure.ReferenceData.Tests/TestAssets/csv/Demo_Bank_(Iqubica)/`) to a deployable data directory (e.g. `Prisma/Data/Veriqan/reference-bundles/Demo_Bank_(Iqubica)/`). Update `CsvReferenceDataAdapter` to read from the configured `CsvReferenceData:RootDirectory` correctly. Author a `BUNDLE-AUTHORING-GUIDE.md` in the data directory documenting the CSV schema and the authoring process.

Context:
- `CsvReferenceDataAdapter.cs:110–113` checks a directory path from options. Currently the only bundle is inside a test project, unreachable from a deployed Worker.
- The move must not break existing tests that reference the original path — update the test fixture to point to the new location.
- `BundleBinder.cs:73–76` and `VerdictAggregator.cs:64–66` consume the bundle — no changes needed there if the adapter is correctly pointed.

Business gate note: the demo bundle (Iqubica) is a test asset only. A real bank's `interest-rates.csv`, `products.csv`, `tolerance-config.csv`, and `mandatory-legends.csv` must be authored by the bank's data team. Document this as a `BUNDLE-AUTHORING-GUIDE.md` in the data directory: who authors each CSV, what schema each follows, what sign-off is required. Mark the bank-authoring step as BLOCKED on the bank relationship (document in the guide, not a code blocker).

Constraints:
- The `Demo_Bank_(Iqubica)` test bundle in its new location must be embedded in the deploy artifact (set `CopyToOutputDirectory` as needed for the compose volume mount).
- All existing `Veriqan.Infrastructure.ReferenceData.Tests` pass after the move.

Definition of Done:
- Demo bundle at new deployable path.
- `BUNDLE-AUTHORING-GUIDE.md` committed.
- All `Veriqan.Infrastructure.ReferenceData.Tests` green after path update.
- `dotnet build` of the ReferenceData project 0/0.

End-to-end evidence AC: the Worker configured with `CsvReferenceData:RootDirectory` pointing to the new data directory can bind a real `StatementContextKey` without returning BLOCKED due to missing bundle.

**Effort:** M

---

### VERIQAN-E1-S7 — Real health checks + /health/ready endpoint

**Traceability:** Closes #23. RC6 W0.7. Addresses U4 (silent-config trap).
**Gate:** `buildable-now`

**Subagent brief**

Task: Replace the two hardcoded health stubs in `Program.cs` with real `IHealthCheck` implementations. Add `/health/ready` endpoint returning 503 until all checks pass.

Context:
- `Program.cs:10–11` returns constant `{"status":"Healthy"}` for `/health` and `/health/live`.
- `VeriqanLegalBaselineStartupService.cs` runs a startup probe but is not wired into the health endpoint.
- Required checks: (1) SQL Server connectivity (attempt `SELECT 1` via `DbContext`); (2) CSV reference-data root directory exists and is non-empty; (3) tolerance-cache warm-up complete (if a cache service exists — check `VeriqanLegalBaselineStartupService`).
- Use ASP.NET Core `IHealthChecksBuilder` with named checks and tags (`ready`, `live`).

Constraints:
- `/health/live` → liveness only (quick, no DB). `/health/ready` → all three checks.
- Do not re-use the hardcoded `MapGet` pattern — use `AddHealthChecks().MapHealthChecks()`.
- One unit test per check (use a test-double `DbContext` and a temp directory).

Definition of Done:
- `/health/ready` returns 200 when DB is up, CSV root exists, and cache is warm; 503 otherwise.
- `/health/live` returns 200 independently of external deps.
- Three unit tests (one per check) are green.
- `dotnet build <Worker.csproj>` 0/0.

End-to-end evidence AC: a Worker started without a SQL connection string returns `/health/ready` → 503 (proven by unit test with null connection string in test DI).

**Effort:** S

---

### VERIQAN-E1-S8 — Wire OTel OTLP exporter and Serilog sink in Worker

**Traceability:** Closes #24. RC6 W0.8.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add `AddOpenTelemetry().WithMetrics(...).WithTracing(...)` with an OTLP exporter to `Program.cs`. Add Serilog with a console sink (and a structured Seq/Elastic sink configurable via `appsettings.json`).

Context:
- `VeriqanMetrics.cs:23–90` defines the OTel Meter and 3 instruments (statement counter, duration histogram, exception counter) but no exporter is registered in `Program.cs`.
- The Worker currently uses only the default `WebApplication.CreateBuilder` console logging.
- Add `ActivitySource` spans for: pipeline entry, each stage (extract, bind, validate, verdict, persist, report, notify). The `VeriqanMetrics` instruments must be consumed inside the pipeline stages that already increment them.
- OTLP endpoint should be configurable via `Veriqan:OtlpEndpoint` (default `http://localhost:4317`).

Constraints:
- NuGet packages for OTel and Serilog must be added via `Directory.Packages.props` (centralised package versions).
- Serilog `WriteTo.Console` is always on; `WriteTo.Seq` only when `Veriqan:Seq:ServerUrl` is non-empty.
- No breaking changes to existing test projects.

Definition of Done:
- `Program.cs` has `AddOpenTelemetry` + `AddSerilog` wired.
- `VeriqanMetrics` instruments are exported to the configured OTLP sink.
- Pipeline stages emit `ActivitySource` spans with stage name as the operation name.
- `dotnet build <Worker.csproj>` 0/0.
- Smoke test: Worker starts and logs one structured Serilog entry to console on startup (verified in the E1-S1 WebApplicationFactory test).

End-to-end evidence AC: a Worker processing a statement emits at least one `StatementProcessed` OTel counter increment (observable via the OTLP sink or a test asserting the metric value).

**Effort:** S

---

### VERIQAN-E1-S9 — Author operational runbook for Veriqan.Worker

**Traceability:** Closes #25.
**Gate:** `buildable-now`

**Subagent brief**

Task: Author `docs/operations/VERIQAN-RUNBOOK.md` covering the six operational scenarios listed in gap #25.

Context:
- Gap #25: no operational documentation for batch initiation, exception-queue triage, reference-data update, mid-batch resume, or on-call escalation.
- The runbook must reference: the `POST /batch` endpoint (E1-S1), the `BatchExceptionLog` table (E3-S3), the `BUNDLE-AUTHORING-GUIDE.md` (E1-S6), the `/health/ready` probe (E1-S7).
- Sections required: (1) Monthly batch start; (2) Exception-queue triage and retry (`BatchExceptionLog` queries); (3) Reference-bundle update procedure; (4) Mid-batch resume; (5) On-call escalation path; (6) SMTP configuration verification.

Constraints:
- This is a documentation task — no code changes.
- The runbook must be accurate to the code as it exists after E1-S1 through E1-S8 are done; include placeholder `[TODO after E3-S3]` references for features not yet built.

Definition of Done:
- `docs/operations/VERIQAN-RUNBOOK.md` committed.
- All six sections present with concrete commands/queries/steps.
- Document reviewed for internal consistency with the E1 story set.

End-to-end evidence AC: N/A (documentation). Gate: operational team can execute a batch start and exception triage using only the runbook without reading source code.

**Effort:** S

---

### VERIQAN-E1-S10 — Add minimum-extraction-coverage floor (Unknown-Unknown U2)

**Traceability:** Closes U2 (no numbered gap row — surfaced as U2 in RC4 §4). Companion to #1 (scanned-PDF abstain guard, addressed in E2-S1).
**Gate:** `buildable-now`

**Subagent brief**

Task: Add a "minimum-extraction-coverage floor" invariant: if fewer than N fields are extracted across all sections (i.e. total `ExtractedField` count with status `Extracted` or `PartiallyExtracted` is below threshold), emit a whole-verdict `BlockedOutcome` with reason `InsufficientExtractionCoverage`. This prevents a near-zero extraction (e.g. layout drift, encrypted empty PDF) from producing a spurious GREEN via universal-abstain.

Context:
- U2 is the dual cardinal rule: if zero/near-zero fields extract, every rule abstains (InsufficientData), `VerdictAggregator` counts only abstains → GREEN. A genuinely defective statement with layout drift gets a false PASS.
- The guard must fire BEFORE section validation, AFTER extraction. A reasonable initial threshold is N=10 extracted fields (document this as configurable via `TenantProfile.MinExtractionCoverageCount` with default 10).
- `PdfPigStatementFieldExtractor` returns a `StatementFields` object — count total populated fields there.
- The guard must use `InsufficientData` / `BlockedOutcome` (never RED), honoring the abstain-safety discipline.

Constraints:
- The threshold must be configurable; do not hardcode 10 as a magic number.
- Add the new `TenantProfile.MinExtractionCoverageCount` property with default 10.
- ITDD: one unit test for the guard firing (< threshold → BLOCKED), one for not firing (>= threshold → pipeline continues).
- Abstain-safety discipline: the guard must produce `BlockedOutcome` (not RED).

Definition of Done:
- Guard implemented and wired into pipeline (after extraction, before bind/validate).
- `TenantProfile.MinExtractionCoverageCount` added with default 10.
- Two unit tests green.
- `dotnet build` of the Orchestration project 0/0.

End-to-end evidence AC: a zero-text PDF submitted to the pipeline returns `VerdictSignal.Blocked` (not `Green` and not `Red`), proven by unit test.

**Effort:** S

---

### VERIQAN-E1-S11 — Streaming batch processor + poison-PDF safeguards

**Traceability:** Closes #58 (batch heap pressure), #59 (password-protected PDF), #61 (poison-PDF DoS). These are Degrades that are buildable-now and tightly coupled to the Worker's ingestion path.
**Gate:** `buildable-now`

**Subagent brief**

Task:
1. Refactor `BatchProcessor` from a full-upfront-task-list pattern (`List<Task>(total)`) to a `Channel<StatementSubmission>`-based streaming consumer with a bounded pool, preventing heap materialisation of N closures for large batches.
2. Add a PDF size limit check (configurable, default 50 MB) before `PdfDocument.Open` in `PdfPigStatementFieldExtractor`.
3. Add a `CancellationToken`-timeout wrapper around `ExtractFullAsync` (configurable, default 30 s) to prevent a pathological PDF from hanging the worker thread.
4. Add a `PasswordProvider` port: if `PdfDocument.Open` throws a password exception, attempt open with a configured password (per-institution); if unavailable, return `Result.WithFailure("PasswordProtected — add institution password to config")`.

Context:
- `BatchProcessor.cs:113,238` materialises the full task list.
- `PdfPigStatementFieldExtractor.cs:189` calls `PdfDocument.Open` with no size check, no timeout, no password param.
- `BatchProcessor.cs:200,214–229` exception queue is in-memory (durability addressed in VERIQAN-E3-S3).
- The `Channel<T>` consumer count should be configurable via `Veriqan:BatchProcessor:MaxConcurrency` (default: `Environment.ProcessorCount`).

Constraints:
- Result<T> + CancellationToken on all async paths.
- Timeout must use the `CancellationToken` plumbing already in the pipeline, not a blocking `Task.Wait`.
- One unit test per new guard (size limit, timeout expiry, password-protected).

Definition of Done:
- `BatchProcessor` uses `Channel<StatementSubmission>` pattern.
- Size limit, timeout, and password guards in `PdfPigStatementFieldExtractor`.
- Three new unit tests green.
- `dotnet build` of both affected projects 0/0.

End-to-end evidence AC: a batch submission with a statement exceeding the size limit returns a `BatchExceptionLog` entry (mocked persistence) with reason `FileSizeLimitExceeded` rather than a process hang.

**Effort:** M

---

## VERIQAN-E2 — Cardinal-Rule and Correctness Fixes

**Goal:** eliminate all latent false-RED and false-PASS risks on real input classes, so the engine can be trusted on any CONDUSEF-issued statement without producing incorrect rulings due to engineering assumptions.

**Wave:** W1 (RC6 §Wave 1 items 1.1–1.9)

**Gate:** `buildable-now` for all except §20 sign (#2) and §16 column-map (#3) which have an **open corpus-verify gate** (the engineering guard is buildable now; corpus confirmation is deferred — stories marked accordingly).

**Gaps covered:** #1, #2, #3, #4, #5, #6, #7, #36, #37, #38, #41, #42, #44, #45, #46, #48

---

### VERIQAN-E2-S1 — Add text-layer-density abstain guard (cardinal rule: scanned PDF false-RED)

**Traceability:** Closes #1. RC6 W1.1. Addresses U1 (scanned PDF cardinal violation).
**Gate:** `buildable-now`

**Subagent brief**

Task: Add a text-layer-density guard that fires immediately after `PdfPigStatementFieldExtractor.ExtractFullAsync` returns. If the total word count across all pages is below a configurable threshold (default: 20 words), emit a whole-verdict `BlockedOutcome` with reason `InsufficientTextLayer` instead of proceeding to section detection. This prevents `BuildAllAbsent` feeding `MandatorySectionsPresenceRule` with 28 present-but-absent sections, which currently produces a false RED on every scanned or image-only compliant statement.

Context:
- `PdfPigStatementFieldExtractor.cs:3224–3256` (`BuildAllAbsent`): when zero text is extracted, all sections are emitted as `IsApplicable=true, IsPresent=false`.
- `MandatorySectionsPresenceRule.cs`: abstain guard fires only on `sections.Count==0`, which is never reached because `BuildAllAbsent` returns 28 populated (but empty) sections → the rule fails all → RED.
- The guard should sit in `VerificationPipeline.ProcessAsync` between the Extract stage and the Bind stage, reading a word-count metric from `StatementFields` (or computing it from `PdfPigStatementFieldExtractor`'s internal page-word-count).
- Threshold: configurable via `TenantProfile.MinTextLayerWordCount` (default 20). An alternative: make `BuildAllAbsent` emit all sections as `IsApplicable=false` when zero text is extracted. The density-guard approach is preferred because it is more general and catches near-zero extraction too.

Constraints:
- Guard must produce `BlockedOutcome` (not RED) — abstain-safety discipline.
- ITDD: one test with a zero-word `StatementFields` → asserts `VerdictSignal.Blocked`; one test with ≥ 20 words → asserts pipeline continues.
- Do not modify `MandatorySectionsPresenceRule` logic — guard must precede it.

Definition of Done:
- Guard implemented in pipeline.
- `TenantProfile.MinTextLayerWordCount` added (default 20).
- Two unit tests green.
- `dotnet build` 0/0.

End-to-end evidence AC: a zero-text PDF traverses the pipeline and produces `VerdictSignal.Blocked` (not `Red`), proven by unit test. (Corpus validation with a real scanned statement deferred to VERIQAN-E5.)

**Effort:** S

---

### VERIQAN-E2-S2 — §20 saldo-a-favor sign abstain guard (cardinal rule: false-RED on negative credit)

**Traceability:** Closes #2 (engineering half). RC6 W1.5 (§20 portion). Corpus-verify gate remains open.
**Gate:** `buildable-now` (engineering guard) + **corpus-verify gate OPEN** (confirm sign convention with one real §20)

**Subagent brief**

Task: Add a sign-convention detection guard in `Section20PaymentDistributionRule`. If `ParsedValue[6]` (saldo-a-favor column) is negative, switch the computation to `+(negative)` path (subtract absolute value). If the sign is ambiguous (i.e. the value could be either interpretation and both produce a diff outside tolerance), emit `InsufficientData` / abstain rather than failing.

Context:
- `Section20PaymentDistributionRule.cs:143,149`: the rule hard-codes subtraction of col[6]. If a bank prints saldo-a-favor as a negative signed amount, subtracting a negative adds instead of subtracts → diff ≈ 2×credit > 0.50 MXN tolerance → false FAIL.
- Sign detection heuristic: if `col[6] < 0`, treat as `balance_credit = abs(col[6])` and subtract; if `col[6] > 0`, treat as-is. If `col[6] == 0`, no impact.
- Ambiguous zone: the MEMORY.md notes "§20 saldo-a-favor sign deferred to corpus" — the sign convention is unconfirmed. If the fix produces a diff within tolerance for both interpretations, proceed; if not, abstain with `InsufficientData` and add a `Finding` noting the ambiguity.

Constraints:
- Abstain-safety discipline: if ambiguous, produce `InsufficientData` (never RED).
- ITDD: one unit test with a negative col[6] value that would have produced false-FAIL without the fix; assert the fix produces PASS or InsufficientData.
- Add a code comment: `// TODO(corpus): Confirm saldo-a-favor sign convention against a real §20 specimen (VERIQAN-E5-S2).`

Definition of Done:
- Sign-convention guard implemented.
- Unit test green.
- `dotnet build` 0/0.
- Corpus-verify gate explicitly open: a `// CORPUS-VERIFY` comment labels the section awaiting real §20 data.

End-to-end evidence AC (buildable-now bar): unit test with mocked §20 table confirms the negative-sign path produces PASS or InsufficientData (not false-FAIL). Corpus confirmation is VERIQAN-E5-S2.

**Effort:** S

---

### VERIQAN-E2-S3 — §16 column-map: column-count guard and abstain on unverified layout (cardinal rule)

**Traceability:** Closes #3 (engineering half). Corpus-verify gate remains open.
**Gate:** `buildable-now` (guard) + **corpus-verify gate OPEN**

**Subagent brief**

Task: Add a column-count guard to `Section16OtherCreditLinesRule`. If the parsed table has a column count different from the expected 9-column map (`[ColSaldoPendiente=3, ColIntereses=4, ColIva=5, ColTasa=8]`), emit `InsufficientData` (abstain) instead of proceeding with the unverified mapping.

Context:
- `Section16OtherCreditLinesRule.cs:83–86,98–102,229–238`: the column-index map is self-declared as a guess with comment "no real §16 fixture available to calibrate." A mismatch silently produces false PASS (wrong column skipped as NA) or false FAIL (wrong column produces mismatch).
- The guard: count table columns; if count != 9, emit `InsufficientData` with message "§16 column count {N} does not match expected 9-column map — abstaining pending corpus calibration."
- Also guard: if any expected column index is out of range for the parsed row, abstain for that row rather than reading an adjacent column.

Constraints:
- Abstain-safety: InsufficientData on layout mismatch, never RED.
- ITDD: one test with a 7-column table → asserts InsufficientData; one test with a 9-column table → asserts the rule proceeds normally.
- Add `// CORPUS-VERIFY` comment at the index map constants.

Definition of Done:
- Column-count guard implemented.
- Two unit tests green.
- `dotnet build` 0/0.
- Corpus-verify gate open with comment.

End-to-end evidence AC: a §16 table with wrong column count produces `InsufficientData` (not false-FAIL or false-PASS), proven by unit test. Corpus calibration is VERIQAN-E5-S3.

**Effort:** S

---

### VERIQAN-E2-S4 — FR-12 pHash catalog-image presence rule (missing feature)

**Traceability:** Closes #4. RC6 (W1 — cardinal functional gap).
**Gate:** `buildable-now`

**Subagent brief**

Task: Implement `CatalogImagePresenceRule` (FR-12 / CL-27/CL-30/CL-47) using perceptual hashing. Add a PDF-to-image page-render step; compare each rendered page to the bank-logo / catalog-image hashes stored in `VecReferenceBundle`.

Context:
- No pHash rule, no pHash NuGet, no page-image rendering exists in the current Veriqan codebase.
- `VecReferenceBundle.cs:98–104,166` has schema fields for `ImageRef.perceptualHash` but no consumers.
- Required: (1) Add a pHash NuGet (e.g. `PHash.NET` or `ImageSharp.Drawing` for DCT-hash) via `Directory.Packages.props`; (2) Add a PDFtoImage page-render step (`Docnet.Core` or `PDFtoImage` NuGet, or reuse existing Prisma `IImagingService` if available) to produce a `System.Drawing.Bitmap` or `SixLabors.ImageSharp.Image` per page; (3) Implement `CatalogImagePresenceRule` that: renders each page, computes pHash, compares to all hashes in the bundle's `ImageRef` collection within a Hamming-distance threshold (configurable, default ≤ 5 bits); emits PASS if found on expected pages, FAIL if absent on required pages, InsufficientData if the bundle has no `ImageRef` entries.
- Note: `SixLabors.ImageSharp` is held at `3.1.12` in `Directory.Packages.props` (do NOT bump to 4.x — paid license). Use the existing pinned version.

Constraints:
- Abstain-safety: if `VecReferenceBundle.ImageRef` is empty, emit InsufficientData, not FAIL.
- Hamming-distance threshold configurable via `TenantProfile` or bundle metadata.
- ITDD: one unit test with a matching image → PASS; one with no match → FAIL; one with empty bundle → InsufficientData.
- Build only the Validation and Infrastructure projects (not full solution for speed).

Definition of Done:
- `CatalogImagePresenceRule` implemented and registered.
- pHash and PDF-render NuGets added to `Directory.Packages.props`.
- Three unit tests green.
- `dotnet build` of Validation + Infrastructure projects 0/0.

End-to-end evidence AC: a statement PDF containing a recognizable bank-logo image produces PASS on CL-27 (verified by unit test with a synthetic embedded image and a matching bundle hash).

**Effort:** L

---

### VERIQAN-E2-S5 — Fix CL-35 Aptos font: prefix matching + IsEmbedded flag

**Traceability:** Closes #5. RC6 W1.4.
**Gate:** `buildable-now`

**Subagent brief**

Task: (a) Replace the 8-entry closed suffix allowlist in `Cl35FontComplianceRule.cs` with a `StartsWith("Aptos", StringComparison.OrdinalIgnoreCase)` prefix match. (b) Add an `IsEmbedded` boolean flag to `FontUsage.cs`. (c) Add a fallback to abstain on Type0/CID composite fonts where family name is empty.

Context:
- `Cl35FontComplianceRule.cs:83–91`: suffix allowlist (8 entries) misses `Aptos-Black`, `Aptos Display`, `Aptos-Heavy`, space-separated variants.
- `FontUsage.cs:19`: no `IsEmbedded` field.
- `PdfPigStatementFieldExtractor.cs:1930`: 8-entry suffix strip for normalisation.
- The rule currently false-fails on any Aptos weight variant not in the closed list → false RED on a compliant statement.
- For `IsEmbedded`: PdfPig exposes `FontDetails.IsEmbedded` on `PdfFont`; map this to `FontUsage.IsEmbedded`.

Constraints:
- ITDD: one test with `Aptos-Black` font name → asserts PASS (was false-FAIL before fix); one test with `Arial` → asserts FAIL (unchanged); one test with empty family name on Type0 → asserts InsufficientData.
- Do not change the rule's overall structure — minimal surgical fix.

Definition of Done:
- `FontUsage.cs` has `IsEmbedded` field.
- `Cl35FontComplianceRule.cs` uses `StartsWith("Aptos")` + Type0 abstain.
- Three unit tests green.
- `dotnet build` of Validation project 0/0.

End-to-end evidence AC: a statement using `Aptos Display` produces PASS on CL-35 (not false-FAIL), proven by unit test.

**Effort:** M

---

### VERIQAN-E2-S6 — Fix CL-34 card-number: image-layer fallback for card-in-graphic

**Traceability:** Closes #6. RC6 W1.3.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add an image-layer fallback to `Cl34CardNumberPresenceRule`. If text-layer `Contains` check fails for a page, and the page contains rendered images, attempt a lightweight digit-pattern match over the page's rendered image (or apply page-1 propagation: if the card number is found in text on page 1, mark all pages as PASS for this check).

Context:
- `Cl34CardNumberPresenceRule.cs:34–114`: text-only `Contains` of card digits fails when the card is embedded in a header graphic (common in bank statements).
- `PdfPigStatementFieldExtractor.cs:2517–2528`: text-only extraction.
- Preferred approach (simpler, lower risk): page-1 propagation. If the card number (or its last-4 masked form) is found in text on page 1, propagate PASS to all subsequent pages. This avoids image rendering for this single rule.
- Secondary approach: use the pHash page render (VERIQAN-E2-S4) to extract text from page images via lightweight OCR (Tesseract, already available in the Prisma stack) on a per-need basis.
- Also fix: masked-vs-full digit mismatch (if extracted card is masked `XXXX-XXXX-XXXX-1234`, match only last-4 digits against the statement's card field). Substring collision (e.g. date "12/34" matching a card digit sequence) → require the match to be a formatted card pattern (4-digit groups).

Constraints:
- ITDD: one test with card in text on page 1 → all pages PASS; one test with card only in image on page 1, no text → InsufficientData (not FAIL, because we cannot extract from the image without rendering); one test with masked card → last-4 match PASS.
- Abstain-safety: if the card cannot be confirmed by any available extraction method, emit InsufficientData.

Definition of Done:
- Page-1 propagation and last-4 masked matching implemented.
- Three unit tests green.
- `dotnet build` of Validation project 0/0.

End-to-end evidence AC: a statement with the card number only on page 1 (text) produces PASS on CL-34 for all pages, proven by unit test.

**Effort:** M

---

### VERIQAN-E2-S7 — Fix CL-31 pagination: footer-band anchor for regex

**Traceability:** Closes #7. RC6 W1.2.
**Gate:** `buildable-now`

**Subagent brief**

Task: Anchor the `\b(\d+)\s+de\s+(\d+)\b` regex in `PdfPigStatementFieldExtractor.cs:2469,2534` to the footer band (bottom 10% of page height). Use the last match on the page rather than the first match.

Context:
- The current first-match approach can match body phrases like "5 de 10 pagos" before the actual footer pagination text "1 de 3".
- `PdfPigStatementFieldExtractor.cs:2469`: regex match with no positional constraint.
- PdfPig provides glyph Y-coordinates; filter to only words with `Y_position < (pageHeight * 0.10)` (bottom 10%) before applying the regex.
- Prefer `LastMatch` over `FirstMatch` as an additional safeguard.

Constraints:
- ITDD: one test where a body phrase "5 de 10" appears above the footer "1 de 3" → asserts extracted pagination = (1, 3) not (5, 10).
- One test where pagination appears correctly in footer → asserts unchanged behaviour.

Definition of Done:
- Footer-band filter and last-match applied.
- Two unit tests green.
- `dotnet build` of Extraction project 0/0.

End-to-end evidence AC: a statement with "5 de 10 pagos" in body and "1 de 3" in footer produces CL-31 PASS (pagination = 1/3), proven by unit test.

**Effort:** S

---

### VERIQAN-E2-S8 — Fix NFR-5 non-determinism: inject TimeProvider into extractor

**Traceability:** Closes #36. RC6 W1.7.
**Gate:** `buildable-now`

**Subagent brief**

Task: Replace `DateTimeOffset.UtcNow.Year` in `PdfPigStatementFieldExtractor.cs:1360` with a `TimeProvider` injected via constructor. Use the `VerificationJob.ReceivedAtUtc` timestamp (available in the pipeline context) as the stable reference clock so the same statement processed in December and January produces the same Finding.

Context:
- `PdfPigStatementFieldExtractor.cs:1360`: ambient clock for truncated-date year-repair.
- .NET 8+ has `TimeProvider` as a first-class injectable abstraction. The Prisma stack already uses it — import the same pattern.
- Pass `job.ReceivedAtUtc` as a `DateTimeOffset` to the extractor's year-repair logic.

Constraints:
- ITDD: one unit test with a `FakeTimeProvider` set to 2026-01-01; assert that a date extracted in "Dec" context is repaired to 2025 (not 2026).
- Constructor injection, not ambient.

Definition of Done:
- `PdfPigStatementFieldExtractor` accepts `TimeProvider` (or `DateTimeOffset referenceDate`) via constructor.
- Unit test with `FakeTimeProvider` is green.
- `dotnet build` of Extraction project 0/0.

End-to-end evidence AC: two pipeline runs of the same statement with different system clocks (simulated via `FakeTimeProvider`) produce identical Findings, proven by unit test.

**Effort:** S

---

### VERIQAN-E2-S9 — Fix statement timezone: apply America/Mexico_City to date arithmetic

**Traceability:** Closes #37. RC6 (W1 correctness).
**Gate:** `buildable-now`

**Subagent brief**

Task: Define `America/Mexico_City` (`TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")`) as the canonical timezone for all period-date comparisons in the Veriqan extractor and validation rules. Apply it consistently to all `DateOnly`-derived period-start/period-end comparisons.

Context:
- `PdfPigStatementFieldExtractor.cs:1511–1544` (`TryParseSpanishDate`): dates parsed as timezone-naive `DateOnly`.
- `grep TimeZoneInfo` in Veriqan → 0 hits.
- Risk: a cut-date on December 31 could be interpreted as January 1 in UTC, causing off-by-one period classification.
- Approach: add a `VeriqanTimeZone` static constant (`TimeZoneInfo`) to a shared `VeriqanConstants` class; document as a stated assumption in a `// ASSUMPTION` comment.

Constraints:
- Cross-platform: use `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` to select `"Central Standard Time"` vs `"America/Mexico_City"` per OS.
- ITDD: one test asserting that a date parsed on the Mexico City timezone boundary (e.g. 23:59 UTC = 17:59 CST on same day) is resolved to the correct local date.

Definition of Done:
- `VeriqanConstants.MexicoCityTimezone` defined.
- All date comparisons updated to use it.
- Unit test green.
- `dotnet build` 0/0.

End-to-end evidence AC: period-boundary dates are resolved to `America/Mexico_City` local date (proven by unit test).

**Effort:** S

---

### VERIQAN-E2-S10 — Fix European number-format mis-parse: es-MX culture guard

**Traceability:** Closes #38.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add an `es-MX` number-format detection step to `PdfPigStatementFieldExtractor.cs:1299–1301,1605–1614,3624–3660,4218–4279`. Detect whether the first parsed amount uses European format (`1.234,56`) or US/MX format (`1,234.56`) and apply consistently. If ambiguous, return `InvalidFormat`/`NotExtracted`.

Context:
- All amount parsing uses `decimal.Parse(s, CultureInfo.InvariantCulture)` with a `,` strip — fails silently on European format.
- `grep CultureInfo` in Veriqan → 0 hits.
- Strategy: on first encountered amount, detect format by presence of `.` as thousands separator (e.g. 3 digits after `.` followed by `,`) vs US convention. Apply detected format for the statement duration.

Constraints:
- Detection result should be stored per-statement-parse-session (not a global static).
- ITDD: one test with `"1.234,56"` → asserts parsed as 1234.56 MXN; one test with `"1,234.56"` → asserts unchanged (1234.56); one test with ambiguous single number → asserts InsufficientData.

Definition of Done:
- Format detection logic implemented and applied to all amount-parsing sites.
- Three unit tests green.
- `dotnet build` of Extraction project 0/0.

End-to-end evidence AC: an amount string `"1.234,56"` is correctly parsed as 1234.56 (not 1.234 with truncation), proven by unit test.

**Effort:** S

---

### VERIQAN-E2-S11 — Fix CL-48 blank-page: invisible-glyph filter and 2 cm gap check

**Traceability:** Closes #41.
**Gate:** `buildable-now`

**Subagent brief**

Task: (a) Update `Cl48BlankPageRule` to filter out whitespace-only or invisible-glyph words before `HasContent` evaluation. (b) Add a 2 cm intra-page gap check per the actual DOF numeral intent (the regulation prohibits any blank space exceeding 2 cm, not only whole blank pages).

Context:
- `Cl48BlankPageRule.cs:64`: `HasContent = words.Count > 0` — a page with one invisible/whitespace token passes.
- `PdfPigStatementFieldExtractor.cs:2507`: extracts words without filtering whitespace-only glyphs.
- DOF numeral: "sin espacio en blanco mayor a 2 cm" (no blank space > 2 cm) — the existing whole-blank-page check misses intra-page vertical gaps.
- For (b): detect the maximum vertical gap between consecutive text lines on a page; if > 56.7 points (≈ 2 cm at 28.35 pts/cm), emit a FAIL Finding for that page.

Constraints:
- ITDD: one test for (a): a page with one whitespace token → HasContent=false → FAIL; one test for (b): a page with 2.5 cm vertical gap → FAIL; one test with 1.5 cm gap → PASS.

Definition of Done:
- Whitespace-glyph filter applied.
- 2 cm gap check added as a separate Finding.
- Three unit tests green.
- `dotnet build` of Validation project 0/0.

End-to-end evidence AC: a page with only a whitespace glyph produces `CL-48 FAIL` (not spurious PASS), proven by unit test.

**Effort:** S

---

### VERIQAN-E2-S12 — Fix VerdictAggregator default arm: unknown verdict → abstain, not pass

**Traceability:** Closes #42. RC6 W1.6.
**Gate:** `buildable-now`

**Subagent brief**

Task: Replace the `default: passCount++` arm in `VerdictAggregator.cs:96–99` with an explicit `InsufficientData`/abstain fallback. Add a test for an unrecognised future enum value.

Context:
- `VerdictAggregator.cs:96–99`: any unrecognised `FindingVerdict` value silently becomes a PASS. This is a latent false-PASS risk on any future enum addition or deserialisation of an out-of-range value.
- Replacement: add a dedicated `abstainCount++` arm for unrecognised values; log a Serilog `Warning` with the unexpected value; do not throw (a throw would crash the pipeline on a new enum value).

Constraints:
- ITDD: one test passing a future `FindingVerdict` value (cast from `(FindingVerdict)999`) → asserts the verdict does NOT change to GREEN; one test for all current enum values → asserts unchanged behaviour.

Definition of Done:
- `VerdictAggregator.cs` default arm → abstain.
- Two tests green.
- `dotnet build` of Verdict project 0/0.

End-to-end evidence AC: an unknown `FindingVerdict` value causes abstain (not pass), proven by unit test.

**Effort:** S

---

### VERIQAN-E2-S13 — Move IVA rate to reference bundle config

**Traceability:** Closes #44.
**Gate:** `buildable-now`

**Subagent brief**

Task: Replace the hard-coded 16% IVA constant in `Section6PaymentSimulationRule.cs:94` and `Section16OtherCreditLinesRule.cs:104` with a value read from the reference bundle's `tolerance-config.csv` (or `TenantProfile`).

Context:
- Two rules hard-code `0.16m` (16% IVA). A rate change or period-specific variation would require a code change.
- Add `IvaRate` to `DefaultLegalToleranceProvider` or as a configurable field in `VecReferenceBundle` (whichever is the correct extension point — check `DefaultLegalToleranceProvider.cs:61–92` to confirm).
- Default: 16% (current Mexican IVA for financial services).

Constraints:
- ITDD: one test confirming the rule reads the rate from config (inject a bundle with 0.08 → assert the computation uses 8%).

Definition of Done:
- `IvaRate` configurable from bundle.
- Hard-coded 0.16 constants removed from both rules.
- Unit test green.
- `dotnet build` 0/0.

End-to-end evidence AC: rules use the bundle-supplied IVA rate (proven by unit test with non-default rate).

**Effort:** S

---

### VERIQAN-E2-S14 — Fix FR-16 marked-PDF Y-flip for rotated and CropBox-offset pages

**Traceability:** Closes #45.
**Gate:** `buildable-now`

**Subagent brief**

Task: Read `page.Rotation` and `CropBox` offset in `MarkedPdfGenerator.cs:249–252` and apply the corresponding coordinate transform before computing the Y-axis flip.

Context:
- `MarkedPdfGenerator.cs:249–252`: Y-flip assumes `CropBox == MediaBox` at origin and ignores `/Rotate`. A rotated page (90°/180°/270°) places highlights in the wrong position.
- PdfSharp exposes `page.Rotate` and `page.CropBox.X`/`page.CropBox.Y`.
- Transforms: 0° → standard flip; 90° → swap X/Y + flip; 180° → double flip; 270° → swap X/Y + double flip; plus CropBox offset subtraction.

Constraints:
- ITDD: one test for each rotation value (0°, 90°, 180°, 270°) asserting the highlight rectangle falls within the expected quadrant.

Definition of Done:
- Rotation and CropBox transforms applied.
- Four unit tests green.
- `dotnet build` of PDF generation project 0/0.

End-to-end evidence AC: a marked PDF generated from a 90°-rotated statement places highlights in the correct position (verified by unit test asserting rectangle coordinates).

**Effort:** S

---

### VERIQAN-E2-S15 — Add duplicate-alert guard: persisted alert-sent flag on JobVerdict

**Traceability:** Closes #46. RC6 W1.9.
**Gate:** `buildable-now` (depends on VERIQAN-E1-S5 for persistence layer)

**Subagent brief**

Task: Add an `AlertSentAt` nullable timestamp column to `JobVerdict`. In `VecAlertService.cs:57–132`, before sending, check `JobVerdict.AlertSentAt == null`; only send if null, then set `AlertSentAt = UtcNow` and persist. This prevents duplicate alerts on reprocess/retry.

Context:
- `VecAlertService.cs:57–132`: no dedup state — every invocation sends.
- `JobVerdict` entity: add `AlertSentAt` column; add EF migration.
- Check must be atomic with the send (or use optimistic concurrency on the `AlertSentAt` field to prevent race conditions on concurrent retry).

Constraints:
- Optimistic concurrency: use `[ConcurrencyToken]` on `AlertSentAt` to prevent two concurrent retry paths both sending.
- ITDD: one test where `AlertSentAt` is already set → asserts email is NOT sent; one test where null → asserts email IS sent and `AlertSentAt` is set.

Definition of Done:
- `AlertSentAt` added to `JobVerdict`; EF migration generated.
- Dedup check in `VecAlertService`.
- Two unit tests green.
- `dotnet build` 0/0.

End-to-end evidence AC: reprocessing a RED statement does not send a duplicate alert (proven by unit test asserting alert-sent count = 1 after two invocations).

**Effort:** S

---

### VERIQAN-E2-S16 — Fix ingestion dedup: ConcurrentDictionary.GetOrAdd (InMemory) and retry-on-conflict (SQL)

**Traceability:** Closes #48. RC6 W1.8.
**Gate:** `buildable-now`

**Subagent brief**

Task: Fix concurrency-unsafe dedup in `StatementIngestionService`. (a) InMemory: change `InMemoryVerificationJobRepository` from TOCTOU-unsafe check-then-insert to `ConcurrentDictionary.GetOrAdd`. (b) SQL: in `EfVerificationJobRepository.cs:88–91`, catch `DbUpdateException` from unique-constraint violation, re-query, and return the existing job (not `Result.WithFailure`).

Context:
- `InMemoryVerificationJobRepository.cs:32,44`: check-then-add is not atomic.
- `EfVerificationJobRepository.cs:88–91`: duplicate key → `Result.WithFailure` instead of returning the existing job.
- `StatementIngestionService.cs:121–128`: idempotent dedup intent — same PDF hash → same job.

Constraints:
- ITDD: one concurrent-scenario test (two threads ingesting the same PDF simultaneously) → asserts exactly one job GUID returned to both; one SQL-retry test (mock `DbUpdateException` → asserts existing job re-queried and returned).

Definition of Done:
- `ConcurrentDictionary.GetOrAdd` in InMemory repo.
- SQL retry-on-conflict in EF repo.
- Two tests green.
- `dotnet build` 0/0.

End-to-end evidence AC: two simultaneous submissions of the same PDF produce the same job GUID (proven by concurrent test).

**Effort:** S

---

## VERIQAN-E3 — Persistence, Durability, and Audit

**Goal:** all verdict findings, resume-state, reprocess-audit records, and exception-queue entries are durable in SQL Server, survive process restart, and the disposition audit trail is tamper-evident.

**Wave:** W2 (RC6 §Wave 2 items 2.1–2.5)

**Gate:** `buildable-now`

**Gaps covered:** #15, #19, #47, #50, #56

---

### VERIQAN-E3-S1 — Implement EfVerificationResultStore and EfReprocessAuditRepository

**Traceability:** Closes #15. RC6 W2.1.
**Gate:** `buildable-now`

**Subagent brief**

Task: Implement `EfVerificationResultStore` (EF Core implementation of `IVerificationResultStore`) and `EfReprocessAuditRepository` (EF Core implementation of `IReprocessAuditRepository`). Add EF migrations for their tables. Wire them in the SQL branch of `AddVeriqan` (replacing the `TryAddSingleton<InMemory...>` registrations).

Context:
- `VeriqanOrchestrationExtensions.cs:99–100`: `InMemoryVerificationResultStore` and `InMemoryReprocessAuditRepository` are registered unconditionally via `TryAddSingleton`, regardless of SQL connection string — durable EF impls don't exist yet.
- `InMemoryVerificationResultStore.cs` and `InMemoryReprocessAuditRepository.cs` are the in-memory impls to replace in the SQL branch.
- Schema: `VerificationResult` table (job GUID, stage reached, result JSON, created/updated); `ReprocessAuditEntry` table (original job GUID, reprocess job GUID, reason, actor, created). Use `[ConcurrencyToken]` for optimistic locking on `VerificationResult`.
- Keep in-memory impls for test scenarios with no SQL — switch to EF only when `ConnectionStrings:VeriqanDb` is present.

Constraints:
- Follow existing EF Core patterns in the codebase (check `EfDispositionRepository.cs` for conventions: `DbContext` injection, async methods, Result<T>, CancellationToken).
- Testcontainers integration test in the persistence test project using `SqlServerContainerFixture.CreateIsolatedDatabaseAsync`.

Definition of Done:
- `EfVerificationResultStore` and `EfReprocessAuditRepository` implemented.
- EF migrations generated and committed.
- Integration tests (Testcontainers) green: write and re-read a result; write and re-read an audit entry.
- `dotnet build` of persistence project 0/0.

End-to-end evidence AC: after process restart (simulated by disposing and recreating the `DbContext`), a previously persisted `VerificationResult` can be retrieved from SQL (proven by Testcontainers test).

**Effort:** M

---

### VERIQAN-E3-S2 — Convert Disposition table to SQL Server 2022 append-only ledger

**Traceability:** Closes #19. RC6 W2.2.
**Gate:** `buildable-now`

**Subagent brief**

Task: Convert the `Dispositions` table to a SQL Server 2022 append-only ledger table (`WITH (LEDGER = ON, APPEND_ONLY = ON)`). Add a `DENY UPDATE, DELETE TO [veriqan_app_login]` grant as a defence-in-depth companion. Document the 7-year regulatory retention policy in the migration comments.

Context:
- `AddDispositionAudit` migration `20260618001142.cs:14–47`: ordinary table with no ledger/trigger.
- SQL Server 2022 append-only ledger: `CREATE TABLE ... WITH (LEDGER = ON, APPEND_ONLY = ON, SYSTEM_VERSIONING = ON)`.
- Testcontainers `mssql` image: confirm `mcr.microsoft.com/mssql/server:2022-latest` supports ledger tables (it does since SQL Server 2022 CU1+).
- Add a new EF migration that drops and re-creates the table with ledger ON, or adds the `ALTER TABLE ... ENABLE LEDGER` DDL if supported.

Constraints:
- The EF `DbContext` must not execute `UPDATE` or `DELETE` on `Dispositions` after this change — verify all disposition code paths are insert-only.
- Testcontainers integration test: attempt an `UPDATE` on a disposition row → asserts it throws `SqlException` (DENY grant enforced).
- Document: "Disposition records are retained for 7 years per PRD §13 regulatory retention requirement."

Definition of Done:
- Migration committed with ledger DDL.
- `DENY UPDATE, DELETE` grant in migration.
- Integration test verifying append-only behaviour is green.
- `dotnet build` 0/0.

End-to-end evidence AC: an attempted `UPDATE` on a Disposition row raises `SqlException` at the database level (proven by Testcontainers integration test).

**Effort:** M

---

### VERIQAN-E3-S3 — Durable BatchExceptionLog for dead-letter queue

**Traceability:** Closes #50. RC6 W2.3. Addresses U7 (no durable dead-letter).
**Gate:** `buildable-now`

**Subagent brief**

Task: Add a `BatchExceptionLog` EF entity + migration + repository. Wire into `BatchProcessor` to persist failed-item records (replacing the in-memory `ConcurrentBag<ExceptionQueueEntry>`). Add a `GET /exceptions` endpoint to the Worker for triage.

Context:
- `BatchProcessor.cs:200,214–229`: failed items accumulated in `ConcurrentBag<ExceptionQueueEntry>` in `BatchReport` — lost on process restart.
- Schema: `BatchExceptionLog(Id, BatchId, StatementHash, InstitutionId, FailureReason, FailedAt, RetryCount, LastRetryAt, ResolvedAt)`.
- `BatchReport` should still include the exception summary in-memory for the immediate batch response; durable log is the source of truth for ops.
- `GET /exceptions?batchId={id}` returns the durable log; add to the Worker's `Program.cs`.

Constraints:
- Testcontainers integration test: submit a batch with one valid + one malformed statement; after processing, query `GET /exceptions` → asserts one exception logged.
- `dotnet build` of BatchProcessor and Worker projects 0/0.

Definition of Done:
- `BatchExceptionLog` entity + migration + repository committed.
- `BatchProcessor` persists failures.
- `GET /exceptions` endpoint live.
- Integration test green.

End-to-end evidence AC: after a process restart, previously failed batch items are retrievable from `GET /exceptions` (proven by Testcontainers test restart-simulation).

**Effort:** M

---

### VERIQAN-E3-S4 — Stamp EngineVersion and ReferenceBundleVersion on JobVerdict

**Traceability:** Closes #47 (engineering half; `AcuerdoVersion` deferred to VERIQAN-E5-S6 / E13-gated). RC6 W2.4.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add `EngineVersion` (assembly version of `Veriqan.Infrastructure`) and `ReferenceBundleVersion` (from `BundleMetadata.Version`) columns to `JobVerdict`. Populate both from the pipeline context during the Persist stage (VERIQAN-E1-S5).

Context:
- `JobVerdictConfiguration.cs:13–34`: no version columns.
- `Finding.cs:63–64`: partial provenance fields.
- `BundleMetadata` should already have a `Version` field — confirm by reading `VecReferenceBundle.cs`.
- Engine version: `typeof(VerificationPipeline).Assembly.GetName().Version?.ToString() ?? "unknown"`.

Constraints:
- Add EF migration for new columns.
- ITDD: one test asserting `EngineVersion` is non-null after persist.

Definition of Done:
- `EngineVersion` and `ReferenceBundleVersion` on `JobVerdict`; migration committed.
- Unit test green.
- `dotnet build` 0/0.

End-to-end evidence AC: a persisted `JobVerdict` row has non-null `EngineVersion` and `ReferenceBundleVersion` (proven by Testcontainers test).

**Effort:** S

---

### VERIQAN-E3-S5 — Add ef migrations bundle CI/CD entrypoint

**Traceability:** Closes #56. RC6 W2.5.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add an `ef migrations bundle` CI/CD target for the Veriqan `DbContext`, plus a `--migrate-only` CLI subcommand to `Program.cs` that runs migrations and exits cleanly.

Context:
- `VeriqanLegalBaselineStartupService.cs:63`: runs migrations on host boot — CI cannot pre-migrate without starting the Worker.
- `dotnet ef migrations bundle --project <VeriqanDbContext.csproj> --output efbundle` produces a standalone migration executable.
- Add `args.Contains("--migrate-only")` check to `Program.cs`: if present, run `context.Database.MigrateAsync()` and exit 0.

Constraints:
- Document the CI/CD step in `VERIQAN-RUNBOOK.md` (cross-reference E1-S9).
- ITDD: one test asserting that `--migrate-only` exits without starting the HTTP server.

Definition of Done:
- `efbundle` build step documented in a CI script (or `Makefile` target).
- `--migrate-only` subcommand in `Program.cs`.
- Unit test for `--migrate-only` exit.
- `dotnet build` 0/0.

End-to-end evidence AC: `dotnet run --project Veriqan.Worker -- --migrate-only` applies migrations and exits 0 without binding any HTTP port (proven by integration test with a test container).

**Effort:** S

---

## VERIQAN-E4 — Security Hardening

**Goal:** the Worker is hardened to a level appropriate for a regulated document-automation pipeline: authn/authz enforced, TLS active, secrets managed, key custody auditor-grade, PII retention designed, and the fail-open/closed gate policy decided and implemented.

**Wave:** W3 (RC6 §Wave 3 items 3.1–3.5, 3.14)

**Gate:** mixed — `buildable-now` for authn/TLS/bundle-signature; `business-gated` for key custody / secrets vault / LFPDPPP / fail-policy / CNBV CUB.

**Gaps covered:** #17, #18, #20, #21, #22, #26, #51, #52, #53, #54 (partial, E13 half deferred)

**Security review note:** this entire epic is flagged for a **separate, dedicated security review** before stories are executed. The stories below define the engineering backlog that review should verify and prioritise. Do not begin implementation until the security-review gate is cleared.

---

### VERIQAN-E4-S1 — Add authn/authz to Worker (JWT Bearer + [Authorize])

**Traceability:** Closes #17. RC6 W1.10.
**Gate:** `buildable-now` | `security-review-dependent`

**Subagent brief**

Task: Add ASP.NET Core JWT Bearer authentication + `[Authorize]` policy to the `POST /verify`, `POST /batch`, `GET /exceptions`, and any future disposition endpoints in `Veriqan.Worker/Program.cs`.

Context:
- `Program.cs` has no `AddAuthentication`, `UseAuthorization`, or `[Authorize]` — all endpoints are unauthenticated.
- Reuse the existing Prisma JWT infrastructure (`IProcessClearanceTokenService` / JWT issuer config) if available, or add a minimal `AddJwtBearer` configuration.
- Minimum: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `ValidIssuer`, `ValidAudience`, `IssuerSigningKey` from config. Document the required JWT claims in `appsettings.Production.json.template`.
- Add a `[Authorize(Roles = "VecSubmitter")]` policy on submission endpoints; `[Authorize(Roles = "VecAdmin")]` on disposition and exception endpoints.

Constraints:
- Per-process asymmetric JWT keys (RS256) are the target state — document as a `// TODO(E4-S2)` placeholder for the Key Vault story. This story uses a symmetric HMAC key from config as an interim.
- ITDD: one test asserting `POST /verify` returns 401 without a valid token; one asserting 202 with a valid token.

Definition of Done:
- JWT Bearer auth wired.
- `[Authorize]` on all named endpoints.
- Two integration tests green.
- `dotnet build <Worker.csproj>` 0/0.

End-to-end evidence AC: `POST /verify` without a token returns HTTP 401 (proven by integration test). With a valid token, returns HTTP 202.

**Effort:** M

---

### VERIQAN-E4-S2 — Enforce TLS on Worker and SMTP (TLS + HTTPS redirect)

**Traceability:** Closes #20. RC6 W1.11.
**Gate:** `buildable-now`

**Subagent brief**

Task: Add `UseHttpsRedirection()`, `RequireHttps`, and HSTS to `Program.cs`. Set `Veriqan:Smtp:EnableSsl=true` as the default in `SmtpOptions` and `appsettings.json`.

Context:
- `Program.cs`: plain `MapGet`, no HTTPS middleware.
- `SmtpOptions.cs:38`: `EnableSsl=false` default.
- Change `SmtpOptions.EnableSsl` default to `true`; document that production requires a valid TLS certificate.
- Worker should refuse HTTP (redirect to HTTPS) in production mode; in development mode, accept HTTP for local testing.

Constraints:
- Use `app.Environment.IsProduction()` check before `UseHsts()` (HSTS not appropriate for development).
- ITDD: one test asserting `GET http://...` returns 301 redirect to HTTPS in production mode.

Definition of Done:
- `UseHttpsRedirection()` + HSTS added.
- `SmtpOptions.EnableSsl` default = true.
- Integration test green.
- `dotnet build` 0/0.

End-to-end evidence AC: a plain HTTP request to the Worker returns HTTP 301 in production mode (proven by integration test).

**Effort:** S

---

### VERIQAN-E4-S3 — Replace AES-CBC with AES-GCM + key-id on ciphertext; integrate Key Vault

**Traceability:** Closes #18. RC6 W3.1.
**Gate:** `business-gated` (Key Vault / HSM provisioning is an ops/procurement decision)

**BLOCKED — pending Key Vault provisioning decision.**

Unlock: ops/infrastructure team provisions Azure Key Vault (or equivalent HSM) and allocates a key slot for Veriqan AES-256 key. Record the provisioning artifact and update this story before execution.

**Subagent brief (for when unblocked)**

Task: Replace `AesEncryptedDecimalConverter` (AES-CBC, no HMAC/AEAD) with AES-GCM (authenticated encryption). Add a `key-id` field to each encrypted ciphertext row so rotation can identify which key version was used. Integrate Azure Key Vault (or the provisioned HSM) for key custody via `ConfigurationCryptoKeyProvider`.

Context:
- `ConfigurationCryptoKeyProvider.cs:34–61`: reads base64 key from `IConfiguration` — dev-grade secret.
- `AesEncryptedDecimalConverter.cs:43–44`: CBC mode, no HMAC.
- Replace with `System.Security.Cryptography.AesGcm` (authenticated, tamper-evident). Ciphertext format: `[key-id:4 bytes][nonce:12 bytes][tag:16 bytes][ciphertext:N bytes]`.
- Key Vault integration: use `Azure.Security.KeyVault.Secrets` NuGet to retrieve the AES key at startup; cache for the process lifetime; expose a rotation method that reloads the key.
- Add a rotation capability: expose `POST /admin/rotate-key` (admin-only, `[Authorize(Roles = "VecAdmin")]`) that triggers a key-id increment and re-encrypts existing rows. Document as a runbook step.

Constraints:
- Existing ciphertext rows (if any) with CBC encoding must be detected by absence of the key-id prefix and either re-encrypted or flagged for migration.
- ITDD: one unit test confirming AES-GCM round-trip; one asserting tamper detection (modified ciphertext → authentication tag failure).

Definition of Done (when unblocked):
- AES-GCM implemented; key-id in ciphertext.
- Key Vault integration for key retrieval.
- Migration path for existing CBC rows.
- Two unit tests green.
- `dotnet build` 0/0.

End-to-end evidence AC: an encrypted row with a tampered ciphertext byte causes decryption to throw `AuthenticationTagMismatchException` (not return garbage), proven by unit test.

**Effort:** L

---

### VERIQAN-E4-S4 — Integrate secrets manager for AES key, SQL connection, SMTP credentials

**Traceability:** Closes #21. RC6 W3.2.
**Gate:** `business-gated` (secrets vault procurement)

**BLOCKED — pending Key Vault provisioning decision (same as E4-S3).**

Unlock: same as E4-S3. Vault provisioning enables this story. Execute after E4-S3.

**Subagent brief (for when unblocked)**

Task: Integrate Azure Key Vault (or equivalent) into `Program.cs` / `appsettings.Production.json` so that `ConnectionStrings:VeriqanDb`, `Veriqan:Smtp:Password`, and `Veriqan:LegalBaseline:EncryptionKey` are all retrieved from the vault at startup — never from plain env vars in production.

Context:
- RC1-multitenancy §Sub-area 3 and RC3-deploy §Config: all critical secrets flow through plain `IConfiguration`.
- Add `AddAzureKeyVault(...)` to the `WebApplication.CreateBuilder` config chain.
- Prohibit plaintext secrets in env vars in production config (document in `appsettings.Production.json.template` with a warning comment).

Definition of Done (when unblocked):
- `AddAzureKeyVault` in `Program.cs` config chain.
- `appsettings.Production.json.template` updated with vault references.
- All secret reads in tests use a local test secret store (not real Key Vault — use `IConfiguration` override).
- `dotnet build` 0/0.

**Effort:** M

---

### VERIQAN-E4-S5 — Design and implement LFPDPPP PII retention/erasure regime

**Traceability:** Closes #22. RC6 W3.3. Addresses U7 (LFPDPPP).
**Gate:** `business-gated` (retention policy is a legal/privacy decision)

**BLOCKED — pending legal/privacy policy decision.**

Unlock: legal/privacy counsel defines the retention schedules (how long processing data vs audit trail is kept), the ARCO mechanism, and the PII minimization requirements. Record the policy document and update this story before execution.

**Subagent brief (for when unblocked)**

Task: Implement LFPDPPP PII retention/erasure: (1) add TTL columns to `VerificationJob` and `Disposition` entities; (2) add a scheduled purge `BackgroundService` that deletes records past TTL; (3) add ARCO request handling (`DELETE /gdpr/erasure?subject={rfcHash}`) that marks all records for a given data-subject as erased (soft-delete with erasure timestamp); (4) mask card number in all Serilog log outputs; (5) document LFPDPPP lawful basis and ARCO process in `docs/compliance/LFPDPPP-COMPLIANCE.md`.

Context:
- `grep retention|erasure|LFPDPPP|purge` → 0 hits in codebase.
- `VerificationJob.cs` and `Disposition.cs` have no TTL fields.
- Card masking in logs: configure Serilog destructuring policy to replace card-number strings with `****-****-****-{last4}`.

Definition of Done (when unblocked):
- TTL fields + migration.
- Purge `BackgroundService`.
- ARCO erasure endpoint.
- Card masking in Serilog.
- `LFPDPPP-COMPLIANCE.md` committed.
- Integration test: a record past TTL is purged by the background service (Testcontainers).
- `dotnet build` 0/0.

**Effort:** L

---

### VERIQAN-E4-S6 — Decide and implement fail-open vs fail-closed policy + circuit-breaker

**Traceability:** Closes #26. RC6 W3.4. Addresses U5 (unowned gate-policy), U15 (no circuit-breaker).
**Gate:** `business-gated` (stakeholder policy decision)

**BLOCKED — pending stakeholder fail-open/closed policy decision.**

Unlock: the product owner and the bank integration team agree in writing whether Veriqan failing (timeout/error) should (a) block statement generation (fail-closed) or (b) allow the statement through unflagged (fail-open). Record the decision in `docs/architecture/adr/ADR-VEC-FAILPOLICY.md` before execution.

**Subagent brief (for when unblocked)**

Task: Implement the agreed policy: (1) add a `CancellationToken`-timeout at the `IVerificationPipeline.ProcessAsync` entry point (configurable, suggested 45 s); (2) add a circuit-breaker (`Polly.CircuitBreaker` or `Microsoft.Extensions.Http.Resilience`) wrapping the pipeline call in any host that invokes Veriqan; (3) implement the fail-open or fail-closed behaviour as decided; (4) record the decision as ADR-VEC-FAILPOLICY.

Context:
- `IVerificationPipeline.ProcessAsync` has no timeout.
- No circuit-breaker exists in any caller path.

Definition of Done (when unblocked):
- Pipeline timeout implemented.
- Circuit-breaker wired.
- Fail-open or fail-closed behaviour implemented.
- ADR-VEC-FAILPOLICY committed.
- Integration test: pipeline exceeding timeout returns the policy-agreed outcome (BLOCKED or allowed-through).
- `dotnet build` 0/0.

**Effort:** M

---

### VERIQAN-E4-S7 — Add reference-bundle SHA-256/HMAC signature verification

**Traceability:** Closes #53. RC6 W3.5. Addresses U19 (bundle authenticity).
**Gate:** `buildable-now` | `security-review-dependent`

**Subagent brief**

Task: Add a SHA-256/HMAC signature field to `BundleMetadata`. Verify the signature at bundle load time in `CsvReferenceDataAdapter`; reject unverifiable bundles as BLOCKED.

Context:
- `CsvReferenceDataAdapter.cs:154–161`: validates schema only (no authenticity check).
- Add `BundleMetadata.Signature` (base64 SHA-256 HMAC of all CSV file contents, keyed with a bundle-signing secret from config).
- At load time: compute HMAC of all CSVs in the bundle directory; compare to `metadata.Signature`; if mismatch → `Result.WithFailure("BundleSignatureMismatch")` → all binds BLOCKED.
- Bundle-authoring tooling: document the HMAC signing step in `BUNDLE-AUTHORING-GUIDE.md` (cross-reference E1-S6).

Constraints:
- ITDD: one test with a valid signature → load succeeds; one with tampered CSV → load fails with `BundleSignatureMismatch`.

Definition of Done:
- `BundleMetadata.Signature` field added.
- Signature verification in `CsvReferenceDataAdapter`.
- `BUNDLE-AUTHORING-GUIDE.md` updated with signing step.
- Two unit tests green.
- `dotnet build` 0/0.

End-to-end evidence AC: a bundle with a tampered CSV file is rejected at load time (not silently accepted), proven by unit test.

**Effort:** S

---

### VERIQAN-E4-S8 — CNBV CUB outsourcing-regime alignment (documentation + counsel)

**Traceability:** Closes #52. RC6 W3.14.
**Gate:** `business-gated` (legal counsel required)

**BLOCKED — pending legal counsel engagement.**

Unlock: engage legal/compliance counsel; document which CNBV CUB outsourcing provisions apply; confirm data residency; update architecture to address documented obligations.

This story produces `docs/compliance/CNBV-CUB-ALIGNMENT-VERIQAN.md` documenting: applicable CUB provisions, data-residency constraints, mandatory independent review controls, and any architecture changes required. The engineering changes (if any) are follow-on stories derived from the legal findings. No code in this story.

**Effort:** L

---

### VERIQAN-E4-S9 — Process-split for PDF parsing vs key-holding (defence-in-depth)

**Traceability:** Closes #51. RC6 (W3 security/degrades).
**Gate:** `business-gated` (architecture decision) | `security-review-dependent`

**BLOCKED — pending security review and architecture decision.**

Unlock: security review recommends process-split; ops team approves the additional process overhead.

This story mirrors the Prisma A1–A6 3-process split mandate: isolate the PdfPig parser (which processes untrusted PDFs) in a separate process or AppDomain from the process that holds the AES-256 key material, preventing a parser exploit from reaching key material.

No engineering in this story until the security-review gate clears. When unblocked, the implementation follows the Prisma Ember-coordinated process-split pattern (ADR-009).

**Effort:** L

---

## VERIQAN-E5 — Corpus Calibration and E13 Productization

**Goal:** once real CONDUSEF specimens are available and/or the E13 buyer gate clears, calibrate all thresholds against real data and build the multi-tenant productization layer.

**Wave:** W4 (corpus-gated items) + W6 (E13-gated items)

**Gate:** `corpus-gated` (W4 items) + `E13-gated` (W6 items)

All stories in this epic are **BLOCKED**. They are planned here so their dependency is tracked and the engineering scope is clear for when the unlock arrives.

**Gaps covered:** #2 (verify), #3 (verify), #27, #29, #30, #31, #32, #33, #34, #35, #39, #40, #43, #49, #54, #57, #64

---

### VERIQAN-E5-S1 — Run calibration harness against real corpus; compute real FPR/DR

**Traceability:** Closes #27 (p95 measurement) + RC4 W4.10 (harness FPR/DR).
**Gate:** `corpus-gated`

**BLOCKED — unlock: CPA-2 (real CONDUSEF corpus in calibration harness).**

Task (when unblocked): run `Orchestration.Tests/Calibration/` harness against real KnownGood + KnownBroken corpus. Measure p95 statement latency (NFR-1: ≤ 10 000 ms). Compare to the 30 s/statement synthetic result; resize worker pool if needed. Compute real FPR and detection rate. Record results in `docs/qa/calibration/calibration-report.md`.

**Effort:** M

---

### VERIQAN-E5-S2 — Confirm and harden §20 saldo-a-favor sign convention

**Traceability:** Closes #2 (corpus-verify half). RC4 W4.8.
**Gate:** `corpus-gated`

**BLOCKED — unlock: at least one real §20 table specimen.**

Task (when unblocked): run a real §20 table through the sign-convention guard implemented in VERIQAN-E2-S2. If the bank prints saldo-a-favor as a negative signed amount, confirm the guard produces PASS (not InsufficientData). Remove the abstain path if the sign convention is confirmed unambiguous. Update the `// CORPUS-VERIFY` comment to `// CALIBRATED YYYY-MM-DD`.

**Effort:** S

---

### VERIQAN-E5-S3 — Calibrate §16 column-map against real §16 specimens

**Traceability:** Closes #3 (corpus-verify half). RC4 W4.9.
**Gate:** `corpus-gated`

**BLOCKED — unlock: real §16 statement with known column order.**

Task (when unblocked): verify the 9-column map against a real §16 table; update indices if wrong; remove the `InsufficientData` abstain guard once the map is confirmed correct; update `// CORPUS-VERIFY` comment.

**Effort:** S

---

### VERIQAN-E5-S4 — Calibrate arithmetic tolerance defaults against real corpus

**Traceability:** Closes #29. RC4 W4.1.
**Gate:** `corpus-gated`

**BLOCKED — unlock: CPA-2.**

Task: run calibration harness; measure arithmetic diffs on real statements; replace `⚠️ ESTIMATED` tolerance constants in `DefaultLegalToleranceProvider.cs:61–92` with measured-and-legally-confirmed values; obtain legal/compliance sign-off.

**Effort:** M

---

### VERIQAN-E5-S5 — Calibrate §6 payment-simulation recursion; §8 coherence check; §19 rate thresholds

**Traceability:** Closes #30, #31, #43, #57. RC4 W4.2, W4.3.
**Gate:** `corpus-gated`

**BLOCKED — unlock: real statements containing §6, §8, §19 tables.**

Task (bundle of related corpus-calibration items): (1) acquire real §6 table → calibrate column X-ranges → verify recursion vs printed values; (2) build §8 coherence check once real §8 specimens available; (3) calibrate §19 rate ambiguous-zone boundaries.

**Effort:** M

---

### VERIQAN-E5-S6 — Replace 3-value constant confidence with measured per-field score

**Traceability:** Closes #32, #64. RC4 W4.4.
**Gate:** `corpus-gated`

**BLOCKED — unlock: CPA-2 (real distribution needed to calibrate `ConfidenceGuard` threshold).**

Task: replace `ExtractedField.cs:69–83` constant confidence (1.0/0.7/0.0) with a per-field measured score (fuzzy-match for text fields, cell-parse success ratio for numerics). Recalibrate `ConfidenceGuard` threshold against real distribution. Update `TenantProfile.LegalMinFieldConfidenceDefault` from the engineering guess to a measured value.

**Effort:** L

---

### VERIQAN-E5-S7 — Dynamic layout detection (geometry calibration to real bank layouts)

**Traceability:** Closes #33. RC4 W4.5.
**Gate:** `corpus-gated`

**BLOCKED — unlock: real bank statements with known layout.**

Task: replace hardcoded geometry constants in `PdfPigStatementFieldExtractor.cs:46–91,1084–1092,3297–3327` with a layout-learning pass that detects section anchors dynamically; compute column X-ranges from the actual table geometry per statement; fall back to defaults only when anchors not found.

**Effort:** L

---

### VERIQAN-E5-S8 — Real advertising detection from corpus; restore §12 threshold

**Traceability:** Closes #34. RC4 W4.6.
**Gate:** `corpus-gated`

**BLOCKED — unlock: labelled corpus samples with advertising content.**

Task: build a real ad-detection heuristic from corpus; restore §12 threshold from 805 to 700 chars; model `página-cero` permitted zone.

**Effort:** M

---

### VERIQAN-E5-S9 — Logo presence rule: real pHash logo identification (replace image-count proxy)

**Traceability:** Closes #40.
**Gate:** `corpus-gated` + depends on VERIQAN-E2-S4 (pHash infrastructure)

**BLOCKED — unlock: bank logo templates in reference bundle.**

Task: replace `Cl33LogoPresenceRule` image-count proxy (≥1 image = logo present) with real pHash comparison against bank-logo template in reference bundle. Depends on E2-S4 pHash infrastructure.

**Effort:** M

---

### VERIQAN-E5-S10 — Bold detection via glyph metrics (replace font-name substring)

**Traceability:** Closes #39.
**Gate:** `corpus-gated`

**BLOCKED — unlock: real specimen to validate glyph stroke-width inference.**

Task: investigate glyph stroke-width inference via PdfPig render metrics for `Cl29HeaderStylingRule` and `MandatedBoldFieldsRule`; if viable, replace font-name substring `Contains("Bold")` with glyph-metric bold detection.

**Effort:** M

---

### VERIQAN-E5-S11 — E13 productization: runtime tenant selection + config-only onboarding

**Traceability:** Closes #35, #49. RC4 W5.1–5.2.
**Gate:** `E13-gated`

**BLOCKED — unlock: CPA-1 (GitHub issue #17 buyer discovery, OPEN).**

Task (when unblocked): implement runtime tenant selection (`ITenantProfileResolver` by tenant-id); config-only profile loading; embeddable SDK / HTTP gate API with documented latency budget; bundle `toleranceConfig` binding to tenant profile overlay.

**Effort:** L

---

### VERIQAN-E5-S12 — E13 productization: traceability-matrix export and AcuerdoVersion provenance

**Traceability:** Closes #47 (AcuerdoVersion half), #54. RC4 W5.4–5.5.
**Gate:** `E13-gated`

**BLOCKED — unlock: CPA-1.**

Task (when unblocked): traceability-matrix export (CheckId + DOF numeral + verdict + evidence locator); `AcuerdoVersion` field on rules + `JobVerdict`; Acuerdo-edition governance process (versioned-rule migration process for in-flight statements when Acuerdo changes).

**Effort:** M

---

## VERIQAN-COSMETIC — Doc and Code Cosmetics

**Goal:** correct misleading XML documentation and stale comments.

**Wave:** W1 (small, can run in parallel with any E2 story)

**Gate:** `buildable-now`

**Gaps covered:** #62, #63

---

### VERIQAN-COSMETIC-S1 — Fix VerdictSignal.Blocked XML doc comment

**Traceability:** Closes #62.
**Gate:** `buildable-now`

Task: Fix `Enums/VerdictSignal.cs:15–19` XML doc comment. Replace the misleading "Blocked = InsufficientData and no fail" with the accurate description: "Blocked = emitted by `BlockedOutcome`; InsufficientData-only correctly yields Green."

Definition of Done: XML doc corrected; `dotnet build` 0/0.

**Effort:** S

---

### VERIQAN-COSMETIC-S2 — Fix Section6PaymentSimulationRule stale doc-comment

**Traceability:** Closes #63.
**Gate:** `buildable-now`

Task: Update `Section6PaymentSimulationRule.cs:26–33` doc-comment from "extractor has NOT yet been built" to "extractor is built (`ExtractSection6Table`) but uncalibrated — pending real §6 specimen (VERIQAN-E5-S5)."

Definition of Done: doc-comment updated; `dotnet build` 0/0.

**Effort:** S

---

## Gap Coverage Table

Every gap from RC4 mapped to a story ID. Gaps marked RESOLVED need no story. Gaps marked DEFERRED are assigned to a BLOCKED story with the reason.

### Blocks-production (#1–#28, #16 RESOLVED)

| Gap # | Title (abbreviated) | Story | Status |
|-------|---------------------|-------|--------|
| #1 | Scanned PDF false-RED (cardinal) | VERIQAN-E2-S1 | buildable-now |
| #2 | §20 saldo-a-favor sign (cardinal) | VERIQAN-E2-S2 (guard) + VERIQAN-E5-S2 (verify) | guard buildable-now; verify corpus-gated |
| #3 | §16 column-map (cardinal) | VERIQAN-E2-S3 (guard) + VERIQAN-E5-S3 (verify) | guard buildable-now; verify corpus-gated |
| #4 | FR-12 pHash catalog-image missing | VERIQAN-E2-S4 | buildable-now |
| #5 | CL-35 Aptos font variants (cardinal) | VERIQAN-E2-S5 | buildable-now |
| #6 | CL-34 card-in-image (cardinal) | VERIQAN-E2-S6 | buildable-now |
| #7 | CL-31 pagination first-match (cardinal) | VERIQAN-E2-S7 | buildable-now |
| #8 | No ingestion entry point | VERIQAN-E1-S1 | buildable-now |
| #9 | Report/notify stages orphaned | VERIQAN-E1-S4 | buildable-now |
| #10 | No appsettings.json | VERIQAN-E1-S2 | buildable-now |
| #11 | No Dockerfile | VERIQAN-E1-S3 | buildable-now |
| #12 | No real ingestion path | VERIQAN-E1-S1 (covered) | buildable-now |
| #13 | No real reference-bundle provenance | VERIQAN-E1-S6 | buildable-now (engineering); business-gated (bank data) |
| #14 | Verdict/Findings never persisted | VERIQAN-E1-S5 | buildable-now |
| #15 | Resume-state / reprocess-audit in-memory | VERIQAN-E3-S1 | buildable-now |
| #16 | 2 persistence integration tests RED | **RESOLVED** — commit d0d9ef65 on `Liv` | No action needed |
| #17 | No authn/authz on Worker | VERIQAN-E4-S1 | buildable-now |
| #18 | AES-CBC / no key management | VERIQAN-E4-S3 | BLOCKED (business-gated: Key Vault provisioning) |
| #19 | Audit immutability not enforced | VERIQAN-E3-S2 | buildable-now |
| #20 | No TLS | VERIQAN-E4-S2 | buildable-now |
| #21 | No secrets management | VERIQAN-E4-S4 | BLOCKED (business-gated) |
| #22 | LFPDPPP PII retention/erasure absent | VERIQAN-E4-S5 | BLOCKED (business-gated: legal/privacy policy) |
| #23 | Health probes are stubs | VERIQAN-E1-S7 | buildable-now |
| #24 | No OTel backend | VERIQAN-E1-S8 | buildable-now |
| #25 | No runbook | VERIQAN-E1-S9 | buildable-now |
| #26 | Fail-open/closed policy unowned | VERIQAN-E4-S6 | BLOCKED (business-gated: stakeholder policy decision) |
| #27 | No production latency characterization | VERIQAN-E5-S1 | BLOCKED (corpus-gated) |
| #28 | Missing reference bundle → BLOCKED | VERIQAN-E1-S6 (covered) | buildable-now (engineering path); business-gated (real bank data) |

### Degrades (#29–#61)

| Gap # | Title (abbreviated) | Story | Status |
|-------|---------------------|-------|--------|
| #29 | Tolerance defaults are ESTIMATED | VERIQAN-E5-S4 | BLOCKED (corpus-gated) |
| #30 | §6 recursion never executed on real input | VERIQAN-E5-S5 | BLOCKED (corpus-gated) |
| #31 | §8 coherence check omitted | VERIQAN-E5-S5 | BLOCKED (corpus-gated) |
| #32 | Confidence is 3-value constant | VERIQAN-E5-S6 | BLOCKED (corpus-gated) |
| #33 | Geometry hardcoded to fixture #1 | VERIQAN-E5-S7 | BLOCKED (corpus-gated) |
| #34 | Advertising detection 7-phrase allowlist | VERIQAN-E5-S8 | BLOCKED (corpus-gated) |
| #35 | toleranceConfig bundle override not wired | VERIQAN-E5-S11 | BLOCKED (E13-gated) |
| #36 | NFR-5 non-determinism: ambient clock | VERIQAN-E2-S8 | buildable-now |
| #37 | Timezone undefined | VERIQAN-E2-S9 | buildable-now |
| #38 | European number format mis-parse | VERIQAN-E2-S10 | buildable-now |
| #39 | Bold detection font-name substring only | VERIQAN-E5-S10 | BLOCKED (corpus-gated) |
| #40 | CL-33 logo: image-count proxy | VERIQAN-E5-S9 | BLOCKED (corpus-gated + E2-S4 pHash infra) |
| #41 | CL-48 blank-page: invisible-glyph misses | VERIQAN-E2-S11 | buildable-now |
| #42 | VerdictAggregator default→passCount | VERIQAN-E2-S12 | buildable-now |
| #43 | §19 rate normalisation thresholds | VERIQAN-E5-S5 | BLOCKED (corpus-gated) |
| #44 | IVA hard-coded at 16% | VERIQAN-E2-S13 | buildable-now |
| #45 | FR-16 marked-PDF Y-flip on rotated pages | VERIQAN-E2-S14 | buildable-now |
| #46 | FR-17 duplicate alert on retry | VERIQAN-E2-S15 | buildable-now |
| #47 | AR-9 verdict provenance: no EngineVersion/BundleVersion | VERIQAN-E3-S4 (versions) + VERIQAN-E5-S12 (AcuerdoVersion) | versions: buildable-now; AcuerdoVersion: E13-gated |
| #48 | Dedup concurrency-unsafe | VERIQAN-E2-S16 | buildable-now |
| #49 | E13 product half missing | VERIQAN-E5-S11 | BLOCKED (E13-gated) |
| #50 | Exception queue in-memory | VERIQAN-E3-S3 | buildable-now |
| #51 | PDF-parse / key-holding in same process | VERIQAN-E4-S9 | BLOCKED (business-gated: security review + architecture decision) |
| #52 | No CNBV CUB outsourcing alignment | VERIQAN-E4-S8 | BLOCKED (business-gated: legal counsel) |
| #53 | Reference-bundle authenticity untrusted | VERIQAN-E4-S7 | buildable-now |
| #54 | No rule-version ↔ Acuerdo-edition governance | VERIQAN-E5-S12 | BLOCKED (E13-gated) |
| #55 | StatementContextKey schema undocumented | VERIQAN-E1-S6 (BUNDLE-AUTHORING-GUIDE) | buildable-now |
| #56 | No EF migration CI/CD entrypoint | VERIQAN-E3-S5 | buildable-now |
| #57 | §6 moat latency unknown | VERIQAN-E5-S5 | BLOCKED (corpus-gated) |
| #58 | BatchProcessor loads all tasks up-front | VERIQAN-E1-S11 | buildable-now |
| #59 | Password-protected PDF silent failure | VERIQAN-E1-S11 | buildable-now |
| #60 | Idempotency key is raw byte-hash | VERIQAN-E2-S16 (logical identity key) | buildable-now |
| #61 | Poison-PDF DoS: no parse timeout | VERIQAN-E1-S11 | buildable-now |

### Cosmetic (#62–#64)

| Gap # | Title (abbreviated) | Story | Status |
|-------|---------------------|-------|--------|
| #62 | VerdictSignal.Blocked XML doc misleading | VERIQAN-COSMETIC-S1 | buildable-now |
| #63 | Section6PaymentSimulationRule doc stale | VERIQAN-COSMETIC-S2 | buildable-now |
| #64 | MinFieldConfidence 0.8 is untuned guess | VERIQAN-E5-S6 | BLOCKED (corpus-gated — annotated there) |

### Unknown-Unknowns (U2 only — others mapped to numbered gaps above)

| UU # | Description | Story |
|------|-------------|-------|
| U2 | No minimum-extraction-coverage floor (false-PASS risk) | VERIQAN-E1-S10 |

All other U-items (U1, U3–U18) map directly to numbered gap rows already covered above.

---

## Deliberately Deferred Items and Reasons

| Item | Reason for deferral |
|------|---------------------|
| #18, #21 (AES-GCM / secrets vault) | Business-gated: Key Vault provisioning is an ops/procurement decision. Stories are BLOCKED placeholders with full engineering ACs ready for execution the moment the unlock arrives. |
| #22 (LFPDPPP) | Business-gated: retention schedule and ARCO mechanism must be defined by legal/privacy counsel before engineering can be spec'd. |
| #26 (fail-open/closed) | Business-gated: the highest-stakes single design decision in the system. Stakeholder alignment required before any code is written. |
| #51, #52 (process-split; CNBV CUB) | Business-gated: requires security review recommendation and legal counsel engagement respectively. |
| #27, #29–#34, #39, #40, #43, #57, #64 | Corpus-gated: genuinely unknowable without real CONDUSEF specimen data. Engineering guards (abstain paths) are built in E2; calibration waits for CPA-2. |
| #35, #47(AcuerdoVersion), #49, #54 | E13-gated: dependent on issue #17 buyer discovery. No engineering investment until the product shape is confirmed. |
| ISO 27001 / SOC 2 assessment | Separate pass — referenced as a dependency in E4 (RC6 W3.13) but not spec'd here; commission as a distinct engagement after W3 engineering is complete. |

---

*Plan-only. No production code was read, modified, or created in the course of writing this document. Evidence cited is from RC4-VERIQAN-READINESS-MATRIX.md (file:line references) and RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md.*
