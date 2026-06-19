# PRISMA-REMEDIATION-EPICS.md

**Subsystem:** Prisma MVP (ExxerCube.Prisma OCR document-processing pipeline)
**Source matrix:** `docs/planning-artifacts/readiness-challenge/RC5-PRISMA-READINESS-MATRIX.md` (2026-06-18)
**Wave ordering:** mirrored from `RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` (2026-06-18), Prisma items tagged [Prisma]
**Gap count:** 44 total open gaps — 16 Blocks (P1–P16), 24 Degrades (D1–D24), 4 Cosmetic (C1–C4)
**Ordering note:** Prisma is planned first (closest to production-ready; residual is deploy/ops/security, not functional capability).
**Executor model:** each story is authored as a self-contained subagent brief (agent-executable, not loose human ticket).
**Evidence bar:** a green unit test is explicitly NOT readiness. Every story's DoD requires end-to-end evidence on real infrastructure (Testcontainers SQL, real TCP, real OCR, or containerized compose).
**Do NOT re-plan:** items in RC5 §3 "Resolved Since 2026-06-11" (A1–A6, B1, B2, C3-override-persist, D2-SLA, E1/E2, quality-coefficients-real, Counter/Weather removed, F1 E2E) — those are DONE.

---

## Epic Index

| ID | Title | Wave(s) | Gate | Gaps Covered |
|----|-------|---------|------|-------------|
| PRISMA-E1 | Deployable Composition | W0 | buildable-now | P1 P2 P3 P4 P5 P9 P10 D7 D8 D9 D12 |
| PRISMA-E2 | Real-Process and Observability Proof | W1 | buildable-now | P6 P12 P16 D10 D11 D13 D20 D21 D22 D24 |
| PRISMA-E3 | Security Hardening | W2 W3 | business-gated / ops-gated / security-review-dependent | P8 P11 P13 P14 P15 D15 D16 D17 D18 D23 |
| PRISMA-E4 | Persistence, Durability, and Failure-Mode Coverage | W2 | buildable-now | D6 D13 |
| PRISMA-E5 | Code-Quality and Cosmetic Remediation | W1 | buildable-now | C1 C2 C3 C4 D2 D3 D5 |
| PRISMA-GATED | Live SIARA Portal Ingestion | W4 W5 | legal-gated / corpus-gated / ops-gated | P7 D1 D4 D19 |

---

## PRISMA-E1 — Deployable Composition

**Wave:** W0 (Minimum Deployable Composition)
**Goal:** any developer can run `docker-compose up` and process a document through all three Prisma worker processes against the SIARA simulator, with real health probes and real logging, using only env-var configuration.
**Gate:** buildable-now (no legal/corpus/ops gate blocks these items)
**Gaps covered:** P1, P2, P3, P4, P5, P9, P10, D7, D8, D9, D12

### PRISMA-E1-S1 — Author Dockerfiles for All Four Production Hosts

**Traceability:** Closes P1 (no deploy artifacts for any of the 4 production hosts), RC6 W0.9
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Author production-ready `Dockerfile` files for the four Prisma hosts: `Orion.Worker`, `Athena.Worker`, `Reconciliator.Worker`, and `Web.UI`. Add a Docker image-build step to the existing CI workflow.

Context:
- P1 evidence: RC5d §1.3 — a glob over the entire repo returns zero Dockerfiles for any production host; only `Prisma/Deployments/Siara.Simulator/` has a container artifact.
- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime); use `mcr.microsoft.com/dotnet/sdk:10.0` for the build stage.
- Build artifacts output to `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\` (configured in `Directory.Build.props`) — the Dockerfile must not assume this Windows path; use a standard multi-stage `dotnet publish -c Release -o /app/publish` inside the container.
- Each host is a separate `.csproj` under: `Prisma/Code/Src/CSharp/04 Services/Orion/`, `04 Services/Athena/`, a Reconciliator path, and `07 UI/`.
- CI workflow lives at `Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` — append a `docker build` step per image after the existing build+test steps.

Constraints:
- No production code changes; only new `Dockerfile` files and CI YAML additions.
- Dockerfile `ENTRYPOINT` must use the array form (not shell form) for proper signal handling.
- Each Dockerfile must accept all sensitive config as ENV variables (no hardcoded connection strings).
- Do not add `--no-verify` or extra `dotnet` flags.
- A single-project `dotnet build` of each host csproj must succeed (build 0/0) before writing the Dockerfile.

Definition of Done:
- `Dockerfile` present at the root of each of the four host project directories.
- `docker build` of each image succeeds on a Linux host without error.
- CI `quality-gates.yml` has a new `docker-build` job that builds all four images.
- Build 0/0 on the full solution after adding these files.

End-to-end evidence AC: `docker build -t prisma-orion .` (and equivalents for the other three hosts) completes with exit code 0; the resulting image starts and reaches its health endpoint when given valid env-var config.

---

### PRISMA-E1-S2 — Auto-Migration Runner and Multi-DbContext Sequencing

**Traceability:** Closes P2 (no auto-migration runner), RC6 W0.10, U12
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Add a startup migration step (or a `--migrate-only` CLI entrypoint) to each of the three worker hosts (`Orion.Worker`, `Athena.Worker`, `Reconciliator.Worker`) covering the three DbContext types (`PrismaDbContext`, `ApplicationDbContext`, `ExportAdaptiveDbContext`). Document the correct application order. Wire the migration sequence as a pre-traffic step in CI.

Context:
- P2 evidence: `Athena.Worker/Program.cs:29-41` shows graceful DB skip on placeholder connection string; no `MigrateAsync()` call exists in any host's composition root; `Web.UI/Program.cs:97` has `UseMigrationsEndPoint` (dev-only, not production).
- Reference: only Veriqan has auto-migration — `VeriqanDbInitialiser.cs:54`; use that pattern.
- Three DbContext types must be migrated in a specific order (PrismaDbContext → ApplicationDbContext → ExportAdaptiveDbContext) to avoid FK/schema dependency violations. This order must be documented in the runbook (PRISMA-E2-S2) AND enforced in code via sequenced `MigrateAsync()` calls.
- A `--migrate-only` CLI argument pattern (check `args` for `"--migrate-only"` before `host.Run()`) is preferred over a startup task, as it allows Kubernetes init-containers to run migrations before traffic starts.

Constraints:
- Pattern: `Result<T>` wrapping if a migration helper method is authored; `CancellationToken` propagated.
- If `MigrateAsync()` fails, the process must log a structured error and exit with a non-zero code (do not swallow).
- Test project to verify: author one integration test in the existing `Tests.System.Storage` test project (Testcontainers SQL) that runs `--migrate-only` against a fresh container and asserts all three schemas are applied with no pending migrations.

Definition of Done:
- Each worker's `Program.cs` contains a sequenced `MigrateAsync()` call (or `--migrate-only` path) covering all three DbContext types.
- One integration test in `Tests.System.Storage` verifies the migration sequence end-to-end against a Testcontainers SQL Server container.
- Build 0/0 on the solution; the new test is green.

End-to-end evidence AC: `dotnet run --project Athena.Worker -- --migrate-only` (or equivalent) against a fresh SQL Server container completes with exit 0 and no pending EF Core migrations remain.

---

### PRISMA-E1-S3 — Config Externalization: Remove All Hardcoded Machine-Specific Values

**Traceability:** Closes P3 (Web.UI hardcodes `DESKTOP-FB2ES22` and `(localdb)`), P4 (SIARA URL hardcoded `localhost:5002`), P5 (Windows path `C:\\SiaraData\\logs\\` in simulator Production config), RC6 W0.11
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Remove all machine-specific and path-specific hardcoded values from committed appsettings files in the Web.UI and Siara Simulator. Replace with env-var references. Produce a `.env.example` file documenting all required environment variables.

Context:
- P3 evidence: `07 UI/.../appsettings.Development.json:4-5` contains `Server=DESKTOP-FB2ES22\SQL2022`; `appsettings.json:4` contains `(localdb)\MSSQLLocalDB`; `UI/Program.cs:228` throws if `DefaultConnection` is missing.
- P4 evidence: `Web.UI/appsettings.json:15` sets `"SiaraUrl": "https://localhost:5002"`.
- P5 evidence: `tools/Siara.Simulator/appsettings.Production.json:33` and `Prisma/Deployments/Siara.Simulator/app/appsettings.Production.json:33` both contain `C:\\SiaraData\\logs\\`.
- Workers already use `DEV-PLACEHOLDER` correctly (confirmed in RC5 "Resolved" section); only Web.UI and the simulator configs need fixing.
- Pattern: replace string literals with `${ENV_VAR_NAME}` or `""` (empty, requiring override) + ASP.NET Core env-var override convention (`ConnectionStrings__DefaultConnection`, `SiaraUrl`, `SIARA_LOG_PATH`).

Constraints:
- After the change, `dotnet run` in the development environment must still work using the `ASPNETCORE_ENVIRONMENT=Development` + env-var pattern (document in `.env.example`).
- The `.env.example` must document: `ConnectionStrings__DefaultConnection`, `SiaraUrl`, `SIARA_LOG_PATH`, and any other env-var dependencies surfaced during the sweep.
- Do NOT hardcode any other machine name, IP, or Windows absolute path in the committed files.

Definition of Done:
- `appsettings.json` and `appsettings.Development.json` for Web.UI contain no `DESKTOP-FB2ES22` or `(localdb)` strings (verified by `git grep`).
- `appsettings.Production.json` for both Siara Simulator copies contain no `C:\` absolute path.
- `.env.example` file committed at the repo root listing all required env vars with safe placeholder values.
- Build 0/0 on the solution.

End-to-end evidence AC: `git grep -r "DESKTOP-FB2ES22" -- "*.json"` returns zero matches; `git grep -r "localdb" -- "*.json"` returns zero matches in committed production/development appsettings; `git grep -r "C:\\\\SiaraData" -- "*.json"` returns zero matches.

---

### PRISMA-E1-S4 — Wire Real Readiness Probes: Reconciliator and Web.UI

**Traceability:** Closes P9 (Reconciliator health probes hardcoded stub), P10 (Web.UI health probe hardcoded stub), RC6 W0.12
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Replace the hardcoded stub health probes in the Reconciliator worker and Web.UI with real `IHealthCheck` / `IReadinessProbe` implementations. Wire them into the existing `/health` and `/health/ready` endpoints.

Context:
- P9 evidence: `Reconciliator/Program.cs:113-114` — hardcoded constant `"Healthy"` response; no `IReadinessProbe` wired in the Reconciliator DI root.
- P10 evidence: `HealthCheckController.cs:39` — hardcoded `"Healthy"`; `Web.UI/Program.cs:114` has `AddHealthChecks()` but no domain-specific `IHealthCheck` implementations registered.
- Reference pattern: Athena and Orion have real readiness probes — `ExtractionPipelineService.IsStarted` and `SiaraWatchLoop.IsRunning` (confirmed DONE in RC5 §3); mirror this pattern.
- Reconciliator probe: should return `Unhealthy` (503) until the `ReconciliationPipelineService` is subscribed and its observable is active.
- Web.UI probe: must check DB connectivity (call `_dbContext.Database.CanConnectAsync()`) and return 503 if the DB is unreachable; also check that ASP.NET Core Identity is properly initialized.

Constraints:
- `IHealthCheck.CheckHealthAsync` must propagate `CancellationToken` through all downstream async calls.
- Return `HealthCheckResult.Unhealthy(...)` with a descriptive reason string (not an exception) for observability.
- The `/health/ready` endpoint must return HTTP 503 when unhealthy (use `RequireHost` + `AllowAnonymous` pattern consistent with Athena/Orion).
- Single-project builds: build `Reconciliator.Worker.csproj` and `Web.UI.csproj` individually and confirm 0/0 before declaring done.

Definition of Done:
- `Reconciliator/Program.cs` registers a real `IReadinessProbe` and wires it into `MapHealthChecks("/health/ready")`.
- `Web.UI/Program.cs` registers at minimum a DB-connectivity `IHealthCheck`; `MapHealthChecks("/health")` returns real state.
- An integration test (existing or new in `Tests.System.Storage` or `Tests.UI`) asserts that `/health/ready` returns 503 when the pipeline is not subscribed and 200 when it is.
- Build 0/0 on both projects; the test is green.

End-to-end evidence AC: `GET /health/ready` on a Reconciliator process that has NOT yet subscribed its pipeline returns HTTP 503; after the pipeline starts, the same endpoint returns HTTP 200 with body `"Healthy"`.

---

### PRISMA-E1-S5 — Author Full-Stack docker-compose.dev.yml

**Traceability:** Closes D9 (no docker-compose for full local-dev stack), RC6 W0.13
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Author a `docker-compose.dev.yml` file at the repo root wiring all six components of the Prisma stack: Orion Worker, Athena Worker, Reconciliator Worker, Web.UI, SIARA Simulator, SQL Server, and Seq (structured logging). All six must share a correct inter-process Docker network and a shared storage volume for document files.

Context:
- D9 evidence: RC5d §1.3 — "Developer cannot stand up the full stack with one command"; no `docker-compose.dev.yml` exists (verified by glob).
- SIARA Simulator already has a Docker artifact at `Prisma/Deployments/Siara.Simulator/`; reference this image.
- The three workers coordinate over IndFusion.Ember SignalR hubs — the compose file must ensure worker-to-hub TCP connectivity via the Docker network (not `localhost`).
- Seq image: `datalust/seq:latest`; expose port 5341 for the UI.
- SQL Server image: `mcr.microsoft.com/mssql/server:2022-latest` with `SA_PASSWORD` env var.
- Each service must reference env vars from a `.env` file (pattern from PRISMA-E1-S3 `.env.example`).
- The `depends_on` + `healthcheck` fields must enforce startup order: SQL Server healthy → workers start.

Constraints:
- No hardcoded passwords or connection strings in the compose file itself; all sensitive values via `.env`.
- The shared document-file volume must be named and mounted consistently across Orion (write) and Athena/Reconciliator (read).
- A single `docker-compose -f docker-compose.dev.yml up` must reach a state where `GET /health` on Web.UI returns 200.

Definition of Done:
- `docker-compose.dev.yml` committed at the repo root.
- All six services defined with correct image references, env-var wiring, network, and volume mounts.
- `docker-compose up` on a clean Linux machine with a populated `.env` file brings all six services to healthy state (verified by `docker-compose ps` showing all services `Up (healthy)`).
- Build 0/0 on the solution (the compose file does not affect the C# build, but confirm no regressions).

End-to-end evidence AC: `docker-compose -f docker-compose.dev.yml up -d && docker-compose ps` shows all six services with status `Up (healthy)` within 3 minutes on a machine with the required images cached.

---

### PRISMA-E1-S6 — Add Serilog Remote Log Sinks to All Three Worker Configs

**Traceability:** Closes D8 (workers log to console only; no structured remote sink), D7 (Seq endpoint hardcoded to localhost in Web.UI), RC6 W0.14
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Add Serilog file and/or Seq remote sink configuration to the `appsettings.json` of all three Prisma workers (Orion, Athena, Reconciliator). Also replace the hardcoded `http://localhost:5341` Seq URLs in `Web.UI/appsettings.json:73,92` with env-var references. Document all required observability env vars in the `.env.example` (coordinate with PRISMA-E1-S3 if run in the same pass).

