# ITDD/TDD Implementation Plan — Orion/Athena/Sentinel/Auth/HMI

## Purpose
Actionable, test-first plan to deliver the dual-worker topology (Orion ingestion, Athena processing), Sentinel monitor, auth abstraction, and HMI event consumption. Aligned with Clean Architecture, SOLID, and repo coding standards. Each stage starts with tests and ends with clear exit criteria.

## ⚠️ CRITICAL ADR: Architectural Refactoring Required (2025-12-02)

**Discovery**: During Stage 7 implementation, we discovered that the original plan used incorrect abstractions for event broadcasting and error handling:

**Wrong Abstractions (Original Plan)**:
- ❌ Generic `IEventPublisher` interface (not transport-agnostic)
- ❌ Direct SignalR `HubConnection` usage (unmockable, tightly coupled)
- ❌ Exception-based error handling (breaks Railway-Oriented Programming)

**Correct Abstractions (Owned Packages)**:
- ✅ **IndFusion.Ember**: `IExxerHub<T>` for transport-agnostic event broadcasting
- ✅ **IndQuestResults**: `Result<T>` for Railway-Oriented Programming (no exceptions for control flow)
- ✅ Three Actors Pattern: ExxerHub<T>, ServiceHealth<T>, Dashboard<T>

**Impact**: Stages 2, 3, 4, 5, 7 require refactoring to use these owned packages.

**Resolution Strategy**:
1. Complete original stages as planned (baseline implementation)
2. Add refactoring stages (2.5, 3.5, 4.5, 5.5) to migrate to correct abstractions
3. Stage 7 already refactored (13/13 tests passing with Ember + Result)

**References**:
- IndFusion.Ember README: `F:\Dynamic\IndFusion\IndFusion.Ember\README.md`
- IndQuestResults Manual: `docs/Result-Manual.md`
- ROP Best Practices: `docs/ROP-with-IndQuestResults-Best-Practices.md`

---

## Guiding Principles
- Hexagonal: interfaces/contracts in Domain/Contracts; implementations in Infrastructure; hosts only wire endpoints.
- SOLID: small classes, constructor DI, pure functions where possible, no service locators.
- ITDD/TDD: write/commit tests first for DI, services, endpoints, and E2E flows.
- Observability: correlation IDs preserved end-to-end; health/heartbeat per worker.
- Idempotency: ingestion and processing must tolerate retries and partial failures.
- **Railway-Oriented Programming**: Use `Result<T>` for all operations that can fail; no exceptions for control flow.
- **Transport-Agnostic Events**: Use `IExxerHub<T>` from IndFusion.Ember for all event broadcasting.

## Project Names (already scaffolded)
- `Prisma.Shared.Contracts` — event/DTO contracts, correlation conventions.
- `Prisma.Orion.Ingestion` (lib) + `Prisma.Orion.Worker` (host).
- `Prisma.Athena.Processing` (lib) + `Prisma.Athena.Worker` (host).
- `Prisma.Sentinel.Monitor` (monitor utility).
- `Prisma.Auth.Domain` + `Prisma.Auth.Infrastructure` (auth abstraction).
- HMI: existing MudBlazor for Demo development Purpouse UI, to be wired to events/auth.
- New HMI: add with evokative name desing for monitoring dashboard reporting panel admin, user admin etc...

## Stage Overview (Tests First)
1. DI & Contracts Baseline
2. Orion Ingestion (Baseline)
   - **2.5 Orion Refactoring** - Migrate to IExxerHub<T> and Result<T>
3. Athena Processing Orchestrator (Baseline)
   - **3.5 Athena Refactoring** - Migrate to IExxerHub<T> and Result<T>
4. Health & Dashboard Endpoints (Baseline)
   - **4.5 Health Refactoring** - Migrate to Result<T> pattern
5. Sentinel Monitor (Baseline)
   - **5.5 Sentinel Refactoring** - Migrate to IExxerHub<T> and Result<T>
