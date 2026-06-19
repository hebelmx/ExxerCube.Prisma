# RC5d — Prisma MVP: Deploy/Ops + Real-Data Journey + Security Lenses

**Date:** 2026-06-18 · **Branch:** Liv · **Author:** Architect (Winston) — read-only audit
**Feeds into:** RC6 cross-cutting synthesis
**Methodology:** Three parallel investigation agents; evidence-first, refute-don't-accept.

---

## Lens 1 — Deploy / Ops

### 1.1 Hardcoded / Machine-Specific Values

| Topic | State today (path / evidence) | Gap | Severity |
|-------|-------------------------------|-----|----------|
| SQL Server hostname `DESKTOP-FB2ES22` | `07 UI/UI/ExxerCube.Prisma.Web.UI/appsettings.Development.json` lines 4–5: `Server=DESKTOP-FB2ES22\\SQL2022;Database=PrismaID` and `Database=Prisma` | Dev-only file committed to repo; will prevent any off-box developer launch without manual override; not in Production appsettings | Cosmetic (dev file) but Blocks demo on any box that is not `DESKTOP-FB2ES22` |
| LocalDB fallback in Web.UI base appsettings | `appsettings.json` line 4: `(localdb)\\MSSQLLocalDB` | Windows-only; non-portable; blocks any Linux/Docker deploy of Web.UI without env-var override | Degrades (blocks containerisation) |
| Seq endpoint `http://localhost:5341` | Web.UI `appsettings.json` lines 73 + 92; Siara.Simulator `appsettings.json` line 53 | Hardcoded; override requires `Serilog:WriteTo:Seq:serverUrl` and `OpenTelemetry:Seq:Endpoint` env vars — not documented anywhere in the repo | Degrades |
| Siara simulator URL `https://localhost:5002` | Web.UI `appsettings.json` line 15: `"SiaraUrl": "https://localhost:5002"` | Only usable when simulator runs on same box; prod must point at real SIARA host | Blocks (prod) |
| Windows absolute path `C:\\SiaraData\\logs\\` | `tools/Siara.Simulator/appsettings.Production.json` line 33; `Prisma/Deployments/Siara.Simulator/app/appsettings.Production.json` line 33 | Hard Windows path in a Production config — fatal on Linux container or any non-C: host | Blocks |
| DEV-PLACEHOLDER connection strings (workers) | All three worker `appsettings.json` line 3: `"DefaultConnection": "DEV-PLACEHOLDER: set via …"` | By design — workers require `ConnectionStrings__DefaultConnection` env var; graceful skip if absent | Not a gap (design choice documented) |
| JWT secret DEV-PLACEHOLDER (workers) | Athena/Orion `appsettings.json` line 12: `"JwtSecret": "DEV-ONLY-PLACEHOLDER-REPLACE-VIA-ENV-OR-KEYVAULT-IN-PRODUCTION"` | By design; requires env-var override before production | Not a gap (design choice documented) |

**Hardcoded-config headline:** The critical machine-specific value is `Server=DESKTOP-FB2ES22\\SQL2022` in `appsettings.Development.json` (Web.UI, lines 4–5). Secondary: `(localdb)\\MSSQLLocalDB` in the base `appsettings.json` blocks containerisation; `https://localhost:5002` blocks real SIARA integration; `C:\\SiaraData\\logs\\` in the Simulator's Production config will crash on any Linux/non-C: host.

---

### 1.2 Secrets Posture