Context:
- D8 evidence: RC5d §1.6 — "Workers have no structured remote log sink in production"; no Serilog file or Seq sink in any worker's `appsettings.json`.
- D7 evidence: `Web.UI/appsettings.json:73,92` hardcode `http://localhost:5341` for Serilog Seq sink and OTLP endpoint; no env-var override documented.
- Serilog configuration pattern: add a `"Serilog"` section with `"WriteTo"` containing both a `"Console"` sink and a `"Seq"` sink whose `"serverUrl"` reads from `${SEQ_URL}` via ASP.NET Core config substitution.
- Log-level minimums: set `"Default": "Information"` and `"Microsoft.AspNetCore": "Warning"` for production noise reduction.
- Workers already use Serilog (confirmed from RC5a) — only the sink configuration is missing.

Constraints:
- Do NOT hardcode any URL, IP, or port in committed appsettings; use env-var substitution.
- `SEQ_URL` and `OTLP_ENDPOINT` must be added to `.env.example`.
- A test (manual or automated) must verify that a log line written during a real pipeline run appears in Seq when `SEQ_URL` points to a running Seq instance.

Definition of Done:
- All three worker `appsettings.json` files have a `"Serilog"` section with both console and Seq sinks using env-var URL references.
- `Web.UI/appsettings.json` lines 73 and 92 contain no `localhost:5341` literal.
- `.env.example` documents `SEQ_URL` and `OTLP_ENDPOINT`.
- Build 0/0 on the solution.

End-to-end evidence AC: With `SEQ_URL=http://seq:5341` (docker-compose service name), a worker started via `docker-compose` emits structured log events visible in the Seq UI within 30 seconds of startup.

---

### PRISMA-E1-S7 — Wire Worker Dashboard Metrics (Replace Hardcoded Zeros)

**Traceability:** Closes D12 (worker dashboard metrics return hardcoded zeros), RC6 W1.14, U16
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Wire `RecordDocumentProcessed()` calls into `IngestionOrchestrator.IngestCaseAsync` (Orion) and the relevant stage-completion point in `ExtractionPipelineService` (Athena). Remove the hardcoded-zero stubs in `AthenaDashboardService` and `OrionDashboardService`.

Context:
- D12 evidence: `AthenaDashboardService.cs:46-57` and `OrionDashboardService.cs:46-57` return `QueueDepth:0` and a never-incremented document counter; `RecordDocumentProcessed()` is defined but never called from any orchestrator.
- The goal is that after processing a document, the `/dashboard` endpoint for that worker returns an incremented count (not zero).
- Two implementation approaches: (a) call `RecordDocumentProcessed()` from the orchestrators at the correct completion point; (b) replace the in-memory counter with a query to the real `IProcessingMetricsService` (already confirmed real in RC5c). Prefer approach (b) if `IProcessingMetricsService` is DI-accessible from the dashboard services, as it avoids duplicating counter state.
- `IProcessingMetricsService` location: confirmed real in RC5c §Dashboard metrics — `ProcessingMetricsService.cs:43-44`.

Constraints:
- Result<T> pattern on any new service method.
- CancellationToken must be threaded through.
- No new external dependencies.
- The test project to use: extend the existing `Tests.EndToEnd` or `MaxFidelityGateFullPipelineE2ETests` to assert that after a document is processed, `GET /dashboard` on the Athena worker returns a non-zero document count.

Definition of Done:
- `AthenaDashboardService` and `OrionDashboardService` no longer return hardcoded zeros; the count reflects the actual pipeline throughput.
- At least one integration/E2E test asserts `GET /dashboard` returns `documentCount > 0` after processing one document.
- Build 0/0 on both worker projects; the test is green.

End-to-end evidence AC: After the max-fidelity E2E gate processes one document, `GET /dashboard` on the Athena worker returns JSON with a document count of at least 1 (not zero).

---

## PRISMA-E2 — Real-Process and Observability Proof

**Wave:** W1 (Operational Readiness)
**Goal:** the three-process Prisma split is proven over real TCP with real OCR in a single test run; the system has a documented production operational runbook; CI produces publishable Docker images; latent failure modes are regression-covered; Sentinel's status is known and addressed.
**Gate:** buildable-now (except D13 metrics persistence, which is a design decision)
**Gaps covered:** P6, P12, P16, D10, D11, D13, D20, D21, D22, D24

### PRISMA-E2-S1 — Real-TCP Cross-Process Acceptance Test

**Traceability:** Closes P6 (real-TCP/real-OS-process cross-process wire never proven alongside real pipeline), D10 (in-memory SignalR transport — TCP reconnect/back-pressure unproven), RC6 W1.12
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Author an integration test (or acceptance scenario) that boots three real OS processes — Orion Worker, Athena Worker, and Reconciliator Worker — on real TCP ports (not ASP.NET in-memory TestServer), drives a document through the full pipeline against the SIARA Simulator, and asserts cross-process events arrive and produce a SIRO XML artifact in the database.

Context:
- P6 evidence: `MaxFidelityGateFullPipelineE2ETests.cs:40-41` uses in-memory transport; RC5b §Biggest readiness gaps #1; RC5c §Biggest readiness gaps #2; ADR-012 limitation #4 — "real TCP between OS processes ... is proven separately by `HubWireTests` but NEVER in combination with real OCR + real SQL + the full pipeline."
- `HubWireTests` already proves real TCP connectivity in isolation; the gap is the COMBINATION with real OCR + real SQL + the full 3-process pipeline.
- D10 evidence: RC5b §A3/A4 caveats — reconnect/retry under real TCP has never been proven; add a sub-scenario that disconnects one hub client mid-run and verifies Orion queues or retries the event.
- Recommended approach: use `Process.Start` (or `CliWrap`) from the test project to launch the three worker executables with test-environment config (Testcontainers SQL, local SIARA Simulator on a fixed port, Seq on a fixed port). Assert the SIRO XML file is written to the shared volume directory.
- Test project location: add to the existing `Tests.EndToEnd` project or a new `Tests.RealTcp` project that mirrors the `Tests.EndToEnd` layout.