6. Auth Abstraction ✅ (Already uses Result<T>)
7. HMI Event Consumption ✅ (Already refactored with Ember + Result)
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

## Stage 2: Orion Ingestion (TDD) - BASELINE IMPLEMENTATION
**Goal**: Watch SIARA, download to `year/month/day`, persist manifest to DB (hash, correlation, URL, stored path, timestamp), emit `DocumentDownloadedEvent`.

⚠️ **Note**: This baseline implementation will use temporary abstractions. **Stage 2.5** will refactor to use IndFusion.Ember and IndQuestResults.

- Tests (new):
  - `Prisma.Orion.Ingestion.Tests`:
    - Watcher triggers download on new case.
  - File stored at `root/yyyy/MM/dd/{filename}`.
  - Manifest row in DB contains hash, size, URL, stored path, correlation, timestamp; idempotent on rerun (unique hash+URL).
  - Emits `DocumentDownloadedEvent` with stored path + manifest key.
- Interfaces (in contracts/domain to use/reuse):
    - Reuse existing: `IBrowserAutomationAgent` (watch/identify/download), `IDownloadStorage` (deterministic save), `IDownloadTracker` (duplicate detection)
    - ⚠️ Temporary: `IEventPublisher` (will be replaced with `IExxerHub<DocumentDownloadedEvent>` in Stage 2.5)
    - Add: `IIngestionJournal` (DB-backed manifest read/write), optional `IContentHasher` (if hashing not folded into tracker)
- Implementation:
  - `IngestionOrchestrator` coordinates watcher → downloader → hasher → journal → event
  - ⚠️ Temporary: Returns `Task` instead of `Task<Result<T>>` (will be refactored in Stage 2.5)
  - Ensure idempotency (check journal/hash before re-download)
- Exit Criteria:
  - Tests green; orchestrator host-agnostic; correlation/file IDs set; partitioned path verified
  - ⚠️ Known debt: Will need refactoring in Stage 2.5 for Ember + Result patterns

---

## Stage 2.5: Orion Refactoring - EMBER + RESULT MIGRATION
**Goal**: Migrate Orion from temporary abstractions to IndFusion.Ember (`IExxerHub<T>`) and IndQuestResults (`Result<T>`).

**Prerequisites**: Stage 2 complete

- Tests (refactored):
  - Update `Prisma.Orion.Ingestion.Tests` to use `IExxerHub<DocumentDownloadedEvent>` mocks (NSubstitute)
  - Add Railway-Oriented Programming tests:
    - `IngestDocument_WithValidUrl_ReturnsSuccessAndBroadcastsEvent`
    - `IngestDocument_WhenDownloadFails_ReturnsFailureWithoutBroadcast`
    - `IngestDocument_WhenCancelled_ReturnsCancelledResult`
    - `IngestDocument_WhenDuplicate_ReturnsSuccessWithoutRedownload` (idempotency)
- Interfaces (refactored):
  - ❌ Remove: `IEventPublisher`
  - ✅ Add: `IExxerHub<DocumentDownloadedEvent>` dependency
- Implementation Changes:
  ```csharp
  public class IngestionOrchestrator
  {
      private readonly IBrowserAutomationAgent _browser;
      private readonly IDownloadStorage _storage;
      private readonly IIngestionJournal _journal;
      private readonly IExxerHub<DocumentDownloadedEvent> _hub;

      // ✅ Returns Result<T> instead of Task
      public async Task<Result<IngestionResult>> IngestDocumentAsync(
          string url,
          CancellationToken cancellationToken)
      {
          return await ValidateUrl(url)
              .ThenAsync(u => DownloadAsync(u, cancellationToken))
              .ThenAsync(file => HashAndStoreAsync(file, cancellationToken))
              .ThenAsync(manifest => JournalAsync(manifest, cancellationToken))
              .ThenTap(manifest => BroadcastEventAsync(manifest, cancellationToken));
      }

      private async Task BroadcastEventAsync(
          Manifest manifest,
          CancellationToken ct)
      {
          var evt = new DocumentDownloadedEvent(
              FileId: manifest.FileId,
              FileName: manifest.FileName,
              StoredPath: manifest.StoredPath,
              CorrelationId: manifest.CorrelationId,
              Timestamp: manifest.Timestamp
          );
          await _hub.SendToAllAsync(evt, ct);
      }
  }
  ```
