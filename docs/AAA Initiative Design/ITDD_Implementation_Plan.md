# ITDD/TDD Implementation Plan — Orion/Athena/Sentinel/Auth/HMI

## Purpose
Actionable, test-first plan to deliver the dual-worker topology (Orion ingestion, Athena processing), Sentinel monitor, auth abstraction, and HMI event consumption. Aligned with Clean Architecture, SOLID, and repo coding standards. Each stage starts with tests and ends with clear exit criteria.

## Guiding Principles
- Hexagonal: interfaces/contracts in Domain/Contracts; implementations in Infrastructure; hosts only wire endpoints.
- SOLID: small classes, constructor DI, pure functions where possible, no service locators.
- ITDD/TDD: write/commit tests first for DI, services, endpoints, and E2E flows.
- Observability: correlation IDs preserved end-to-end; health/heartbeat per worker.
- Idempotency: ingestion and processing must tolerate retries and partial failures.

## Project Names (already scaffolded)
- `Prisma.Shared.Contracts` — event/DTO contracts, correlation conventions.
- `Prisma.Orion.Ingestion` (lib) + `Prisma.Orion.Worker` (host).
- `Prisma.Athena.Processing` (lib) + `Prisma.Athena.Worker` (host).
- `Prisma.Sentinel.Monitor` (monitor utility).
- `Prisma.Auth.Domain` + `Prisma.Auth.Infrastructure` (auth abstraction).
- HMI: existing MudBlazor UI, to be wired to events/auth.

## Stage Overview (Tests First)
1. DI & Contracts Baseline
2. Orion Ingestion
3. Athena Processing Orchestrator
4. Health & Dashboard Endpoints
5. Sentinel Monitor
6. Auth Abstraction
7. HMI Event Consumption
8. End-to-End Validation

---

## Stage 1: DI & Contracts Baseline
**Goal**: Contracts serialize correctly; DI resolves all services in Orion/Athena/Auth/Sentinel extension methods.

- Tests (new):
  - `Prisma.Shared.Contracts.Tests`: round-trip JSON for events/DTOs (PascalCase preserved).
  - `Prisma.Composition.Tests`: DI resolution smoke — `IServiceProvider.GetRequiredService<T>` for Orion, Athena, Auth extensions.
- Work:
  - Add DI extension classes per lib (no host references).
  - Validate options binding with defaults; fail fast on missing required settings.
- Exit Criteria:
  - All contract serialization tests green.
  - DI resolution tests green without host projects.

## Stage 2: Orion Ingestion (TDD)
**Goal**: Watch SIARA, download to `year/month/day`, write JSON journal (hash, correlation, URL, timestamp), emit `DocumentDownloadedEvent`.

- Tests (new):
  - `Prisma.Orion.Ingestion.Tests`:
    - Watcher triggers download on new case.
    - File stored at `root/yyyy/MM/dd/{filename}`.
    - Journal contains hash, size, URL, correlation, timestamp; idempotent on rerun.
    - Emits `DocumentDownloadedEvent` with path + journal path.
- Interfaces (in contracts/domain to use/reuse):
    - Reuse existing: `IBrowserAutomationAgent` (watch/identify/download), `IDownloadStorage` (deterministic save), `IDownloadTracker` (duplicate detection), `IEventPublisher`.
    - Add: `IIngestionJournal` (journal read/write), optional `IContentHasher` (if hashing not folded into tracker).
- Implementation:
  - `IngestionOrchestrator` coordinates watcher → downloader → hasher → journal → event.
  - Ensure idempotency (check journal/hash before re-download).
- Exit Criteria:
  - Tests green; orchestrator host-agnostic; correlation/file IDs set; partitioned path verified.

## Stage 3: Athena Processing Orchestrator (ITDD)
**Goal**: Consume download events/journal/files → quality → OCR → XML extract → fusion → classification → export → emit events → persist audit trail.

- Tests (new system/integration):
  - `Prisma.Athena.Processing.Tests.System`:
    - Given `DocumentDownloadedEvent` + journal/file, pipeline runs and persists audit records.
    - CorrelationId/FileId preserved across `QualityCompleted`, `OcrCompleted`, `ClassificationCompleted`, `ProcessingCompleted`.
    - Conflict/manual-review path emits flag/review events.
  - Interfaces (contracts/domain to reuse):
    - Quality: `IImageQualityAnalyzer`, `IFilterSelectionStrategy`
    - OCR: `IOcrExecutor`, `IOcrProcessingService`, `IOcrSessionRepository`
    - XML/Metadata: `IMetadataExtractor`, `IFieldExtractor<T>`, `IXmlNullableParser<T>`
    - Fusion/Reconciliation: `IFusionExpediente`, `IFieldMatcher`
    - Classification: `IFileClassifier`, `ILegalDirectiveClassifier`
    - Export: `IResponseExporter`, `IAdaptiveExporter`
    - Audit/Events: `IAuditLogger`, `IEventPublisher`
- Implementation:
  - `ProcessingOrchestrator` subscribes to event stream or folder/journal watcher; orchestrates pipeline; publishes events.
  - Propagate correlation; wrap failures in error events without stopping flow (defensive).
- Exit Criteria:
  - System tests green; audit trail entries match event sequence; no manual publishes needed.

## Stage 4: Health & Dashboard Endpoints (TDD)
**Goal**: `/health` (liveness/readiness) and `/dashboard` (basic stats) on both workers.

- Tests (new host tests):
  - `Prisma.Orion.Worker.Tests`, `Prisma.Athena.Worker.Tests`: endpoints return 200; liveness reflects orchestrator start; dashboard returns counts/last heartbeat.
