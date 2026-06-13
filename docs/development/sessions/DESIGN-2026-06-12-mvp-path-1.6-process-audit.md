# Design — MVP-PATH 1.6 Per-process access audit (A6 DoD)

**Date:** 2026-06-12 · **Branch:** `Kt2` · **DoD anchor:** `MVP-PATH-2026-06-11.md` item 1.6 (A6)
**Prerequisite:** MVP-PATH 1.5 (A5) DONE — `ISiaraActorIdentityProvider` / `SiaraActor` /
`ProcessIdentityOptions` wired in all three workers; `AddProcessIdentity` called in each `Program.cs`.

This is the intended-solution doc. Implementation follows this; adversarial review checks the diff against it.

---

## KEY FINDING — current audit state: ALL worker-pipeline calls are no-ops today

**None of the three worker hosts register `IAuditLogger` / `AddDatabaseServices`.** Confirmed by reading
all three `Program.cs` files:

- `04 Services/Orion/Prisma.Orion.Worker/Program.cs` — no `AddDatabaseServices`, no `IAuditLogger`.
- `04 Services/Athena/Prisma.Athena.Worker/Program.cs` — no `AddDatabaseServices`, no `IAuditLogger`.
- `04 Services/Reconciliator/Prisma.Reconciliator.Worker/Program.cs` — no `AddDatabaseServices`, no `IAuditLogger`.

`LogAuditAsync` IS called from four `Application` services
(`DocumentIngestionService`, `ExportService`, `DecisionLogicService`, `MetadataExtractionService`) — but
**none of these are registered in any worker DI container**. They live in the `Application` layer and are
wired only in `Web.UI`. The worker pipelines go through `IngestionOrchestrator`, `ExtractionOrchestrator`,
and `ReconciliationOrchestrator` — none of which call `IAuditLogger` today.

Result: **zero audit records are produced by the three pipeline worker processes today.** The A6 gap is
net-new coverage, not fixing a broken caller. The existing `EventPersistenceWorker` (DB-backed) only runs
in `Web.UI`; it is not started in any worker host.

---

## 1. Process-identity dimension — extending LogAuditAsync without breaking callers

### The concrete problem

`AuditRecord` (`01 Core/Domain/Entities/AuditRecord.cs:6`) has no `ProcessId` or `ProcessClearance` field.
`IAuditLogger.LogAuditAsync` (`01 Core/Domain/Interfaces/IAuditLogger.cs:21`) has no process-identity
parameter. Adding one as a required positional parameter breaks all ~8 existing call sites
(in `DocumentIngestionService`, `ExportService`, `DecisionLogicService`, `MetadataExtractionService` and
their tests).

### Recommendation: reuse `UserId` + `ActionDetails` JSON — no EF migration needed

Two viable options:

**Option A — Reuse existing fields (RECOMMENDED for MVP)**
- `UserId` (`AuditRecord.cs:36`) is `string?` and is already `null` for system actions in
  `EventPersistenceWorker.MapEventToAuditRecord` (`Infrastructure.Database/Services/EventPersistenceWorker.cs:131`).
  Populate it with `SiaraActor.ActorId` for worker-originated records (e.g. `"orion-downloader-prod"`).
  This reuses the existing `GetAuditRecordsAsync(userId: ...)` filter without any schema change.
- `ActionDetails` is `string?` (free-form JSON). Extend the serialized payload to include a `processIdentity`
  sub-object:
  ```json
  {
    "processId": "orion-downloader-prod",
    "processType": "ServiceAccount",
    "processClearance": "Download",
    "displayName": "Orion Downloader",
    "fileId": "...",
    "action": "DocumentReceived"
  }
  ```
  The existing query methods already return `ActionDetails` on each record; callers that need the process
  dimension parse this JSON.
- **No EF Core migration required.** `PrismaDbContext.AuditRecords` maps the existing 10 columns; nothing
  changes. `QueuedAuditLoggerService` and `AuditLoggerService` pass `actionDetails` through unchanged.
- **The `LogAuditAsync` signature is unchanged** — existing callers compile without modification. The
  worker-side audit calls supply `userId: actor.ActorId` and `actionDetails: JsonSerializer.Serialize(processIdentity)`.