| Topic | State today (path / evidence) | Gap | Severity |
|-------|-------------------------------|-----|----------|
| DB credentials | All worker appsettings use `DEV-PLACEHOLDER` with explicit instruction to supply via env var or user-secrets | No actual credential in repo; pattern is correct | None |
| JWT process-identity secret | `DEV-ONLY-PLACEHOLDER` in worker appsettings; identical shared secret across Orion/Athena/Reconciliator (symmetric HMAC — see Security section) | Shared key means no per-process blast-radius isolation; asymmetric hardening deferred (ADR-012 addendum, OD-1–OD-7 open) | Degrades (security posture) |
| SIARA credentials | `ISiaraCredentialSource` port defined; `ConfiguredSiaraCredentialSource` reads `Vault:Siara:User/Pass` from configuration — but no vault provider is wired in any host's Program.cs | Production credential injection path exists in code but has zero wiring; must be solved before `AutomatedLogin` mode is enabled | Blocks (production automation) |
| Azure Key Vault | `CertificateOptions.cs` + `DigitalPdfSigner.cs` wire Azure SDK for PDF-signing certs; `DefaultAzureCredential()` used | Certificate path ready; general-purpose vault for other secrets is NOT wired | Degrades |
| User Secrets (`UserSecretsId`) | Web.UI `.csproj` has `UserSecretsId` — workers do NOT | Workers have no local development secret override mechanism beyond env vars | Degrades (DX) |
| Plaintext secrets in source | None found — only placeholders with explicit production-override instructions | — | None |

---

### 1.3 Deploy Artifacts

| Host | Dockerfile | docker-compose | K8s / Helm | CI publish step |
|------|-----------|---------------|------------|-----------------|
| Web.UI | **ABSENT** | **ABSENT** | **ABSENT** | **ABSENT** |
| Athena Worker | **ABSENT** | **ABSENT** | **ABSENT** | **ABSENT** |
| Orion Worker | **ABSENT** | **ABSENT** | **ABSENT** | **ABSENT** |
| Reconciliator Worker | **ABSENT** | **ABSENT** | **ABSENT** | **ABSENT** |
| Siara.Simulator | Present (`Prisma/Deployments/Siara.Simulator/`) | Present (same folder) | **ABSENT** | **ABSENT** |
| CI quality gate | `Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` — build + test + security scan only | — | — | No deploy steps |

**Verdict:** Zero deployment artifacts exist for the four production hosts. The Siara Simulator is the only containerised component. The CI pipeline is quality-gate only; there is no publish, tag, or deploy stage.

| Topic | State today (path / evidence) | Gap | Severity |
|-------|-------------------------------|-----|----------|
| Dockerfiles for 4 hosts | Absent (confirmed by full repo glob) | Must create before any cloud/container deployment | Blocks |
| docker-compose for local dev | Absent | Developer cannot stand up the full stack with one command | Degrades |
| K8s / Helm charts | Absent | No path to cloud production deployment | Blocks |
| CI deploy pipeline | Absent (quality gate only) | No automated release path | Blocks |

---

### 1.4 EF Core Migrations Runner

| Host | Migration call at startup | Evidence |
|------|--------------------------|----------|
| Web.UI | `app.UseMigrationsEndPoint()` (dev only) — development endpoint, NOT automatic apply | `07 UI/.../Program.cs` line 97 |
| Athena Worker | None | Program.cs lines 29–41: graceful DB skip if placeholder; no `MigrateAsync` |
| Orion Worker | None | Same pattern |
| Reconciliator Worker | None | Same pattern |
| Veriqan subsystem (separate) | `VeriqanDbInitialiser.cs` line 54: `await context.Database.MigrateAsync(ct)` — real auto-migrate | Only Veriqan has auto-migration |

**Gap:** Production first-run for all Prisma hosts requires a manual `dotnet ef database update` or a dedicated migration job before workers start. No auto-migration runner exists in any Prisma worker. Schema divergence across services (three DbContext scopes: `PrismaDbContext`, `ApplicationDbContext` for Identity, `ExportAdaptiveDbContext`) multiplies the migration coordination problem.

| Topic | State today | Gap | Severity |
|-------|------------|-----|----------|
| Auto-migration on worker startup | Absent in all 3 workers; dev-only endpoint in Web.UI | Production first-run schema bootstrap is a manual step — no operator runbook documents it | Blocks |
| Multi-context migration sequencing | Three separate DbContext types across hosts | Migration order dependency undocumented | Degrades |

---

### 1.5 Health / Readiness Probes

