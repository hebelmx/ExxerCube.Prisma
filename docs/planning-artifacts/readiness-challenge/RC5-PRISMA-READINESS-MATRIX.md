# RC5 — Prisma MVP Readiness Gap Matrix

**Date:** 2026-06-18
**Branch:** `Liv`
**Author:** RC5 Synthesis — Winston (Architect) — read-only on all production code; no files modified
**Inputs consumed:** RC5a (reality map) · RC5b (ingestion + 3-process) · RC5c (pipeline stages + UI) · RC5d (deploy/security lenses) + Orchestrator-verified ground truth
**Supersedes:** `GAP-MATRIX-2026-06-11.md` · `MVP-PATH-2026-06-11.md` · `MVP-DEFINITION-2026-06.md` for current Prisma MVP state as of 2026-06-18.

---

## 1. Executive Summary

The honest state of Prisma MVP in twenty lines:

**What the prior matrix said and what is now true:**
The 2026-06-11 GAP-MATRIX classified the entire ingestion + 3-process security spine (A1–A6) as "Planned/Partial — the MVP-blocking cluster." That classification is obsolete. All six items are DONE and wired. Three separate production worker processes exist (Orion Downloader, Athena Extractor, Reconciliator), each under JWT hub-level auth with per-process audit. `StubDocumentDownloader` and `StubExxerHub` are dead code registered nowhere. The `IngestionOrchestrator.Task.CompletedTask` placeholder is gone; `SiaraWatchLoop.RunAsync` is the real poll loop.

**What is built and E2E-proven:**
The full pipeline — Quality (EmguCV real coefficients) → OCR (native Tesseract) → Multi-source Fusion (XML+DOCX+PDF) → Classification → SIRO XML + Datos-Carga Export → SQL Audit — runs end-to-end in `MaxFidelityGateFullPipelineE2ETests` (21m, no stub on the data path, real Testcontainers SQL, real Playwright browser, real multi-source expediente handoff across all three processes, with JWT clearance on both cross-process edges). MVP gate issue #5 was closed 2026-06-14.

**The two named E2E caveats (not engineering tasks):**
(1) The document source is the SIARA *simulator* (`tools/Siara.Simulator`), not the live `siara.cnbv.gob.mx` portal. Real-portal access is LEGAL-GATED: ADR-010 §Legal preconditions, `Siara:AllowProductionHost=false`. P1 counsel sign-off PENDING.
(2) The cross-process SignalR transport in the capstone gate is an in-memory ASP.NET TestServer seam — production hub + JWT auth + pipeline code all execute, but no single test crosses real TCP simultaneously with real OCR. Real TCP is proven separately in `HubWireTests`.

**The residual tail (what is NOT done):**
Deploy artifacts (zero Dockerfiles/compose/k8s for the 4 production hosts), config externalization (UI appsettings hardcodes `DESKTOP-FB2ES22`, LocalDB, localhost:5002), real-TCP-process proof over a network, Reconciliator and Web.UI health probes (stubs), Sentinel service (unknown state), worker /dashboard metrics (hardcoded zeros), observability backends, alerting, operational runbook, security hardening (symmetric HMAC key, no field-level encryption-at-rest, audit not immutable, SIARA credential vault not wired), quality model provenance (real GA coefficients but `TrainedDate=null`).

**Distance to full production:** Moderate. The engineering gap is almost entirely deploy/ops/security hardening, not functional capability. The functional and pipeline capability is built and proven (on the simulator). The critical-path blocker for live ingestion is the LEGAL gate (P1 counsel), not engineering.

---

## 2. Readiness Gap Matrix

**Column key:** `#` | `Dimension (D1–D9)` | `Requirement / Intent` | `Built-state` | `Evidence (file:line)` | `Severity [Blocks-production / Degrades / Cosmetic]` | `Effort [S/M/L]` | `Dependency` | `Owner-action`

**Dimension key:** D1=Functional Correctness · D2=E2E Deployable Composition · D3=Multi-tenancy · D4=Ingestion · D5=Persistence & Data · D6=Security & Compliance · D7=Operational Readiness · D8=Performance / SLA · D9=Failure Modes

**Dependency key:** `technical` = buildable now · `legal-gated` = blocked on P1 counsel sign-off · `business-gated` = blocked on procurement / policy / ops decision · `ops-gated` = blocked on environment provisioning

---

### 2.1 Blocks-production