- Exit Criteria:
  - All tests green with `IExxerHub<T>` mocks
  - `IngestionOrchestrator` returns `Result<T>` for all operations
  - No exceptions thrown for control flow (use `Result.Failure()` instead)
  - Events broadcast via `IExxerHub<T>.SendToAllAsync()`

## Stage 3: Athena Processing Orchestrator (ITDD) - BASELINE IMPLEMENTATION
**Goal**: Consume download events/journal/files → quality → OCR → XML extract → fusion → classification → export → emit events → persist audit trail.

⚠️ **Note**: This baseline implementation will use temporary abstractions. **Stage 3.5** will refactor to use IndFusion.Ember and IndQuestResults (most complex refactoring).

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
    - Audit: `IAuditLogger`
    - ⚠️ Temporary: `IEventPublisher` (will be replaced with multiple `IExxerHub<T>` in Stage 3.5)
- Implementation:
  - `ProcessingOrchestrator` subscribes to event stream or folder/journal watcher; orchestrates pipeline; publishes events
  - ⚠️ Temporary: Uses try/catch for error handling (will be refactored to Railway-Oriented Programming in Stage 3.5)
  - Propagate correlation; wrap failures in error events without stopping flow (defensive)
- Exit Criteria:
  - System tests green; audit trail entries match event sequence; no manual publishes needed
  - ⚠️ Known debt: Will need extensive refactoring in Stage 3.5 for Ember + Result patterns

---

## Stage 3.5: Athena Refactoring - EMBER + RESULT MIGRATION ⚠️ COMPLEX
**Goal**: Migrate Athena from try/catch error handling to Railway-Oriented Programming with Result<T>, and from generic event publisher to typed IExxerHub<T> for each event type.

**Prerequisites**: Stage 3 complete

**Complexity**: ⚠️ **HIGH** - This is the most complex refactoring due to multiple event types and long processing pipeline.

- Tests (refactored):
  - Update `Prisma.Athena.Processing.Tests.System` to use `IExxerHub<T>` mocks for all 4 event types
  - Add Railway-Oriented Programming tests:
    - `ProcessDocument_FullPipeline_ReturnsSuccessAndEmitsAllEvents`
    - `ProcessDocument_WhenQualityFails_ReturnsFailureWithoutDownstreamEvents`
    - `ProcessDocument_WhenOcrFails_EmitsQualityEventButNotDownstream`
    - `ProcessDocument_WhenCancelled_PreservesPartialResults`
    - `ProcessDocument_WithPartialData_ReturnsSuccessWithWarnings` (confidence/missing data)
- Interfaces (refactored):
  - ❌ Remove: `IEventPublisher`
  - ✅ Add:
    - `IExxerHub<QualityCompletedEvent>`
    - `IExxerHub<OcrCompletedEvent>`
    - `IExxerHub<ClassificationCompletedEvent>`
    - `IExxerHub<ProcessingCompletedEvent>`
  - ✅ Update: All pipeline interfaces should return `Result<T>` instead of throwing exceptions
