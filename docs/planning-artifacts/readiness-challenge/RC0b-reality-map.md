# RC0b — Built-Reality Map (Veriqan VEC)

**Date:** 2026-06-18  
**Branch:** `Liv`  
**Auditor:** Claude Code (RC.0 ground-truth agent, read-only, no production code modified)  
**Anchor doc:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` §3b  
**Bar:** Full production — believe wiring and ground-truth runs, never comments or prose.

---

## Part 1 — Composition-Root Wiring Map

### Worker Entry Point

`Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/Program.cs` — 21 lines total.

```
builder.Services.AddVeriqan(builder.Configuration);
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
await app.RunAsync();
```

The Worker is a **`Microsoft.NET.Sdk.Web` ASP.NET Core minimal-API host** (not a Worker SDK host).
It wires all services in ONE call to `AddVeriqan(config)` and then boots. There is **no** HTTP endpoint
that accepts a statement for processing, no hosted background service that polls a queue or folder,
no gRPC endpoint, no message-bus listener. The `IBatchProcessor` is DI-registered but never **called**
from the worker itself — it is only tested from test projects.

### `AddVeriqan` expansion (VeriqanOrchestrationExtensions.cs line 51–103)

| Registration call | What it wires |
|---|---|
| `AddVeriqanIngestion()` | `IStatementIngestionService` → `StatementIngestionService` (Scoped) |
| `AddVeriqanBinding()` | `IProductResolver` → `ProductResolver`, `IBundleBinder` → `BundleBinder` (Scoped) |
| `AddVeriqanVerdict()` | `IVerdictAggregator` → `VerdictAggregator` (Singleton), `ITenantProfileResolver` → `TenantProfileResolver` (Singleton), `TenantProfile` = `LegalBaseline()` (Singleton, SINGLE fixed profile) |
| `AddVeriqanReferenceData()` | `IVecReferenceDataProvider` → `CsvReferenceDataAdapter` (Transient); NO csv root configured (default options) |
| `AddVeriqanExtraction()` | `IStatementFieldExtractor` → `PdfPigStatementFieldExtractor` (Singleton) |
| `AddVeriqanValidation()` | `IVecValidationEngine` → `VecValidationEngine`, all `IVecValidationRule` implementations in Validation assembly (Transient, Scrutor scan) |
| `AddVeriqanVisual()` | All `IVecValidationRule` implementations in Visual assembly (Transient, Scrutor scan) |
| `AddVeriqanReporting(config)` | `IMarkedPdfGenerator` → `MarkedPdfGenerator` (PdfSharp), `IEmailSender` → `SmtpEmailSender` (System.Net.Mail), `IVecAlertService` → `VecAlertService` (Polly retry) |
| Persistence (conditional) | If `"ConnectionStrings:VeriqanDb"` present → EF Core SQL Server + `SqlLegalToleranceProvider` + `VeriqanLegalBaselineStartupService`; else → `AddVeriqanInMemoryPersistence()` |
| `AddSingleton<VeriqanMetrics>()` | OTel Meter + counters (not exported — no OTLP/Prometheus exporter wired) |
| `AddScoped<IVerificationPipeline, VerificationPipeline>()` | The real 8-stage pipeline |
| `AddSingleton<IBatchProcessor, BatchProcessor>()` | Batch processor (wired but **never invoked** by the Worker) |
| `TryAddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>()` | In-memory when SQL absent |
| `TryAddSingleton<IReprocessAuditRepository, InMemoryReprocessAuditRepository>()` | In-memory when SQL absent |
| `AddScoped<IReprocessService, ReprocessService>()` | Reprocess service |

### Per-stage wiring verdict

| Stage | Port / Adapter Wired | Status | Evidence |
|---|---|---|---|
| **Ingest** | `IStatementIngestionService` → `StatementIngestionService` (persists `VerificationJob`, computes SHA-256) | **WIRED (real)** | `VeriqanIngestionExtensions.cs:38` |
| **Extract** | `IStatementFieldExtractor` → `PdfPigStatementFieldExtractor` (real PdfPig text, layout, typography extraction) | **WIRED (real)** | `VeriqanExtractionExtensions.cs:23` |
| **Bind** | `IBundleBinder` → `BundleBinder` + `IVecReferenceDataProvider` → `CsvReferenceDataAdapter` | **WIRED (real adapter, BUT CSV root path unconfigured in Worker — no appsettings.json exists in worker project)** | `VeriqanBindingExtensions.cs:32-33`, Worker has no appsettings.json (git ls-files confirms only `Program.cs` + `.csproj`) |
| **Validate** | `IVecValidationEngine` → `VecValidationEngine` + all `IVecValidationRule` (Scrutor scan, both assemblies) | **WIRED (real)** | `VeriqanValidationServiceCollectionExtensions.cs:53-57`, `VeriqanVisualServiceCollectionExtensions.cs:46-52` |
| **Verdict** | `IVerdictAggregator` → `VerdictAggregator`, `TenantProfile` = single fixed `LegalBaseline()` | **WIRED (real, but single-tenant only — per-statement profile selection deferred to E13)** | `VeriqanVerdictExtensions.cs:39-43` |
| **Report** | `IMarkedPdfGenerator` → `MarkedPdfGenerator` (PdfSharp), `IVecAlertService` → `VecAlertService` (SMTP) | **WIRED (real adapters) but alert SMTP config absent in Worker — no appsettings.json** | `ServiceCollectionExtensions.cs:40,55,58`; no appsettings.json |
| **Persist** | Conditional: SQL (`EfVerificationJobRepository`, `SqlLegalBaselineStore`) OR in-memory fallback | **CONDITIONAL — will use in-memory if `VeriqanDb` connection string absent; Worker has no appsettings.json so defaults to in-memory** | `VeriqanOrchestrationExtensions.cs:73-85` |
| **Notify** | `IBatchProcessor` wired as Singleton; `IReprocessService` wired | **WIRED BUT NEVER INVOKED** — no HTTP endpoint, no background service, no queue consumer in Worker accepts incoming statements | `Program.cs` (absence of any controller/endpoint/hosted service) |

### Key finding on Worker architecture

The Veriqan Worker is an **orphaned shell**: it DI-wires the full stack but provides **zero entry points for statement submission**. There is no HTTP POST endpoint for `StatementSubmission`, no `IHostedService` background loop, no file-watcher, no message-queue consumer. The `IBatchProcessor` is wired but unreachable from the outside. The Worker's only HTTP surface is the `/health` and `/health/live` stubs (hardcoded `"Healthy"`, not real probes). **End-to-end processing is only reachable through the test harnesses (integration + E2E test projects).**

### InMemory repositories (Orchestration/InMemory/)

- `InMemoryVerificationJobRepository` — holds jobs in a `ConcurrentDictionary<Guid, VerificationJob>`
- `InMemoryDispositionRepository` — holds dispositions in a `ConcurrentDictionary`
- `InMemoryVerificationResultStore` — resume-mode content-hash map (in-memory)
- `InMemoryReprocessAuditRepository` — audit log in-memory

All four are **always** registered when no SQL connection string is present (the expected runtime condition in the Worker as shipped, since no appsettings.json exists).

---

## Part 2 — Test-Substrate Inventory

### 2.1 — Test project list

From `git ls-files | grep -i veriqan | grep Tests | grep csproj`:

| Test Project | Location |
|---|---|
| `Veriqan.Application.Tests` | `08 Tests/01 Core/` |
| `Veriqan.Infrastructure.Extraction.Tests` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Infrastructure.ReferenceData.Tests` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Infrastructure.Reporting.Tests` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Infrastructure.Tests.Smoke` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Infrastructure.Validation.Tests` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Infrastructure.Visual.Tests` | `08 Tests/02 Infrastructure/` |
| `Veriqan.Orchestration.Tests` | `08 Tests/03 Orchestration/` |
| `Veriqan.Infrastructure.Persistence.IntegrationTests` | `08 Tests/05 System/` |

