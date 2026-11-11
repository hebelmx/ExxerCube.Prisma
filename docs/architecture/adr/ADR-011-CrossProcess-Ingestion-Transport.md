# ADR-011: Cross-Process Ingestion Transport (Downloader → Extractor)

**Date**: 2026-06-12
**Status**: Accepted
**Deciders**: Owner + Development Team
**Tags**: ingestion, signalr, indfusion-ember, three-actors, orion, athena, mvp-path-1.3
**Related**: ADR-009 (Real-time comm extracted to IndFusion.Ember), ADR-010 (SIARA auth seam), MVP-PATH 1.1/1.2

## Context

The Orion *Downloader* and Athena *Extractor* run as **separate processes** (the security-mandated
3-process split — Downloader / Extractor / Reconciliator). After MVP-PATH 1.1 (real SIARA downloader) and
1.2 (watch loop), Orion ingests documents and raises a `DocumentDownloadedEvent` through
`IExxerHub<DocumentDownloadedEvent>` — but that hub was `StubExxerHub`, which only logs. Meanwhile Athena's
`ProcessingOrchestrator` subscribes to its **own in-process** Rx `EventPublisher` for
`DocumentDownloadedEvent`. Nothing connected the two: Orion's events never reached Athena. This is the first
real cross-process edge of the Three-Actors design.

## Decision

Use the **IndFusion.Ember SignalR transport** for the Downloader → Extractor edge.

- **Orion (publisher / host)** hosts a SignalR hub `IngestionHub : ExxerHub<DocumentDownloadedEvent>` at
  `/hubs/ingestion`. The production `IExxerHub<DocumentDownloadedEvent>` implementation,
  `SignalRIngestionBroadcaster`, broadcasts each event to all connected clients on the Ember
  `"ReceiveMessage"` protocol. It replaces `StubExxerHub`.
- **Athena (consumer / client)** runs `SiaraIngestionHubClient` (a `BackgroundService`) that opens a SignalR
  `HubConnection` to the configured Orion hub URL (`Ingestion:HubUrl`), listens on `"ReceiveMessage"` for
  `DocumentDownloadedEvent`, and hands each one to `IngestionEventForwarder`, which **republishes** it onto
  Athena's local `IEventPublisher`. The existing `ProcessingOrchestrator` subscription then drives the
  pipeline — no orchestrator change required.

## Rationale / Key decisions

1. **Broadcast via `IHubContext`, not a resolved hub.** `ExxerHub<T>` derives from SignalR `Hub`, whose
   `Clients` property is only populated by the runtime during an *inbound* invocation. A hub instance
   resolved from DI for *outbound* sending has a null `Clients` and silently fails. So the broadcaster wraps
   `IHubContext<IngestionHub>`. (The Web.UI `AddScoped<IExxerHub<DomainEvent>, ProcessingHub>()` precedent is
   actually broken-but-swallowed for this reason; we did it correctly here.)
2. **Republish onto the local stream rather than re-plumb the pipeline.** Forwarding received events into
   Athena's `EventPublisher` keeps `ProcessingOrchestrator` and all five pipeline stages untouched — the
   transport is a thin edge, not a pipeline rewrite.
3. **Resilient and optional.** The client retries the initial connect and uses SignalR automatic reconnect
   for drops, so a transient Orion outage never crashes Athena. With no `Ingestion:HubUrl` configured the
   subscriber stays idle (logged), so single-service and test deployments boot cleanly. Broadcasting is
   Railway-Oriented: a failed send is a `Result` failure the caller logs and continues on — event delivery
   never breaks ingestion.
4. **Ember provides server + client connection infra, not a client wrapper.** `IExxerHub`/`ExxerHub` are
   server-side; the consumer uses a plain `Microsoft.AspNetCore.SignalR.Client` `HubConnection` with the
   `"ReceiveMessage"` method name, matching `ExxerHub<T>`'s wire contract.

## Consequences

- The `DocumentDownloadedEvent` now crosses the process boundary over a real transport; an end-to-end wire
  test (TestServer-hosted hub + real `HubConnection`) proves delivery, and the broadcaster + forwarder are
  unit-tested independently.