| Service | `/health/live` | `/health/ready` | Actual or stubbed? | Evidence |
|---------|---------------|-----------------|-------------------|----------|
| Athena Worker | Present | Present — delegates to `IReadinessProbe` (ExtractionPipelineService) | **Real readiness probe** | `AthenaHealthCheckService.cs` |
| Orion Worker | Present | Present — delegates to `IReadinessProbe` (SiaraWatchLoop) | **Real readiness probe** | `OrionHealthCheckService.cs` |
| Reconciliator Worker | `/health` + `/health/live` hardcoded `"Healthy"` | **ABSENT** | **Stubbed / hardcoded** | `Reconciliator/Program.cs` lines 112–114 comment: "no orchestrator readiness gate yet" |
| Web.UI | `/health` mapped via `MapHealthChecks` | Present (`HealthCheckController` GET `/api/healthcheck`) | **Stubbed** — controller returns hardcoded `"Healthy"`, no component checks | `HealthCheckController.cs` line 39 |
| Sentinel | Not found | Not found | **ABSENT** | No startup code located |
| Dashboard metrics (all workers) | Exists at `/dashboard` | — | **Zeros / TODO** | `AthenaDashboardService.cs` / `OrionDashboardService.cs` lines 46–49: `// TODO: Get actual metrics` |

---

### 1.6 Observability

| Topic | State today (path / evidence) | Gap | Severity |
|-------|-------------------------------|-----|----------|
| OpenTelemetry exporter | Web.UI `Program.cs` lines 409–450: OTLP traces + metrics exported to Seq at `http://localhost:5341` | Seq endpoint hardcoded to localhost; production requires env-var override; no Grafana/Prometheus/Jaeger | Degrades |
| Serilog sinks | Web.UI: Console + File + Seq. Workers: Console only (no file/Seq sinks in worker appsettings) | Workers have no structured remote log sink in production | Degrades |
| Custom SLA metrics | `"ExxerCube.Prisma.SLA"` Meter registered in Web.UI | Metric exists but no alerting rules or dashboards consume it | Degrades |
| Dashboards | Seq (if deployed) for ad-hoc queries only | No Grafana boards, no alert rules | Degrades |
| Alerting | None | No on-call/paging configured | Blocks (production ops) |
| Operational runbook | None found (only `docs/demos/CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` — demo only) | Zero operational runbooks for incident response | Blocks (production ops) |

---

### 1.7 Template Leftovers / Demo Cruft

| Item | Status | Evidence |
|------|--------|----------|
| `Counter.razor` | **ABSENT** — removed | Grep found no match |
| `Weather.razor` | **ABSENT** — removed | Grep found no match |
| Demo capture runbook | Present: `docs/demos/CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` | Not a code artifact; appropriate to keep in docs | Cosmetic |

**Template leftovers are clean.** The CLAUDE.md warning about Counter/Weather is stale — both have been removed.

---

## Lens 2 — Real-Data Journey

### 2.1 Orion Ingestion Layer

**Correction vs. CLAUDE.md / prior gap-matrix:** The Orion Worker composition root is **more complete than CLAUDE.md describes.** Stubs are dead code; the real scraper and real hub are wired.

| Component | State today | Evidence |
|-----------|------------|----------|
| `IDocumentDownloader` | **REAL**: `SiaraDocumentDownloader` wired | `Orion/Program.cs` line 64 — explicit comment "Replaces StubDocumentDownloader" |
| `StubDocumentDownloader` | Dead code — not registered | Exists in source but no DI registration |
| `IExxerHub<DocumentDownloadedEvent>` | **REAL**: `SignalRIngestionBroadcaster` wired | `Orion/Program.cs` line 137 — "Replaces StubExxerHub" |
| `StubExxerHub` | Dead code — not registered | Exists in source but no DI registration |
| `SiaraWatchLoop` (poll/watcher) | **REAL**: registered as `IHostedService` + `IReadinessProbe` | `Orion/Program.cs` line 201; `SiaraWatchLoop` is a live headless poller |
| `SiaraNavigationTarget` / `DocumentIngestionService` | **REAL**: wired in worker | `Orion/Program.cs` lines 54–56; not just in Web.UI |
| Stubs in Orion DI | **Zero** | Both stubs are dead; all production paths are wired |