Constraints:
- The test must use a Testcontainers SQL Server container (not LocalDB).
- The SIARA Simulator must be running against `tools/Siara.Simulator` (not the live portal — `Siara:AllowProductionHost=false`).
- No stubs on the pipeline data path.
- Test timeout: set to 10 minutes minimum (the in-memory gate took 21 minutes; real TCP adds overhead).
- CancellationToken from `TestContext.Current.CancellationToken`.

Definition of Done:
- A new test class with at least one `[Fact]` that: starts 3 real OS processes on real TCP, submits a document via the SIARA Simulator, and asserts a SIRO XML artifact exists in the configured output location within the timeout.
- A second `[Fact]` covering the reconnect scenario (D10): disconnects the Athena hub client mid-run and verifies Orion retries.
- Both tests green in the CI run.
- Build 0/0 on the test project.

End-to-end evidence AC: Both test facts pass in CI against Testcontainers SQL + real TCP + SIARA Simulator with zero stubs on the pipeline. The SIRO XML artifact is present in the output location. The reconnect test verifies event delivery is not lost on client disconnect.

---

### PRISMA-E2-S2 — Production Operational Runbook

**Traceability:** Closes P12 (no operational runbook), RC6 W1.13
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Author a production operational runbook at `docs/operations/PRISMA-PRODUCTION-RUNBOOK.md` covering: worker startup/restart sequence, migration execution order (three DbContext types — document the specific CLI commands), SIARA credential rotation, pipeline failure triage (Athena unreachable, Tesseract failure, SIARA 503), database backup/recovery, and on-call escalation path.

Context:
- P12 evidence: RC5d §1.6 — only `docs/demos/CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` exists (demo runbook, not production ops).
- The multi-DbContext migration order (P2 / PRISMA-E1-S2) must be documented here as the definitive procedure: `PrismaDbContext` first, `ApplicationDbContext` second, `ExportAdaptiveDbContext` third. Explain why that order is required (FK dependencies).
- SIARA credential rotation procedure must reference the vault integration (PRISMA-E3-S2) when it exists, and document the manual fallback path until then.
- Pipeline failure triage must cover the four scenarios in D20 (Athena unreachable, Tesseract failure, SIARA 503, Reconciliator restart) and the Tesseract second-init deadlock (D21).
- On-call escalation path is a placeholder section that names where the paging configuration lives (PRISMA-E3-S6) — it does not need to be finalized until alerting is wired, but the section must exist as a placeholder.