**Option B — New `ProcessId` column on `AuditRecord` (owner-gated)**
- Add `public string? ProcessId { get; set; }` to `AuditRecord.cs`.
- Add a new optional parameter `string? processId = null` to `IAuditLogger.LogAuditAsync` (trailing
  optional, existing callers compile unchanged since it has a default).
- Add a new EF migration. The column is nullable so existing rows are unaffected.
- Enables a first-class `WHERE ProcessId = ?` filter on the query methods without JSON parsing.
- **Requires an EF migration — owner decision required (see §8).**

**Recommendation:** ship Option A for MVP. The `UserId` field already indexes the actor id; the
`ActionDetails` JSON carries the full process identity for richer queries without schema churn. Option B
can follow when a dedicated "filter by process" query endpoint is required.

---

## 2. Where audit gets persisted for the workers (OWNER DECISION — see §8)

Two architecture options:

### Option A — Each worker takes direct shared-audit-DB access (RECOMMENDED)

Each of the three workers calls `AddDatabaseServices(connectionString, configuration)` in `Program.cs`.
This registers `IAuditLogger` (via `QueuedAuditLoggerService`) + `PrismaDbContext` + `EventPersistenceWorker`
in the worker host. The shared-audit-DB pattern is already used by `Web.UI`; the connection string is
the same SQL Server instance, supplied per-worker via `ConnectionStrings:DefaultConnection` config (or an
environment variable in a container).

Pros:
- Audit records are written with the worker's own actor identity immediately, in the same transaction window
  as the pipeline work.
- No new infrastructure component; reuses the already-production-hardened `QueuedAuditLoggerService` (with
  its channel + `QueuedAuditProcessorService` background worker — already in `AddDatabaseServices`).
- Queries (`GetAuditRecordsByFileIdAsync`) work across all four hosts (UI + 3 workers) against the same DB.
- `EventPersistenceWorker` (which maps domain events → audit records) also starts in each worker host,
  giving automatic audit coverage for events the worker publishes (e.g. `DocumentDownloadedEvent`,
  `QualityAnalysisCompletedEvent`, `OcrCompletedEvent`, `ClassificationCompletedEvent`,
  `DocumentProcessingCompletedEvent`).

Cons:
- Each worker needs its own DB connection pool (4 pools total). For an unattended 3-process deployment,
  this is negligible — SQL Server connection pools are lightweight.
- The connection string (and DB credentials) must be provisioned in each worker's config. In a container
  deployment, mount as a Kubernetes Secret or use a managed identity connection string.

### Option B — Workers emit audit as domain events; a central persister writes

Workers publish `AuditEvent` (a new domain event carrying the audit payload) onto their local
`IEventPublisher`. A new central `AuditRelayService` (running in one designated process — or a fifth
microservice) receives all audit events over an Ember/SignalR edge and writes them to the DB.

Pros:
- Worker processes have zero DB dependency; easier to run in a locked-down network zone.
- Single DB writer eliminates multi-source write conflicts.

Cons:
- Requires a new Ember cross-process edge (a fourth SignalR edge on top of the three already built in 1.3/1.4).
- Audit is eventually-consistent: a worker crash before the relay receives the event loses the record.
- Significantly more infrastructure code; audit relay is itself a new audit gap until it is itself audited.
- Over-engineering for MVP: the relay service gives the same guarantees as the `QueuedAuditLoggerService`
  channel that already exists in Option A, at much higher complexity.

**Recommendation:** Option A. The DB-direct path reuses proven infrastructure and provides durable, per-worker
audit without new cross-process edges. The shared DB connection is the same one used by `Web.UI` today.

---

## 3. Audit call sites — concrete points per worker

### A. Orion Downloader — `IngestionOrchestrator`

`IngestionOrchestrator` (`04 Services/Orion/Prisma.Orion.Ingestion/IngestionOrchestrator.cs`) is host-agnostic.
Add `IAuditLogger? auditLogger = null` as an optional constructor parameter (keeps existing `Web.UI` registration
unchanged) and `ISiaraActorIdentityProvider? actorIdentityProvider = null`. Workers wire both; existing callers
that omit them get no-op behavior.