- Implementation Changes:
  ```csharp
  public class ProcessingOrchestrator
  {
      private readonly IImageQualityAnalyzer _qualityAnalyzer;
      private readonly IOcrExecutor _ocrExecutor;
      private readonly IFileClassifier _classifier;
      private readonly IResponseExporter _exporter;
      private readonly IExxerHub<QualityCompletedEvent> _qualityHub;
      private readonly IExxerHub<OcrCompletedEvent> _ocrHub;
      private readonly IExxerHub<ClassificationCompletedEvent> _classificationHub;
      private readonly IExxerHub<ProcessingCompletedEvent> _processingHub;

      // ✅ Railway-Oriented Programming: no try/catch, failures propagate as Result
      public async Task<Result<ProcessingResult>> ProcessDocumentAsync(
          DocumentDownloadedEvent evt,
          CancellationToken cancellationToken)
      {
          var correlationId = evt.CorrelationId;
          var startTime = DateTimeOffset.UtcNow;

          // ✅ Quality analysis
          var qualityResult = await _qualityAnalyzer.AnalyzeAsync(evt.StoredPath, cancellationToken)
              .ThenTap(quality => BroadcastQualityEventAsync(quality, correlationId, cancellationToken));

          if (qualityResult.IsFailure)
              return Result<ProcessingResult>.WithFailure(qualityResult.Errors);

          // ✅ OCR processing
          var ocrResult = await _ocrExecutor.ExecuteAsync(qualityResult.Value, cancellationToken)
              .ThenTap(ocr => BroadcastOcrEventAsync(ocr, correlationId, cancellationToken));

          if (ocrResult.IsFailure)
              return Result<ProcessingResult>.WithFailure(ocrResult.Errors);

          // ✅ Classification
          var classificationResult = await _classifier.ClassifyAsync(ocrResult.Value, cancellationToken)
              .ThenTap(classification => BroadcastClassificationEventAsync(
                  classification, correlationId, cancellationToken));

          if (classificationResult.IsFailure)
              return Result<ProcessingResult>.WithFailure(classificationResult.Errors);

          // ✅ Export
          var exportResult = await _exporter.ExportAsync(classificationResult.Value, cancellationToken);

          if (exportResult.IsFailure)
              return Result<ProcessingResult>.WithFailure(exportResult.Errors);

          // ✅ Final event
          var processingResult = new ProcessingResult(
              FileId: evt.FileId,
              FileName: evt.FileName,
              Status: "Success",
              ProcessingDuration: DateTimeOffset.UtcNow - startTime,
              CorrelationId: correlationId
          );

          await BroadcastProcessingEventAsync(processingResult, cancellationToken);

          return Result<ProcessingResult>.Success(processingResult);
      }

      private async Task BroadcastQualityEventAsync(
          QualityAnalysisResult quality,
          Guid correlationId,
          CancellationToken ct)
      {
          var evt = new QualityCompletedEvent(
              FileId: quality.FileId,
              QualityScore: quality.Score,
              CorrelationId: correlationId,
              Timestamp: DateTimeOffset.UtcNow
          );
          await _qualityHub.SendToAllAsync(evt, ct);
      }

      // Similar for OcrCompleted, ClassificationCompleted, ProcessingCompleted...
  }
  ```
- Exit Criteria:
  - All tests green with 4 separate `IExxerHub<T>` mocks
  - `ProcessingOrchestrator` returns `Result<T>` for all operations
  - No try/catch blocks for control flow (only for truly exceptional cases)
  - Events broadcast via `IExxerHub<T>.SendToAllAsync()` at each pipeline stage
  - Failures at any stage prevent downstream events (Railway pattern)
  - Correlation ID preserved across all events

## Stage 4: Health & Dashboard Endpoints (TDD) - BASELINE IMPLEMENTATION
**Goal**: `/health` (liveness/readiness) and `/dashboard` (basic stats) on both workers.

⚠️ **Note**: This baseline implementation will return simple status codes. **Stage 4.5** will refactor to use Result<T> pattern.

- Tests (new host tests):
  - `Prisma.Orion.Worker.Tests`, `Prisma.Athena.Worker.Tests`: endpoints return 200; liveness reflects orchestrator start; dashboard returns counts/last heartbeat
- Implementation:
  - Minimal ASP.NET Core endpoints in worker hosts only
  - ⚠️ Temporary: Endpoints return plain objects instead of Result<T>
  - Dashboard data sourced from orchestrator metrics (downloads processed, last event time, queue depth if available)