| # | Dimension | Requirement / Intent | Built-state | Evidence (file:line) | Severity | Effort | Dependency | Owner-action |
|---|-----------|----------------------|-------------|----------------------|----------|--------|------------|--------------|
| P1 | D2 | **No deploy artifacts for any of the 4 production hosts.** Web.UI, Orion Worker, Athena Worker, Reconciliator Worker each need a Dockerfile, docker-compose (local dev), and either a k8s manifest or Helm chart for production. | **Missing.** Zero Dockerfiles/compose/k8s for production hosts. Only the Siara Simulator has a container artifact. | RC5d §1.3: full glob over repo returns 0 Dockerfiles for any production host; `Prisma/Deployments/Siara.Simulator/` is the only containerized component; `CI/quality-gates.yml` has no publish/deploy step | **Blocks-production** | M | technical | Author `Dockerfile` for each of the 4 hosts (`mcr.microsoft.com/dotnet/aspnet:10.0` base); author a `docker-compose.yml` for local full-stack dev; produce a k8s manifest or Helm chart for the 3-process worker topology with shared-volume and per-process env config. |
| P2 | D2 | **No auto-migration runner for any Prisma worker.** First-run requires a manual `dotnet ef database update` on three separate `DbContext` types (`PrismaDbContext`, `ApplicationDbContext`, `ExportAdaptiveDbContext`) in the correct order. No operator runbook documents the sequence. | **Missing.** Workers boot gracefully but skip DB on placeholder connection strings; no auto-migration path exists in production. | RC5d §1.4: `Athena.Worker/Program.cs:29-41` — graceful DB skip; no `MigrateAsync`; `Web.UI/Program.cs:97` — `UseMigrationsEndPoint` (dev only); only Veriqan has auto-migration (`VeriqanDbInitialiser.cs:54`) | **Blocks-production** | S | technical | Add a startup migration step (or a separate `--migrate-only` CLI entrypoint) to each worker; document multi-DbContext application order and the dependency between contexts; wire in CI as a pre-traffic step. |
| P3 | D2 | **Config externalization incomplete — Web.UI hardcodes machine-specific values.** `appsettings.Development.json` contains `Server=DESKTOP-FB2ES22\SQL2022` (line 4-5); `appsettings.json` base contains `(localdb)\MSSQLLocalDB` (line 4). Neither is portable. Worker configs correctly use DEV-PLACEHOLDER. | **Partial.** Workers clean; Web.UI base and Development appsettings contain hardcoded values. | RC5d §1.1: `07 UI/.../appsettings.Development.json:4-5` (`DESKTOP-FB2ES22`); same file lines 4-5 base (`(localdb)`); UI `Program.cs:228` throws if `DefaultConnection` missing | **Blocks-production** | S | technical | Remove `DESKTOP-FB2ES22` and `(localdb)` from committed appsettings; replace with env-var or Azure App Config references; provide a `.env.example` documenting all required environment variables. |
| P4 | D2 | **SIARA simulator URL hardcoded in Web.UI.** `appsettings.json:15` sets `"SiaraUrl": "https://localhost:5002"`. Any deployment other than a single developer box fails to reach SIARA (simulator or production). | **Hardcoded.** | RC5d §1.1: `Web.UI/appsettings.json:15` | **Blocks-production** | S | technical | Replace with an env-var reference; document the two valid values (simulator URL vs `siara.cnbv.gob.mx` behind the legal gate). |
| P5 | D2 | **Windows absolute path in Siara Simulator Production config.** `tools/Siara.Simulator/appsettings.Production.json:33` contains `C:\\SiaraData\\logs\\` — fatal on Linux containers or any non-C: host. | **Hardcoded in Production config.** | RC5d §1.1: simulator `appsettings.Production.json:33` + `Prisma/Deployments/Siara.Simulator/app/appsettings.Production.json:33` | **Blocks-production** | S | technical | Replace with a path environment variable (`SIARA_LOG_PATH` or equivalent); update the Docker Compose mount accordingly. |
| P6 | D2 | **Real-TCP / real-OS-process cross-process wire never proven end-to-end alongside a real pipeline run.** The max-fidelity E2E gate routes both SignalR edges through ASP.NET in-memory TestServer. Real TCP between OS processes on a network is proven separately by `HubWireTests` but NEVER in combination with real OCR + real SQL + the full pipeline. | **Unproven.** | RC5b §Biggest readiness gaps #1; RC5c §Biggest readiness gaps #2; `MaxFidelityGateFullPipelineE2ETests.cs:40-41` (in-memory transport); ADR-012 limitation #4 | **Blocks-production** | M | technical | Author an integration test or acceptance scenario that boots 3 real OS processes (not TestServer) on real TCP ports, drives a document through the full pipeline, and asserts the cross-process events arrive and produce a SIRO XML artifact. Run against the simulator. |
| P7 | D4 | **Live SIARA portal access is LEGAL-GATED.** The entire ingestion E2E is proven only against `tools/Siara.Simulator`. `Siara:AllowProductionHost=false` is a code-level gate. Connecting to `siara.cnbv.gob.mx` requires legal pre-clearance (ADR-010 P1, counsel sign-off PENDING). | **Legal gate.** Code is production-ready on this dimension; the unlock is non-engineering. | RC5d §3.5 S18; ADR-010 §Legal preconditions; `MaxFidelityGateE2EBase.cs:57` (`localhost:5001`); `appsettings.json` `Siara:AllowProductionHost=false` | **Blocks-production** | — | legal-gated (P1 counsel) | Engage legal counsel; obtain written authorization to automate SIARA portal access; flip `Siara:AllowProductionHost=true` only after sign-off; document the authorization artifact in ADR-010. |
| P8 | D4 | **SIARA credential vault not wired.** `ISiaraCredentialSource` port exists; `ConfiguredSiaraCredentialSource` reads from config but no vault provider is wired in any host's `Program.cs`. `AutomatedLogin` mode cannot function in production without vault-backed credential injection. | **Port exists, implementation absent.** | RC5d §1.2 S10; no `IKeyVaultCredentialSource` or equivalent in any composition root | **Blocks-production** | M | ops-gated (secret store provisioning) | Implement a vault-backed `ISiaraCredentialSource` (Azure Key Vault or client-provided equivalent); wire it in `Orion.Worker/Program.cs` when `AutomatedLogin` mode is enabled; document required vault secrets layout. |
| P9 | D7 | **Reconciliator health/readiness probes are hardcoded stubs.** `/health` and `/health/live` return constant `"Healthy"` (`Reconciliator/Program.cs:113-114`). No `IReadinessProbe` wired. A Kubernetes orchestrator cannot distinguish a live Reconciliator from a misconfigured one. | **Stub.** | RC5d §1.5: `Reconciliator/Program.cs:113-114` hardcoded; no `IReadinessProbe` in Reconciliator DI | **Blocks-production** | S | technical | Implement `IReadinessProbe` for the Reconciliator (analogous to Athena's `ExtractionPipelineService.IsStarted`); wire into a real `/health/ready` endpoint returning 503 until the pipeline is subscribed and ready. |
| P10 | D7 | **Web.UI health probe is a hardcoded stub.** `HealthCheckController.cs:39` returns hardcoded `"Healthy"` with no component checks (no DB probe, no service-availability check). `MapHealthChecks("/health")` added but no domain-specific `IHealthCheck` implementations registered. | **Stub.** | RC5d §1.5: `HealthCheckController.cs:39`; `Web.UI/Program.cs:114` — `AddHealthChecks()` only, no domain checks | **Blocks-production** | S | technical | Register real `IHealthCheck` implementations: DB connectivity, identity service availability; wire into `MapHealthChecks`. Add a `/health/ready` endpoint that returns 503 until DB migrations are verified. |
| P11 | D7 | **No operational alerting configured.** Zero alerting rules, PagerDuty/OpsGenie integration, or on-call escalation path. Pipeline failures and SLA breaches are not surfaced to operators in real time. | **Missing.** | RC5d §1.6: "No on-call/paging configured"; no alerting config in any `appsettings.json` or CI pipeline | **Blocks-production** | M | business-gated (ops tooling procurement) | Define SLA-breach and pipeline-error alert rules; wire to a notification channel (email, Slack, PagerDuty); document on-call escalation path in the operational runbook. |
| P12 | D7 | **No operational runbook.** Zero runbooks for normal operation (batch initiation, watch-loop restart), incident response (pipeline stuck, OCR failure, SIARA unreachable), or database maintenance. | **Missing.** | RC5d §1.6: only `docs/demos/CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` (demo, not ops) | **Blocks-production** | M | technical | Author a production operational runbook covering: worker startup/restart sequence, migration execution order, SIARA credential rotation, pipeline failure triage, database backup/recovery, on-call escalation path. |
| P13 | D6 | **Symmetric HMAC shared JWT secret across all 3 processes.** One shared `ProcessIdentity:JwtSecret` signs every process's clearance tokens; any process holding the secret can mint any clearance. Per-process asymmetric key isolation is proposed (ADR-012 addendum) but not implemented. | **Degrades / Blocks for a regulated gate.** | RC5d §3.1 S1; RC5b §Biggest readiness gaps #3; `Athena/Orion/appsettings.json:12` identical `JwtSecret` placeholder | **Blocks-production** | M | business-gated (key provisioning decision) | Implement per-process asymmetric key pairs (RS256 or ECDSA); each process signs with its own private key and verifies with the peer's public key; remove the shared HMAC secret; update ADR-012 OD-1–OD-7. |
| P14 | D6 | **No field-level encryption at rest for legal PII in the Prisma pipeline.** Extracted expediente fields (case numbers, party names, RFC, CLABE) are stored as plaintext DB rows in `PrismaDbContext` entities. Veriqan has AES-256 column encryption for its legal baseline; Prisma has no equivalent. | **Missing.** | RC5d §3.4 S15: "sensitive legal doc fields ... in plaintext DB rows"; grep for field-level encryption over `PrismaDbContext` entities → 0 hits | **Blocks-production** | L | business-gated (data classification and key management decision) | Classify which expediente fields are regulated PII; apply AES-GCM column encryption (reuse or extend the Veriqan pattern); provision a Key Vault key; document data-classification decision. |
| P15 | D6 | **Audit trail is not immutable.** `AuditRecords` is a standard EF Core table — no temporal tables, no `DENY UPDATE/DELETE`, no cryptographic tamper-evidence, no HMAC chaining. A DBA or any `DbContext` holder can mutate records retroactively. ADR-012 §Consequences accepts this for MVP; it blocks regulated production. | **Accepted MVP gap, blocks regulated production.** | RC5d §3.6 S24; no SQL temporal/ledger in EF migrations; ADR-012 §Consequences | **Blocks-production** | M | technical | Convert `AuditRecords` to a SQL Server 2022 append-only ledger table or add a `DENY UPDATE, DELETE` grant to the app login; add row HMAC chain; document 7-year retention policy. |
| P16 | D6 | **Sentinel service is a black box.** No startup code, no auth, no health checks, no observability artifacts found for Sentinel. Its readiness state is fully unknown — it may be empty scaffolding or partially wired. CLAUDE.md explicitly flags it as "not yet traced." | **Unknown / likely missing.** | RC5d §Unk-1; CLAUDE.md "not yet traced; status unknown"; no `Prisma.Sentinel.Worker/Program.cs` located in the composition-root scan | **Blocks-production** | L | technical | Trace Sentinel's actual composition root; classify its real status; if it is a functional service, wire health probes, observability, and auth; if it is empty scaffolding, decide whether it is in MVP scope and document. |

---

### 2.2 Degrades (materially limits production quality or operability)

| # | Dimension | Requirement / Intent | Built-state | Evidence (file:line) | Severity | Effort | Dependency | Owner-action |
|---|-----------|----------------------|-------------|----------------------|----------|--------|------------|--------------|
| D1 | D1 | **Quality model uncalibrated against production data.** `PolynomialImageQualityAnalyzer` uses real GA-optimized coefficients (R²>0.89, not stub), but `TrainedDate=null`, `TrainingDataSize=0` — baked-in defaults never validated against a real CONDUSEF/CNBV corpus. Miscalibrated quality scores could produce incorrect OCR/fusion routing decisions at volume. | **Partial (real coefficients, unvalidated provenance).** | RC5c §Quality: `TrainedPolynomialModel.cs:55` parameterless ctor; `PolynomialModelOptions.cs:49-144` (real coefficients, `TrainedDate=null`); RC5d §Unk-4 | **Degrades** | M | business-gated (real document corpus required) | Acquire a labelled document corpus (real CONDUSEF/Banamex documents); retrain and validate the polynomial quality model; set `TrainedDate` and `TrainingDataSize` from training provenance; add a provenance check on startup. |
| D2 | D1 | **DocxFieldExtractor regex too narrow for real SAT DOCX.** On real SIARA documents the DOCX (SAT requerimiento) contributes zero fields under `DocxFieldExtractor`'s current regex patterns. XML is the canonical source; best-effort holds. Tracked in issue #2 (hardening). | **Partial (known hardening item).** | RC5a §Part 2 B1 note; issue #2 open; `DocxFieldExtractor.cs` regex patterns | **Degrades** | M | technical (open issue #2) | Extend `DocxFieldExtractor` regex patterns to cover the real SAT DOCX field layout; add integration tests using a real SAT requerimiento fixture against the simulator. |
| D3 | D1 | **Classification is keyword-rule only; no semantic/NLP field extraction.** `FileClassifierService.cs:76-251` is a keyword-scored L1/L2 classifier. Deeper semantic extraction is a TODO. Classification accuracy degrades on atypical document layouts or new document types. | **Partial (shallow, non-NLP).** | RC5c §Classification; CLAUDE.md "deeper semantic field extraction is TODO" | **Degrades** | L | business-gated (ML/NLP capability decision) | Define the semantic extraction requirements; decide whether NLP (e.g., NER) is in-scope; if so, implement and validate against a labelled corpus. |
| D4 | D1 | **SIRO XSD validation absent.** `SiroXmlExporter` produces real SIRO XML but there is no XSD validation step to guarantee conformance to Banamex's schema. Tracked in issue #2 (blocked on Banamex providing the `.xsd`). | **Partial (unvalidated output).** | RC5a §Part 2 (SIRO XSD open, blocked on Banamex `.xsd`); issue #2 | **Degrades** | S | business-gated (Banamex must supply `.xsd`) | Obtain Banamex SIRO `.xsd`; add a `SchemaValidationStep` in `SiroXmlExporter` or as a post-export assertion; add a test that validates exported XML against the XSD. |
| D5 | D1 | **Mexican-holiday calendar simplification.** SLA deadline calculation uses weekend-only business-day skipping (`SLAEnforcerService.cs:66-67`). Mexican national holidays are not modeled. Incorrect SLA deadlines on holiday-adjacent dates. | **Partial (explicitly simplified, documented as P1).** | RC5c §SLA surface; `SLAEnforcerService.cs:66-67`; MVP-PATH D1 note | **Degrades** | M | technical | Integrate a Mexican national holiday calendar (e.g., `Nager.Date` or a CNBV-published schedule); replace weekend-only logic with a real business-day calculator; add tests for known holidays. |
| D6 | D1 | **PersonIdentityResolver has no DB persistence.** `PersonIdentityResolverService.cs:30-60` performs real RFC-variant generation and name normalization but stores nothing; in-memory only. Registered in Web.UI. Per-session identity resolves correctly but cannot accumulate learning or provide audit evidence. | **Partial (in-memory only).** | RC5a §Part 2 PersonIdentityResolver row; `PersonIdentityResolverService.cs:30-60` (no `_dbContext`) | **Degrades** | M | technical | Add an EF Core entity + migration for persisted identity mappings; wire `PersonIdentityResolverService` to a repository; ensure the identity store is auditable. |
| D7 | D2 | **Seq observability endpoint hardcoded to localhost.** `Web.UI/appsettings.json:73,92` hardcode `http://localhost:5341` for Serilog and OTLP. Any server or container deployment silently loses telemetry without the env-var override (which is not documented anywhere). | **Degrades.** | RC5d §1.6; `Web.UI/appsettings.json:73,92`; no override documented in any runbook or README | **Degrades** | S | technical | Replace hardcoded Seq URL with an env-var reference; document required observability env vars in `.env.example`; confirm in operational runbook. |
| D8 | D2 | **Worker logs emit to console only; no structured remote sink.** Orion, Athena, and Reconciliator workers have no Serilog file or Seq sink in their `appsettings.json`. Production log visibility requires external log aggregation that is not configured. | **Partial (console only).** | RC5d §1.6: "Workers have no structured remote log sink in production" | **Degrades** | S | technical | Add Serilog file + remote sink (Seq or Elastic) to each worker's `appsettings.json`; configure log-level minimums per environment. |
| D9 | D2 | **No docker-compose for full local-dev stack.** A developer cannot stand up the full 3-process topology (Orion + Athena + Reconciliator + Web.UI + SIARA Simulator + SQL Server + Seq) with a single command. | **Missing.** | RC5d §1.3: "Developer cannot stand up the full stack with one command" | **Degrades** | S | technical | Author a `docker-compose.dev.yml` wiring all 6 components with correct env-var wiring, shared-storage volume, and inter-process network. |
| D10 | D4 | **In-memory SignalR transport in the max-fidelity gate — TCP reconnect / back-pressure unproven.** The capstone E2E uses in-memory TestServer for both hub edges. Production reconnect behaviour, back-pressure under volume, and latency-induced event reordering on a real TCP link are unverified. | **Unverified.** | RC5b §Biggest readiness gaps #1; RC5b §A3/A4 caveats | **Degrades** | M | technical | Covered by gap P6 (real-TCP proof); additionally: add a reconnect/retry integration test that disconnects the Athena hub client mid-run and verifies Orion queues or retries the event. |
| D11 | D4 | **SIARA session longevity under production volume not proven.** The E2E gate exercises one document ingestion; long-running session behaviour (warm-session across hundreds of documents, rate limits from the portal, session expiry and re-login) is not tested. | **Unverified.** | RC5b §A2 caveat: "one-warm-session longevity across long runs unproven"; RC5c §Biggest readiness gaps #2 | **Degrades** | M | technical | Author a session-longevity soak test against the simulator (e.g., 50 sequential documents); verify session is refreshed on expiry without a restart; measure throughput under the real cadence target (≤2k/day). |
| D12 | D5 | **Worker dashboard metrics return hardcoded zeros.** `AthenaDashboardService.cs:46-57` and `OrionDashboardService.cs:46-57` return `QueueDepth:0` and a never-incremented document counter. `RecordDocumentProcessed()` is defined but never called from any orchestrator. Operators see a blank operational picture for the headless workers. | **Stub.** | RC5c §Worker `/dashboard`; RC5d §1.5 + §Unk-6 | **Degrades** | S | technical | Wire `RecordDocumentProcessed()` calls into `IngestionOrchestrator.IngestCaseAsync` (Orion) and `ExtractionPipelineService` (Athena); or replace the in-memory counter with a real `IProcessingMetricsService` query. |
| D13 | D5 | **ProcessingMetricsService is in-memory only; metrics reset on restart.** The working UI Dashboard reads `ProcessingMetricsService` (real, real-time event tracking) but it uses `ConcurrentDictionary`/`ConcurrentQueue` with no DB persistence. Historical/aggregate metrics and SLA trend views are unavailable after any restart. | **Partial (in-memory by design).** | RC5c §Dashboard metrics; `ProcessingMetricsService.cs:43-44` | **Degrades** | M | technical | Decide whether real-time in-memory metrics are sufficient for MVP or whether historical aggregate persistence is required; if the latter, add a time-series snapshot to the DB or integrate a time-series backend. |
| D14 | D5 | **Observability backend not verified as connected.** Web.UI exports OTLP traces and metrics but no Seq/Grafana/Prometheus collector is deployed in any CI/staging environment. The SLA meter (`ExxerCube.Prisma.SLA`) is registered and exported but no consumer is known to receive it. | **Partial (export wired, no verified consumer).** | RC5c §SLA surface; RC5d §1.6 | **Degrades** | M | ops-gated (environment provisioning) | Deploy a Seq or Prometheus/Grafana stack in the staging environment; verify metric ingestion end-to-end; author SLA alert rules that fire on the registered SLA meter. |
| D15 | D6 | **Hub endpoints secured by JWT only; no mTLS between processes.** `/hubs/ingestion` and `/hubs/reconciliation` enforce JWT connection-level auth but assume a trusted internal network. No mutual TLS, no network policy restricting which pods/hosts can connect. | **Partial (JWT real, transport isolation missing).** | RC5b §Biggest readiness gaps #5; RC5d §3.2 | **Degrades** | M | business-gated (network topology / mTLS provisioning decision) | Design a network segmentation policy (k8s NetworkPolicy or equivalent) restricting hub access to authorised worker addresses; evaluate mTLS for inter-process communication; document in ADR-012 update. |
| D16 | D6 | **Audit fail-open; pipeline-availability prioritized over audit-write guarantees.** Audit failures in all three workers are swallowed so the pipeline never blocks on an audit write. A regulator requires audit-write guarantees. | **Accepted MVP gap.** | RC5b §A6; RC5d §3.6 S24; ADR-012 §Consequences | **Degrades** | M | technical + business-gated (policy decision on audit-write semantics) | Decide on audit-write SLA: if audit-write failure should halt the pipeline for a regulated document type, add a fail-closed audit mode for those classifications; otherwise document the fail-open policy explicitly in the compliance narrative. |
| D17 | D6 | **No secret rotation mechanism for the shared JWT process-identity key.** Rotating the shared `JwtSecret` requires a coordinated simultaneous re-deploy of all three workers with no documented procedure and no rolling-rotation support. A single leaked secret requires emergency downtime. | **Missing.** | RC5d §1.2 S11; RC5d §Unk-5 | **Degrades** | M | technical | Document a rotation runbook; implement an overlapping-key grace period in `IProcessClearanceTokenService` (accept tokens signed by either old or new key during rotation window); automate the rotation via the planned Key Vault integration (P13). |
| D18 | D6 | **CNBV CUB outsourcing-regime obligations undocumented.** ADR-010 acknowledges the CNBV regulatory context. No analysis of mandatory outsourcing-regime controls, data-residency enforcement, or third-party-processor artefacts required under CUB. | **Missing documentation.** | RC5d §3.5 S19 | **Degrades** | L | business-gated (legal/compliance counsel required) | Engage legal/compliance counsel to enumerate applicable CUB outsourcing provisions; document data-residency requirements; update architecture to address documented obligations. |
| D19 | D8 | **End-to-end pipeline latency under real volume not characterized.** The max-fidelity gate ran one document in 21 minutes (dominated by Tesseract OCR + Playwright browser session startup). Real SIARA volume (≤2k/day, estimated peak bursts) has not been load-tested; no throughput or p95 SLA is documented. | **Unverified.** | RC5c §Stage OCR; RC5d §2.4: gate was 15-min timeout, one document | **Degrades** | M | business-gated (real volume corpus required for representative test) | Once the SIARA legal gate is cleared, run a volume soak test against the simulator at realistic cadence (200 documents); measure p95 latency and throughput; compare to any documented SLA; resize worker pool as needed. |
| D20 | D9 | **No chaos / failure-mode regression tests.** No test exercises Athena unreachable mid-document (Orion already broadcast), Tesseract returning garbled text, SIARA returning a 503, or the Reconciliator pod restarting mid-handoff. The "InsufficientData / best-effort on failure" path is asserted in documentation but not covered by any regression test at the pipeline boundary. | **Missing.** | RC5d §Unk-8 | **Degrades** | M | technical | Add a failure-mode integration test suite: (a) Athena hub unreachable — assert Orion retries or queues; (b) Tesseract fails — assert best-effort handoff still produces a partial expediente; (c) SIARA 503 — assert watch loop back-off and retry; (d) Reconciliator restart — assert handoff artifact is durable. |
| D21 | D9 | **Tesseract second-init deadlock under multi-worker restart.** `MaxFidelityGatePartialCaseE2ETests` explicitly disables the extraction pipeline to avoid a Tesseract re-init deadlock. This latent defect could manifest under any Athena deployment where the pipeline is restarted without a full process restart. | **Known defect, not covered by regression.** | RC5d §Unk-3; `MaxFidelityGatePartialCaseE2ETests.cs` — pipeline disabled to avoid deadlock | **Degrades** | M | technical | Reproduce the deadlock in isolation; fix the Tesseract lifecycle management (likely: init once at startup in a static/singleton context, not per-pipeline-start); add a restart regression test. |
| D22 | D9 | **`ProcessId` audit column adoption incomplete.** ADR-012 added `processId` to `IAuditLogger.LogAuditAsync` as a backward-compatible optional parameter, but call-site adoption across `IngestionOrchestrator`, `ProcessingOrchestrator`, and `ReconciliationPipelineService` was not verified. Audit records may systematically lack process identity, defeating forensic non-repudiation. | **Partial (API exists, call-site adoption unknown).** | RC5d §Unk-7; RC5d §3.6 S23 | **Degrades** | S | technical | Grep all `LogAuditAsync` call sites; verify each passes the `processId` parameter from the injected `ISiaraActorIdentityProvider`; add a process-identity assertion to the max-fidelity gate's audit-row check. |
| D23 | D6 | **ISO 27001 / SOC 2 trajectory undefined.** A bank deploying a regulated document-automation pipeline will require a third-party compliance certification or at minimum a certification roadmap. No trajectory, no controls mapped, no gap assessment started. | **Missing.** | RC5d §3.5 S20 | **Degrades** | L | business-gated (executive/procurement decision) | Decide which certification is required by target bank clients; commission a gap assessment against ISO 27001 Annex A or SOC 2 Trust Services Criteria; produce a remediation roadmap. |
| D24 | D7 | **No CI deploy pipeline; quality gate only.** `Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` runs build + test + security scan but has no publish, image-tag, or deploy step. There is no automated path from a passing PR to a deployed image. | **Missing.** | RC5d §1.3: "No automated release path" | **Degrades** | M | technical | Extend the CI pipeline with: `dotnet publish` → Docker image build → image tag → push to registry; optionally add a deploy step targeting staging. |

---

### 2.3 Cosmetic

| # | Dimension | Requirement / Intent | Built-state | Evidence | Severity | Effort | Dependency | Owner-action |
|---|-----------|----------------------|-------------|----------|----------|--------|------------|--------------|
| C1 | D7 | **`EfCoreIdentityAdapter` (JWT) exists but is unregistered.** The real JWT identity adapter class exists but is wired in no host. Web.UI uses ASP.NET Core Identity cookies for browser auth; the worker security model (per-process clearance) is the A5 path. The unregistered class is not a gap but may confuse future maintainers. | **Not a gap — by design for the worker security model (ADR-012).** | RC5a §1D; RC5d §3.2 S7: "EfCoreIdentityAdapter (JWT) exists but NOT registered; Web.UI → worker JWT path absent" | **Cosmetic** | S | technical | Add a clear code comment at the `EfCoreIdentityAdapter` class pointing to ADR-012 explaining why it is intentionally unregistered. |
| C2 | D7 | **`SignalREventBroadcaster` in Web.UI is commented out.** `Web.UI/Program.cs:333` has `//services.AddHostedService<Services.SignalREventBroadcaster>()` — disabled ("demo/isolation mode"). Real-time UI event streaming via this path is not available. | **Disabled by design ("demo mode").** | RC5a §1D: commented-out line 333 | **Cosmetic** | S | technical | Decide whether real-time UI streaming is in-scope for MVP; if yes, wire and test; if not, remove the commented line and document the decision. |
| C3 | D1 | **Legacy `ProcessingOrchestrator` ROP path still hardcodes XML/DOCX null in `FuseAsync`.** `ProcessingOrchestrator.cs:483-486` passes `FuseAsync(null, pdf, null, …)`. The production Extractor (`ExtractionOrchestrator`) uses multi-source fusion. The legacy path is dead in the 3-process model but creates confusion. | **Dead code (production path uses ExtractionOrchestrator).** | RC5c §Fusion: "Legacy ProcessingOrchestrator ROP path still hardcodes XML/DOCX null" | **Cosmetic** | S | technical | Remove or clearly archive the legacy `ProcessingOrchestrator` ROP path; add a `[Obsolete]` attribute if it must remain for reference. |
| C4 | D7 | **Counter.razor / Weather.razor already removed.** CLAUDE.md warned about template leftovers; they are gone. No action needed — recording as resolved to close the CLAUDE.md stale warning. | **RESOLVED.** | RC5d §1.7: "Counter.razor ABSENT; Weather.razor ABSENT" | **Cosmetic** | — | — | Update CLAUDE.md to remove the Counter/Weather warning. |

---

## 3. Resolved Since 2026-06-11 (ADVANCED-SINCE items — NOT gaps)

These items appeared as Planned/Partial in the 2026-06-11 GAP-MATRIX and are now confirmed DONE:

| Prior gap | Prior status | Now | Evidence |
|-----------|-------------|-----|----------|
| A1 — Real SIARA downloader | Planned | **DONE** | `Orion.Worker/Program.cs:64` `SiaraDocumentDownloader`; stub absent from DI |
| A2 — Poll/watch loop | Planned (no-op) | **DONE** | `SiaraWatchLoop.RunAsync` singleton; placeholder removed |
| A3 — Real Ember transport in workers | Planned | **DONE** | `SignalRIngestionBroadcaster` (Orion) + `SignalRReconciliationBroadcaster` (Athena); stubs dead |
| A4 — 3-process split | Partial | **DONE** | Three separate `*.Worker.csproj` + `Program.cs`; Reconciliator is a new third host |
| A5 — Per-stage auth + data minimization | Planned | **DONE** | JWT bearer + `ProcessClearance` hub policies in all 3 workers; `jti` replay guard |
| A6 — Per-process audit | Partial | **DONE** | `ISiaraActorIdentityProvider` + `IAuditLogger` + `ProcessId` column in all 3 workers |
| B1 — Multi-source fusion in worker path | Partial (single-source) | **DONE** | `ExtractionOrchestrator.cs:313-316` feeds all 3 sources |
| B2 — Native PDF text extraction | Planned | **DONE** | `PdfMetadataExtractor.cs:220-235` real PdfPig `ContentOrderTextExtractor` |
| C3 — Override/notes persisted to unified record | Partial | **DONE** | `DecisionLogicService.cs:791-835` `SaveAsync` to unified record (commit `e3fd56a`) |
| D2 — SLA telemetry / SLA meter exported | Partial | **DONE** | `Web.UI/Program.cs:431` `.AddMeter("ExxerCube.Prisma.SLA")` |
| E1 — Real readiness probes (Athena/Orion) | Partial | **DONE** | `ExtractionPipelineService.IsStarted` + `SiaraWatchLoop.IsRunning` |
| E2 — Config externalized (workers) | Partial | **DONE** | All worker `appsettings.json` use DEV-PLACEHOLDER + env-var pattern |
| Quality model — "stub coefficients" | Claimed stub | **REFUTED** | `PolynomialModelOptions.cs:49-144` real GA-optimized coefficients, R²>0.89 |
| Counter.razor / Weather.razor leftovers | Present | **REMOVED** | RC5d §1.7: both absent |
| F1 — One real E2E, no stub on pipeline | Planned | **DONE** | `MaxFidelityGateFullPipelineE2ETests` 2/2 passed 2026-06-14 (21m) |

---

## 4. Path-to-Production (Prisma)

### Critical-Path Non-Engineering Unlocks

**CPP-1. Legal gate P1 — Live SIARA portal authorization (Gap P7)**
Counsel sign-off on `Siara:AllowProductionHost=true` is PENDING. Until this is cleared, the entire production ingestion chain is simulator-only. This is not an engineering task; it is a legal/business decision. No engineering action unblocks it.

**CPP-2. Real-corpus document acquisition**
Quality model calibration (D1), SIRO XSD validation (D4), classification depth (D3), and volume/latency characterization (D19) all require a labelled corpus of real CNBV/Banamex documents. This is a data-acquisition / bank-relationship task.

**CPP-3. Key Vault and observability infrastructure provisioning (ops-gated gaps)**
SIARA credential vault (P8), JWT key management (P13, D17), structured log sinks (D8), observability backend (D14), and alerting (P11) all require cloud/ops environment provisioning decisions. These are procurement and ops decisions, not engineering.

---

### Wave 0 — Minimum Deployable Composition (BUILDABLE NOW, unblocked by legal/corpus/ops gates)

Completing these 8 items allows the full Prisma MVP to run in a containerized environment for the first time, end-to-end, against the simulator.

| W0 item | Gap # | Description | Effort |
|---------|--------|-------------|--------|
| W0.1 | P1 | Author Dockerfiles for all 4 production hosts; publish steps in CI | M |
| W0.2 | P2 | Add auto-migration runner (or `--migrate-only` CLI) to each worker; document multi-DbContext sequencing | S |
| W0.3 | P3 | Remove `DESKTOP-FB2ES22` and `(localdb)` from Web.UI appsettings; add env-var references; produce `.env.example` | S |
| W0.4 | P4/P5 | Replace hardcoded SIARA URL and Windows log path with env-var references | S |
| W0.5 | P9 | Wire real `IReadinessProbe` in Reconciliator; add `/health/ready` returning 503 until subscribed | S |
| W0.6 | P10 | Register real `IHealthCheck` (DB + service) in Web.UI; wire into `MapHealthChecks` | S |
| W0.7 | D9 | Author `docker-compose.dev.yml` wiring all 6 components (Orion+Athena+Reconciliator+UI+Simulator+SQL+Seq) | S |
| W0.8 | D8 | Add Serilog remote sink to all 3 worker `appsettings.json`; configure log-level minimums | S |

Estimated effort: ~1 sprint (2–3 weeks for a single developer).
Gate: after W0, any developer can `docker-compose up` the full stack and submit a document via the SIARA simulator with a single command.

---

### Wave 1 — Operational Readiness (BUILDABLE NOW)

| W1 item | Gap # | Description | Effort |
|---------|--------|-------------|--------|
| W1.1 | P6 | Real-TCP cross-process proof: author an acceptance test booting 3 OS processes over real TCP | M |
| W1.2 | P12 | Author production operational runbook (startup sequence, migration order, failure triage, SIARA rotation) | M |
| W1.3 | D12 | Wire `RecordDocumentProcessed()` into orchestrators; fix worker dashboard metrics returning zeros | S |
| W1.4 | D7 | Replace hardcoded Seq endpoint with env-var; add to `.env.example` | S |
| W1.5 | D24 | Extend CI pipeline with Docker image build + publish + push steps | M |
| W1.6 | D22 | Audit all `LogAuditAsync` call sites for `processId` parameter adoption; fix gaps | S |
| W1.7 | D21 | Reproduce + fix Tesseract second-init deadlock; add restart regression test | M |
| W1.8 | P16 | Trace Sentinel service; classify its status; wire health probes or document as out-of-MVP | L |

---

### Wave 2 — Persistence & Durability (BUILDABLE NOW)

| W2 item | Gap # | Description | Effort |
|---------|--------|-------------|--------|
| W2.1 | P15 | Convert `AuditRecords` to SQL Server 2022 append-only ledger or add `DENY UPDATE/DELETE` + row HMAC | M |
| W2.2 | D6 | Implement `PersonIdentityResolver` DB persistence (EF entity + migration + repository) | M |
| W2.3 | D20 | Add failure-mode integration test suite (Athena unreachable, Tesseract fail, SIARA 503, Reconciliator restart) | M |
| W2.4 | D10 | Add reconnect/retry integration test for hub-client disconnect mid-run | M |
| W2.5 | D11 | Author SIARA session-longevity soak test (50 documents sequential against simulator) | M |

---

### Wave 3 — Security Hardening (PARTIALLY BUSINESS-GATED; flag for dedicated security review)

| W3 item | Gap # | Description | Effort | Gate |
|---------|--------|-------------|--------|------|
| W3.1 | P13 | Implement per-process asymmetric JWT keys (RS256/ECDSA); remove shared HMAC secret; update ADR-012 | M | technical |
| W3.2 | P8 | Implement vault-backed `ISiaraCredentialSource`; wire in Orion.Worker for AutomatedLogin mode | M | ops-gated |
| W3.3 | P14 | Apply field-level AES-GCM column encryption to regulated PII in `PrismaDbContext` entities | L | business-gated |
| W3.4 | D17 | Implement overlapping-key grace period in `IProcessClearanceTokenService` for zero-downtime rotation | M | technical |
| W3.5 | D16 | Design and implement fail-closed audit mode for classified document types; document fail-open policy | M | business-gated |
| W3.6 | D15 | Design network segmentation policy (k8s NetworkPolicy) restricting hub access; evaluate mTLS | M | business-gated |
| W3.7 | D18 | Engage counsel on CNBV CUB outsourcing-regime obligations; document data-residency requirements | L | business-gated |
| W3.8 | D23 | Commission ISO 27001 / SOC 2 gap assessment; produce certification roadmap | L | business-gated |
| W3.9 | P11 | Configure alerting (SLA breach + pipeline error); wire on-call escalation path | M | ops-gated |

---

### Wave 4 — Real-Data / Live-Portal Validation (LEGAL-GATED and CORPUS-GATED)

Unblockable until CPP-1 (legal gate P1) and CPP-2 (corpus acquisition) are resolved.

| W4 item | Gap # | Description |
|---------|--------|-------------|
| W4.1 | P7 | Enable `Siara:AllowProductionHost=true` after counsel sign-off; run first live-SIARA ingestion test |
| W4.2 | D1 | Retrain and validate quality model against real CONDUSEF/Banamex document corpus; set `TrainedDate` |
| W4.3 | D19 | Volume soak test at realistic cadence (≤2k/day); characterize p95 per-document latency |
| W4.4 | D4 | Validate SIRO XSD conformance once Banamex supplies the `.xsd` (issue #2 dependency) |
| W4.5 | D3 | Evaluate semantic/NLP classification once real document corpus is available |
| W4.6 | D14 | Verify observability backend receives metrics in staging; author SLA alert rules |

---

## 5. Unknown-Unknowns Surfaced (Prisma)

| # | Unknown-unknown | Severity | Buildable? |
|---|-----------------|----------|------------|
| U1 | **Sentinel Monitor is a black box.** No startup code, auth, health checks, or observability found. Could be empty scaffolding or partially wired. Its readiness state is unknown. | **Blocks** | After tracing (Gap P16) |
| U2 | **Multi-DbContext migration sequencing is undocumented.** Three separate DbContext types across multiple hosts have no documented application order, no CI orchestration, and no runbook step. A wrong sequence on first-run could leave the schema in an inconsistent state. | **Blocks (first-run ops)** | Buildable now (Gap P2) |
| U3 | **Tesseract second-init deadlock under pipeline restart.** The partial-case gate test disables the pipeline to avoid this. The defect could surface in any production scenario where Athena is restarted without a full process restart. | **Degrades** | Buildable now (Gap D21) |
| U4 | **Quality model provenance is unverifiable.** Real coefficients exist but `TrainedDate=null`, `TrainingDataSize=0`. There is no way to know when the model was trained, on what data, or whether it is still valid. A data drift event would be invisible. | **Degrades** | Corpus-gated (Gap D1) |
| U5 | **No secret rotation coordination mechanism.** Three workers share a JWT secret with no rolling-rotation support. A single leaked secret requires simultaneous emergency redeploy of all three workers with no runbook, no rollback path, and no documented procedure. | **Blocks (incident response)** | Buildable now (Gap D17, P13) |
| U6 | **Worker metrics counters wired but never called.** `RecordDocumentProcessed()` is defined in both dashboard services but zero orchestrators call it. The dashboard shows zeros even when the pipeline is processing documents at full rate. Operators have no visibility into headless worker throughput. | **Degrades** | Buildable now (Gap D12) |
| U7 | **`ProcessId` audit adoption incomplete.** The backward-compatible API exists but call-site adoption was not verified. Audit rows may systematically lack process identity, making forensic non-repudiation impossible even though the schema supports it. | **Degrades** | Buildable now (Gap D22) |
| U8 | **No chaos / failure-mode regression coverage.** "InsufficientData / best-effort on failure" is documented but not tested at the pipeline boundary. The first production failure that doesn't match a covered failure mode may produce an unhandled exception instead of a graceful degradation. | **Degrades** | Buildable now (Gap D20) |

---

## 6. Supersession Note

This document (RC5) provides the ground-truth Prisma MVP readiness state as of 2026-06-18 (branch `Liv`). It supersedes:
- `GAP-MATRIX-2026-06-11.md` — for all Prisma MVP content; its A1–A6 "Planned/Partial" classifications are refuted by ground-truth wiring.
- `GAP-MATRIX-2026-06-dual-ground-truth.md` — for all Prisma content.
- `MVP-PATH-2026-06-11.md` — the ordered work plan; all Workstream 1–5 items are DONE (MVP gate issue #5 closed 2026-06-14).
- `MVP-DEFINITION-2026-06.md` — the agreed MVP bar was reached; this matrix targets the **full-production** bar (RC Brief §2).

Key corrections to CLAUDE.md and the prior matrices recorded here:
- **"StubDocumentDownloader wired"** — **REFUTED.** Real `SiaraDocumentDownloader` is wired; stub is dead code.
- **"IngestionOrchestrator.StartAsync() is a placeholder"** — **REFUTED.** `SiaraWatchLoop.RunAsync` is the real poll loop; placeholder is removed.
- **"trained filter-selection models are stub coefficients"** — **REFUTED.** `PolynomialModelOptions.cs` carries real GA-optimized coefficients.
- **"B1 single-source fusion in worker path"** — **REFUTED** for the production `ExtractionOrchestrator` path; still true for the legacy dead `ProcessingOrchestrator` ROP.
- **"E1 readiness probes stubbed"** — **PARTIALLY REFUTED:** Athena and Orion probes are real; Reconciliator is still stubbed (Gap P9).

**Gap count summary:**
- Blocks-production: 16 (P1–P16)
- Degrades: 24 (D1–D24)
- Cosmetic: 4 (C1–C4)
- Total open gaps: 44
- Of the 16 Blocks: 3 are legal/business-gated (P7, P8, P11), 1 is fully unknown/trace-needed (P16), 12 are buildable now.

---

*RC5 synthesis complete. No production code was read, modified, or created in the course of writing this document.*
