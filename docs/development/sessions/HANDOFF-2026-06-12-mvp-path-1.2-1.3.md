# Handoff — MVP-PATH 1.2 (watch loop) + 1.3 (Ember transport) — DONE

**Date:** 2026-06-12 · **Branch:** `Kt2` (pushed) · **Supersedes:** `HANDOFF-2026-06-12-mvp-path-1.1-siara-downloader.md`
**Scope:** MVP-PATH Workstream 1.2 (SIARA watch loop) + 1.3 (real IndFusion.Ember cross-process transport).
**Result:** **Workstream 1 (ingestion + SIARA) is end-to-end complete.** 1.1 + 1.2 + 1.3 all done, pushed, green.

---

## TL;DR

The Orion worker now runs a real **headless ingestion loop** (1.2) and broadcasts each downloaded document
to Athena over a real **SignalR transport** (1.3), where it drives the processing pipeline. The full path is:

```
SiaraWatchLoop (discover → per-doc scope) → IngestionOrchestrator.IngestDocumentAsync
  → SiaraDocumentDownloader (1.1) → SHA-256 journal → SignalRIngestionBroadcaster (IExxerHub)
  → IngestionHub (/hubs/ingestion)  ⇒⇒ cross-process ⇒⇒  SiaraIngestionHubClient (Athena)
  → IngestionEventForwarder → local EventPublisher → ProcessingOrchestrator pipeline (Quality→OCR→Fusion→Classify→Export)
```

**Everything green, build 0/0:** Domain.Interfaces 315 · BrowserAutomation 190 · BrowserAutomation.E2E 33
(incl. 2 live sim tests) · Orion.Ingestion 14 · Orion.Worker 15 · Athena.Processing 38 · Athena.Worker 9 ·
Architecture 22 · EndToEnd 27.

### Commits (all on `Kt2`, pushed)
| Commit | What |
|---|---|
| `d841d76` | **1.2** SIARA watch loop + ISiaraDocumentSource discovery port + real SiaraDocumentSource + ITDD + live E2E |
| `6dd92f0` | **1.3** real Ember cross-process transport (Orion publisher hub + Athena consumer client) + tests |
| `910c771` | **1.3 docs** ADR-011 cross-process ingestion transport |

---

## 1.2 — SIARA watch loop (commit `d841d76`)

Two owner-confirmed design calls (via AskUserQuestion): **(a)** a new singleton `SiaraWatchLoop` (NOT a loop
inside the scoped `IngestionOrchestrator` — its placeholder `StartAsync` was removed); **(b)** a dedicated
discovery port `ISiaraDocumentSource` (NOT extending the just-stabilized `IDocumentDownloader`).

- `ISiaraDocumentSource` (Domain) — `DiscoverDocumentIdsAsync` → `Result<IReadOnlyList<string>>`.
- `SiaraDocumentSource` (Infra.BrowserAutomation/Siara) — rides the ADR-010 auth seam; keeps **one warm
  session** across cycles (acquire once → `EnsureValidAsync` → re-acquire only on death; passthrough
  pre-launches the browser once via a `_browserLaunched` guard); hydrate→navigate→list; `IAsyncDisposable`
  releases on scope teardown.
- `SiaraWatchLoop` (Orion.Ingestion, **singleton**) — owns the scopes via `IServiceScopeFactory`: ONE
  long-lived discovery scope (session stays warm) + a **fresh scope per document** →
  `IngestionOrchestrator.IngestDocumentAsync`. Resilient (a failed pass/doc is logged + retried, never
  throws out of `RunAsync` — a throwing BackgroundService stops the host). `WatchLoopOptions.PollInterval`
  default 5 min. Driven by `OrionWorkerService`.
- ITDD: contract + fake + inheritors + 9 mechanics tests; **5 loop tests over a real `ServiceProvider`**
  (real orchestrator + file journal — exercises per-doc scoping + SHA-256 idempotency, no browser); a **live
  simulator E2E** for discovery (force-added; the two SIARA-sim E2E classes now share
  `[Collection("SiaraSimulator")]` to serialize — they manage a sim on the same port 5001).

## 1.3 — real Ember cross-process transport (commits `6dd92f0` + `910c771`)

Owner chose the **larger scope**: wire the first cross-process edge (Downloader→Extractor), not just host a
hub. **ADR-011** records the decision.

- **Orion (publisher):** `IngestionHub : ExxerHub<DocumentDownloadedEvent>` at `/hubs/ingestion`;
  `SignalRIngestionBroadcaster : IExxerHub<DocumentDownloadedEvent>` broadcasts via `IHubContext` on the
  Ember `"ReceiveMessage"` protocol. Replaces `StubExxerHub`. Files under `Prisma.Orion.Worker/Ingestion/`.
- **Athena (consumer):** `IngestionEventForwarder` (Processing lib — republishes received event onto local
  `IEventPublisher`; null-safe, unit-testable) + `SiaraIngestionHubClient` (worker BackgroundService —
  `HubConnection`.On("ReceiveMessage")→forward; retry + auto-reconnect; **idle+logged when
  `Ingestion:HubUrl` is blank**) + `IngestionClientOptions`. Pipeline UNCHANGED.
- Tests: broadcaster unit (IHubContext mock), **real end-to-end SignalR wire test** (TestServer-hosted hub →
  live `HubConnection` receives), forwarder unit over a real `EventPublisher`.