- Exit Criteria:
  - Endpoint tests green; health reflects failure state when orchestrator not running
  - ⚠️ Known debt: Will need refactoring in Stage 4.5 for Result<T> pattern

---

## Stage 4.5: Health Endpoints Refactoring - RESULT MIGRATION
**Goal**: Migrate health and dashboard endpoints to return Result<T> for consistent error handling.

**Prerequisites**: Stage 4 complete

- Tests (refactored):
  - Update endpoint tests to validate `Result<HealthStatus>` and `Result<DashboardMetrics>`
  - Add Result<T> tests:
    - `GetHealth_WhenOrchestratorRunning_ReturnsSuccessWithStatus`
    - `GetHealth_WhenOrchestratorStopped_ReturnsFailure`
    - `GetDashboard_WithMetrics_ReturnsSuccessWithData`
    - `GetDashboard_WhenMetricsUnavailable_ReturnsFailure`
- Interfaces (refactored):
  - ✅ Add:
    ```csharp
    public interface IHealthReporter
    {
        Result<HealthStatus> GetStatus();
    }

    public interface IMetricsSnapshot
    {
        Result<DashboardMetrics> Snapshot();
    }
    ```
- Implementation Changes:
  ```csharp
  // In Prisma.Orion.Worker/Prisma.Athena.Worker
  app.MapGet("/health", (IHealthReporter reporter) =>
  {
      var result = reporter.GetStatus();
      return result.Match(
          onSuccess: status => Results.Ok(status),
          onFailure: errors => Results.ServiceUnavailable(new { Errors = errors })
      );
  });

  app.MapGet("/dashboard", (IMetricsSnapshot metrics) =>
  {
      var result = metrics.Snapshot();
      return result.Match(
          onSuccess: data => Results.Ok(data),
          onFailure: errors => Results.Problem(detail: string.Join(", ", errors))
      );
  });
  ```
- Exit Criteria:
  - All tests green with Result<T> pattern
  - Health endpoint returns 503 (ServiceUnavailable) when orchestrator not running
  - Dashboard endpoint returns 500 (Problem) when metrics unavailable
  - No exceptions thrown; all errors as Result.Failure()

## Stage 5: Sentinel Monitor (ITDD) - BASELINE IMPLEMENTATION
**Goal**: Detect lost heartbeats/zombie workers and trigger restart hook; log incidents.

⚠️ **Note**: This baseline implementation will poll health endpoints. **Stage 5.5** will refactor to use IExxerHub<WorkerHeartbeat> and Result<T>.

- Tests (new):
  - `Prisma.Sentinel.Monitor.Tests`: missing 3 heartbeat within SLA triggers implement forgive missed restart action; restart result logged
- Implementation:
  - Use `WorkerHeartbeat` contract; poll health endpoints (temporary); abstract restart via interface (e.g., `IProcessRestarter`)
  - ⚠️ Temporary: Polling instead of event subscription (will use IExxerHub<WorkerHeartbeat> in Stage 5.5)
  - ⚠️ Temporary: Restart operations throw exceptions instead of returning Result<T>
  - Configurable thresholds/timeouts
- Exit Criteria:
  - Tests green; sentinel runnable headless; restart hook injectable
  - ⚠️ Known debt: Will need refactoring in Stage 5.5 for Ember + Result patterns

---

## Stage 5.5: Sentinel Refactoring - EMBER + RESULT MIGRATION
**Goal**: Migrate Sentinel from polling to event-driven heartbeat consumption via IExxerHub<WorkerHeartbeat>, and add Result<T> for restart operations.

**Prerequisites**: Stage 5 complete, Stage 2.5 complete (WorkerHeartbeat events being broadcast)