### 2.2 — Per-project substrate classification

| Test Project | Substrate | Notes |
|---|---|---|
| `Veriqan.Application.Tests` | In-memory fakes, hand-built domain objects | Unit tests for application services (`StatementIngestionService`, `BundleBinder`, etc.); no real PDF, no real DB |
| `Veriqan.Infrastructure.Extraction.Tests` | **Real synthetic PDFs** (the 3 PRP2 Dummie VEC fixtures in `Prisma/Fixtures/PRP2/`) + some in-memory byte arrays | PdfPig-based extractor runs against real PDF bytes; BUT fixtures are non-compliant synthetics (see §2.3) |
| `Veriqan.Infrastructure.ReferenceData.Tests` | In-memory / hand-built `VecReferenceBundle` objects, real CSV parsing tests | Unit + integration of CSV adapter against test CSVs |
| `Veriqan.Infrastructure.Reporting.Tests` | In-memory fakes + synthetic PDFs (PdfSharp round-trips) | MarkedPdfGenerator tested against synthetic PDFs; SmtpEmailSender tested with fake SMTP |
| `Veriqan.Infrastructure.Tests.Smoke` | In-memory DI composition smoke test | Builds DI container, checks all services resolve without exception |
| `Veriqan.Infrastructure.Validation.Tests` | Hand-built `VerificationContext`/`StatementModel` objects, no real PDFs | **468 tests, 0 failures** (2026-06-18 run); rules exercised against constructed in-memory models |
| `Veriqan.Infrastructure.Visual.Tests` | Hand-built `StatementModel`/`TypographySample` objects, no real PDFs | Visual rules (font compliance, typography) tested against constructed models |
| `Veriqan.Orchestration.Tests` | Real PdfPig extraction against the 3 PRP2 synthetic PDF fixtures + in-memory substitutes for reference data | **37 tests, 0 failures** (2026-06-18 run); pipeline E2E runs against synthetic PDFs with `NSubstitute` fake `IVecReferenceDataProvider`; calibration harness also runs here |
| `Veriqan.Infrastructure.Persistence.IntegrationTests` | **Testcontainers (real SQL Server via Docker)** | **5 tests: 3 passed, 2 FAILED** (2026-06-18 run — see §2.4 and Part 3) |

