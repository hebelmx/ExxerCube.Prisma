# RC.3 — Negative-Space Lenses: Deploy/Ops + Integration-as-Pipeline-Gate

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.3 lens agent, read-only — no production code modified)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` §5 (deploy/ops lens, integration lens)
**Inputs:** `RC0b-reality-map.md` · `RC1-multitenancy-ops-security.md` · direct file reads of Worker, Orchestration, Infrastructure
**Bar:** Full production — negative-space hunt. Cite absences with the same rigour as presences.

---

## Lens 1 — Deploy / Ops

### Method

Primary sources: `Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/Program.cs` (21 lines),
`ExxerCube.Prisma.Veriqan.Worker.csproj` (15 lines, single `ProjectReference`),
`VeriqanOrchestrationExtensions.cs`, `ConfigurationCryptoKeyProvider.cs`,
`CsvReferenceDataOptions.cs`, `SmtpOptions.cs`, `VeriqanMetrics.cs`,
`VeriqanLegalBaselineStartupService.cs`.
Also: `git ls-files | grep -iE "veriqan" | grep -iE "dockerfile|docker-compose|\.ya?ml|appsettings"` — zero results.

---

### Findings Table — Deploy / Ops

| Topic | State today (path/evidence) | Gap to production | Severity |
|---|---|---|---|
| **Config: `appsettings.json`** | **Absent.** `git ls-files` for Veriqan.Worker shows exactly two tracked files: `Program.cs` + `.csproj`. No `appsettings.json`, no `appsettings.*.json`, no `appsettings.Production.json`. `WebApplication.CreateBuilder` in Program.cs will produce an empty `IConfiguration`. | Every config value the system needs (see rows below) silently gets its default or throws at runtime. The Worker cannot be run as-is without injecting all config externally (env vars or a mounted secrets file). No template or manifest documents what values are required. | **Blocks-production** |
| **Config: SQL connection string** | `VeriqanOrchestrationExtensions.cs:73` — `config.GetConnectionString("VeriqanDb")`. If absent (which it is in the Worker as shipped), the branch at line 73–85 silently falls to `AddVeriqanInMemoryPersistence()`. No log warning is emitted. | No SQL persistence in production — all jobs and dispositions are lost on restart. The fail-loud startup contract (`VeriqanLegalBaselineStartupService`) is bypassed entirely; the encrypted SQL legal-baseline store is never used; `DefaultLegalToleranceProvider` (in-code constants) replaces it. | **Blocks-production** |
| **Config: CSV reference-data root** | `CsvReferenceDataOptions.RootDirectory` defaults to `string.Empty` (`CsvReferenceDataOptions.cs:14`). `AddVeriqanReferenceData()` in `VeriqanOrchestrationExtensions.cs:64` is called with no `configureOptions` delegate — it uses the empty-string default. `CsvReferenceDataAdapter` will fail or return empty when it tries to resolve institution sub-directories under the empty root. | Reference-data bundle is unresolvable; the Bind stage will produce `ReferenceDataAvailability` with no capabilities, forcing every statement to `Blocked` (InsufficientData) rather than being verified. Completely silent at startup. | **Blocks-production** |
| **Config: AES-256 encryption key** | `ConfigurationCryptoKeyProvider.cs:38` reads `Veriqan:LegalBaseline:EncryptionKey`. Missing key throws `InvalidOperationException` at DI resolution time (not silently). BUT this code path is only reached when `ConnectionStrings:VeriqanDb` is present (see row above). With no connection string the key is never read and encryption is bypassed entirely. | With connection string: startup throws if key absent — fail-loud is correct. Without connection string: key is silently unused and unvalidated. Production needs both. | **Blocks-production (with SQL); Masked (without SQL)** |
| **Config: SMTP host / credentials** | `SmtpOptions.cs:17` — `Host` defaults to `"localhost"`, `Port` defaults to 25, `EnableSsl` to false, credentials to null (unauthenticated relay). `ServiceCollectionExtensions.cs:44-47` binds from `"Veriqan:Smtp"`. No section means defaults apply silently; no error. | SMTP alerts (RED-verdict notifications) will silently attempt to relay via `localhost:25`. In a production environment without a local relay this fails at send time, not at startup. No validation of SMTP connectivity at startup. | **Degrades** |
| **Config: SMTP alert recipients** | `AlertOptions` binds from `"Veriqan:Alerts"` section (`ServiceCollectionExtensions.cs:45`). No defaults visible for recipient addresses. | If alert recipients are unconfigured, `VecAlertService` produces no-ops silently — RED statements go unreported. | **Degrades** |
| **Config: OTel / OTLP endpoint** | No OTLP configuration exists anywhere in the Worker. `VeriqanMetrics.cs` creates a BCL `Meter` but no exporter is wired in `Program.cs` or in `AddVeriqan`. There is no `AddOpenTelemetry()` call. | Metrics are emitted into a void. No Prometheus scrape endpoint, no OTLP push. Confirmed: `Program.cs` has no OTel registration. | **Degrades** |
| **Config: tenant profile** | `VeriqanVerdictExtensions.cs:43` (cited in RC0b) hardcodes `TenantProfile.LegalBaseline()` as a Singleton. No config key or tenant-ID selection. | Single-tenant operation only. Mitigated for now (E13 is gated), but no config path means even switching the legal baseline requires a code change. Not an E13 gap — it is an ops gap for any future-baseline update. | **Degrades (E13-gated for multi-tenant)** |
| **Secrets: vault / user-secrets / env template** | **None.** No Azure Key Vault, no `dotnet user-secrets`, no `.env.example` or `env.template` file anywhere in the Veriqan tree. The XML doc on `ConfigurationCryptoKeyProvider.cs:11` says "For production, set this key in environment variables or Azure Key Vault" — this is documentation only, not wiring. | Operator has no documented list of required secrets, no vault integration, no secrets-hygiene policy. AES key, connection string, and SMTP creds would arrive as plain environment variables with no governance. | **Blocks-production (security)** |
| **Deploy artifacts: Dockerfile / compose / k8s** | **None.** `git ls-files | grep -iE "dockerfile\|docker-compose"` returns only `bmad-eval-runner/assets/Dockerfile` (an unrelated skill asset). No Dockerfile, docker-compose, Helm chart, or k8s manifest for Veriqan.Worker anywhere in the repo. | No repeatable deployment. The Worker cannot be containerised without authoring artifacts from scratch. No CI publish target produces a deployable image. | **Blocks-production** |
| **Migrations runner (startup)** | `VeriqanLegalBaselineStartupService.cs:63` calls `VeriqanDbInitialiser.InitialiseAsync` which applies EF Core migrations + seeds the tolerance table. This is a real, working, fail-loud startup hook — **BUT** it only runs when `ConnectionStrings:VeriqanDb` is present (the condition in `VeriqanOrchestrationExtensions.cs:74-80`). | Migrations runner is real and correctly wired. Gap: it is bypassed whenever the connection string is absent (the default Worker state). A deploy script / compose file invoking the Worker with the connection string will pick it up. No separate migration CLI entrypoint exists for CI-gated migration-before-deploy patterns. | **Partial (functional when connection string present)** |
| **Health/readiness probes** | `Program.cs:10-11` — two routes: `/health` and `/health/live`, both returning `Results.Ok(new { status = "Healthy" })` as hardcoded literals. **No `/health/ready`.** No real component checks: no DB connectivity test, no reference-data probe, no SMTP probe, no tolerance-cache warm-up check. | A liveness probe returns `Healthy` even when the DB is down, the AES key is absent, or reference data is unreachable. A readiness probe does not exist. K8s / load-balancer readiness gates are non-functional. | **Blocks-production** |
| **Observability backend** | `VeriqanMetrics.cs` creates BCL `Meter("ExxerCube.Prisma.Veriqan", "1.0.0")` with 3 instruments (StatementDuration histogram, StatementProcessed counter, StatementExceptions counter). No `AddOpenTelemetry`, no `WithMetrics`, no OTLP/Prometheus exporter, no `ActivitySource` for tracing anywhere in `Program.cs` or `AddVeriqan`. | Meter emits to whatever process-local `MeterListener` attaches — in production, nothing listens. No traces. No dashboards. No alerting rules. No Serilog sink configuration (Worker relies on `WebApplication.CreateBuilder` default console logging only). | **Blocks-production** |
| **Runbook** | **None.** No operational runbook found anywhere in `docs/` for Veriqan batch initialisation, exception-queue triage, reference-data update procedure, or on-call escalation path. | Operator has no documented procedure for any runtime event. | **Blocks-production** |
| **Hardcoded dev values analogous to `Server=DESKTOP-...`** | Veriqan does NOT have hardcoded SQL server names. The connection string branch is fully config-driven (`config.GetConnectionString("VeriqanDb")`). BUT `SmtpOptions.Host` defaults to `"localhost"` and `SmtpOptions.Port` to 25 — these are functionally equivalent hardcoded dev defaults that silently do the wrong thing in prod. `CsvReferenceDataOptions.RootDirectory` defaults to `string.Empty`. | Two silent wrong-environment defaults: SMTP relay on localhost and empty CSV root. These don't throw at startup — they fail at first use. | **Degrades** |

---

### Config value inventory (the complete required set)

Every value the composed system needs at runtime, with confirmation of its absence in the Worker:

| Config key | Consumer | Missing from Worker? | Silent fallback |
|---|---|---|---|
| `ConnectionStrings:VeriqanDb` | `VeriqanOrchestrationExtensions.cs:73` | YES | In-memory persistence (bypasses SQL + encryption + startup service) |
| `Veriqan:LegalBaseline:EncryptionKey` | `ConfigurationCryptoKeyProvider.cs:38` | YES (unreachable without SQL branch) | Key never loaded; AES encryption bypassed |
| `Veriqan:Smtp:Host` | `SmtpOptions.cs:17` | YES | Defaults to `"localhost"` — wrong in prod |
| `Veriqan:Smtp:Port` | `SmtpOptions.cs:19` | YES | Defaults to 25 |
| `Veriqan:Smtp:From` | `SmtpOptions.cs:21` | YES | Defaults to `"noreply@veriqan.local"` |
| `Veriqan:Smtp:UserName` / `Password` | `SmtpOptions.cs:26,33` | YES | Defaults to null (unauthenticated relay) |
| `Veriqan:Smtp:EnableSsl` | `SmtpOptions.cs:38` | YES | Defaults to false |
| `Veriqan:Alerts:*` (recipients) | `AlertOptions` | YES | Default; no recipients → silent no-op |
| `CsvReferenceDataOptions:RootDirectory` | `CsvReferenceDataOptions.cs:14` | YES | Defaults to `string.Empty` → adapter fails at bundle lookup |
| OTLP endpoint / Prometheus scrape | No config key exists | N/A — no exporter wired at all | Metrics emitted to void |

**Every config value the system needs at runtime is absent from the Worker project and has a silent or wrong-direction fallback.**

---

## Lens 2 — Integration as a Bank-Pipeline GATE (E13 13.1)

### Method

Sources: `IVerificationPipeline.cs`, `IBatchProcessor.cs`, `StatementSubmission.cs`,
`VerificationOutcome.cs`, `Program.cs` (absence of HTTP endpoints), RC1 rows for E13.1/PG-20.

---

### Findings Table — Integration as Gate

| Topic | State today (path/evidence) | Gap to production | Severity |
|---|---|---|---|
| **API/SDK surface the host calls** | `IVerificationPipeline` is `internal sealed class VerificationPipeline` inside `Veriqan.Orchestration` — it is a DI-internal type. The interface is public (`IVerificationPipeline.cs`) but there is no NuGet package, no versioned public SDK, no HTTP POST endpoint, no gRPC controller, no message-bus consumer. The Worker's only HTTP surface is two health stubs. A bank's pipeline has no call-site. | **Complete absence of integration surface.** To embed Veriqan as a gate, the bank must either (a) reference the DI library in-process (undocumented, no public SDK boundary), (b) wait for an HTTP `POST /verify` endpoint to be added, or (c) wait for a gRPC service (E13.1). None of these exist. | **Blocks-production** |
| **In-process library embedding** | The closest usable path today: the bank's app adds a `ProjectReference` to `Veriqan.Orchestration`, calls `AddVeriqan(config)`, resolves `IVerificationPipeline` or `IBatchProcessor`, and calls `ProcessAsync` / `ProcessBatchAsync` directly. This is technically possible. | No public SDK contract, no versioned NuGet, no documented embedding guide, no sample host. The `StatementSubmission` record requires `byte[] Pdf + FileName + StatementContextKey` — the context key's schema and the CSV root's institution-directory layout are undocumented for external callers. The SQL connection string and AES key must be supplied by the host. This is not an embeddable product; it is a library with no external API discipline. | **Blocks-production** |
| **Latency budget** | `VeriqanMetrics.StatementDuration` histogram exists with description "NFR-1: p95 ≤ 10 000 ms". `BatchReport` computes `P95` and `ThroughputPerSecond`. Neither instrument has ever been measured on real statements. Orchestration tests run against 3 synthetic PDFs in ~90 seconds total (batch mode, 3 items) = ~30 s/statement average. That is a test-host number, not a prod latency — and it violates NFR-1 by 3x even on synthetic fixtures. | **No production latency characterisation exists.** The 10-second NFR is stated but never measured. The test-host number suggests p95 may already exceed the NFR on synthetics. A gate inside a bank's generation pipeline almost certainly has a tighter per-statement budget (sub-second to low single-digit seconds). This gap is corpus-gated (real performance requires real PDFs), but the absence of any measurement is itself a Blocks-production gap for a latency-sensitive gate. | **Blocks-production** |
| **Host-pipeline failure semantics (fail-open vs fail-closed)** | **No policy exists anywhere in the codebase.** `IVerificationPipeline.ProcessAsync` returns `Result<VerificationOutcome>` — a failure result or cancellation result is possible. `IBatchProcessor.ProcessBatchAsync` isolates individual failures to an exception queue and always completes the batch. Neither defines what the *host pipeline* should do when Veriqan errors or times out: block all statement generation (fail-closed) or allow it through unflagged (fail-open). There is no `TimeoutPolicy` on the pipeline entry point. | **Critical unowned design decision.** For a preventive compliance gate, fail-closed on error means the bank's generation pipeline halts for all statements while Veriqan is down — production-halting for the bank. Fail-open means non-compliant statements are generated without being flagged during outages — a regulatory risk. Neither is documented, designed, or owned. This is the single highest-severity integration unknown-unknown. | **Blocks-production (critical design gap)** |
| **Versioning / provenance the host receives** | `Finding.cs:63-64` carries `EngineVersion` (required). `Disposition.cs:151-161` carries optional `EngineVersion` + `ReferenceBundleVersion`. `VerificationOutcome` (the gate return value) contains a `VerdictSummary` — no `AcuerdoVersion`, no `ReferenceBundleVersion` stamped at the outcome level. The host receives a verdict but cannot record which Acuerdo edition or rule-version produced it. | E13.4 intent requires `AcuerdoVersion`/edition/date stamped on every verdict for audit trails. The bank's compliance record must reference which regulatory edition was applied. Neither `VerificationOutcome` nor `VerdictSummary` carries this field today. The `ReferenceBundleVersion` exists only on individual `Disposition` rows (optional, nullable), not on the gate's return value. | **Blocks-production (E13.4 gated)** |
| **Batch vs per-statement gate semantics** | `IBatchProcessor` is designed for end-of-period QC batches (FR-11). A preventive gate in a generation pipeline needs per-statement, low-latency, synchronous verdict. The two call patterns (`ProcessAsync` for single, `ProcessBatchAsync` for batch) both exist, but the Worker exposes neither. | The per-statement path (`ProcessAsync`) is the right API for an inline gate. It exists as an internal interface. Exposing it requires at minimum an HTTP endpoint or SDK method. No transport wrapping exists. | **Blocks-production** |
| **Integration contract / SLA agreement** | `PG-20` (pipeline-gate integration contract) is `Missing` per RC1. No documented latency SLA, no error-budget, no circuit-breaker specification, no API versioning policy. | The bank's engineering team needs a documented API contract before they can plan integration. No SLA means no on-call escalation agreement if the gate causes a generation-pipeline outage. | **Blocks-production** |

---

## Unknown-Unknowns Surfaced

The following production concerns were not captured in any RC0a requirement, FR/NFR, or epic AC, and are first surfaced here:

1. **Fail-open vs fail-closed policy for the gate is a business/legal decision, not an engineering one** — and it has zero owners, zero documentation, and zero code. It must be decided before E13.1 can be designed. The wrong default (fail-closed) will halt bank statement generation on every Veriqan outage.

2. **The Worker's config-absence is a silent correctness trap, not a startup error** — the system boots successfully into an in-memory shell that looks healthy (green probe) but processes nothing correctly. No monitoring catches this. A production deploy with a misconfigured or missing config secret passes all health checks and silently discards all data.

3. **The 30-second test-harness p95 on synthetics** — if real CONDUSEF PDFs are larger or more complex, the p95 could be higher, not lower. The 10 000 ms NFR may already be unachievable before any real measurement. This needs to be stress-tested with real documents before committing to the NFR.

4. **No circuit-breaker on downstream calls from the gate host** — if the bank's pipeline calls Veriqan and Veriqan is slow, the absence of a timeout/circuit-breaker in the integration means the bank's pipeline inherits Veriqan's latency tail. No `CancellationToken` timeout is imposed at the entry point.

5. **EF Core migration runner is bypassed in all non-SQL deployments** — there is no separate migration CLI entrypoint (`dotnet ef database update` or an `ef bundle`). A CI/CD pipeline that wants to run migrations before traffic is served must invoke the Worker itself with the connection string, start it, and let the startup service run — then kill it. This is an anti-pattern for CI/CD.

6. **The SMTP default of `localhost:25` with `EnableSsl=false` is a credentialling trap** — if a production network has a localhost SMTP relay, alerts will appear to work during testing but bypass TLS entirely. Security and ops may assume TLS is on when it is not.

7. **`StatementContextKey` schema is undocumented for external integrators** — the binding stage uses `StatementContextKey.ProductId` and the institution sub-directory layout of the CSV root to resolve a `VecReferenceBundle`. No external caller knows what values are valid without reading source. This is a hidden integration contract.

---

## Minimum Deployable-Composition Checklist

The concrete missing pieces to make the Worker process one real statement end-to-end in a deployed process:

1. **Add an `appsettings.json` (or env-var template) to `Veriqan.Worker`** documenting all required keys: `ConnectionStrings:VeriqanDb`, `Veriqan:LegalBaseline:EncryptionKey`, `Veriqan:Smtp:*`, `Veriqan:Alerts:*`, and `CsvReferenceData:RootDirectory`. Without this the Worker boots into a non-functional in-memory shell.

2. **Add a statement-ingestion entry point to the Worker** — at minimum: `POST /verify` (HTTP) accepting `multipart/form-data` with PDF bytes + `StatementContextKey`, resolved to `IVerificationPipeline.ProcessAsync`. Without this no statement can enter the system from outside the test harness.

3. **Wire the OTel exporter in `Program.cs`** — `builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(VeriqanMetrics.MeterName).AddPrometheusExporter())` (or OTLP). Without this all production metrics are silently discarded.

4. **Replace the hardcoded `/health` stub with real component checks** — at minimum: DB connectivity (when SQL path active), reference-data root existence, tolerance-cache populated. Add `/health/ready` that returns 503 until all checks pass.

5. **Add a `Dockerfile` (and optionally `docker-compose.yml`)** for the Worker — `mcr.microsoft.com/dotnet/aspnet:10.0` base, publish output, ENV placeholders for config. Without this there is no repeatable deploy artifact.

6. **Fix the 2 failing persistence integration tests** — `LegalBaselineEncryptedStoreTests` asserts 14 rows but seeder now seeds 19 (Epic 11 expansion). Update the test assertions to 19. These are the only active red tests in the suite and block CI confidence in the SQL persistence path.

---

*RC.3 Lens — Deploy/Ops + Integration-as-Gate. Findings are read-only observations; no production code was modified.*