- Tests (refactored):
  - Update `Prisma.Sentinel.Monitor.Tests` to use `IExxerHub<WorkerHeartbeat>` mocks
  - Add Railway-Oriented Programming and event-driven tests:
    - `MonitorWorkers_ReceivesHeartbeats_TracksWorkerStatus`
    - `MonitorWorkers_MissedHeartbeats_TriggersRestart`
    - `RestartWorker_WhenSuccessful_ReturnsSuccess`
    - `RestartWorker_WhenFails_ReturnsFailureWithReason`
    - `MonitorWorkers_MultipleWorkersMissing_RestartsAllAndCombinesResults`
- Interfaces (refactored):
  - ✅ Add: `IExxerHub<WorkerHeartbeat>` dependency (subscribe to heartbeat events)
  - ✅ Update:
    ```csharp
    public interface IProcessRestarter
    {
        // ✅ Returns Result<T> instead of throwing exceptions
        Task<Result<RestartResult>> RestartAsync(
            string workerId,
            CancellationToken cancellationToken);
    }
    ```
- Implementation Changes:
  ```csharp
  public class SentinelService : BackgroundService
  {
      private readonly IExxerHub<WorkerHeartbeat> _heartbeatHub;
      private readonly IProcessRestarter _restarter;
      private readonly ILogger<SentinelService> _logger;
      private readonly HeartbeatTracker _tracker;

      protected override async Task ExecuteAsync(CancellationToken ct)
      {
          // ✅ Subscribe to heartbeat events (event-driven, not polling)
          _heartbeatHub.On<WorkerHeartbeat>("ReceiveHeartbeat", heartbeat =>
          {
              _tracker.RecordHeartbeat(heartbeat);
              _logger.LogDebug("Heartbeat from {WorkerId}: {Status}",
                  heartbeat.WorkerId, heartbeat.Status);
          });

          // Monitor loop
          while (!ct.IsCancellationRequested)
          {
              await Task.Delay(TimeSpan.FromSeconds(10), ct);

              var missedWorkers = _tracker.GetMissedWorkers(threshold: 3);

              if (missedWorkers.Any())
              {
                  // ✅ Railway-Oriented: restart returns Result<T>
                  var restartResult = await RestartWorkersAsync(missedWorkers, ct);

                  if (restartResult.IsFailure)
                  {
                      _logger.LogError("Worker restart failed: {Errors}",
                          string.Join(", ", restartResult.Errors));
                  }
              }
          }
      }

      private async Task<Result> RestartWorkersAsync(
          IEnumerable<string> workerIds,
          CancellationToken ct)
      {
          var results = new List<Result>();

          foreach (var workerId in workerIds)
          {
              _logger.LogWarning("Restarting worker {WorkerId} due to missed heartbeats", workerId);

              var result = await _restarter.RestartAsync(workerId, ct);
              results.Add(result);

              if (result.IsSuccess)
              {
                  _logger.LogInformation("Worker {WorkerId} restarted successfully", workerId);
              }
              else
              {
                  _logger.LogError("Failed to restart {WorkerId}: {Errors}",
                      workerId, string.Join(", ", result.Errors));
              }
          }

          // ✅ Combine all results (succeeds only if all restarts succeeded)
          return Result.Combine(results.ToArray());
      }
  }
  ```
- Exit Criteria:
  - All tests green with `IExxerHub<WorkerHeartbeat>` mock
  - Sentinel subscribes to heartbeat events (no polling)
  - `IProcessRestarter.RestartAsync()` returns `Result<RestartResult>`
  - No exceptions thrown for restart failures (use Result.Failure())
  - Multi-worker restart uses `Result.Combine()` for aggregation

## Stage 6: Auth Abstraction (TDD) ✅ COMPLETE
**Goal**: Provider-agnostic auth; secure endpoints/event consumers.
**Status**: 14/14 tests passing (100%)
**Commit**: 0b5d4a4

