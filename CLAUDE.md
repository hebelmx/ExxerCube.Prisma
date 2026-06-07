# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ExxerCube.Prisma is an enterprise OCR document processing system for Spanish legal documents. It combines C# (.NET 10) services with Python ML/OCR modules via CSnakes interop. The system uses a 5-stage pipeline: Quality Analysis → OCR → Fusion/Reconciliation → Classification → Export.

## Build & Test Commands

```bash
# Solution is at Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln (200+ projects)
dotnet restore "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet test "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"

# Build/test a single project
dotnet build "Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Processing/ExxerCube.Prisma.Athena.Processing.csproj"
dotnet test "Prisma/Code/Src/CSharp/08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ExxerCube.Prisma.Athena.Processing.Tests.csproj"

# Run a single test (xUnit v3 filter query syntax)
dotnet test <test-project.csproj> --filter-query "/Namespace/ClassName/MethodName"
dotnet test <test-project.csproj> --filter-query "[category=fast]"
```

Build artifacts go to `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\` (configured in Directory.Build.props).

## Architecture

**Hexagonal/Clean Architecture** with numbered folder convention:

```
Prisma/Code/Src/CSharp/
├── 01 Core/          Domain (entities, value objects, interfaces) + Application (services)
├── 02 Infrastructure/ Adapters: Database, FileStorage, OCR, Classification, Export, Imaging, Python interop
├── 03 Orchestration/  Pipeline coordination (ProcessingOrchestrator)
├── 04 Services/       Microservices: Orion (ingestion), Athena (processing), Sentinel (monitoring)
├── 07 UI/             Blazor web UI (MudBlazor)
├── 08 Tests/          Mirrors production structure (01-09 subfolders)
└── 09 Testing/        Shared test abstractions, contracts, fixtures
```

**Three Python subsystems** under `Prisma/Code/Src/Python/`:
- `prisma-ocr-pipeline/` — Tesseract-based OCR with watermark removal, deskewing
- `prisma-ai-extractors/` — Vision-Language Models (SmolVLM2, GOT-OCR2, PaddleOCR)
- `Prisma-dumy-generator-AAA/` — Synthetic test document generation (the "document generator")

> ⚠️ **Python tree duplication (known issue, do not blindly dedupe):** the C#
> build is wired (via the CSnakes `SetPythonPathForCSnakes` MSBuild target and
> `<PythonRoot>Python/python</PythonRoot>` in `02 Infrastructure/Infrastructure`)
> to **`Prisma/Code/Src/CSharp/Python/`** — so that copy must NOT be removed. It
> overlaps/diverges with `Src/Python/Prisma-dumy-generator-AAA/`. Reconciling the
> two needs a focused pass. See `docs/development/archive/root-cleanup-2026-06.md`.

## Repository Layout (top level)

```
ExxerCube.Prisma/            (git repo root)
├── Prisma/                  Main product: Code/Src/{CSharp,Python}, Fixtures, scripts, Docs
├── docs/                    Consolidated documentation (see docs/README.md)
├── scripts/                 Operational scripts: docker/ build/ db/ generators/ data-extraction/
├── tools/                   Standalone dev tools (e.g. tools/Siara.Simulator/ — external SIARA portal sim)
├── CLAUDE.md, AGENTS.md     Agent instructions
├── nuget.config, package.json, playwright.config.ts, coverage.runsettings
└── run-coverage[-ci].ps1    Root-coupled coverage runners (CI)
```

The real-time communication / SignalR hub abstraction was **extracted out** of
this repo into the published **`IndFusion.Ember`** NuGet package
(`github.com/hebelmx/IndFusion.Ember`); reference that package rather than
reviving in-repo SignalR code. See `docs/architecture/adr/ADR-009-*`.

## Critical Code Patterns

### Result<T> Pattern (mandatory — no exceptions for business logic)
```csharp
public async Task<Result<T>> ProcessAsync(..., CancellationToken cancellationToken = default)
{
    if (cancellationToken.IsCancellationRequested)
        return ResultExtensions.Cancelled<T>();

    // Validation returns Result, never throws
    if (input is null)
        return Result<T>.WithFailure("Input cannot be null");

    // Chain with functional operations: Map, Bind, Match, ThenAsync
    return Result<T>.WithSuccess(value);
}
```

### CancellationToken (required on every async method)
- Signature: `CancellationToken cancellationToken = default`
- Propagate to all downstream async calls
- Check early: `cancellationToken.IsCancellationRequested` → return `ResultExtensions.Cancelled<T>()`
- Never throw `OperationCanceledException`; catch and convert to `Result`
- In tests use `TestContext.Current.CancellationToken`, not manual tokens

### Null Safety
- Nullable reference types enabled, warnings-as-errors
- Constructor: `ArgumentNullException` for required dependencies
- Method params: validate and return `Result.WithFailure` (not throw)
- Use `IsSuccessMayBeNull` / `IsSuccessNotNull` on Result values

### Async/Await
- `ConfigureAwait(false)` on background/library code
- All async methods accept and propagate `CancellationToken`

## Testing Stack

- **Framework:** xUnit v3 (not v2)
- **Assertions:** Shouldly (`value.ShouldBe(expected)`)
- **Mocking:** NSubstitute (`Substitute.For<IService>()`)
- **Logging:** `Meziantou.Extensions.Logging.Xunit.v3` (real logging, not mocked)
- **Integration:** Testcontainers (MsSql, Ollama), WebApplicationFactory
- **Test naming:** `MethodUnderTest_Scenario_ExpectedBehavior`
- **Do NOT use:** Moq, FluentAssertions

## Logging & Observability

- Serilog structured logging: use `{PropertyName}` placeholders, never string concatenation
- Correlation IDs via `LogBeginScope` with context dictionary
- OpenTelemetry metrics: `Meter`, `Counter<long>`, `Histogram<double>`
- Health checks per service: `/health/live`, `/health/ready`
- Always measure and log durations with `Stopwatch`

## Key Configuration

- **.NET 10**, `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`
- `EnforceCodeStyleInBuild=true`, `GenerateDocumentationFile=true`
- Centralized package versions via `Directory.Packages.props`
- C#/Python interop via CSnakes.Runtime

## Event Architecture

The system uses **Rx.NET Observables** (not traditional IEventHandler registration):
- `EventPublisher` (Infrastructure) — production implementation using `Subject<DomainEvent>`
- `ProcessingOrchestrator.StartAsync()` subscribes to `GetEventStream<DocumentDownloadedEvent>()`
- `EventPersistenceWorker` subscribes to `GetAllEventsStream()` for audit trail persistence
- `SignalREventBroadcaster` subscribes to `GetAllEventsStream()` for UI real-time updates
- `InMemoryEventBus` — legacy string-based pub/sub, **not used in production** (EventPublisher is the real impl)

## Services

| Service | Role | Location |
|---------|------|----------|
| **Orion** | Document ingestion & download | `04 Services/Orion/` |
| **Athena** | Document processing pipeline | `04 Services/Athena/` |
| **Sentinel** | System monitoring & health | `04 Services/Sentinel/` |

## Release Status

> The status below was last asserted **2026-02-18** and has **not been
> re-verified** since the project resumed after a dormant period. Treat it as
> historical until a fresh scope audit (build + test + feature walk) confirms it.

**Claimed production-ready (2026-02-18):** Field extractors (PDF/DOCX/XML/TXT), FusionExpedienteService, Database/EF Core, Export services, File storage, Architecture enforcement tests, Blazor UI with full DocumentProcessing page.

**Deferrable to v1.1:** CSnakes Python ML interop (placeholder), PersonIdentityResolver DB persistence, Worker dashboard real metrics (UI dashboard uses IProcessingMetricsService which works), 3 skipped TXT extractor edge cases.

### Current build status (2026-06-07)

✅ **`dotnet build` of the main solution succeeds** (0 errors, 0 warnings) and
`dotnet list package --vulnerable` reports **no vulnerabilities**. The NU1903 CVE
was fixed by enabling `CentralPackageTransitivePinningEnabled` and pinning
`System.Security.Cryptography.Xml 10.0.8`; OpenTelemetry was bumped to 1.15.x to
clear its Moderate CVEs. Packages were updated to latest stable (see
`Directory.Packages.props`).

**Held back deliberately (need a dedicated, decision-gated upgrade):**
- **MudBlazor** at `8.11` — `9.x` is a major UI migration (`ChartSeries<T>`,
  `ActivatorContent`, runtime/visual changes); needs visual testing.
- **SixLabors.ImageSharp** at `3.1.12` — `4.x` requires a **paid commercial
  license** (enforced at build). A business/legal decision.
- **Emgu.CV** at `4.12.0.5764` — `4.13` changes the native `CvInvoke.CLAHE`
  signature; `Contrib`/`ubuntu-x64` have no matching upstream release.
- **Testcontainers** at `4.9.0` — `4.12` obsoletes the parameterless builder
  ctors (CS0618-as-error).
- **BouncyCastle.Cryptography** on `2.7.0-beta` — the only stable (`2.6.2`) is
  older than the pinned beta.

### Testing stack (xunit.v3 + Microsoft.Testing.Platform)

This combination is **version-coupled and fragile** — get it wrong and tests fail
at *run* time with `System.MissingMethodException` (e.g.
`IOutputDevice.DisplayAsync` not found), because xunit's MTP integration is
compiled against a specific MTP ABI. The current **working** set (verified: build
0/0, `dotnet test` runs, Tests.Domain 337/337):

- Test projects reference **`xunit.v3.mtp-v2`** `3.2.2` (NOT the default
  `xunit.v3`, which is the `mtp-v1` variant). This is the switch that selects MTP v2.
- `Microsoft.Testing.Platform` (+ `.MSBuild` + `Extensions.HangDump/TrxReport/VSTestBridge`) at **`2.1.0`**;
  `Microsoft.Testing.Extensions.CodeCoverage` `18.5.2`; `xunit.analyzers` `1.27.0`;
  `Meziantou.Extensions.Logging.Xunit.v3` `2.0.1`.
- **`global.json`** opts `dotnet test` into MTP on the .NET 10 SDK:
  `{ "test": { "runner": "Microsoft.Testing.Platform" } }` — without it,
  `dotnet test` errors (VSTest target unsupported).
- `CentralPackageTransitivePinningEnabled` is on, so an MTP `PackageVersion` pin
  also forces xunit's transitive MTP — every `Microsoft.Testing.*` version must
  align to the same MTP minor (here 2.1.0). Bump the whole set together.
- CodeCoverage 18.5.2 is compiled against MTP 2.1.0, so MTP must be ≥ 2.1.0 (this
  is why 2.0.2 — what xunit's mtp-v2 declares — wasn't enough; CS1705).

### Repo hygiene

The repo was reorganized on 2026-06-07 (branch `chore/repo-reorg`): root clutter
removed, docs/scripts/tools organized, and the superseded SignalR projects removed
(now the `IndFusion.Ember` package). See
`docs/development/archive/root-cleanup-2026-06.md` for the full record and the
list of deferred follow-ups (Python tree dedupe, stale DB scripts, `Veriqan`
clone).