### 2.3 — The 3 PRP2 Dummie VEC fixtures (`Prisma/Fixtures/PRP2/`)

All three fixtures are labelled `KnownSynthetic` in `corpus-manifest.json`. The manifest explicitly states they are **NOT compliant statements** and lists extensive `knownFixtureDefects`:

| Specimen | Known Fixture Defects (rules they genuinely fail) |
|---|---|
| `01+Dummie+VEC+jul_ago+20252.pdf` | CL-31, CL-34, CL-35, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§26-NOTAS, LAW-§27-GLOSARIO (12 rules) |
| `02+Dummie+VEC+ago_sep+2025.pdf` | CL-28, CL-31, CL-34, CL-35, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§23-STATUS, LAW-§26-NOTAS, LAW-§27-GLOSARIO (14 rules) |
| `03+Dummie+VEC+sep_oct+2025.pdf` | CL-31, CL-34, CL-35, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§26-NOTAS, LAW-§27-GLOSARIO (12 rules) |

**Confirmed non-compliant** — these are the only PDF specimens in the entire Veriqan corpus. There are zero `KnownGood` specimens and zero `KnownBroken` specimens with `IntendedDefects`. The calibration harness's false-positive rate (FPR) and detection-rate (DR) computations over KnownBroken specimens are **vacuous** — no KnownBroken specimens exist.

Manifest location: `Prisma/Fixtures/PRP2/corpus-manifest.json`

### 2.4 — Calibration harness

**Files:**
- `Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/CalibrationHarness.cs`
- `Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/CalibrationDriverTests.cs`
- `Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/CorpusSchema.cs`

**Is it driving a real corpus?** NO. The corpus is the 3 KnownSynthetic synthetic fixtures only. Zero real CONDUSEF-issued bank statements exist in the corpus. The harness gracefully skips specimens whose PDF file is absent (`if (!File.Exists(pdfPath)) → Skipped=true`).