| Event | AuditActionType | ProcessingStage | When |
|---|---|---|---|
| Document received (start of `IngestDocumentAsync`) | `Download` | `Ingestion` | Before `DownloadDocumentAsync` |
| Document stored successfully | `Download` | `Ingestion` | After `StoreDocumentAsync` success, `success: true` |
| Duplicate detected + skipped | `Download` | `Ingestion` | After `CheckDuplicateAsync`, `actionDetails: "duplicate"` |
| Ingestion failed | `Download` | `Ingestion` | On `result.IsFailure`, `success: false` |

The actor id for all Orion records: `SiaraActor.ActorId` from `ISiaraActorIdentityProvider.GetCurrentActorAsync`.
The `DocumentDownloadedEvent` already carries `context.ActorId` (from the downloader provenance) — pass it as
`userId` so existing records from the UI's `DocumentIngestionService` remain queryable by actor.

### B. Athena Extractor — `ExtractionPipelineService`

`ExtractionPipelineService` (`04 Services/Athena/Prisma.Athena.Processing/ExtractionPipelineService.cs`) is
the Extractor actor's entry point. Add `IAuditLogger? auditLogger = null` and
`ISiaraActorIdentityProvider? actorIdentityProvider = null`.

| Event | AuditActionType | ProcessingStage | When |
|---|---|---|---|
| Extraction started | `Extraction` | `Extraction` | Top of `ProcessAsync`, before `ExtractAsync` |
| Quality rejected | `Extraction` | `Extraction` | After `extraction.QualityRejected`, `success: false`, details: `"quality_rejected"` |
| Extraction + handoff completed | `Extraction` | `Extraction` | After `_reconciliationHub.SendToAllAsync` succeeds, `success: true` |
| Extraction failed (no expediente / store/broadcast failure) | `Extraction` | `Extraction` | On failure returns, `success: false` |

The actor id: `ISiaraActorIdentityProvider.GetCurrentActorAsync()` → `SiaraActor.ActorId`.