### ⚠️ The one non-obvious gotcha (don't regress it)
`ExxerHub<T>` derives from SignalR `Hub`, whose `Clients` property is **only populated per-invocation by the
runtime**. A hub instance resolved from DI for *outbound* sending has a **null `Clients`** and fails
silently. So broadcasting must go through `IHubContext<THub>`, never a resolved hub. (The Web.UI
`AddScoped<IExxerHub<DomainEvent>, ProcessingHub>()` precedent is broken-but-swallowed for this reason.)

### IndFusion.Ember surface notes
`IExxerHub<T>`/`ExxerHub<T>` are **server-side only**; broadcast method name is `"ReceiveMessage"`. Ember has
**no client wrapper** — the consumer uses a plain `Microsoft.AspNetCore.SignalR.Client` `HubConnection`.
Local source: `E:\Dynamic\IndFusion\IndFusion.Ember\…`; canonical host+client pattern in its
`SignalRIntegrationTests.cs`.

---

## Deferred follow-ups (the real NEXT work) — see ADR-011

1. **Shared document storage (highest-leverage).** The transport delivers the event, but the event carries
   only `FileName`; Athena's `FileSystemLoader` must resolve it against storage **both processes share**.
   Until that volume/path is wired, Athena receives the event and then logs-and-continues when it cannot
   load the file. This is what makes ingestion *actually* feed processing end-to-end on a real deployment.
   - Orion stores at `storage/YYYY/MM/DD/{documentId}.pdf` (see `IngestionOrchestrator.StoreDocumentAsync`);
     the event's `FileName` is just `{documentId}.pdf` (no path). Decide the shared-storage contract (shared
     volume + a base path both workers read, or carry the stored path on the event).
2. **Reconciliator edge (Extractor → Reconciliator).** The third actor of the 3-process split. Same pattern:
   host a typed hub on Athena, subscribe + republish on the Reconciliator. Not built.
3. **Hub auth/authz.** `/hubs/ingestion` is currently an internal, network-bounded endpoint with no auth.
   Harden before any untrusted-network exposure.
4. Other open partials (unchanged from prior handoffs): worker `/dashboard` metrics stubs, auth-abstraction
   wiring, PersonIdentityResolver DB persistence, etc. — see `docs/planning/gap-analysis/`.

---

## HARD CONSTRAINTS (carry forward)
- **Conventions:** xUnit v3 + Shouldly + NSubstitute + Meziantou logger; **NO** Moq/FluentAssertions;
  `TestContext.Current.CancellationToken`; `Result<T>` + `CancellationToken` everywhere, pre-cancel →
  `ResultExtensions.Cancelled<T>()`; ITDD per ADR-005 (port → `*Contract` + fake + ≥1 inheritor).
- **No raw credential storage** ever (reflection-enforced). Honor ADR-010.
- `dotnet test <project.csproj>` with **no extra flags** (breaks MTP). Build single projects for speed.
- Commit code+tests separately from docs; end commits with the `Co-Authored-By: Claude Opus 4.8` line; push
  `Kt2`. Live E2E files must be **force-added** (`git add -f`) — the `*.e2e` gitignore rule matches the
  `.E2E` dir case-insensitively on Windows.

## Verify current state
```
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"                                                                                   # 0/0
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/04 Services/Orion/Prisma.Orion.Ingestion.Tests/ExxerCube.Prisma.Orion.Ingestion.Tests.csproj"  # 14 (incl. 5 watch-loop)
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/04 Services/Orion/Prisma.Orion.Worker.Tests/ExxerCube.Prisma.Orion.Worker.Tests.csproj"        # 15 (incl. SignalR wire)
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ExxerCube.Prisma.Athena.Processing.Tests.csproj"  # 38 (incl. forwarder)
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/*.csproj"                                  # 33 (live; needs sim + Chromium)
```

---

## Short prompt for the next agent

> MVP-PATH Workstream 1 (SIARA ingestion) is **DONE and pushed** on `Kt2`: 1.1 real downloader, 1.2 watch
> loop, 1.3 real IndFusion.Ember cross-process transport (Orion Downloader hub → Athena Extractor). Build
> 0/0, all suites green. Read this handoff + the auto-memory `mvp-path-1.3-ember-transport.md` and
> `mvp-path-1.2-watch-loop.md` first. **The cross-process event is delivered but the file is not yet
> shared:** the highest-leverage next task is wiring **shared document storage** so Athena's `FileSystemLoader`
> resolves the `DocumentDownloadedEvent.FileName` against storage both the Orion and Athena workers share
> (Orion stores at `storage/YYYY/MM/DD/{id}.pdf`; the event carries only `{id}.pdf`). Decide the contract
> (shared volume + base path, or carry the stored path on the event), then prove a downloaded document flows
> Orion→Athena→pipeline end-to-end. After that, the **Reconciliator edge** (Extractor→Reconciliator, same
> hub+subscribe+republish pattern) and **ingestion-hub auth** (see ADR-011) remain. Constraints: ITDD per
> ADR-005; `Result<T>`+`CancellationToken`; xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions);
> `dotnet test <csproj>` no extra flags; broadcast outbound via `IHubContext`, never a DI-resolved Hub
> (null `Clients`); commit code+tests separately from docs and push `Kt2`.
