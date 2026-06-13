# Design — MVP-PATH 1.4 Reconciliator edge (3-process split lynchpin)

**Date:** 2026-06-12 · **Branch:** `Kt2` · **DoD anchor:** `MVP-PATH-2026-06-11.md` item 1.4 (A4)
**Owner decisions (AskUserQuestion, this session):**
1. **Handoff transport = shared-storage reference** (NOT payload-on-event). The Extractor serializes the
   fused expediente to the shared volume; the cross-process event carries only a storage-relative path; the
   Reconciliator resolves + loads it. Reuses the 1.4 `IStoragePathResolver` / `StoragePathResolution` plumbing.
2. **Scope = full new Reconciliator host.** Three separately-hosted, Ember-coordinated processes; E2E across all three.

This is the intended-solution doc. Adversarial review checks the diff against THIS, not subagent prose.

---

## Target topology (after this work)

```
Orion.Worker (Downloader)        Athena.Worker (Extractor)            Reconciliator.Worker (NEW)
  SiaraWatchLoop                   SiaraIngestionHubClient (client)     ReconciliationHubClient (client)
  → IngestionOrchestrator          → IngestionEventForwarder            → ReconciliationEventForwarder
  → SiaraDocumentDownloader        → ExtractionOrchestrator (1-3)       → ReconciliationOrchestrator (4-5)
  → store {id}.pdf @ shared vol      stages: Quality→OCR→Fusion           load {id}.fusion.json @ shared vol
  → broadcast DocumentDownloaded     → save {id}.fusion.json @ shared vol  → Classification → Export
     via /hubs/ingestion             → broadcast ExtractionCompleted
                                        via /hubs/reconciliation  ───────────┘ (Reconciliator connects here)
```

- **Hub direction (per the 1.3 precedent):** the upstream actor HOSTS the hub; the downstream connects as a
  client. So **Athena Extractor hosts `/hubs/reconciliation`**; the new Reconciliator worker is its client —
  exactly as Orion hosts `/hubs/ingestion` and Athena connects.
- **Broadcast outbound via `IHubContext<THub>`, never a DI-resolved `Hub`** (null `Clients` — the 1.3 gotcha).

---

## The seam

`ProcessingOrchestrator` runs 5 stages inline. Split at the **Fusion | Classification** boundary:
- **Extractor** = Stage 1 Quality → Stage 2 OCR → Stage 3 Fusion. Output the pipeline needs downstream:
  `FusionResult.FusedExpediente` (an `Expediente`). (Stages 4 & 5 today consume only `fusionResult?.FusedExpediente`.)
- **Reconciliator** = Stage 4 Classification → Stage 5 Export.

### DRY decomposition (keep the monolith green)
Do NOT duplicate ~400 lines of stage logic, and do NOT break the existing monolith path (UI + 38
`Athena.Processing` tests + integration tests). Refactor `ProcessingOrchestrator` to **compose** two new
orchestrators:

- `ExtractionOrchestrator` (Processing lib) — owns stages 1–3 + `BuildPdfExpedienteFromOcrAsync` helper.
  Public: `Task<Result<FusionResult?>> ExtractAsync(DocumentDownloadedEvent, CancellationToken)`.
  In the **split** Extractor worker it ALSO: saves the expediente handoff + broadcasts `ExtractionCompletedEvent`.
- `ReconciliationOrchestrator` (Processing lib) — owns stages 4–5.
  Public: `Task<Result> ReconcileAsync(Expediente?, Guid fileId, Guid? correlationId, CancellationToken)`.
- `ProcessingOrchestrator` — keeps `ProcessDocumentAsync` / `ProcessDocumentWithResultAsync` for the in-process
  monolith (UI/tests), now delegating: `ExtractAsync(...)` → `ReconcileAsync(result.FusedExpediente, ...)`.
  Its existing event emissions and thresholds move WITH the stage methods into the two halves (no behavior change).

> If full delegation-decomposition proves too invasive to keep all monolith tests green in one pass, the
> fallback is to extract stages 1-3 and 4-5 into the two new orchestrators and have `ProcessingOrchestrator`
> call them — same end state. Either way: **all existing Athena.Processing tests stay green.**

---

## Handoff contract (shared-storage reference)

### New domain event — `ExtractionCompletedEvent : DomainEvent`
Fields: `Guid FileId`, (inherited `CorrelationId`), `string Path` (storage-relative path to the fusion
artifact, e.g. `2026/06/12/{fileId}.fusion.json`), plus light provenance (`int FieldsFused`,
`int ConflictsDetected`) for logging/telemetry. ctor sets `EventType = nameof(ExtractionCompletedEvent)`.
**MUST** add `[JsonDerivedType(typeof(ExtractionCompletedEvent), "ExtractionCompletedEvent")]` to
`DomainEvent` (the polymorphic allow-list) — otherwise STJ/SignalR round-trip breaks.

### New domain port — `IExpedienteHandoffStore`
The single seam for persisting/loading the fused expediente across the process boundary:
- `Task<Result<string>> SaveAsync(Expediente expediente, string relativePath, CancellationToken)` — writes the
  serialized expediente at `{Storage:BasePath}/relativePath`; returns the relative path (the value stamped on
  the event). Uses `IStoragePathResolver` to resolve the absolute target; creates parent dirs.