Note: `EventPersistenceWorker` (registered via Option A's `AddDatabaseServices`) will also produce automatic
records for `QualityAnalysisCompletedEvent`, `OcrCompletedEvent`, etc. — but those records carry `UserId: null`.
The explicit `ExtractionPipelineService` call adds the process-identity dimension that `EventPersistenceWorker`
cannot supply.

### C. Reconciliator — `ReconciliationPipelineService`

`ReconciliationPipelineService`
(`04 Services/Athena/Prisma.Athena.Processing/Reconciliation/ReconciliationPipelineService.cs`) is the
Reconciliator actor's entry point. Add `IAuditLogger? auditLogger = null` and
`ISiaraActorIdentityProvider? actorIdentityProvider = null`.

| Event | AuditActionType | ProcessingStage | When |
|---|---|---|---|
| Reconciliation started | `Classification` | `DecisionLogic` | Top of `ProcessAsync`, before `LoadAsync` |
| Handoff load failed | `Classification` | `DecisionLogic` | After `load.IsFailure`, `success: false`, details: path |
| Reconciliation + export completed | `Export` | `Export` | After `ReconcileAsync` + `Publish(DocumentProcessingCompletedEvent)`, `success: true` |

`AuditActionType.Classification` covers the entry stage (classification is the first stage in `ReconciliationOrchestrator`).
`AuditActionType.Export` covers the completion record (export ran and `DocumentProcessingCompletedEvent` was published).

### Process-identity JSON payload (all three workers)

```csharp
private static string BuildProcessAuditDetails(SiaraActor actor, ProcessClearance clearance, string action)
    => JsonSerializer.Serialize(new
    {
        processId     = actor.ActorId,
        processType   = actor.ActorType.ToString(),
        processClearance = clearance.ToString(),
        displayName   = actor.DisplayName,
        action
    });
```

This serialized string goes into `actionDetails`. The `userId` parameter receives `actor.ActorId` so the
standard date-range query `GetAuditRecordsAsync(userId: actor.ActorId)` answers "which records did process X
produce?" without JSON parsing.

---

## 4. A6 DoD test shape

**The canonical A6 DoD test** proves: "an audit query answers who/which-process touched doc X."

```csharp
// Location: new Prisma.Athena.Processing.Tests (integration-tier, using Testcontainers SQL)
// or: existing Tests.Infrastructure.Database (already has real IAuditLogger + DB tests)

[Fact]
public async Task ProcessAudit_AfterExtractionPipeline_GetAuditRecordsByFileIdReturnsProcessIdentity()
{
    // Arrange
    // - Real QueuedAuditLoggerService backed by Testcontainers SQL (or inject AuditLoggerService directly)
    // - Real ExtractionPipelineService with IAuditLogger + ISiaraActorIdentityProvider wired in
    // - ISiaraActorIdentityProvider returns SiaraActor { ActorId = "athena-extractor-test", ... }
    // - ExtractionOrchestrator with stub stages (QualityOK, OCR returns text, Fusion returns expediente)
    // - IExpedienteHandoffStore → InMemoryExpedienteHandoffStore (already exists from 1.4 tests)
    // - IExxerHub<ExtractionCompletedEvent> → fake that returns Success

    var fileId = Guid.NewGuid();
    var correlationId = Guid.NewGuid();

    // Act
    var result = await _extractionPipelineService.ProcessAsync(
        new DocumentDownloadedEvent { FileId = fileId, CorrelationId = correlationId, ... },
        TestContext.Current.CancellationToken);

    result.IsSuccess.ShouldBeTrue();

    // Assert: audit records exist for this fileId and carry the process identity
    var auditResult = await _auditLogger.GetAuditRecordsByFileIdAsync(
        fileId.ToString(), TestContext.Current.CancellationToken);

    auditResult.IsSuccess.ShouldBeTrue();
    var records = auditResult.Value!;
    records.ShouldNotBeEmpty();

    // The extraction-started record
    var startRecord = records.FirstOrDefault(r => r.Stage == ProcessingStage.Extraction
        && r.ActionType == AuditActionType.Extraction);
    startRecord.ShouldNotBeNull();
    startRecord!.UserId.ShouldBe("athena-extractor-test");
    startRecord.ActionDetails.ShouldNotBeNullOrWhiteSpace();
    startRecord.ActionDetails!.ShouldContain("athena-extractor-test");
    startRecord.ActionDetails.ShouldContain("Extract");  // ProcessClearance
}
```

A symmetric test for the Orion Downloader (on `IngestionOrchestrator`) and the Reconciliator
(`ReconciliationPipelineService`) completes the A6 test suite. The three tests can live in a single
`ProcessAuditIntegrationTests` class in `Tests.System.Storage` (which already has Testcontainers SQL
and `IAuditLogger` tests — `08 Tests/05 System/Tests.System.Storage/`).

---

## 5. ITDD impact (ADR-005)

`IAuditLogger` has an ITDD contract base (`AuditLoggerContract`) in `Tests.Domain.Interfaces` — verify it
exists; if not, one must be created as part of this phase (the A6 tests depend on it driving both
`AuditLoggerService` and `QueuedAuditLoggerService`).

**Affected classes (constructors gain optional params):**
- `IngestionOrchestrator` — add `IAuditLogger? auditLogger` + `ISiaraActorIdentityProvider? actorIdentity`
  (optional, default `null`); when null, audit calls are skipped (no-op). Existing tests in
  `Prisma.Orion.Worker.Tests` (if any) compile unchanged.
- `ExtractionPipelineService` — same pattern.
- `ReconciliationPipelineService` — same pattern.

The optional-null pattern is consistent with how `ExtractionOrchestrator` already takes all pipeline
services as optional (`qualityAnalyzer = null`, `ocrExecutor = null`, etc. — see
`ExtractionOrchestrator.cs:51-66`).

**No new `*Contract` abstract base** is needed for the orchestrators/pipeline services — they are concrete,
not interfaces, so they do not fall under the ADR-005 contract-base rule (same decision as 1.5's
forwarder treatment). The `IAuditLogger` contract base (if it exists) gains no new tests here; the A6
test drives the real implementation.

**Architecture guardrail (B3):** no new interface introduced → no new `*Contract` base required → arch
test count unchanged.

---

## 6. Reusing the 1.5 actor identity

`ISiaraActorIdentityProvider.GetCurrentActorAsync()` (`01 Core/Domain/Interfaces/ISiaraActorIdentityProvider.cs:31`)
is the single authoritative source of the process identity for audit purposes, per ADR-010 S8.3. It is
already registered in all three workers via `AddProcessIdentity` (`ProcessIdentityServiceCollectionExtensions.cs:63`
— `TryAddSingleton<ISiaraActorIdentityProvider, ConfiguredSiaraActorIdentityProvider>()`).

The call pattern in each pipeline service:

```csharp
private async Task<(string actorId, string auditDetails)> ResolveProcessIdentityAsync(
    ProcessClearance clearance, string action, CancellationToken ct)
{
    if (_actorIdentityProvider is null)
        return ("system", JsonSerializer.Serialize(new { action }));

    var actorResult = await _actorIdentityProvider.GetCurrentActorAsync(ct).ConfigureAwait(false);
    if (actorResult.IsFailure)
    {
        // Fail-open for audit: log a degraded record rather than crash the pipeline.
        // The actor identity resolution failure is itself logged at Warning.
        return ("unknown-process", JsonSerializer.Serialize(new { action, error = actorResult.Error }));
    }

    var actor = actorResult.Value!;
    return (actor.ActorId, BuildProcessAuditDetails(actor, clearance, action));
}
```

`clearance` comes from `IOptions<ProcessIdentityOptions>.Value.Clearance` — already injected via
`AddProcessIdentity`. The `BuildProcessAuditDetails` helper is a private static (see §3 above).

**Fail-open policy for audit:** an actor-identity resolution failure must NOT stop the pipeline. The
document was already downloaded/extracted/reconciled by the time the audit call happens. A degraded
audit record (`userId: "unknown-process"`) is better than a pipeline crash. The resolution failure is
logged at `Warning` level so ops can detect a misconfigured `Siara:Actor:ActorId`.

---

## 7. DI wiring per worker (Option A — direct DB)

### Step 1 — Add `AddDatabaseServices` to each worker `Program.cs`

```csharp
// Orion, Athena, Reconciliator — add this block (before host.Build()):
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is required for audit persistence.");
builder.Services.AddDatabaseServices(connectionString, builder.Configuration);
```

`AddDatabaseServices` (`Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs`)
registers `IAuditLogger`, `PrismaDbContext`, `QueuedAuditProcessorService` (hosted), `EventPersistenceWorker`
(hosted), SLA services, and audit-retention background service. All of these are already production-hardened
(run in `Web.UI` today).

### Step 2 — Wire `IAuditLogger` + `ISiaraActorIdentityProvider` into pipeline services

Each worker's `Program.cs` passes the two new optional dependencies to the pipeline service constructors.
The pipeline services (`IngestionOrchestrator`, `ExtractionPipelineService`, `ReconciliationPipelineService`)
are registered as `AddScoped` / `AddSingleton` via factory lambdas — add the two resolved services to the
factory:

```csharp
// Example for ExtractionPipelineService in Athena Program.cs
builder.Services.AddSingleton<ExtractionPipelineService>(sp => new ExtractionPipelineService(
    sp.GetRequiredService<IEventPublisher>(),
    sp.GetRequiredService<ExtractionOrchestrator>(),
    sp.GetRequiredService<IExpedienteHandoffStore>(),
    sp.GetRequiredService<IExxerHub<ExtractionCompletedEvent>>(),
    sp.GetRequiredService<ILogger<ExtractionPipelineService>>(),
    auditLogger: sp.GetRequiredService<IAuditLogger>(),               // NEW (A6)
    actorIdentityProvider: sp.GetRequiredService<ISiaraActorIdentityProvider>()));  // NEW (A6)
```

`IAuditLogger` is scoped in `AddDatabaseServices`; `ExtractionPipelineService` is singleton. This requires
either (a) resolving `IAuditLogger` via `IServiceScopeFactory` (same pattern as `QueuedAuditLoggerService`
uses for queries), or (b) registering `ExtractionPipelineService` as scoped. Option (b) is simpler; the
worker resolves a fresh scope per document in `AthenaWorkerService` anyway. **Recommendation: change
`ExtractionPipelineService` and `ReconciliationPipelineService` from singleton to scoped for the worker
registration — consistent with how `IngestionOrchestrator` is scoped in Orion (Orion.Worker `Program.cs:112`).**

---

## 8. Owner decisions required

### Decision 1 — Workers direct-DB vs event-emitted audit (BLOCKING)

**Framing:** do you accept adding `AddDatabaseServices` (and therefore a SQL Server connection string) to
all three worker `Program.cs` files?

- **Yes → Option A (direct-DB).** Simple, proven, audit is durable. Each worker needs `ConnectionStrings:DefaultConnection` in its config (mount as a secret in production). No new code beyond §7 wiring.
- **No → Option B (event-emitted, central relay).** Workers stay DB-free. Requires a new Ember edge + audit-relay service. Higher complexity, eventual-consistency audit. Not recommended for MVP.

**Recommendation: Option A.**

### Decision 2 — New `ProcessId` column + EF migration vs reuse existing fields (OPTIONAL)

**Framing:** is a first-class `ProcessId` column on `AuditRecord` worth an EF migration for MVP?

- **No migration (Option A in §1):** `UserId` carries `actor.ActorId`; `ActionDetails` JSON carries full
  process identity. No schema change. A post-MVP migration can add the column later (nullable, zero-downtime).
  The `GetAuditRecordsAsync(userId: ...)` filter already handles "show me all records from process X."
- **Add migration (Option B in §1):** first-class `ProcessId` column; `GetAuditRecordsAsync` gains a
  `processId` filter parameter. More queryable; costs a migration and a signature change on `IAuditLogger`.

**Recommendation: no migration for MVP.** The `UserId` field carries the actor id; the `ActionDetails`
JSON is sufficient for compliance queries. A follow-up migration can be applied post-MVP.

---

## Work breakdown

1. **Verify/create `IAuditLogger` ITDD contract base** — check whether `AuditLoggerContractBase` exists
   in `Tests.Domain.Interfaces`; create it if absent (per ADR-005 Phase 7 deferred `IFieldExtractor<T>`
   sweep note — `IAuditLogger` is a core domain interface and should have a contract base). Low-risk,
   isolated. Build + green.
2. **Wire `AddDatabaseServices` in all three worker `Program.cs`** — add connection string config + call.
   Build only (no test yet). Confirm each worker host starts with 0/0 errors.
3. **Add optional audit params to pipeline service constructors** — `IngestionOrchestrator`,
   `ExtractionPipelineService`, `ReconciliationPipelineService`. Add `ResolveProcessIdentityAsync` helper +
   `BuildProcessAuditDetails`. Optional params default to null; behavior unchanged when null.
4. **Wire `IAuditLogger` + `ISiaraActorIdentityProvider` in worker factories** — update each `Program.cs`
   factory lambda. Change service lifetime from singleton to scoped where needed (see §7).
5. **Audit call sites** — add the 3–4 `LogAuditAsync` calls per pipeline service (see §3 table).
   All existing tests still compile (optional params).
6. **A6 DoD tests** — three tests in `Tests.System.Storage` (Testcontainers SQL): Downloader / Extractor /
   Reconciliator each produce an audit record with process identity that `GetAuditRecordsByFileIdAsync`
   returns. Full suite green.
7. **Docs** — ADR update + handoff note + MEMORY.md update for 1.6. Separate commit.

## HARD CONSTRAINTS

xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions) · `TestContext.Current.CancellationToken` ·
`Result<T>` + `CancellationToken` on every async, pre-cancel → `ResultExtensions.Cancelled()` · ITDD per
ADR-005 · fail-open for audit (never crash pipeline on audit failure) · `dotnet test <csproj>` no extra
flags · single-project builds · commit code+tests separately from docs · push `Kt2`.