---

### 2.2 Athena Processing Pipeline

| Stage | Implementation | State | Evidence |
|-------|---------------|-------|----------|
| Stage 1 — Quality | `PolynomialImageQualityAnalyzer` (Emgu.CV) | **Real** | `Athena/Program.cs` line 77 |
| Stage 2 — OCR | `TesseractOcrExecutor` (native Tesseract.NET) | **Real** | `Athena/Program.cs` line 78 |
| Stage 3 — Fusion | `FusionExpedienteService` (multi-source voting, 35+ fields) | **Real** | `Athena/Program.cs` lines 83–85; `FusionExpedienteService.cs` lines 53–120 |
| Stage 4 — Classification | `FileClassifierService` | **Real** | `Athena/Program.cs` line 86 |
| Stage 5 — Export | Intentionally NOT in Athena (Reconciliator owns it) | By design | `Athena/Program.cs` comment lines 87–89 |
| Multi-source extractors | `XmlFieldExtractor`, `DocxFieldExtractor`, `AdaptiveTxtFieldExtractor` all wired | **Real** | `Athena/Program.cs` lines 91–96 |

**No stubs wired in Athena.** Quality model uses stub coefficients (trained filter-selection not real data); this is the only functional degradation noted in CLAUDE.md and confirmed.

---

### 2.3 Database / Persistence Path

| Item | State today | Gap | Severity |
|------|------------|-----|----------|
| Auto-migration | Absent in workers | Manual `dotnet ef database update` required before first run | Blocks |
| DB unreachable on startup | Graceful skip — workers boot, audit disabled | Fail-open on audit; pipeline continues | Accepted |
| `EventPersistenceWorker` | Registered conditionally in all hosts when non-placeholder connection string present | Real wiring; subscribed to `GetAllEventsStream()` | None |
| `DESKTOP-FB2ES22` in production path | Only in `appsettings.Development.json` (Web.UI); base appsettings uses LocalDB placeholder | LocalDB blocks Linux/containerised deployment | Degrades |

---

### 2.4 End-to-End Evidence — Is There Proof a Real Document Has Flowed the Full Path?

**YES — proven E2E exists:**

`Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateFullPipelineE2ETests.cs`

What this test actually exercises:
1. Real headless Playwright login to the live Siara Simulator → credential-free storage state
2. Three real worker hosts booted (Orion + Athena + Reconciliator) wired to SQL Server (Testcontainers)
3. Real case discovery from the simulator (3-companion package: PDF + DOCX + XML)
4. Real `SiaraDocumentDownloader` download + SignalR broadcast
5. Real OCR (Tesseract), real quality analysis (Emgu.CV), real multi-source fusion
6. Real SIRO XML export + DatosCargaOficio xlsx export from Reconciliator
7. All three workers persist audit rows to real SQL Server
8. Timeout: 15 minutes (900,000 ms) — genuine slow operations, not mocked

**Caveat:** The test uses the `Siara.Simulator` (not real `siara.cnbv.gob.mx`). The real SIARA host requires legal pre-clearance (ADR-010 P1 — PENDING counsel sign-off).

Also present: `MaxFidelityGatePartialCaseE2ETests.cs` — partial case (extraction pipeline disabled to avoid Tesseract second-init deadlock; known issue).

| Question | Answer |
|----------|--------|
| Any real-data E2E test? | **YES** — `MaxFidelityGateFullPipelineE2ETests` against Siara Simulator + Testcontainers SQL |
| Against real SIARA host? | **NO** — simulator only; real SIARA blocked on legal gate P1 |
| Against real corpus documents? | **NO** — simulator-generated fixtures only |
| Is partial-case path covered? | Yes — `MaxFidelityGatePartialCaseE2ETests` |

---

### 2.5 Dashboard / Metrics — Real vs. Zeros

