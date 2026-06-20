# ExxerCube.Prisma QA Harness — Capability Inventory

**Status:** Phase-1 as-built. **Date:** 2026-06-20. **Branch:** `Liv`.
**Companion docs:** [`ARCHITECTURE.md`](./ARCHITECTURE.md) · [`LIMITATIONS.md`](./LIMITATIONS.md) · [`README.md`](./README.md)

This inventory lists what the harness **actually provides today**, with an honest
maturity tag per capability. The harness provides infrastructure + validation
**primitives only** — it makes **no PASS/FAIL or quality judgment** (that is the
job of the independent Phase-2 QA agent).

## Maturity legend
- **PROVEN-LIVE** — exercised end-to-end against the running system in an executed test (Docker live).
- **UNIT-PROVEN** — logic exercised by a green fast test (no Docker) against real fixtures/representative input.
- **WIRED** — implemented, compiles into the harness, behaves honestly, but its full live path needs an environment gate (live host / corpus / browser) not yet executed in the self-test.

| # | Capability | Component | Maturity | Notes / evidence |
|---|-----------|-----------|----------|------------------|
| **Environment Provisioning** |
| 1 | Docker liveness gate | `DockerHealthGate` | UNIT-PROVEN | `docker info` probe, timeout, never throws → `CapabilityStatus`. |
| 2 | SQL Server provisioning | `PrismaEnvironmentProvisioner` + `SqlServerContainerFixture` | PROVEN-LIVE | Real container + isolated DB stood up in `HarnessIntegration_ProvisionBootHealthCheck` (~2m45s run). †PROVEN-LIVE here = the provision→boot→workflow→report cycle is exercised; the test deliberately does NOT assert product `IsHealthy=true` (would require EF migrations on the isolated DB, and the harness makes no PASS/FAIL judgment). |
| 3 | Corpus seeding (4-priority chain) | `CorpusSeeder` | UNIT-PROVEN | SIARA dir → Python `generate_corpus.py` (`--num`/`--output <file>`) → static `Prisma/Code/Fixtures/` → AbsentNoGenerator. CLI shows `RestoredFromFixtures` with `--repo-root`. |
| 4 | Repo-root resolution | `ProvisioningOptions.RepoRoot` / `PRISMA_REPO_ROOT` / walk-up | UNIT-PROVEN | Needed because `BuildArtifacts` lives outside the repo tree. |
| 5 | Ollama provisioning | (hook present) | WIRED | Deferred; reports capability-unavailable until enabled. |
| **Application Startup** |
| 6 | Web UI host (dual TestServer+Kestrel) | `PrismaWebUiHostController` | PROVEN-LIVE | Boots real Web.UI, binds Kestrel, probes `/health`; proven in the integration test. |
| 7 | Three-process pipeline host | `ThreeProcessHostController` | WIRED | Boots Orion/Athena/Reconciliator via `BuildThreeHostsWithDb`; verifies all 3 Services non-null before reporting healthy. Full live 3-host proof is corpus/Docker-compose-gated. |
| **Workflow Library** (`IWorkflowRunner` + capability gating + auto-traceability) |
| 8 | HealthCheck workflow | `HealthCheckWorkflow` | PROVEN-LIVE | Real `/health/live` + `/health/ready` GETs; status recorded, not judged. |
| 9 | Config workflow | `ConfigWorkflow` | WIRED | HTTP reachability + config-key read (non-sensitive). |
| 10 | Login workflow | `LoginWorkflow` | WIRED | Real Playwright navigation/fill/submit + screenshot when an `IPage` is supplied; abort-path unit-tested. Needs a Playwright browser + live UI to run. |
| 11 | Manual-review workflow | `ManualReviewWorkflow` | WIRED | Playwright review-queue navigation + screenshot + row count. |
| 12 | Ingestion workflow | `IngestionWorkflow` + `OrchestratorIngestionDriver` | WIRED | Real `IngestionOrchestrator.IngestCaseAsync` when resolvable; corpus-absent → `Aborted("CorpusAbsent")`. Never fabricates success. |
| 13 | Export workflow | `ExportWorkflow` + `AdaptiveExporterExportTrigger` | WIRED | Subscribes to the real `ExportCompletedEvent` (60s timeout) + harvests outputs. Needs the 3-process pipeline. |
| **Domain Validators** (report findings + structural `IsConformant` only) |
| 14 | SIRO XML structure + schema | `SiroXmlStructureValidator`, `SiroXmlSchemaValidator` | UNIT-PROVEN | Grounded in real `SiroXmlExporter` (ns `http://siro.regulatory.namespace`, root `SiroResponse`, `NumeroExpediente`/`NumeroOficio`). |
| 15 | Excel 24-column export | `ExcelExportValidator` | UNIT-PROVEN | Headers match `DatosCargaOficioTemplate.Default` exactly (24, text+order). |
| 16 | OCR vs ground truth | `OcrTextValidator` | UNIT-PROVEN | Field-presence match against expected. |
| 17 | Audit-trail completeness | `AuditTrailValidator` | UNIT-PROVEN | Rows present; ProcessId-null reported as Minor observation (nullable-by-design for legacy rows — not a hard fail). |
| 18 | Fusion output | `FusionOutputValidator` | UNIT-PROVEN | `.fusion.json` presence + key-field population. |
| 19 | Health endpoint | `HealthEndpointValidator` | UNIT-PROVEN | Handles BOTH `text/plain "Healthy"` (the real default) and Microsoft JSON shape. |
| **Evidence Collection** |
| 20 | File harvest | `FileEvidenceCollector` | UNIT-PROVEN | Glob harvest into `EvidenceItem`s. |
| 21 | Screenshot | `PlaywrightEvidenceCollector` | WIRED | `IPage.ScreenshotAsync`; needs a live page. |
| 22 | Log snapshot | `LogEvidenceCollector` + `InMemoryLogBuffer` | UNIT-PROVEN | ILogger-buffer capture (Serilog not centrally pinned for this lib). |
| 23 | Network HAR / video | (opt-in hooks) | WIRED | Off by default; report capability-unavailable until enabled. |
| 24 | Composite collector | `CompositeEvidenceCollector` | UNIT-PROVEN | Fulfils `IEvidenceCollector`, delegates, graceful on null sub-collectors. |
| **Traceability** |
| 25 | Traceability map | `TraceabilityMap` | UNIT-PROVEN | Thread-safe, JSON-serializable; auto-populated from workflow `Traces*` attributes (real prd.md FR/NFR IDs). |
| **Reporting** |
| 26 | Markdown report | `MarkdownReportWriter` | UNIT-PROVEN + PROVEN-LIVE | 10 Phase-2 sections; NO pass/fail emitted; written in both self-tests. |
| 27 | HTML report | `HtmlReportWriter` | UNIT-PROVEN | Wraps markdown, inline CSS, no external dep. |
| 28 | JSON report | `JsonReportWriter` | UNIT-PROVEN | `System.Text.Json`, machine-consumable. |
| **CLI** |
| 29 | Shell front-end | `ExxerCube.Prisma.QaHarness.Cli` | PROVEN-LIVE | `--help`/`--workflow`/`--all-workflows`/`--provision-only`/`--report-format`/`--output-dir`/`--repo-root`/`--skip-docker`/`--no-seed-corpus`; exit 0 on run (no verdict). |

## Test coverage summary (as-built)
- **116 self-tests green** (114 fast/no-Docker + 2 in `HarnessIntegrationTests`: 1 fast e2e + 1 live integration).
- Live integration proven on a Docker box (SQL container + Web UI boot + workflow + report).
- All builds 0 warnings / 0 errors under `TreatWarningsAsErrors`.