- **Shared document storage — wired (MVP-PATH 1.4, 2026-06-12).** The event now carries a storage-*relative*
  `Path` (`YYYY/MM/DD/{fileId}.pdf`); each process configures its own `Storage:BasePath` at the shared volume
  and resolves base + relative, so the Downloader and Extractor may mount the volume at different absolute
  paths. Resolution is a Domain port (`IStoragePathResolver`) over a pure, deterministic confinement guard
  (`StoragePathResolution` — blank / rooted / `../` traversal / invalid-char all fail closed, no disk access),
  implemented by `SharedStoragePathResolver` (Athena) and single-sourced with its reference fake. The Athena
  `IngestionEventForwarder` resolves the relative path to a loadable absolute path and stamps it onto
  `FileName` before republishing; it is tolerant of configuration faults — on a blank path or a resolution
  failure it forwards the event unchanged so the orchestrator's existing log-and-continue still holds. The
  Orion `IngestionOrchestrator` stamps the relative path and reads `Storage:BasePath` from config. An
  end-to-end test proves a relative-path event resolves and the pipeline opens the absolute path. See
  `ADR-011` §"Shared document storage" decision recorded by MVP-PATH 1.4.
- Hub authentication/authorization for the ingestion edge is deferred (today it is an internal, network-
  bounded endpoint); harden before any untrusted-network exposure.

## Reconciliator edge (Extractor → Reconciliator) — decided + built (MVP-PATH 1.4, 2026-06-12)

The second cross-process edge of the Three-Actors split is now built, completing the 3-process pipeline
(Downloader → Extractor → Reconciliator) and satisfying MVP DoD **A4**.

### Decision
Apply the **same transport pattern** as the ingestion edge, with the handoff carried as a **shared-storage
reference** (owner-chosen over payload-on-event):

- **Athena Extractor (host/publisher).** The Athena worker becomes the Extractor actor: it runs the Extractor
  half (Quality → OCR → Fusion via `ExtractionOrchestrator`), persists the fused expediente to the shared
  volume via `IExpedienteHandoffStore` (`FileSystemExpedienteHandoffStore`, JSON, reusing the 1.4
  `IStoragePathResolver` confinement guard), and broadcasts an `ExtractionCompletedEvent` — carrying only the
  storage-*relative* handoff path — over a hub it hosts at `/hubs/reconciliation`
  (`ReconciliationHub : ExxerHub<ExtractionCompletedEvent>` + `SignalRReconciliationBroadcaster` via
  `IHubContext`). Driven by `ExtractionPipelineService`.
- **Reconciliator (new host/consumer).** A new `Prisma.Reconciliator.Worker` (own `Program.cs`) is a SignalR
  *client* of `/hubs/reconciliation` (`ReconciliationHubClient` + `ReconciliationClientOptions`, retry +
  auto-reconnect, idle when blank). `ReconciliationEventForwarder` republishes the received event onto the
  local stream; `ReconciliationPipelineService` loads the fused expediente from shared storage and runs the
  Reconciliator half (Classification → Export via `ReconciliationOrchestrator`), emitting the terminal
  `DocumentProcessingCompletedEvent`.
- **Decomposition.** The monolithic `ProcessingOrchestrator` was carved at the Fusion|Classification seam into
  `ExtractionOrchestrator` (1–3) + `ReconciliationOrchestrator` (4–5); `ProcessingOrchestrator` now *composes*
  both, so the in-process monolith path is behavior-preserved (all existing tests green).
- **Data minimization (MVP A5).** Only the derived fused expediente crosses this edge (a shared-storage
  reference); the raw document never does.

### Rationale (additions)
- **Shared-storage reference over payload-on-event.** Keeps events light and reuses the proven 1.4
  storage/resolver plumbing; consistent with the document edge and with passing references/derived data across
  the security boundary.

### Consequences / known limitations (honest)
- The Athena worker is now **Extractor-only** (it no longer runs Classification/Export in-process); a working
  deployment runs all three processes. The `ProcessingOrchestrator` monolith remains available/registered for
  in-process use and health checks.
- **Export is wired-but-optional in the Reconciliator host.** The adaptive exporter is template-**DB-backed**
  (`AddAdaptiveExportServices(connectionString)` + SQL `TemplateDbContext`), so the host wires Classification
  fully and treats `IAdaptiveExporter` as optional (Stage 5 skipped with a warning when absent). Enabling real
  SIRO export = register the adaptive export chain + a connection string. This overlaps the separate MVP gate
  item F1/5.2 ("one real end-to-end run with SIRO export").
- The **3-process E2E** (`ThreeProcessPipelineEndToEndTests`) models the two edges with their real forwarders
  (not a live SignalR wire in that single test); each edge's real SignalR wire is proven separately by
  `IngestionHubWireTests` (1.3) and `ReconciliationHubWireTests` (1.4). A single all-real-wire 3-host E2E is a
  possible future hardening.
- Handoff JSON round-trips the expediente's string/collection fields faithfully; SmartEnum members (e.g.
  `Subdivision`) are best-effort (a dedicated JSON converter is a follow-up).
- `/hubs/reconciliation` has **no auth** (same posture as `/hubs/ingestion`); harden before untrusted-network
  exposure.