| Service | Dashboard endpoint | Metrics | Evidence |
|---------|-------------------|---------|----------|
| Athena | `/dashboard` — exists | **All zeros / TODO** | `AthenaDashboardService.cs` lines 46–49: `// TODO: Get actual metrics` |
| Orion | `/dashboard` — exists | **All zeros / TODO** | `OrionDashboardService.cs` lines 46–49: same TODO; `RecordDocumentProcessed()` method defined but never called |
| Web.UI | `IProcessingMetricsService` — wired | **Real** — UI Dashboard uses this, not worker dashboard | Confirmed by CLAUDE.md |

---

### 2.6 Web.UI Review Flow

| Component | State | Evidence |
|-----------|-------|----------|
| `ManualReviewDashboard.razor` (`/manual-review`) | **Real** — displays Pending/LowConfidence/InProgress/CompletedToday counts; bulk actions (Assign/Approve/Reject); requires `[Authorize(Roles = "Reviewer,Admin")]` | `07 UI/.../Pages/ManualReviewDashboard.razor` |
| `ReviewCaseDetail.razor` (`/manual-review/{CaseId}`) | **Real** — shows fields, SLA timeline, confidence score, Approve/Reject buttons | `07 UI/.../Pages/ReviewCaseDetail.razor` |
| `IManualReviewerPanel` | Wired to `ManualReviewerService` → database | `ServiceCollectionExtensions.cs` line 88 |
| Review decision persistence | **Real** — flows through `ManualReviewerService` to `PrismaDbContext` | Confirmed |

Reviewers can see and action real documents. The manual-review loop is wired to real persistence.

---

## Lens 3 — Security Flags (all items marked: defer-to-security-review)

### 3.1 Three-Process Split (A1–A6)

| # | Topic | State today | Gap | Severity |
|---|-------|------------|-----|----------|
| S1 | Symmetric HMAC shared key across all 3 processes | All three workers share the same `ProcessIdentity:JwtSecret`; any process holding the key can mint tokens for any clearance | Asymmetric per-process keys deferred post-MVP (ADR-012 addendum §9; OD-1–OD-7 open) | **High** — defer-to-security-review |
| S2 | Cross-process hub auth enforced | Orion hub `[Authorize(Policy = "RequireExtractClearance")]`; Athena hub `[Authorize(Policy = "RequireReconcileClearance")]`; per-message clearance tokens validated in forwarders | Properly implemented; no gap | Low |
| S3 | IndFusion.Ember transport wired | `SignalRIngestionBroadcaster` + `SignalRReconciliationBroadcaster` wired in both workers | Stubs dead code; real transport confirmed | Low |
| S4 | Credential-free SIARA guarantee | `SiaraSession` value object carries no username/password members (structural guarantee); credentials handled as transient `char[]`, never serialized or logged | Legal gate P1 (counsel sign-off) still PENDING | Medium — defer-to-security-review |

---

### 3.2 Authn/Authz Across Hosts

| # | Host | Auth mechanism | Hub/endpoint authz | Gap | Severity |
|---|------|---------------|-------------------|-----|----------|
| S5 | Orion Worker | JWT bearer (symmetric HMAC); `ValidateLifetime=true`; `ClockSkew=30s` | `IngestionHub` `[Authorize]` enforced | Shared key blast radius (S1) | High |
| S6 | Athena Worker | JWT bearer (same shared key) | `ReconciliationHub` `[Authorize]` enforced | Same shared key risk | High |
| S7 | Web.UI | ASP.NET Core Identity cookie auth; no JWT for browser clients | `[Authorize(Roles = "Reviewer,Admin")]` on UI pages | `EfCoreIdentityAdapter` (JWT) exists but NOT registered; Web.UI → worker JWT path absent | Medium — defer-to-security-review |
| S8 | Reconciliator Worker | Not fully audited; inferred JWT by pattern | Unknown — no hub/endpoint authz confirmed | Status unknown | Medium — defer-to-security-review |
| S9 | Sentinel | No startup code located; auth posture entirely unknown | Unknown | CLAUDE.md: "not yet traced; status unknown" | Medium — defer-to-security-review |

**HubAuthPolicies.cs:** `RequireExtractClearance` and `RequireReconcileClearance` constants are defined and wired. Authorization is real, not commented-out.

---