- Tests (new):
  - ✅ `Prisma.Auth.Infrastructure.Tests`: 14 TDD tests for EfCoreIdentityAdapter + InMemoryIdentityProvider
  - ✅ All tests passing: 14/14 (8 EfCoreIdentityAdapter + 6 InMemoryIdentityProvider)
  - ✅ Code quality: XML docs, null safety, async/await, InternalsVisibleTo
- Implementation:
  - ✅ Interfaces in `Prisma.Auth.Domain` (IIdentityProvider, ITokenService, IUserContextAccessor)
  - ✅ `EfCoreIdentityAdapter<TUser>` wraps UserManager/SignInManager with JWT tokens
  - ✅ `InMemoryIdentityProvider` for dev/testing
  - ⏳ Wire to worker hosts and HMI (pending Stage 7)
- Exit Criteria:
  - ✅ Tests green (14/14 passing)
  - ✅ Hosts use abstractions (clean interfaces defined)
  - ✅ Easy swap of provider (adapter pattern implemented)

## Stage 7: HMI Event Consumption (ITDD) ✅ COMPLETE - WITH EMBER REFACTORING
**Goal**: UI receives real-time events and shows notifications/alerts with auth.
**Status**: 13/13 tests passing (100%) - Refactored to use IndFusion.Ember + IndQuestResults
**Commits**:
- 55d13f7 (RED - initial baseline with SignalR)
- aa4e0f0 (GREEN - partial with notification rendering)
- 9d99027 (REFACTOR - complete with Ember + Result) ✅

**⚠️ Architectural Discovery**: During implementation, discovered that direct SignalR HubConnection usage was unmockable and tightly coupled. Refactored to use IndFusion.Ember abstractions before baseline completion.

- Tests (refactored):
  - ✅ `Prisma.HMI.Tests`: 13/13 passing (100%)
  - ✅ NotificationRenderingTests: 5/5 passing
    - Severity-based rendering (Success/Warning)
    - Confidence score formatting
    - Timestamp handling
    - Manual review recommendations
  - ✅ EventBroadcastingTests: 8/8 passing (using `IExxerHub<T>`)
    - `BroadcastClassificationEvent_ViaSendToAllAsync_ReturnsSuccess`
    - `BroadcastProcessingEvent_ViaSendToAllAsync_ReturnsSuccess`
    - `BroadcastEvent_WhenCancelled_ReturnsCancelled`
    - `BroadcastEvent_WhenFails_ReturnsFailure`
    - `BroadcastMultipleEvents_MaintainsCorrelationId`
    - `BroadcastToGroup_WithValidGroupName_ReturnsSuccess`
    - `BroadcastToClient_WithValidConnectionId_ReturnsSuccess`
    - `GetConnectionCount_ReturnsCount`
  - ❌ Deleted: SignalREventSubscriptionTests, SignalRAuthenticationTests (unmockable HubConnection)
- Implementation:
  - ✅ Notification rendering logic (severity, formatting, queue)
  - ✅ Event contracts (ClassificationCompletedEvent, ProcessingCompletedEvent)
  - ✅ Event broadcasting via `IExxerHub<T>` (server-side, transport-agnostic)
  - ✅ Railway-Oriented Programming with `Result<T>` (Result.Success, Result.Failure, ResultExtensions.Cancelled)
  - ✅ Correlation ID tracking across event chains
- Exit Criteria:
  - ✅ All tests green (13/13)
  - ✅ Using `IExxerHub<T>` from IndFusion.Ember
  - ✅ Using `Result<T>` from IndQuestResults
  - ✅ No raw SignalR dependencies
  - ⏳ Pending: Wire to actual HMI UI components (Stage 8)

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

---

## 📋 Refactoring Roadmap Summary

### Completed Stages
- ✅ **Stage 6**: Auth Abstraction (14/14 tests) - Already uses Result<T>
- ✅ **Stage 7**: HMI Event Consumption (13/13 tests) - Already refactored with Ember + Result

### Pending Baseline + Refactoring Stages

