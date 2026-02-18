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
- `prisma-document-generator/` — Synthetic test document generation

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

## Release Status (as of 2026-02-18)

**Production-ready:** Field extractors (PDF/DOCX/XML/TXT), FusionExpedienteService, Database/EF Core, Export services, File storage, Architecture enforcement tests, Blazor UI with full DocumentProcessing page.

**Deferrable to v1.1:** CSnakes Python ML interop (placeholder), PersonIdentityResolver DB persistence, Worker dashboard real metrics (UI dashboard uses IProcessingMetricsService which works), 3 skipped TXT extractor edge cases.