### 3.3 Secrets Posture

| # | Topic | State today | Gap | Severity |
|---|-------|------------|-----|----------|
| S10 | Vault integration for SIARA credentials | `ISiaraCredentialSource` port defined; `ConfiguredSiaraCredentialSource` reads from config but no vault provider wired | Production automation blocked until vault is wired | **High** — defer-to-security-review |
| S11 | JWT shared secret rotation | No rotation mechanism in code; env-var sourced but no key-rotation policy | Secret rotation requires re-deploy of all 3 workers simultaneously | Medium |
| S12 | Plaintext credentials in committed files | None found (placeholders only; SIARA Simulator uses fake/public credentials only) | — | None |

---

### 3.4 Encryption

| # | Topic | State today | Gap | Severity |
|---|-------|------------|-----|----------|
| S13 | HTTPS enforcement | `app.UseHttpsRedirection()` in Web.UI (`Program.cs` line 106); HSTS configured | Workers have no explicit HTTPS redirect — rely on hosting layer (reverse proxy) | Medium |
| S14 | Database connection TLS | `Encrypt=True` in connection string examples; documented as required | TDE / encryption-at-rest not verified in deployment configuration | Medium — defer-to-security-review |
| S15 | OCR/document field encryption at rest | No field-level encryption found in `PrismaDbContext` entities; `FileMetadata` and extracted expediente fields stored in plaintext | Sensitive legal doc fields (case numbers, party names) in plaintext DB rows | **High** — defer-to-security-review |
| S16 | Veriqan AES-256-CBC | `LegalBaselineToleranceRecord` columns encrypted; NOT applicable to Prisma pipeline | Prisma pipeline has no equivalent | High |
| S17 | PDF signing certs | Azure Key Vault + Windows Store + file fallback; `DefaultAzureCredential()` used | Cert provisioning is deploy-time concern; code is ready | Low |

---

### 3.5 CNBV / Regulatory Compliance

| # | Topic | State today | Gap | Severity |
|---|-------|------------|-----|----------|
| S18 | Legal P1 gate (authorization to automate SIARA) | ADR-010 §Legal preconditions lines 104–166 — PENDING counsel sign-off; codebase has `Siara:AllowProductionHost = false` default | Cannot enable `AutomatedLogin` against `siara.cnbv.gob.mx` without counsel approval | **Critical** — blocks production |
| S19 | CNBV CUB outsourcing regime | ADR-010 acknowledges CNBV context; no CUB outsourcing-control implementation found | No evidence of mandatory independent review controls, data-residency enforcement, or outsourcing-regime artefacts | High — defer-to-security-review |
| S20 | ISO 27001 / SOC 2 | No references found in codebase or docs | Not in MVP scope; no trajectory defined | Medium — defer-to-security-review |
| S21 | Data retention policy | No purge policy; no minimum retention period enforced | Financial/regulatory records may require 7-year retention; nothing enforces this | Medium — defer-to-security-review |

---

### 3.6 Audit Trail

| # | Topic | State today | Gap | Severity |
|---|-------|------------|-----|----------|
| S22 | `EventPersistenceWorker` wiring | Registered conditionally in all hosts; subscribed to `GetAllEventsStream()`; all major pipeline events mapped (`Download → Quality → OCR → Classification → Export → Error`) | Properly wired | None |
| S23 | Actor identity in audit records | `ISiaraActorIdentityProvider` available; ADR-012 §Decision 5 adds optional `processId` param to `IAuditLogger.LogAuditAsync` | Backward-compatible API exists but orchestrators may not universally emit `processId` — partial adoption | Medium |
| S24 | Audit immutability | `AuditRecords` is a standard EF Core table — no temporal tables, no HMAC chaining, no cryptographic proof; DBA can modify records retroactively | Not immutable; accepted gap for MVP (ADR-012 §Consequences) | **High** — defer-to-security-review |

---

## Unknown-Unknowns Surfaced (Prisma)

1. **Sentinel Monitor security + health posture is a black box.** No startup code located; no auth, health checks, or observability artifacts found. CLAUDE.md explicitly flags it as "not yet traced." This service's readiness state is fully unknown — it could be empty scaffolding or it could be partially wired.