| Stage | Baseline | Refactoring | Complexity | Priority |
|-------|----------|-------------|------------|----------|
| **Stage 2** | Orion Ingestion | **Stage 2.5** - Add IExxerHub<DocumentDownloadedEvent> + Result<T> | Medium | High |
| **Stage 3** | Athena Processing | **Stage 3.5** - Add 4x IExxerHub<T> + Result<T> pipeline | **Very High** | High |
| **Stage 4** | Health Endpoints | **Stage 4.5** - Add Result<T> to endpoints | Low | Medium |
| **Stage 5** | Sentinel Monitor | **Stage 5.5** - Add IExxerHub<WorkerHeartbeat> + Result<T> | Medium | Medium |

### Refactoring Impact Analysis

**IndFusion.Ember (IExxerHub<T>) Required**:
- Stage 2.5: 1 event type (DocumentDownloadedEvent)
- Stage 3.5: 4 event types (QualityCompleted, OcrCompleted, ClassificationCompleted, ProcessingCompleted)
- Stage 5.5: 1 event type (WorkerHeartbeat)
- **Total**: 6 event types needing Ember integration

**IndQuestResults (Result<T>) Required**:
- Stage 2.5: IngestionOrchestrator methods
- Stage 3.5: ProcessingOrchestrator pipeline (most complex)
- Stage 4.5: IHealthReporter, IMetricsSnapshot
- Stage 5.5: IProcessRestarter
- **Total**: ~15-20 methods needing Result<T> conversion

### Recommended Implementation Order
1. Complete Stage 1 (DI & Contracts) - No refactoring needed
2. Complete Stage 2 (Baseline) → **Stage 2.5 (Refactor)** immediately
3. Complete Stage 4 (Baseline) → **Stage 4.5 (Refactor)** immediately (low complexity)
4. Complete Stage 5 (Baseline) → **Stage 5.5 (Refactor)** immediately
5. Complete Stage 3 (Baseline) → **Stage 3.5 (Refactor)** last (highest complexity, needs 2.5 complete)
6. Stage 8 E2E (integrate all refactored stages)

**Rationale**: Stage 3.5 depends on Stage 2.5 (DocumentDownloadedEvent subscription), so refactor 2.5 first. Stage 4.5 is simplest, so knock it out early for momentum.

---

## Interfaces and Classes to be used already existing and implemented
- **Reuse existing Domain interfaces** (do not duplicate):
  - Quality: `IImageQualityAnalyzer`, `IFilterSelectionStrategy`
  - OCR: `IOcrExecutor`, `IOcrProcessingService`, `IOcrSessionRepository`
  - XML/Metadata: `IMetadataExtractor`, `IFieldExtractor<T>`, `IXmlNullableParser<T>`
  - Fusion/Reconciliation: `IFusionExpediente`, `IFieldMatcher`
  - Fusion/Reconciliation: `IFusionExpediente`, `IFieldMatcher`
  - Classification: `IFileClassifier`, `ILegalDirectiveClassifier`
  - Export: `IResponseExporter`, `IAdaptiveExporter`
  - Audit/Events: `IAuditLogger`, `IEventPublisher`
  - Ingestion helpers: `IBrowserAutomationAgent`, `IDownloadStorage`, `IDownloadTracker`

## New/Proposed Interfaces and Classes (to be added)
- **Add to Domain/Contracts**:
  - Optional `IContentHasher` (if hashing not folded into tracker)

- **Classes (libs/hosts already scaffolded)**:
  - Orion: `IngestionOrchestrator` (lib), `OrionWorkerService` (host)
  - Athena: `ProcessingOrchestrator` (lib), `AthenaWorkerService` (host)
  - Sentinel: `SentinelService` (monitor)
  - Auth: `InMemoryIdentityProvider` (initial implementation), plus domain interfaces in `Prisma.Auth.Domain`
  - Shared contracts: `DocumentDownloadedEvent`, `WorkerHeartbeat` (in `Prisma.Shared.Contracts`)