- Implementation:
  - Minimal ASP.NET Core endpoints in worker hosts only.
  - Dashboard data sourced from orchestrator metrics (downloads processed, last event time, queue depth if available).
- Exit Criteria:
  - Endpoint tests green; health reflects failure state when orchestrator not running.

## Stage 5: Sentinel Monitor (ITDD)
**Goal**: Detect lost heartbeats/zombie workers and trigger restart hook; log incidents.

- Tests (new):
  - `Prisma.Sentinel.Monitor.Tests`: missing 3 heartbeat within SLA triggers implement forgive missed restart action, ; restart result logged.
- Implementation:
  - Use `WorkerHeartbeat` contract; poll health endpoints or heartbeat bus; abstract restart via interface (e.g., `IProcessRestarter`).
  - Configurable thresholds/timeouts.
- Exit Criteria:
  - Tests green; sentinel runnable headless; restart hook injectable.

## Stage 6: Auth Abstraction (TDD)
**Goal**: Provider-agnostic auth; secure endpoints/event consumers.

- Tests (new):
  - `Prisma.Auth.Domain.Tests`: token create/validate contract tests.
  - `Prisma.Auth.Infrastructure.Tests`: in-memory/EF impl validates/creates tokens.
  - DI test: hosts resolve `IIdentityProvider`, `ITokenService`, `IUserContextAccessor`.
- Implementation:
  - Interfaces in `Prisma.Auth.Domain`; initial impl in `Prisma.Auth.Infrastructure` (in-memory now, EF later).
  - Wire to worker hosts and HMI.
- Exit Criteria:
  - Tests green; hosts use abstractions (no direct UI auth dependency); easy swap of provider.

## Stage 7: HMI Event Consumption (ITDD)
**Goal**: UI receives real-time events and shows notifications/alerts with auth.

- Tests (new):
  - `Prisma.HMI.Tests`: SignalR/event-stream client subscribes to classification/conflict/completion; renders notification; denies when unauthenticated.
- Implementation:
  - Keep UI logic thin; consume contracts; integrate auth token into SignalR connection.
- Exit Criteria:
  - Tests green; basic notification flow observable with mock events.

## Stage 8: End-to-End Validation
**Goal**: Synthetic SIARA → Orion → Athena → DB/Export → UI notification; audit trail complete; health green.

- Tests (new system/E2E):
  - `Prisma.Tests.System.E2E`: run with fixtures; assert event sequence, DB records, export artifact, UI notification, health endpoints green.
- Implementation:
  - Use containerized SQL for DB; file fixtures for PDFs/XML; orchestrate full pipeline in CI.
- Exit Criteria:
  - E2E test green; artifacts/audit verified; correlation ID consistent across events/DB/UI.

---

## Coding Standards & Practices
- Warnings as errors; nullable enabled; explicit logging with correlation IDs.
- Pure functions where possible; side effects isolated.
- No service locator; prefer constructor DI; validate options on startup.
- Tests before implementation; keep tests deterministic and fixture-backed where possible.

## Sample DI Registration Snippet (Host)
```csharp
builder.Services.AddOrionIngestion(options =>
{
    options.RootPath = config["Orion:RootPath"];
    options.JournalPath = config["Orion:JournalPath"];
});
builder.Services.AddSharedContracts();
builder.Services.AddAuthInfrastructure(config);
builder.Services.AddHostedService<OrionWorkerService>();
```

## Sample Health Endpoint (Host)
```csharp
app.MapGet("/health", (IHealthReporter reporter) =>
    Results.Json(reporter.GetStatus()));
app.MapGet("/dashboard", (IMetricsSnapshot metrics) =>
    Results.Json(metrics.Snapshot()));
```

## Risks & Mitigations
- OCR/quality dependencies may be slow: use configurable timeouts and circuit breakers.
- File watcher races: debounce and hash-based idempotency via journal.
- Auth swap: keep provider behind interfaces; avoid UI-specific auth in core libs.

## Deliverables Checklist per Stage
- Tests added and green.
- Interfaces defined in correct layer.
- Implementations host-agnostic (libs) and wiring in hosts.
- Docs updated (this plan + runbook notes).

---

## New/Proposed Interfaces and Classes (to be added/reused)
- **Reuse existing Domain interfaces** (do not duplicate):
  - Quality: `IImageQualityAnalyzer`, `IFilterSelectionStrategy`
  - OCR: `IOcrExecutor`, `IOcrProcessingService`, `IOcrSessionRepository`
  - XML/Metadata: `IMetadataExtractor`, `IFieldExtractor<T>`, `IXmlNullableParser<T>`
  - Fusion/Reconciliation: `IFusionExpediente`, `IFieldMatcher`
  - Classification: `IFileClassifier`, `ILegalDirectiveClassifier`
  - Export: `IResponseExporter`, `IAdaptiveExporter`
  - Audit/Events: `IAuditLogger`, `IEventPublisher`
  - Ingestion helpers: `IBrowserAutomationAgent`, `IDownloadStorage`, `IDownloadTracker`

- **Add to Domain/Contracts**:
  - `IIngestionJournal` (write/read JSON journal entries)
  - Optional `IContentHasher` (if hashing not folded into tracker)

- **Classes (libs/hosts already scaffolded)**:
  - Orion: `IngestionOrchestrator` (lib), `OrionWorkerService` (host)
  - Athena: `ProcessingOrchestrator` (lib), `AthenaWorkerService` (host)
  - Sentinel: `SentinelService` (monitor)
  - Auth: `InMemoryIdentityProvider` (initial impl), plus domain interfaces in `Prisma.Auth.Domain`
  - Shared contracts: `DocumentDownloadedEvent`, `WorkerHeartbeat` (in `Prisma.Shared.Contracts`)