**Are thresholds calibrated?** NO. All thresholds (8.0 pt body floor, 10.0 pt fecha-límite floor, 805-char §12 limit, 25%/33% section size caps) are **hardcoded constants** in `CalibrationHarness.cs` lines 40–44. No calibration evidence from real statements has been collected. The constants derive from the CONDUSEF legal text (Acuerdo 20/2022), not from measurement of a real corpus. Per the brief (§1): "The calibration harness now exists; the data does not."

The `CalibrationDriverTest.Calibration_SyntheticCorpus_NoNewFalseFails` test skips if the manifest is absent, otherwise runs against the synthetic fixtures. It passes (0 new fails) because all known defects are pre-declared in `knownFixtureDefects`. This test proves **pipeline mechanics work**, not that thresholds are correct for real statements.

---

## Part 3 — Ground-Truth Build/Test Status

### 3.1 — Build: Veriqan Worker

```
Command:
  dotnet build "Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/ExxerCube.Prisma.Veriqan.Worker.csproj"

Output:
  Determining projects to restore...
  All projects are up-to-date for restore.
  ExxerCube.Prisma.Veriqan.Domain -> ...\ExxerCube.Prisma.Veriqan.Domain.dll
  ExxerCube.Prisma.Veriqan.Application -> ...\ExxerCube.Prisma.Veriqan.Application.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.Visual -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.Visual.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.Reporting -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.Persistence -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.Validation -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.Validation.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.dll
  ExxerCube.Prisma.Veriqan.Infrastructure.Extraction -> ...\ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.dll
  ExxerCube.Prisma.Veriqan.Orchestration -> ...\ExxerCube.Prisma.Veriqan.Orchestration.dll
  ExxerCube.Prisma.Veriqan.Worker -> ...\ExxerCube.Prisma.Veriqan.Worker.dll

  Build succeeded.
      0 Warning(s)
      0 Error(s)

  Time Elapsed 00:00:58.59
```

**Result: 0 errors, 0 warnings.**

### 3.2 — Validation tests

```
Command:
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Veriqan.Infrastructure.Validation.Tests/ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests.csproj"

Output:
  Running tests from ...ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests.dll (net10.0|x64)
  ...dll (net10.0|x64) passed (5s 380ms)

  Test run summary: Passed!
    total: 468
    failed: 0
    succeeded: 468
    skipped: 0
    duration: 7s 748ms
```

**Result: 468/468 passed. 0 failed. 0 skipped.**

Substrate: all hand-built in-memory `VerificationContext`/`StatementModel` objects; no real PDFs.

### 3.3 — Orchestration tests

```
Command:
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/ExxerCube.Prisma.Veriqan.Orchestration.Tests.csproj"

Output:
  Running tests from ...ExxerCube.Prisma.Veriqan.Orchestration.Tests.dll (net10.0|x64)
  ...dll (net10.0|x64) passed (1m 30s 094ms)

  Test run summary: Passed!
    total: 37
    failed: 0
    succeeded: 37
    skipped: 0
    duration: 1m 43s 496ms
```

**Result: 37/37 passed. 0 failed. 0 skipped.**

Includes pipeline E2E tests and calibration driver (which ran over the 3 synthetic fixtures — no specimens skipped because the PDFs are present). The long run time (1m 30s) reflects real PdfPig extraction over the 3 PDFs.

### 3.4 — Persistence integration tests

```
Command:
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/05 System/Veriqan.Infrastructure.Persistence.IntegrationTests/ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests.csproj"

Output (stderr):
  failed ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests.LegalBaselineEncryptedStoreTests.SeedAndRead_TolerancesRoundTrip_AndRawColumnsAreCiphertext (11s 042ms)
    Shouldly.ShouldAssertException : tolerances.Count
        should be
    14
        but was
    19
    
    Additional Info:
        Expected 14 seed records (CL-10/17/18/19/20/21/22/24/25/44/ITEM-58/CL-39/CL-37/CL-36). Found 19.
      at LegalBaselineEncryptedStoreTests.cs:86

  failed ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests.LegalBaselineEncryptedStoreTests.SeedAsync_CalledTwice_IsIdempotent (663ms)
    Shouldly.ShouldAssertException : count
        should be
    14
        but was
    19
    
    Additional Info:
        Expected exactly 14 rows after two seed calls. Found 19.
      at LegalBaselineEncryptedStoreTests.cs:215

  Test run summary: Failed!
    total: 5
    failed: 2
    succeeded: 3
    skipped: 0
    duration: 1m 45s 448ms
  Test run completed with non-success exit code: 2
```