- `Task<Result<Expediente>> LoadAsync(string relativePath, CancellationToken)` — resolves + reads + deserializes.
Serialization = `System.Text.Json`. Confinement/traversal guard is already enforced by `StoragePathResolution`
inside the resolver — reuse it, don't re-implement. Relative-path convention mirrors 1.4 documents:
`YYYY/MM/DD/{fileId}.fusion.json` (forward-slash, mount-path independent).

### ITDD (ADR-005) for both new contracts
- `IExpedienteHandoffStore` → `ExpedienteHandoffStoreContract` (abstract base) + `FakeExpedienteHandoffStore`
  (in-memory dict keyed by relative path; round-trips a real `Expediente`) + ≥1 inheritor in
  `Tests.Domain.Interfaces` (fake) and the real impl's inheritor in `Tests.Infrastructure.FileSystem`.
- Real impl `FileSystemExpedienteHandoffStore` (Infrastructure.FileSystem) over `IStoragePathResolver` + `System.IO`.
- The fake and real MUST agree on the traversal/confinement guard (single-source via `IStoragePathResolver`),
  same discipline as `FakeStoragePathResolver` ↔ `SharedStoragePathResolver` in 1.4.

---

## Cross-process transport (mirror 1.3 exactly, new types)

**Athena Extractor (host/publisher):**
- `ReconciliationHub : ExxerHub<ExtractionCompletedEvent>` mapped at `/hubs/reconciliation`.
- `SignalRReconciliationBroadcaster : IExxerHub<ExtractionCompletedEvent>` — broadcasts via
  `IHubContext<ReconciliationHub>` on `"ReceiveMessage"`. ROP, never throws.
- `ExtractionOrchestrator` (split mode) calls `IExxerHub<ExtractionCompletedEvent>.SendToAllAsync(evt)` after
  saving the handoff artifact.

**Reconciliator.Worker (NEW host/consumer):**
- `Prisma.Reconciliator.Worker` — `WebApplication` host (same shape as Athena.Worker), its own `Program.cs`.
- `ReconciliationHubClient : BackgroundService` — `HubConnection` to `Reconciliation:HubUrl`,
  `On<ExtractionCompletedEvent>("ReceiveMessage", …)` → `ReconciliationEventForwarder.Forward`. Retry +
  auto-reconnect; **idle+logged when the URL is blank**. (Copy `SiaraIngestionHubClient` semantics.)
- `ReconciliationEventForwarder` (Processing lib) — republishes the received `ExtractionCompletedEvent` onto
  the local `IEventPublisher`; null-safe. (NO path-stamp needed — the Reconciliator loads via the handoff
  store, which resolves the path itself.)
- DI registers the pipeline's Stage-4/5 deps (classifier, exporter, handoff store, storage resolver) + the
  `ReconciliationOrchestrator` subscribing to the local `ExtractionCompletedEvent` stream.

**Reuse, don't fork:** options classes mirror `IngestionClientOptions` (`Reconciliation:HubUrl`, reconnect).

---

## Verification gate (DoD A4)
- Build 0/0 single-project for each touched project; full solution 0/0.
- Existing suites stay green: `Athena.Processing` (was 42), `Athena.Worker` (9), `Orion.*`, `Architecture` (22),
  `EndToEnd` (27), `Tests.Domain.Interfaces`, `Tests.Infrastructure.FileSystem`.
- New unit tests: handoff-store contract (fake + real), broadcaster (IHubContext mock), forwarder (real
  EventPublisher), real SignalR wire test (TestServer-hosted `ReconciliationHub` → live `HubConnection`).
- **E2E across all three processes**: a doc flows Downloader→Extractor→Reconciliator producing the export +
  completion event, no stub on the path. Live-wire E2E file force-added (`git add -f`).
- Architecture test: the new `*Contract` base has ≥1 inheritor; new ports off any allowlist as needed.

## HARD CONSTRAINTS
xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions) · `TestContext.Current.CancellationToken` ·
`Result<T>` + `CancellationToken` on every async, pre-cancel → `ResultExtensions.Cancelled<T>()` · ITDD per
ADR-005 · broadcast via `IHubContext` · `dotnet test <csproj>` no extra flags · single-project builds ·
commit code+tests separately from docs · push `Kt2`.

## Work breakdown (tracker)
1. **Domain handoff contract** — `ExtractionCompletedEvent` (+ JsonDerivedType) + `IExpedienteHandoffStore` port
   + ITDD contract base + fake. Build Domain + Tests.Domain.Interfaces green.
2. **Real handoff store** — `FileSystemExpedienteHandoffStore` (Infrastructure.FileSystem) + real inheritor test.
3. **Orchestrator decomposition** — `ExtractionOrchestrator` (1-3) + `ReconciliationOrchestrator` (4-5);
   `ProcessingOrchestrator` composes them. All `Athena.Processing` tests stay green.
4. **Extractor transport** — `ReconciliationHub` + `SignalRReconciliationBroadcaster`; ExtractionOrchestrator
   saves handoff + broadcasts; Athena.Worker `Program.cs` hosts `/hubs/reconciliation`. Broadcaster + wire tests.
5. **Reconciliator host** — new `Prisma.Reconciliator.Worker` project + `ReconciliationHubClient` +
   `ReconciliationEventForwarder` + DI + options + health endpoints; add to `.sln`.
6. **E2E + verification** — 3-process E2E test; full suite green; arch test green.
7. **Docs** — ADR-011 addendum (Reconciliator edge) + handoff + memory. (Separate commit from code.)