Constraints:
- Plain Markdown; no diagrams required (keep it maintainable).
- All CLI commands must be copy-pasteable and correct for the production environment.
- The runbook must be reviewed by at least one person with access to the production environment before it is considered "verified" (add a `## Review Status` header with the reviewer's name and date).

Definition of Done:
- `docs/operations/PRISMA-PRODUCTION-RUNBOOK.md` committed covering all six topic areas listed above.
- The migration order section includes verbatim CLI commands for all three DbContext types.
- The SIARA credential rotation section exists (with vault placeholder or manual procedure).
- Build 0/0 (docs-only change; build must still be clean).

End-to-end evidence AC: A new engineer with repo access can follow the runbook from scratch to stand up the full stack against the SIARA Simulator and process one document without additional verbal instructions.

---

### PRISMA-E2-S3 — Trace and Classify Sentinel Service

**Traceability:** Closes P16 (Sentinel service is a black box), RC6 W1.17, U11
**Gate tag:** buildable-now (tracing phase); follow-up story may be technical or out-of-MVP depending on findings
**Effort:** L

**Subagent brief**

Task: Locate and read the Sentinel service composition root (`Prisma.Sentinel.Worker/Program.cs` or equivalent). Classify its actual status: (a) functional and wired — in which case, wire health probes, observability, and auth; (b) partial scaffold — classify exactly what is present; (c) empty scaffolding with no domain logic — decide whether it is in MVP scope and document the decision. Write findings to `docs/operations/SENTINEL-STATUS-2026-06.md`.

Context:
- P16 evidence: RC5d §Unk-1; CLAUDE.md "not yet traced; status unknown"; no `Prisma.Sentinel.Worker/Program.cs` found in the composition-root scan. The Sentinel service is listed in the CLAUDE.md services table as "not yet traced."
- U11 (RC6): "No startup code, auth, health checks, or observability artifacts found for Sentinel."
- If Sentinel is functional: apply the same real health-probe pattern as Athena/Orion (PRISMA-E1-S4 reference). Add JWT auth (`Siara:AllowProductionHost` gate does not apply to Sentinel). Add Serilog Seq sink (PRISMA-E1-S6 pattern).
- If Sentinel is empty scaffolding: document the out-of-MVP decision and add a comment in the services table in CLAUDE.md pointing to the status doc.

Constraints:
- Read-only tracing first; do NOT modify Sentinel code until the classification is confirmed.
- All findings must cite file:line evidence.
- If health probes and auth are added (case a), they must follow the same patterns as Athena/Orion.
- If a test project for Sentinel exists, it must be green after any changes; build the Sentinel project individually (0/0) before and after.

Definition of Done:
- `docs/operations/SENTINEL-STATUS-2026-06.md` committed with a classification (a/b/c) and file:line evidence for the classification.
- If functional: health probes, Serilog sink, and JWT auth wired; the Sentinel project builds 0/0; at least one test (even a smoke test) exercises the health endpoint.
- If scaffolding only: CLAUDE.md services table updated with an "out-of-MVP" note linking to the status doc.
- Build 0/0 on the solution.

End-to-end evidence AC: The status document exists with a definitive classification backed by file:line evidence. If Sentinel is functional, `GET /health` returns a real (non-stub) response. If out-of-MVP, the decision is documented and linked from CLAUDE.md.

---

### PRISMA-E2-S4 — Reproduce and Fix Tesseract Second-Init Deadlock

**Traceability:** Closes D21 (Tesseract second-init deadlock under multi-worker restart), RC6 W1.16, U13
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Reproduce the Tesseract second-init deadlock that causes `MaxFidelityGatePartialCaseE2ETests` to disable the extraction pipeline. Fix the Tesseract lifecycle management so the pipeline can restart without a full process restart. Add a regression test that verifies restart safety.

Context:
- D21 evidence: RC5d §Unk-3; `MaxFidelityGatePartialCaseE2ETests.cs` — the pipeline is explicitly disabled in that test class to avoid the deadlock. This means any production Athena restart without a full process restart may deadlock.
- The root cause is likely Tesseract being initialized more than once in the same process (Tesseract.NET has a known constraint that `TesseractEngine` must not be created twice in the same process without full disposal). The fix: initialize `TesseractEngine` once at startup in a static or singleton context; never create a second instance in the same process lifetime.
- The `TesseractOcrExecutor` is the engine of record (confirmed real in CLAUDE.md); examine its registration lifetime and construction site.
- After the fix, re-enable the disabled test scenario in `MaxFidelityGatePartialCaseE2ETests` and confirm it passes.

Constraints:
- The fix must not change Tesseract from singleton-per-process. `TesseractEngine` must be registered as a singleton in the DI container; verify this is the case or change it.
- Result<T> pattern on any error paths in the executor.
- CancellationToken propagation maintained.
- Build single project `Athena.Worker.csproj` (0/0) and run `Tests.EndToEnd` (all tests green including the previously-disabled one).

Definition of Done:
- `TesseractOcrExecutor` is registered as a singleton; second-init deadlock is eliminated.
- The previously-disabled test scenario in `MaxFidelityGatePartialCaseE2ETests` is re-enabled and green.
- A new regression test explicitly restarts the Athena pipeline (without a process restart) and confirms subsequent OCR calls succeed.
- Build 0/0 on `Athena.Worker.csproj`; all tests in `Tests.EndToEnd` green.

End-to-end evidence AC: `dotnet test Tests.EndToEnd.csproj` passes all tests including the previously-skipped restart scenario, with zero deadlock occurrences.

---

### PRISMA-E2-S5 — Audit All LogAuditAsync Call Sites for ProcessId Adoption

**Traceability:** Closes D22 (`ProcessId` audit column adoption incomplete), RC6 W1.15, U17
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Grep all `LogAuditAsync` call sites across `IngestionOrchestrator`, `ProcessingOrchestrator`, and `ReconciliationPipelineService`. Verify each call passes the `processId` parameter from the injected `ISiaraActorIdentityProvider`. Fix any call sites where `processId` is omitted or passed as null. Add a process-identity assertion to the max-fidelity gate's audit-row check.

Context:
- D22 evidence: RC5d §Unk-7; RC5d §3.6 S23 — "ADR-012 added `processId` to `IAuditLogger.LogAuditAsync` as a backward-compatible optional parameter, but call-site adoption across `IngestionOrchestrator`, `ProcessingOrchestrator`, and `ReconciliationPipelineService` was not verified."
- `ISiaraActorIdentityProvider` is already injected in all three workers (confirmed in RC5 §Resolved for A6). The fix is call-site wiring, not a new interface.
- After fixing call sites: add an assertion to the existing `MaxFidelityGateFullPipelineE2ETests` that reads the AuditRecords table after the pipeline run and asserts every row has a non-null `ProcessId` column.

Constraints:
- Use `git grep` (or the Grep tool) to find ALL `LogAuditAsync` call sites — do not manually enumerate.
- Fix must not change the method signature; only add the `processId:` named argument at call sites where it is missing.
- The audit assertion must query the Testcontainers SQL database (not mock data).

Definition of Done:
- Every `LogAuditAsync` call site in the three orchestrators passes a non-null `processId` from `ISiaraActorIdentityProvider`.
- `MaxFidelityGateFullPipelineE2ETests` includes an assertion that all audit rows in the DB have non-null `ProcessId` after the pipeline run.
- Build 0/0; the E2E test is green.

End-to-end evidence AC: After the max-fidelity gate runs, a SQL query `SELECT COUNT(*) FROM AuditRecords WHERE ProcessId IS NULL` returns 0.

---

### PRISMA-E2-S6 — Failure-Mode Integration Test Suite

**Traceability:** Closes D20 (no chaos/failure-mode regression tests), RC6 W1.18, U18
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Add a failure-mode integration test suite covering four scenarios: (a) Athena hub unreachable mid-document — assert Orion retries or queues; (b) Tesseract failure — assert best-effort handoff still produces a partial expediente; (c) SIARA portal returns 503 — assert watch loop backs off and retries; (d) Reconciliator pod restart mid-handoff — assert the handoff artifact is durable.

Context:
- D20 evidence: RC5d §Unk-8 — "InsufficientData / best-effort on failure is asserted in documentation but not covered by any regression test at the pipeline boundary."
- Tesseract OCR failure scenario: configure `TesseractOcrExecutor` with an invalid model path in the test; assert the pipeline returns a partial expediente (not an unhandled exception) — this exercises the `Result<T>` error path.
- SIARA 503 scenario: the SIARA Simulator should be configurable to return 503 for specific document URLs; if not, mock the HTTP layer at the `IDocumentDownloader` boundary (this is the only permitted stub in this test — it simulates the external SIARA portal; the rest of the pipeline must be real).
- Reconciliator restart scenario: use the same OS-process-restart approach as PRISMA-E2-S1; kill and restart the Reconciliator process mid-handoff and assert the artifact is not lost.
- Test project: add to `Tests.EndToEnd` or a new `Tests.FailureModes` project.

Constraints:
- Scenarios (a), (b), (d) must use real infrastructure (Testcontainers SQL, real TCP — leveraging PRISMA-E2-S1 patterns).
- Only scenario (c) permits a stub at the external HTTP boundary; all other boundaries must be real.
- CancellationToken from `TestContext.Current.CancellationToken`.
- Each scenario must be a separate `[Fact]` with a descriptive method name following `MethodUnderTest_Scenario_ExpectedBehavior` convention.

Definition of Done:
- Four new `[Fact]` methods, one per scenario, all passing.
- Scenario (b) verifies a partial expediente object (not null, not an exception) is returned when Tesseract fails.
- Scenario (c) verifies the watch loop retries with exponential back-off (assert at least one retry log message).
- Scenario (d) verifies the SIRO XML artifact is present in the output location after Reconciliator restart.
- Build 0/0; all four tests green.

End-to-end evidence AC: `dotnet test Tests.FailureModes.csproj` (or equivalent) reports 4/4 tests passed with no unhandled exceptions in any failure scenario.

---

### PRISMA-E2-S7 — SIARA Session-Longevity Soak Test

**Traceability:** Closes D11 (SIARA session longevity under production volume not proven), RC6 W2.9, U20
**Gate tag:** buildable-now (uses simulator, not live portal)
**Effort:** M

**Subagent brief**

Task: Author a session-longevity soak test that submits 50 sequential documents through the SIARA Simulator via the real `SiaraWatchLoop` + `SiaraDocumentDownloader` path. Assert: session is refreshed on expiry without a restart; all 50 documents are processed to completion; throughput meets the documented cadence target (≤2,000 documents/day).

Context:
- D11 evidence: RC5b §A2 caveat — "one-warm-session longevity across long runs unproven"; RC5c §Biggest readiness gaps #2.
- The SIARA Simulator (`tools/Siara.Simulator`) models SIARA's links-on-a-page shape; configure it to serve 50 distinct document links for this test.
- Session expiry simulation: if the Simulator supports a session-TTL configuration, set it to 30 seconds and verify `SiaraDocumentDownloader` re-authenticates transparently. If not, check for a session-refresh code path in the downloader and assert it is exercised when the session cookie expires.
- Throughput assertion: 50 documents ÷ 2,000/day ≈ 43 seconds per document max; assert total run time is under 2,200 seconds (50 × 43 + 10% headroom) for the soak test.

Constraints:
- `Siara:AllowProductionHost=false` must be enforced (simulator only, never the live portal).
- Testcontainers SQL Server for persistence.
- CancellationToken from `TestContext.Current.CancellationToken` with a 45-minute timeout.
- This test is expected to be slow — tag it with `[Trait("category", "slow")]` and exclude it from the standard fast CI gate; it runs in a dedicated nightly or pre-release gate.

Definition of Done:
- A `[Fact]` soak test submits 50 documents and asserts all 50 reach `Processed` status in the database.
- The test asserts at least one session-refresh event was logged (session was renewed mid-run).
- Total run time assertion logged (not failing if slow on first run, but documented).
- Build 0/0 on the test project.

End-to-end evidence AC: `dotnet test --filter-query "[category=slow]"` produces 50/50 documents with status `Processed` in the Testcontainers SQL database; at least one session-refresh log message is present in the test output.

---

### PRISMA-E2-S8 — Extend CI Pipeline with Docker Image Build, Publish, and Push

**Traceability:** Closes D24 (no CI deploy pipeline; quality gate only), RC6 W1.19
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Extend `Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` with a new `publish` job that runs after the existing build+test+security-scan job. The publish job must: run `dotnet publish` for all four production hosts, build a Docker image per host using the Dockerfiles from PRISMA-E1-S1, tag each image with the git SHA, and push to a configured container registry (use a `REGISTRY_URL` GitHub Actions secret; if not yet provisioned, push to GitHub Container Registry `ghcr.io` as a default).

Context:
- D24 evidence: RC5d §1.3 — `quality-gates.yml` has no publish, image-tag, or deploy step; "No automated release path."
- The publish step must reference the Dockerfiles authored in PRISMA-E1-S1.
- Image tags: use both `git rev-parse --short HEAD` (SHA) and `latest` on the main branch.
- The push step should only execute on pushes to `main` (or the configured release branch), not on every PR, to avoid registry pollution.
- Add a `docker scan` or `trivy` image-scan step before push to catch critical CVEs in the published images.

Constraints:
- The publish job must depend on the existing build+test job completing successfully (use `needs:` in the workflow).
- No hardcoded registry credentials in the YAML; use GitHub Actions secrets (`REGISTRY_USERNAME`, `REGISTRY_PASSWORD` or `GITHUB_TOKEN` for ghcr.io).
- If PRISMA-E1-S1 is not yet merged, this story is blocked on it — add `depends_on: PRISMA-E1-S1` to the dependency tracking.

Definition of Done:
- `quality-gates.yml` has a new `publish` job that runs `dotnet publish`, builds four Docker images, scans them, and pushes to the registry on pushes to main.
- The workflow passes on the `Liv` branch (or a test branch) in CI with zero errors.
- Build 0/0 on the solution (CI YAML changes do not affect the C# build).

End-to-end evidence AC: A git push to the release branch triggers the CI pipeline; the `publish` job completes with four Docker images tagged with the git SHA visible in the container registry.

---

### PRISMA-E2-S9 — ProcessingMetricsService Persistence Decision and Implementation

**Traceability:** Closes D13 (ProcessingMetricsService in-memory only; metrics reset on restart)
**Gate tag:** buildable-now (engineering decision; no external gate)
**Effort:** M

**Subagent brief**

Task: Evaluate the `ProcessingMetricsService` (confirmed real in-memory in RC5c, `ProcessingMetricsService.cs:43-44`) and make a documented decision: (a) in-memory is sufficient for MVP and is explicitly accepted as a known limitation; or (b) historical aggregate persistence is required, in which case add a time-series snapshot entity + EF migration. Document the decision in a short ADR stub (`docs/architecture/adr/ADR-013-metrics-persistence.md`).

Context:
- D13 evidence: `ProcessingMetricsService.cs:43-44` uses `ConcurrentDictionary`/`ConcurrentQueue` with no DB persistence. Historical metrics and SLA trend views are unavailable after any process restart.
- The Web.UI Dashboard reads this service in real time (confirmed working in RC5c) — the in-memory approach is functional for the current session but loses history on restart.
- If persistence is chosen: add an `IMetricsSnapshotRepository` EF entity; call `TakeSnapshotAsync()` on a periodic timer (e.g., every 5 minutes) from `ProcessingMetricsService`; store in `PrismaDbContext`.
- If in-memory is accepted: add a clear comment in `ProcessingMetricsService.cs` and document the limitation in the ADR stub.

Constraints:
- The decision must be documented in writing (ADR stub or inline comment) — not left implicit.
- If persistence is implemented: Result<T> pattern; CancellationToken propagated; one integration test (Testcontainers SQL) verifies snapshots survive a service restart.
- If in-memory only: no code changes except the comment; the ADR stub is the deliverable.

Definition of Done:
- `docs/architecture/adr/ADR-013-metrics-persistence.md` committed with the decision and rationale.
- If persistence chosen: EF entity + migration present; integration test green; build 0/0.
- If in-memory accepted: comment in `ProcessingMetricsService.cs` referencing the ADR; build 0/0.

End-to-end evidence AC: If persistence implemented — a Testcontainers SQL integration test verifies metrics survive a simulated service restart. If in-memory accepted — the ADR is committed and the inline comment exists; build 0/0.

---

## PRISMA-E3 — Security Hardening

**Wave:** W2 (Audit Immutability), W3 (Security Hardening)
**Goal:** the Prisma pipeline meets the security baseline required for a regulated bank deployment: immutable audit trail, per-process asymmetric JWT keys, field-level PII encryption at rest, vault-backed SIARA credential management, network segmentation, and documented compliance trajectory.
**Gate:** several stories are business-gated or ops-gated; all are written as full engineering specs with BLOCKED markers where the unlock is non-engineering. This epic is flagged for a dedicated separate security review pass (per locked decisions).
**Gaps covered:** P8, P11, P13, P14, P15, D15, D16, D17, D18, D23

### PRISMA-E3-S1 — Convert AuditRecords to Immutable Ledger Table

**Traceability:** Closes P15 (audit trail not immutable), RC6 W2.6
**Gate tag:** buildable-now (SQL Server 2022 feature; no business gate)
**Effort:** M

**Subagent brief**

Task: Convert the `AuditRecords` table in `PrismaDbContext` to a SQL Server 2022 append-only ledger table (`LEDGER = ON, APPEND_ONLY = ON`) or, as a fallback for environments that do not support SQL ledger, add a `DENY UPDATE, DELETE` grant to the application login and implement row-level HMAC chaining. Document a 7-year retention policy.

Context:
- P15 evidence: RC5d §3.6 S24 — "`AuditRecords` is a standard EF Core table — no temporal tables, no `DENY UPDATE/DELETE`, no cryptographic tamper-evidence, no HMAC chaining"; ADR-012 §Consequences accepts this for MVP but it blocks regulated production.
- SQL Server 2022 ledger tables: use `CREATE LEDGER TABLE` syntax or EF Core migration with raw SQL for the `LEDGER = ON` option (EF Core does not have first-class ledger support as of .NET 10; use `migrationBuilder.Sql()`).
- HMAC chaining: if ledger is not available, add a `PreviousRowHmac` column; compute `HMAC-SHA256(rowContent + previousHmac)` in the `IAuditLogger` before writing; store the chain seed in a separate protected table.
- 7-year retention policy: document in `docs/operations/DATA-RETENTION-POLICY.md`; add a startup check that warns if the SQL Server version does not support ledger.

Constraints:
- EF Core migration must be additive (no data loss on existing `AuditRecords` rows).
- The migration must be tested against a Testcontainers SQL Server 2022 container.
- After the migration, `UPDATE AuditRecords SET ...` must fail at the database level (verified in the integration test).
- Build `PrismaDbContext`-containing project (0/0) after migration.

Definition of Done:
- EF Core migration converting `AuditRecords` to a ledger or equivalent immutable structure is committed.
- An integration test (Testcontainers SQL Server 2022) verifies: (a) an audit record can be inserted; (b) an `UPDATE` on that record fails at the database level; (c) a `DELETE` on that record fails at the database level.
- `docs/operations/DATA-RETENTION-POLICY.md` committed with the 7-year policy statement.
- Build 0/0.

End-to-end evidence AC: `UPDATE AuditRecords SET EventType='TAMPERED' WHERE Id=1` executed against the Testcontainers SQL instance throws a SQL exception (not a silent no-op); the integration test asserts this behavior.

---

### PRISMA-E3-S2 — Implement Per-Process Asymmetric JWT Keys

**Traceability:** Closes P13 (symmetric HMAC shared JWT secret), D17 (no secret rotation mechanism), RC6 W3.6, W3.9, U15
**Gate tag:** business-gated (key provisioning decision) — **BLOCKED on: owner decision on RS256 vs ECDSA and Key Vault provisioning (PRISMA-E3-S5)**
**Effort:** M

**BLOCKED — unlock required:** Owner must decide on the asymmetric key algorithm (RS256 or ECDSA) and confirm the Key Vault (or equivalent) provisioning target before engineering begins. The engineering spec below is complete and ready to execute once unblocked.

**Subagent brief** (execute when unblocked)

Task: Replace the shared HMAC `JwtSecret` in all three worker `appsettings.json` files with per-process asymmetric key pairs (RS256 or ECDSA). Each process signs with its own private key; peer processes verify with the signing process's public key. Remove the shared `ProcessIdentity:JwtSecret` placeholder. Implement overlapping-key grace period in `IProcessClearanceTokenService` for zero-downtime rotation. Update ADR-012 OD-1 through OD-7.

Context:
- P13 evidence: RC5d §3.1 S1; `Athena/Orion/appsettings.json:12` all carry identical `JwtSecret` placeholder; any process holding the secret can mint any clearance token — this violates the 3-process isolation model.
- D17 evidence: RC5d §1.2 S11 — no rolling-rotation support; rotating the shared secret requires coordinated simultaneous re-deploy of all three workers.
- `IProcessClearanceTokenService` is the signing boundary; update it to use `RSA` or `ECDsa` from a key loaded via the vault provider (PRISMA-E3-S5).
- Overlapping-key grace period: `IProcessClearanceTokenService` should accept tokens signed by either the current or previous key pair during a configurable grace window (e.g., 5 minutes); old keys are rotated out after the grace window expires.
- ADR-012 update: document the new key isolation model, the rotation procedure, and the grace-period mechanism.

Constraints:
- Result<T> pattern on all new methods; CancellationToken propagated.
- No new external dependencies beyond what the vault provider supplies.
- The existing max-fidelity E2E gate (`MaxFidelityGateFullPipelineE2ETests`) must remain green after the change (update its test configuration to use test key pairs).
- Build all three worker projects (0/0) after the change.

Definition of Done:
- `ProcessIdentity:JwtSecret` removed from all three worker configs.
- Each process loads its private key from the configured vault provider (or from test key material in the test environment).
- `IProcessClearanceTokenService` signs with the process's private key and verifies with peer public keys.
- Overlapping-key grace period implemented and tested.
- ADR-012 updated.
- All three worker projects build 0/0; max-fidelity E2E gate remains green.

End-to-end evidence AC: The max-fidelity E2E gate passes with asymmetric keys and a key-rotation scenario (swap the Orion signing key mid-test; assert Athena still accepts tokens signed by the new key after the grace period).

---

### PRISMA-E3-S3 — Field-Level AES-GCM PII Encryption at Rest

**Traceability:** Closes P14 (no field-level encryption at rest for legal PII), RC6 W3.8
**Gate tag:** business-gated (data classification + key management decision) — **BLOCKED on: owner data-classification decision identifying which expediente fields are regulated PII, and Key Vault provisioning (PRISMA-E3-S5)**
**Effort:** L

**BLOCKED — unlock required:** Owner must provide a written data-classification decision identifying which `PrismaDbContext` entity fields are regulated PII (RFC, CLABE, party names, case numbers, etc.) and confirm the AES-GCM key custody target (Key Vault or equivalent). Engineering spec below is complete and ready to execute when unblocked.

**Subagent brief** (execute when unblocked)

Task: Apply AES-GCM column encryption to all classified PII fields in `PrismaDbContext` entities. Reuse or extend the Veriqan AES-256 column encryption pattern (adapted to AES-GCM for authenticated encryption). Provision a Key Vault key for the encryption key. Document the data-classification decision.

Context:
- P14 evidence: RC5d §3.4 S15 — "sensitive legal doc fields (case numbers, party names, RFC, CLABE) in plaintext DB rows"; grep for field-level encryption over `PrismaDbContext` entities returns 0 hits.
- Veriqan has AES-256 column encryption for its legal baseline; reuse or extend that pattern but upgrade to AES-GCM (authenticated encryption) per RC6 W3.8 recommendation.
- EF Core value converters: implement a `PiiEncryptedStringConverter` that wraps `AesGcm` from `System.Security.Cryptography`; register it in `PrismaDbContext.OnModelCreating` for each classified column.
- The encryption key must be loaded from the vault provider (PRISMA-E3-S5), not hardcoded.

Constraints:
- AES-GCM: use a 96-bit nonce (randomly generated per encryption); store `nonce || ciphertext || tag` in the column.
- The EF Core migration must be carefully ordered: existing plaintext rows must be encrypted in a migration step or a one-time migration script.
- An integration test (Testcontainers SQL) must verify: (a) a PII field written via the EF context is stored as ciphertext in the DB (raw SQL query returns non-plaintext bytes); (b) reading back through the EF context returns the original plaintext.

Definition of Done:
- `PiiEncryptedStringConverter` implemented and registered for all classified PII columns.
- EF Core migration handles existing rows (encryption migration script committed).
- Integration test verifies ciphertext-at-rest + plaintext-via-ORM round-trip.
- Key loaded from vault provider; no key material in committed appsettings.
- Build 0/0.

End-to-end evidence AC: A raw SQL query `SELECT RFC FROM Expedientes WHERE Id=1` returns a base64-encoded ciphertext blob, not the original RFC string. Reading the same row via the EF Core context returns the original RFC string correctly.

---

### PRISMA-E3-S4 — Vault-Backed SIARA Credential Source

**Traceability:** Closes P8 (SIARA credential vault not wired), RC6 W3.7
**Gate tag:** ops-gated (secret store provisioning) — **BLOCKED on: infrastructure team provisioning the Azure Key Vault (or client-equivalent) and allocating the SIARA credential secret slots**
**Effort:** M

**BLOCKED — unlock required:** Ops/infrastructure team must provision a Key Vault instance and provide the vault URI, the secret names for SIARA username and password, and the managed-identity or client-credential for the worker to authenticate to the vault.

**Subagent brief** (execute when unblocked)

Task: Implement a vault-backed `IKeyVaultSiaraCredentialSource` that reads SIARA username/password from Azure Key Vault (or the client-provided equivalent). Wire it in `Orion.Worker/Program.cs` when `AutomatedLogin` mode is enabled. Remove the current `ConfiguredSiaraCredentialSource` as the production default (retain it for development/test with a non-production flag).

Context:
- P8 evidence: RC5d §1.2 S10 — "`ISiaraCredentialSource` port exists; `ConfiguredSiaraCredentialSource` reads from config but no vault provider is wired in any host's `Program.cs`."
- `ISiaraCredentialSource` port: exists; no implementation in any composition root that reads from a vault.
- Azure Key Vault SDK: `Azure.Security.KeyVault.Secrets` NuGet package (check `Directory.Packages.props` for existing version pin or add one).
- The vault-backed source must: load credentials lazily (not at startup); refresh the cached credential before it expires; handle `RequestFailedException` and return `Result.WithFailure(...)` (not throw).

Constraints:
- Result<T> pattern on `GetCredentialsAsync`; CancellationToken propagated.
- No vault secrets in committed config; only the vault URI and secret names.
- An integration test (Testcontainers + a mock vault or the real Key Vault in a test subscription) verifies credential retrieval.
- `Orion.Worker.csproj` builds 0/0 after the change.

Definition of Done:
- `IKeyVaultSiaraCredentialSource` implemented and wired in `Orion.Worker/Program.cs` under `AutomatedLogin` mode.
- Integration test verifies credential retrieval from the vault (or a compatible mock).
- Build 0/0 on `Orion.Worker.csproj`.

End-to-end evidence AC: With `AutomatedLogin=true` and a valid vault URI in config, the Orion worker successfully retrieves SIARA credentials from Key Vault on startup without any plaintext credentials in the config files.

---

### PRISMA-E3-S5 — Key Vault Provisioning (Ops-Gated Placeholder)

**Traceability:** Dependency unlock for PRISMA-E3-S2 (asymmetric JWT keys), PRISMA-E3-S3 (PII encryption), PRISMA-E3-S4 (SIARA credentials), RC6 W5.5
**Gate tag:** ops-gated — **BLOCKED on: infrastructure provisioning decision**
**Effort:** S (engineering work; provisioning itself is ops)

**BLOCKED — unlock required:** Infrastructure/ops owner must provision an Azure Key Vault (or client-equivalent) instance, allocate key slots for: (a) AES-GCM PII encryption key, (b) per-process JWT key pairs (Orion, Athena, Reconciliator), (c) SIARA credentials. Provide the vault URI and managed-identity configuration.

**Engineering placeholder subagent brief** (execute after provisioning)

Task: Once the vault is provisioned, write a `KeyVaultBootstrap` utility that validates vault connectivity on worker startup, enumerates the expected secrets/keys, and fails fast with a descriptive error if any required secret is missing. Document the expected vault secret hierarchy in `docs/operations/KEY-VAULT-LAYOUT.md`.

Definition of Done:
- `KEY-VAULT-LAYOUT.md` committed documenting the secret hierarchy.
- `KeyVaultBootstrap` utility present and called from each worker's startup sequence.
- Build 0/0.

---

### PRISMA-E3-S6 — Configure Production Alerting and On-Call Escalation

**Traceability:** Closes P11 (no operational alerting configured), RC6 W3.16
**Gate tag:** ops-gated (ops tooling procurement) — **BLOCKED on: ops team deciding on alerting platform (PagerDuty / OpsGenie / Slack) and provisioning the account**
**Effort:** M

**BLOCKED — unlock required:** Ops/business owner must decide on the alerting platform, provision an account, and provide the webhook URL or API key. Until then, this story is an engineering placeholder with the alert-rule definitions written but not wired.

**Subagent brief** (execute when unblocked)

Task: Define SLA-breach and pipeline-error alert rules targeting the `ExxerCube.Prisma.SLA` meter (confirmed registered and exported in RC5 §Resolved). Wire them to the provisioned notification channel. Document the on-call escalation path in the operational runbook (PRISMA-E2-S2 §On-Call section).

Context:
- P11 evidence: RC5d §1.6 — "No on-call/paging configured; no alerting config in any `appsettings.json` or CI pipeline."
- `ExxerCube.Prisma.SLA` meter: registered and exported via OTLP in `Web.UI/Program.cs:431` (confirmed DONE in RC5 §Resolved for D2-SLA). Alert rules must consume this meter from the observability backend (PRISMA-E3-S7).
- Alert rules to define: (a) SLA breach — `SlaDeadlineMissed > 0` in the last 5 minutes; (b) pipeline error — `PipelineFailures > 0` in the last 5 minutes; (c) worker health-check failure — any worker `/health/ready` returns non-200 for 2 consecutive minutes.

Definition of Done:
- Alert rules defined in the observability backend (Prometheus Alertmanager or equivalent).
- At least one end-to-end test that fires a test alert and verifies it reaches the notification channel.
- On-call escalation section in the operational runbook updated with the escalation path.
- Build 0/0 (config-only changes; no C# code changes).

---

### PRISMA-E3-S7 — Network Segmentation and mTLS Evaluation (Business-Gated)

**Traceability:** Closes D15 (hub endpoints secured by JWT only; no mTLS between processes), RC6 W3.10
**Gate tag:** business-gated (network topology / mTLS provisioning decision) — **BLOCKED on: owner decision on network segmentation model (k8s NetworkPolicy vs service mesh) and whether mTLS is required**
**Effort:** M

**BLOCKED — unlock required:** Architecture/security owner must decide: (a) whether a Kubernetes NetworkPolicy restricting hub access to authorized worker IP ranges is sufficient, or (b) whether mutual TLS is required. Document the decision in ADR-012 update.

**Engineering placeholder subagent brief** (execute when unblocked)

Task: Implement the approved network segmentation model. If NetworkPolicy: write `k8s/network-policy-prisma.yaml` restricting `/hubs/ingestion` and `/hubs/reconciliation` to authorized pod CIDR ranges. If mTLS: configure `CertificateAuthenticationHandler` in each worker and the hub host; provision client certificates per process; update ADR-012 OD-3.

Definition of Done: Network segmentation policy committed and documented; ADR-012 updated; build 0/0.

---

### PRISMA-E3-S8 — Audit Fail-Closed Mode Design and Policy (Business-Gated)

**Traceability:** Closes D16 (audit fail-open; pipeline-availability prioritized over audit-write guarantees), RC6 W3.11
**Gate tag:** business-gated (policy decision on audit-write semantics) — **BLOCKED on: owner decision on whether audit-write failure should halt the pipeline for regulated document types**
**Effort:** M

**BLOCKED — unlock required:** Owner/compliance team must decide: for regulated document types (CNBV category), does an audit-write failure halt the pipeline (fail-closed) or allow the pipeline to continue (fail-open with documented risk acceptance)?

**Engineering placeholder subagent brief** (execute when unblocked)

Task: Implement the approved audit-write policy. If fail-closed for regulated types: add a document-type check in `IAuditLogger`; if audit write fails for a regulated type, return `Result.WithFailure(...)` from the pipeline stage (not swallow the error). If fail-open is accepted: add a structured log warning on every swallowed audit failure and document the risk acceptance in ADR-012 §Consequences.

Definition of Done: Policy implemented per the approved decision; ADR-012 updated; build 0/0.

---

### PRISMA-E3-S9 — CNBV CUB Outsourcing Regime and ISO 27001 / SOC 2 Trajectory (Business-Gated)

**Traceability:** Closes D18 (CNBV CUB obligations undocumented), D23 (ISO 27001 / SOC 2 trajectory undefined), RC6 W3.12, W3.13, W3.15
**Gate tag:** business-gated (legal/compliance counsel + executive decision) — **BLOCKED on: engagement of legal/compliance counsel and an executive decision on certification target**
**Effort:** L

**BLOCKED — unlock required:** Legal/compliance counsel must enumerate applicable CNBV CUB outsourcing-regime controls and data-residency requirements. Executives must decide on the certification target (ISO 27001 and/or SOC 2). Engineering work (gap assessment, controls mapping, data-residency architecture) follows after these decisions.

**Placeholder note:** once counsel delivers the enumeration, create a sub-story for each mandated control or data-residency requirement. This story's done definition is the delivery of the legal input package and the commissioning of the gap assessment; not the gap assessment itself.

---

## PRISMA-E4 — Persistence and Durability

**Wave:** W2 (Persistence Durability)
**Goal:** PersonIdentityResolver persists identity mappings across sessions; the choice on metrics persistence is documented.
**Gate:** buildable-now
**Gaps covered:** D6, D13 (D13 is also covered by PRISMA-E2-S9)

Note: D13 is covered primarily by PRISMA-E2-S9 (metrics persistence decision). PRISMA-E4 focuses on D6.

### PRISMA-E4-S1 — PersonIdentityResolver DB Persistence

**Traceability:** Closes D6 (PersonIdentityResolver has no DB persistence), RC6 W2.7
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Add EF Core entity + migration for persisted identity mappings in `PersonIdentityResolverService`. Wire the service to a new `IPersonIdentityRepository` backed by `PrismaDbContext`. Ensure the identity store is auditable (writes trigger audit log entries).

Context:
- D6 evidence: `PersonIdentityResolverService.cs:30-60` — performs real RFC-variant generation and name normalization but stores nothing; no `_dbContext` reference; in-memory only; registered in Web.UI.
- The entity must store: resolved RFC canonical form, name normalization result, input variants seen, timestamp of first resolution, timestamp of last seen.
- Audit: each identity resolution (new entity or update) must call `IAuditLogger.LogAuditAsync` with event type `IdentityResolved`.
- Migration: add to `PrismaDbContext`; the migration must apply after `PrismaDbContext` base migration (per the sequencing documented in PRISMA-E1-S2).

Constraints:
- Result<T> pattern on `ResolveAsync`; CancellationToken propagated.
- `PersonIdentityResolverService` must remain registered in Web.UI's DI root; update the registration to inject the new repository.
- Test project: add integration tests in `Tests.System.Storage` (Testcontainers SQL) verifying: (a) a new identity is persisted on first resolution; (b) subsequent resolutions update the `LastSeen` timestamp; (c) the audit log contains an `IdentityResolved` entry.

Definition of Done:
- `PersonIdentityRepository` EF entity + migration committed.
- `PersonIdentityResolverService` wired to the repository.
- Three integration tests (a, b, c) green in `Tests.System.Storage`.
- Build 0/0 on the solution.

End-to-end evidence AC: After two calls to `ResolveAsync` with the same person, a Testcontainers SQL query `SELECT COUNT(*) FROM PersonIdentities WHERE ...` returns 1 (not 2 — no duplicates) and `LastSeen` reflects the second call's timestamp.

---

## PRISMA-E5 — Code-Quality and Cosmetic Remediation

**Wave:** W1 (Operational Readiness — low-effort quality items run alongside W1 engineering)
**Goal:** dead code is removed or clearly archived; legacy confusion points are eliminated; documented-but-resolved warnings in CLAUDE.md are updated; quality improvements (holiday calendar, regex hardening) that do not require corpus data are implemented.
**Gate:** buildable-now (except D3 which is business-gated)
**Gaps covered:** C1, C2, C3, C4, D2, D3, D5

### PRISMA-E5-S1 — Remove Legacy ProcessingOrchestrator ROP Path and Archive EfCoreIdentityAdapter Comment

**Traceability:** Closes C1 (EfCoreIdentityAdapter unregistered — needs a clear comment), C3 (legacy ProcessingOrchestrator ROP path hardcodes XML/DOCX null), RC6 (cosmetic)
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: (1) Add a clear code comment at the `EfCoreIdentityAdapter` class pointing to ADR-012 explaining why it is intentionally unregistered. (2) Remove or `[Obsolete]`-tag the legacy `ProcessingOrchestrator` ROP path that hardcodes `FuseAsync(null, pdf, null, ...)` in `ProcessingOrchestrator.cs:483-486`. The production path uses `ExtractionOrchestrator`; the legacy path is dead code in the 3-process model.

Context:
- C1 evidence: RC5a §1D; RC5d §3.2 S7 — `EfCoreIdentityAdapter` (JWT) exists but is NOT registered; Web.UI uses cookie auth; the adapter is intentionally unregistered per ADR-012 (worker security model is the A5 path).
- C3 evidence: RC5c §Fusion — "Legacy `ProcessingOrchestrator` ROP path still hardcodes XML/DOCX null in `FuseAsync`"; production path uses `ExtractionOrchestrator`.
- The `[Obsolete]` attribute approach is preferred over deletion if the class is referenced from any test project.

Constraints:
- Do NOT delete `EfCoreIdentityAdapter` — only add the comment.
- Before adding `[Obsolete]` to the legacy path: confirm it is unreachable from any non-test code path (grep for callers).
- Build 0/0 after the change.

Definition of Done:
- `EfCoreIdentityAdapter.cs` has a `// This adapter is intentionally unregistered. See ADR-012 §Worker Security Model.` comment.
- `ProcessingOrchestrator.cs` legacy `FuseAsync(null, pdf, null)` path is marked `[Obsolete("Dead code — production path uses ExtractionOrchestrator. See ADR-012.")]` or removed if safe.
- Build 0/0; all tests green.

End-to-end evidence AC: `dotnet build` of the affected projects returns 0/0 with no new warnings-as-errors introduced by the `[Obsolete]` attributes (use `#pragma warning disable CS0618` in any test callers if needed, and document why).

---

### PRISMA-E5-S2 — Decide and Clean Up SignalREventBroadcaster (C2)

**Traceability:** Closes C2 (SignalREventBroadcaster in Web.UI commented out)
**Gate tag:** buildable-now (decision first, then engineering)
**Effort:** S

**Subagent brief**

Task: Decide whether real-time UI event streaming via `SignalREventBroadcaster` is in-scope for MVP. If yes: re-enable and test the commented-out line in `Web.UI/Program.cs:333`. If no: remove the commented line entirely and add a brief comment explaining the decision (e.g., `// Real-time streaming de-scoped for MVP; IndFusion.Ember provides cross-process events. See ADR-009.`).

Context:
- C2 evidence: RC5a §1D — `Web.UI/Program.cs:333` has `//services.AddHostedService<Services.SignalREventBroadcaster>()` disabled ("demo/isolation mode").
- IndFusion.Ember (ADR-009) handles cross-process event delivery; the `SignalREventBroadcaster` would add in-browser real-time updates for the Web.UI. If the UI Dashboard already shows real-time data via page refresh or polling, the broadcaster may be non-critical for MVP.

Constraints:
- If re-enabled: add at least one `Tests.UI` Playwright test that asserts a UI element updates in real time after a document is processed.
- If removed: no test changes needed beyond build verification.
- Build 0/0 after either choice.

Definition of Done:
- The commented-out line in `Web.UI/Program.cs:333` is either re-enabled (with a test) or removed (with a comment explaining the decision).
- Build 0/0; existing `Tests.UI` 21/21 still green.

---

### PRISMA-E5-S3 — Update CLAUDE.md to Remove Stale Warnings

**Traceability:** Closes C4 (Counter.razor / Weather.razor already removed — CLAUDE.md stale warning); also corrects other stale claims identified in RC5 §6 supersession note
**Gate tag:** buildable-now
**Effort:** S

**Subagent brief**

Task: Edit `CLAUDE.md` to: (1) remove the Counter.razor / Weather.razor warning (they are already removed — confirmed by RC5d §1.7); (2) correct the "StubDocumentDownloader wired" stale claim; (3) correct the "IngestionOrchestrator.StartAsync() is a placeholder" stale claim; (4) update the project count from "200+ projects" to "~70 projects (33 production + 37 test)" per the RC5d §9 correction; (5) update the "Release Status" section to reference RC5 as the canonical current-state source.

Context:
- C4 evidence: RC5d §1.7 — both `Counter.razor` and `Weather.razor` are absent from the codebase.
- RC5 §6 lists the specific CLAUDE.md corrections needed: stub claims for `StubDocumentDownloader` and `IngestionOrchestrator.StartAsync()` are both refuted.
- The "200+ projects" figure is inflated by backup directories; the correct count is ~70 (33 + 37 test) as documented in RC5d §9.

Constraints:
- Minimal targeted edits to CLAUDE.md — do not restructure the file.
- After editing, a `git diff CLAUDE.md` review must confirm only the targeted stale passages were changed.
- Build 0/0 (doc-only change).

Definition of Done:
- `CLAUDE.md` Counter.razor / Weather.razor warning removed.
- Stub claims for `StubDocumentDownloader` and `IngestionOrchestrator.StartAsync()` corrected to reflect the current DONE state.
- Project count updated to "~70 projects".
- "Release Status" section references RC5 as canonical.
- Build 0/0.

---

### PRISMA-E5-S4 — Implement Mexican Holiday Calendar in SLA Enforcer

**Traceability:** Closes D5 (Mexican-holiday calendar simplification — weekend-only business-day skipping), RC5 §D5
**Gate tag:** buildable-now
**Effort:** M

**Subagent brief**

Task: Replace the weekend-only business-day skipping logic in `SLAEnforcerService.cs:66-67` with a real Mexican national holiday calendar. Integrate `Nager.Date` (or a CNBV-published holiday schedule) for Mexican holiday awareness. Add tests for known holiday-adjacent SLA deadlines.

Context:
- D5 evidence: `SLAEnforcerService.cs:66-67` uses weekend-only skipping; Mexican national holidays (e.g., Día de la Independencia 16 September, Navidad 25 December) are not modeled, producing incorrect SLA deadlines on holiday-adjacent dates.
- `Nager.Date` NuGet package: `Nager.Date` — check `Directory.Packages.props` for existing pin or add one; it supports `CountryCode.MX` for Mexico.
- The fix: wrap the business-day calculation in a `IBusinessCalendar` interface; inject it into `SLAEnforcerService`; implement `MexicanBusinessCalendar` using `Nager.Date`; register in the composition roots.

Constraints:
- `IBusinessCalendar` interface enables testability (inject a test calendar with known holidays).
- Result<T> pattern on SLA calculation methods; CancellationToken propagated.
- Add unit tests (fast, no DB) for: (a) deadline on a Friday before a Monday holiday — assert deadline skips 3 days, not 2; (b) deadline on 15 September — assert 16 September is skipped.
- Build the `SLAEnforcerService`-containing project (0/0) after the change.

Definition of Done:
- `IBusinessCalendar` interface introduced; `MexicanBusinessCalendar` implementation using `Nager.Date`; `SLAEnforcerService` injects it.
- Unit tests for (a) and (b) above green.
- Build 0/0.

End-to-end evidence AC: `SLAEnforcerService.CalculateDeadline(submittedAt: new DateTime(2026, 9, 15), businessDays: 5)` returns a date that skips 16 September (Día de la Independencia) in addition to weekends, verified by a unit test.

---

### PRISMA-E5-S5 — Harden DocxFieldExtractor Regex for Real SAT DOCX (D2)

**Traceability:** Closes D2 (DocxFieldExtractor regex too narrow for real SAT DOCX), RC5 §D2
**Gate tag:** buildable-now (issue #2 open — engineering item, no external gate)
**Effort:** M

**Subagent brief**

Task: Extend `DocxFieldExtractor.cs` regex patterns to cover the real SAT requerimiento DOCX field layout. Add integration tests using a real (or representative synthetic) SAT DOCX fixture. This closes issue #2 hardening item for the DOCX extractor.

Context:
- D2 evidence: RC5a §Part 2 B1 note — "on real SIARA documents the DOCX (SAT requerimiento) contributes zero fields under `DocxFieldExtractor`'s current regex patterns"; issue #2 open.
- The SIARA Simulator's document fixtures (`tools/Siara.Simulator`) should include a representative SAT requerimiento DOCX; if not, use one of the existing test fixtures in the test infra and note the limitation.
- `DocxFieldExtractor` regex patterns: examine the current patterns; extend them to match the SAT field header layout (e.g., "RFC del contribuyente:", "Expediente:", "Número de folio:").
- The `Tests.Infrastructure.Extraction.Adaptive` test project (126 tests, all green after PRISMA-E1 fix) is the target for new tests.

Constraints:
- Do NOT change the `IFieldExtractor<DocxSource>` interface.
- New regex patterns must be added with named capture groups and documented with a comment citing the SAT document field name.
- Each new pattern must have a corresponding unit test in `Tests.Infrastructure.Extraction.Adaptive`.
- Build `Tests.Infrastructure.Extraction.Adaptive.csproj` (0/0) after the change; all 126+ tests green.

Definition of Done:
- `DocxFieldExtractor.cs` has extended regex patterns covering the SAT requerimiento field layout.
- At least 5 new unit tests covering the new patterns.
- All 126+ existing tests still green; new tests green.
- Build 0/0.

---

### PRISMA-E5-S6 — Semantic Classification Enhancement Decision (D3 — Business-Gated)

**Traceability:** Closes D3 (classification is keyword-rule only; no semantic/NLP field extraction)
**Gate tag:** business-gated (ML/NLP capability decision) — **BLOCKED on: owner decision on whether semantic/NLP classification is in-scope for Prisma MVP**
**Effort:** L

**BLOCKED — unlock required:** Owner must decide whether NLP-based semantic field extraction is in-scope. If yes: define the semantic extraction requirements and approve the ML capability approach (e.g., rule-based NER vs a pre-trained Spanish NER model). Engineering implementation follows.

**Placeholder note:** Until the owner decision lands, the keyword-rule classifier (`FileClassifierService.cs:76-251`) remains the production classification engine. This is a known limitation documented in RC5 §D3 and does not block the current MVP pipeline.

---

## PRISMA-GATED — Live SIARA Portal Ingestion and Real-Data Validation

**Wave:** W4 (Real-Data / Live-Portal Validation), W5 (Business/Legal/Corpus Unlocks — parallel tracks)
**Goal:** once legal and corpus gates clear, enable live SIARA portal access, validate the quality model against a real corpus, characterize production volume/latency, and confirm SIRO XSD conformance.
**Gate:** legal-gated (P7 counsel sign-off), corpus-gated (D1, D4, D19), and business-gated (D4 Banamex `.xsd`)
**Gaps covered:** P7, D1, D4, D19

### PRISMA-GATED-S1 — Enable Live SIARA Portal Access (Legal-Gated)

**Traceability:** Closes P7 (live SIARA portal access is legal-gated), RC6 W5.2, CPA-2
**Gate tag:** legal-gated — **BLOCKED on: legal counsel sign-off per ADR-010 §Legal preconditions; `Siara:AllowProductionHost=false` code gate**
**Effort:** — (engineering is trivial — flip one config flag; the work is legal)

**BLOCKED — unlock required:** Legal counsel must provide written authorization for automated access to `siara.cnbv.gob.mx`. The authorization artifact must be recorded in ADR-010. Only then: flip `Siara:AllowProductionHost=true` in the production config; run the first live-SIARA ingestion test against the real portal.

**Subagent brief** (execute when legal gate clears)

Task: (1) Record the legal authorization artifact reference in ADR-010 with date and counsel name. (2) Set `Siara:AllowProductionHost=true` in the production `appsettings.json` for Orion Worker. (3) Run the max-fidelity E2E gate against the live SIARA portal (not the simulator) and assert one real document traverses the full pipeline.

Definition of Done:
- ADR-010 updated with authorization artifact reference and date.
- One successful end-to-end run against live `siara.cnbv.gob.mx` producing a real SIRO XML artifact in the database.
- The test run is documented (screenshot or log excerpt) as the go-live evidence artifact.

---

### PRISMA-GATED-S2 — Retrain Quality Model Against Real Corpus (Corpus-Gated)

**Traceability:** Closes D1 (quality model uncalibrated against production data), RC6 W4.11, U14
**Gate tag:** corpus-gated — **BLOCKED on: acquisition of a labelled CNBV/Banamex document corpus (RC6 CPA-3b, RC6 W5.4)**
**Effort:** M

**BLOCKED — unlock required:** Business/data team must acquire a labelled corpus of real CONDUSEF/Banamex documents with ground-truth quality scores. Engineering work (retraining the GA optimizer, setting `TrainedDate`, updating `PolynomialModelOptions.cs`) follows immediately after corpus delivery.

**Subagent brief** (execute when corpus is acquired)

Task: Retrain the polynomial quality model (`PolynomialImageQualityAnalyzer`) against the acquired corpus. Update `PolynomialModelOptions.cs` coefficients, set `TrainedDate` to the training date, and set `TrainingDataSize` to the corpus size. Add a startup provenance check that warns (but does not halt) if `TrainedDate` is null or older than 90 days.

Context:
- D1 evidence: `TrainedPolynomialModel.cs:55` parameterless ctor; `PolynomialModelOptions.cs:49-144` real GA-optimized coefficients but `TrainedDate=null`, `TrainingDataSize=0`.

Definition of Done:
- `PolynomialModelOptions.cs` updated with corpus-validated coefficients, non-null `TrainedDate`, and `TrainingDataSize > 0`.
- Startup provenance check implemented and logged.
- Existing quality-model unit tests still green; new unit tests validate the startup warning path.
- Build 0/0.

---

### PRISMA-GATED-S3 — SIRO XSD Conformance Validation (Business-Gated)

**Traceability:** Closes D4 (SIRO XSD validation absent), RC6 W4.13
**Gate tag:** business-gated — **BLOCKED on: Banamex supplying the SIRO `.xsd` schema file (issue #2)**
**Effort:** S

**BLOCKED — unlock required:** Banamex must provide the official SIRO `.xsd` schema file. Engineering (adding `SchemaValidationStep` in `SiroXmlExporter`) is trivial and executes immediately upon receipt.

---

### PRISMA-GATED-S4 — Volume Soak Test at Production Cadence (Corpus/Legal-Gated)

**Traceability:** Closes D19 (end-to-end pipeline latency under real volume not characterized), RC6 W4.12
**Gate tag:** legal-gated (requires live SIARA access for representative test) + corpus-gated (representative document mix)
**Effort:** M

**BLOCKED — unlock required:** PRISMA-GATED-S1 (live SIARA access) must be complete first. A representative document corpus must also be available. Engineering (volume soak test at ≤2,000 documents/day cadence) follows after both unlocks.

---

## Coverage Matrix

This table maps every open gap (P1–P16, D1–D24, C1–C4) to its story. No gap is orphaned.

### Blocks-Production (P1–P16)

| Gap | Severity | Story | Status |
|-----|----------|-------|--------|
| P1 | Blocks | PRISMA-E1-S1 | buildable-now |
| P2 | Blocks | PRISMA-E1-S2 | buildable-now |
| P3 | Blocks | PRISMA-E1-S3 | buildable-now |
| P4 | Blocks | PRISMA-E1-S3 | buildable-now |
| P5 | Blocks | PRISMA-E1-S3 | buildable-now |
| P6 | Blocks | PRISMA-E2-S1 | buildable-now |
| P7 | Blocks | PRISMA-GATED-S1 | BLOCKED — legal-gated (P1 counsel sign-off) |
| P8 | Blocks | PRISMA-E3-S4 | BLOCKED — ops-gated (Key Vault provisioning) |
| P9 | Blocks | PRISMA-E1-S4 | buildable-now |
| P10 | Blocks | PRISMA-E1-S4 | buildable-now |
| P11 | Blocks | PRISMA-E3-S6 | BLOCKED — ops-gated (alerting platform procurement) |
| P12 | Blocks | PRISMA-E2-S2 | buildable-now |
| P13 | Blocks | PRISMA-E3-S2 | BLOCKED — business-gated (key provisioning decision) |
| P14 | Blocks | PRISMA-E3-S3 | BLOCKED — business-gated (data classification + key management) |
| P15 | Blocks | PRISMA-E3-S1 | buildable-now |
| P16 | Blocks | PRISMA-E2-S3 | buildable-now |

### Degrades (D1–D24)

| Gap | Severity | Story | Status |
|-----|----------|-------|--------|
| D1 | Degrades | PRISMA-GATED-S2 | BLOCKED — corpus-gated |
| D2 | Degrades | PRISMA-E5-S5 | buildable-now |
| D3 | Degrades | PRISMA-E5-S6 | BLOCKED — business-gated (ML/NLP decision) |
| D4 | Degrades | PRISMA-GATED-S3 | BLOCKED — business-gated (Banamex `.xsd`) |
| D5 | Degrades | PRISMA-E5-S4 | buildable-now |
| D6 | Degrades | PRISMA-E4-S1 | buildable-now |
| D7 | Degrades | PRISMA-E1-S6 | buildable-now |
| D8 | Degrades | PRISMA-E1-S6 | buildable-now |
| D9 | Degrades | PRISMA-E1-S5 | buildable-now |
| D10 | Degrades | PRISMA-E2-S1 (reconnect sub-scenario) | buildable-now |
| D11 | Degrades | PRISMA-E2-S7 | buildable-now |
| D12 | Degrades | PRISMA-E1-S7 | buildable-now |
| D13 | Degrades | PRISMA-E2-S9 | buildable-now |
| D14 | Degrades | PRISMA-E3-S6 (observability backend dependency) | BLOCKED — ops-gated |
| D15 | Degrades | PRISMA-E3-S7 | BLOCKED — business-gated (network topology decision) |
| D16 | Degrades | PRISMA-E3-S8 | BLOCKED — business-gated (audit policy decision) |
| D17 | Degrades | PRISMA-E3-S2 (rotation grace period) | BLOCKED — business-gated (key provisioning) |
| D18 | Degrades | PRISMA-E3-S9 | BLOCKED — business-gated (legal counsel) |
| D19 | Degrades | PRISMA-GATED-S4 | BLOCKED — legal-gated + corpus-gated |
| D20 | Degrades | PRISMA-E2-S6 | buildable-now |
| D21 | Degrades | PRISMA-E2-S4 | buildable-now |
| D22 | Degrades | PRISMA-E2-S5 | buildable-now |
| D23 | Degrades | PRISMA-E3-S9 | BLOCKED — business-gated (executive decision) |
| D24 | Degrades | PRISMA-E2-S8 | buildable-now |

### Cosmetic (C1–C4)

| Gap | Severity | Story | Status / Note |
|-----|----------|-------|---------------|
| C1 | Cosmetic | PRISMA-E5-S1 | buildable-now |
| C2 | Cosmetic | PRISMA-E5-S2 | buildable-now |
| C3 | Cosmetic | PRISMA-E5-S1 | buildable-now |
| C4 | Cosmetic | PRISMA-E5-S3 | buildable-now — ALREADY RESOLVED (Counter/Weather removed); residual is CLAUDE.md doc edit only |

---

## Wave Summary and Execution Order

### Wave 0 — Minimum Deployable Composition (execute first; all buildable-now)

Stories: PRISMA-E1-S1, PRISMA-E1-S2, PRISMA-E1-S3, PRISMA-E1-S4, PRISMA-E1-S5, PRISMA-E1-S6, PRISMA-E1-S7
Estimated effort: 1 sprint (~2–3 weeks for one subagent loop or developer)
Gate outcome: `docker-compose up` runs the full 4-host + Simulator + SQL + Seq stack; all health probes are real; worker dashboards show non-zero metrics.

### Wave 1 — Operational Readiness (after W0; all buildable-now)

Stories: PRISMA-E2-S1, PRISMA-E2-S2, PRISMA-E2-S3, PRISMA-E2-S4, PRISMA-E2-S5, PRISMA-E2-S6, PRISMA-E2-S7, PRISMA-E2-S8, PRISMA-E2-S9, PRISMA-E5-S1, PRISMA-E5-S2, PRISMA-E5-S3, PRISMA-E5-S4, PRISMA-E5-S5, PRISMA-E5-S6 (blocked)
Estimated effort: 2 sprints (~4–6 weeks for one subagent loop or developer)
Gate outcome: real-TCP 3-process proof passes; operational runbook exists; Sentinel classified; Tesseract deadlock fixed; CI publishes images; failure-mode tests green.

### Wave 2 — Persistence and Audit (after W1; all buildable-now except blocked items)

Stories: PRISMA-E3-S1, PRISMA-E4-S1
Estimated effort: 1 sprint (~2 weeks)
Gate outcome: AuditRecords is immutable (SQL ledger or DENY grant); PersonIdentityResolver persists identity mappings.

### Wave 3 — Security Hardening (after W2; partially blocked)

Stories (buildable-now): PRISMA-E3-S2 (unblocks after key decision), PRISMA-E3-S4 (unblocks after vault provisioned), PRISMA-E3-S6 (unblocks after alerting platform), PRISMA-E3-S9 (first phase after counsel)
Stories (blocked, run concurrently when unlocked): PRISMA-E3-S3, PRISMA-E3-S5, PRISMA-E3-S7, PRISMA-E3-S8
Estimated effort: 2–3 sprints for the engineering tail; non-engineering unlocks (key provisioning, legal counsel, ops tooling) run on a parallel track and may take longer.
Gate outcome: per-process asymmetric JWT keys; vault-backed SIARA credentials; immutable audit with HMAC chaining; compliance trajectory documented.

### Wave 4 + 5 — Live-Portal and Real-Data Validation (legal-gated and corpus-gated)

Stories: PRISMA-GATED-S1, PRISMA-GATED-S2, PRISMA-GATED-S3, PRISMA-GATED-S4
These run when the legal gate (P1 counsel sign-off) and corpus acquisition (CPA-3b) clear. They are parallel non-engineering unlock tracks (W5) that must be owned and driven concurrently with the W0–W3 engineering work.

### Critical-Path Non-Engineering Unlocks (drive in parallel with all engineering waves)

1. CPP-2 Legal gate — live SIARA portal authorization (ADR-010 P1, counsel sign-off). Blocks PRISMA-GATED-S1 and PRISMA-GATED-S4.
2. Key Vault provisioning — infrastructure team. Blocks PRISMA-E3-S2, PRISMA-E3-S3, PRISMA-E3-S4 (PRISMA-E3-S5).
3. Real corpus acquisition — bank relationship / NDA. Blocks PRISMA-GATED-S2, PRISMA-GATED-S4.
4. Alerting platform procurement — ops team. Blocks PRISMA-E3-S6.
5. Data-classification decision — owner/compliance. Blocks PRISMA-E3-S3.
6. Audit-write policy decision — owner/compliance. Blocks PRISMA-E3-S8.
7. Network segmentation decision — architecture/security. Blocks PRISMA-E3-S7.

---

## Deferred Items (with reason)

| Gap | Deferral reason |
|-----|----------------|
| D3 (semantic classification) | Explicitly business-gated; the keyword classifier is functional. No engineering action until the NLP capability decision is made. |
| D14 (observability backend not verified as connected) | Ops-gated; the engineering work (alert rules) is specified in PRISMA-E3-S6, but the backend must be provisioned first. |
| D19 (volume soak at real cadence) | Corpus-gated AND legal-gated. Cannot run representative test without both the live portal and a real document mix. |
| PRISMA-E3-S9 (ISO 27001 / SOC 2 + CNBV CUB) | Business/legal gated. The engineering gap assessment cannot begin until counsel and executive decisions are made. This is NOT deferred indefinitely — it must start in parallel with W0–W3. |

C4 is noted as RESOLVED (Counter.razor / Weather.razor already removed); the only residual is a CLAUDE.md doc edit captured in PRISMA-E5-S3.

---

*Plan authored 2026-06-18 from RC5 + RC6 ground truth. No production code was read beyond the file:line citations in the RC5 matrix. No production files were modified.*