**Result: 5 total. 3 passed. 2 FAILED.**

Docker IS available (Testcontainers connected successfully to `npipe://./pipe/docker_engine`, SQL Server container started, migrations applied). The failures are **not a Docker issue**.

**Root cause:** `LegalBaselineSeeder.BuildSeedRecords()` now seeds **19 records** (11 CurrencyMxn rules + 5 LAW-§20/§19/§6/§8/§16 computation rules added in Epic 11 commit `062d9fed` + CL-39 + CL-37 + CL-36), but the integration tests still assert `count == 14` (the pre-Epic-11 count). The test was never updated when the seeder was expanded. This is a **test-vs-production drift**: the seeder is correct (19 matches `DefaultLegalToleranceProvider`), but the test hardcodes the old row count.

---

## Biggest Gap

**The Veriqan Worker has no statement ingestion entry point — it is an unreachable shell.**

The Worker (`Program.cs`, 21 lines) DI-wires the complete pipeline but provides **no mechanism for a statement to arrive**. There is no HTTP endpoint accepting `StatementSubmission`, no hosted background service polling a folder/queue, no gRPC controller, no message-bus consumer, no file-system watcher. The `IBatchProcessor` is registered but never called from the Worker. The "Worker" currently boots, registers health probes, and **waits** — but nothing can trigger it to verify a statement.

This is the single most significant "looks-done-but-isn't" finding: every internal stage (extraction, validation, verdict, persistence) is wired and unit-tested, but the system has no operational surface for a production statement to enter the pipeline. Solving this requires adding at minimum one of: (a) an HTTP `POST /verify` endpoint wired to `IBatchProcessor`/`IVerificationPipeline`, (b) a `BackgroundService` that polls a folder/queue and feeds `BatchProcessor`, or (c) an SDK surface (E13 13.1 embeddable gate). None of these exist today.

**Secondary critical gap (active breakage):** `LegalBaselineEncryptedStoreTests` has 2 failing tests caused by seeder/test drift — the seeder was expanded from 14 to 19 records in Epic 11 but the integration test was not updated. This is an **active red test** on the persistence integration suite.

---

## Summary of Gaps by Dimension (§2 of brief)

| Dimension | Status |
|---|---|
| 1. Functional correctness on real data | BLOCKED — zero real CONDUSEF statements in corpus; all thresholds uncalibrated; 3 fixtures are synthetic non-compliant placeholders |
| 2. End-to-end deployable composition | MISSING — Worker has no ingestion entry point; pipeline is only reachable from tests |
| 3. Multi-tenancy (E13) | E13-GATED — single fixed `LegalBaseline()` TenantProfile; no per-statement selection, no overlay, no traceability export |
| 4. Ingestion | MISSING — no `StatementSubmission` receiver in Worker; `CsvReferenceDataAdapter` has no CSV root configured in Worker |
| 5. Persistence & data | PARTIAL — EF Core SQL path exists and works (Testcontainers confirmed) but 2 integration tests are RED; key management reads from `IConfiguration` key `"Veriqan:LegalBaseline:EncryptionKey"` (no key management infra) |
| 6. Security & compliance posture | NOT AUDITED (deferred to dedicated security review per brief §2) |
| 7. Operational readiness | STUB — health probes return hardcoded "Healthy" (no real checks); no appsettings.json in Worker; no OTLP/Prometheus exporter wired; OTel Meter registered but no exporter attached; no runbook |
| 8. Performance / scale / SLA | UNVERIFIED — BatchProcessor exists with bounded concurrency; never run against real volume; NFR-1 (p95 ≤ 10 000 ms) unmeasured on real statements |
| 9. Failure modes | PARTIAL — pipeline degrades gracefully to `Result.WithFailure` / `Blocked`; but never stress-tested on malformed PDFs at volume |