2. **Multi-DbContext migration sequencing is undocumented.** Three separate `DbContext` types (`PrismaDbContext`, `ApplicationDbContext` for Identity, `ExportAdaptiveDbContext`) with separate migration histories must be applied in the correct order across multiple hosts. No runbook or CI step orchestrates this.

3. **Tesseract second-init deadlock under multi-worker deployment.** The `MaxFidelityGatePartialCaseE2ETests` explicitly disables the extraction pipeline to avoid a Tesseract re-init deadlock. This latent defect could manifest under any deployment where the Athena pipeline is restarted without full process restart.

4. **Quality analysis uses stub polynomial coefficients.** `PolynomialImageQualityAnalyzer` is wired and real (Emgu.CV), but the trained filter-selection model coefficients are stubs. Under real document volume, quality scores may be miscalibrated — which could cause downstream OCR/fusion decisions to be based on incorrect quality signals. No test validates calibration against real document samples.

5. **No secret rotation mechanism.** The shared JWT secret across three processes has no rotation API or restart-coordination. A single compromised secret requires a coordinated simultaneous re-deploy of all three workers — a manual, error-prone operation with no documented procedure.

6. **Worker metrics counters wired but never called.** `AthenaDashboardService` and `OrionDashboardService` have a `RecordDocumentProcessed()` increment method that is never called from any orchestrator. Dashboard will show zeros even when the pipeline is running documents, masking real throughput from operators.

7. **`ProcessId` audit column adoption is incomplete.** ADR-012 added the field and backward-compatible API, but the call-site audit across orchestrators (IngestionOrchestrator, ProcessingOrchestrator, ReconciliationPipelineService) was not verified. Audit records may systematically lack process identity, defeating forensic non-repudiation.

8. **No chaos / failure-mode test.** No test exercises what happens when Athena becomes unreachable mid-document (Orion has already broadcast), when Tesseract returns garbled text, or when the SIARA simulator returns a 503. The "InsufficientData / abstain on failure" path is asserted in documentation but not regression-tested at the pipeline boundary.

---

## Minimum Deployable-Composition Checklist (Prisma MVP)

The following six items must all be true before the Prisma MVP can be deployed to any environment beyond a developer's own machine:

1. **Dockerfiles + docker-compose for all 4 hosts.** Currently absent for Web.UI, Athena Worker, Orion Worker, Reconciliator Worker. Without these, no deployment is possible to any cloud or server environment.

2. **Migration runner wired in each host (or a dedicated migration job).** Either add `await db.Database.MigrateAsync(ct)` to each worker's startup with a guard, or create a one-shot migration job that runs before all workers start. Document the multi-DbContext application order.

3. **Environment-variable configuration sweep.** Replace every hardcoded value in the deployment path: `DESKTOP-FB2ES22` → param; `(localdb)` → param; `localhost:5341` (Seq) → param; `localhost:5002` (SIARA URL) → param; `C:\\SiaraData\\logs\\` → param. Produce a canonical `.env.example` or Helm `values.yaml`.

4. **SIARA credential vault wired.** Implement a real `ISiaraCredentialSource` backed by the target secret store (Azure Key Vault, or client-provided equivalent). Without this, `AutomatedLogin` mode cannot run against real SIARA; only `InteractiveLogin` or `SessionPassthrough` modes are available.

5. **Legal gate P1 cleared.** Counsel must sign off on the authorization-to-automate SIARA before the worker can be pointed at `siara.cnbv.gob.mx`. Code is gated by `Siara:AllowProductionHost = false`; the flip requires an explicit decision and sign-off record.

6. **Reconciliator + Sentinel health probes made real.** Reconciliator currently returns hardcoded `"Healthy"` with no readiness probe. Sentinel has no health endpoint at all. Both must have meaningful liveness and readiness signals before a production orchestrator (Kubernetes, etc.) can reliably manage process restarts.

---

*End of RC5d — feeds into RC6 cross-cutting synthesis.*
