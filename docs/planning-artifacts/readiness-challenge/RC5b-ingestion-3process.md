# RC5b — Ingestion + 3-Process Security Spine (Track B, MVP headline blocker)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Mode:** read-only trace; believe wiring + file:line, never comments/prose.
**Scope:** the MVP headline blocker per MVP-PATH-2026-06-11 §Workstream-1 — ingestion + the security-mandated
3-process split (A1–A6: Downloader / Extractor / Reconciliator). Security posture (CNBV/ISO/SOC) = **FLAG-ONLY**
(owner decided a separate deep audit) — flagged below as *defer-to-security-review*.

> **Headline reversal vs the 2026-06-07/06-11 GAP-MATRIX baseline.** The prior matrices (and CLAUDE.md's
> "Planned / Stub" classification) said Orion wires `StubDocumentDownloader` + `StubExxerHub`, and
> `IngestionOrchestrator.StartAsync` is a `Task.CompletedTask` placeholder. **All three of those are now FALSE
> against current code.** Workstream-1 (1.1–1.6 + follow-ups) was built on `Kt2` and is on `Liv` today. The
> stubs still exist in the tree but are **not registered**; the placeholder method was **removed**. This RC5b
> supersedes the GAP-MATRIX ingestion rows for current state.

---

## Verdicts (the questions asked)

### Downloader: real-vs-stub wired verdict
**REAL is wired.** `Orion.Worker/Program.cs:64` registers
`AddScoped<IDocumentDownloader, SiaraDocumentDownloader>()` — the real browser-automation scraper behind the
credential-free SIARA auth seam (ADR-010). `StubDocumentDownloader` still exists at
`Prisma.Orion.Ingestion/StubDocumentDownloader.cs` **but is registered nowhere** (every grep hit is a comment,
doc, test, or arch-test reference — never a `services.Add…`). It also no longer returns empty bytes — it
**fails closed** (`StubDocumentDownloader.cs:36-39`: returns a failure Result instructing to register
`SiaraDocumentDownloader`). So even if mis-wired it would not silently pass empty content.

### Is the 3-process split actually 3 coordinated processes? — **YES.**
Three separate hosted executables, each its own `Program.cs` + `.csproj`, coordinated over two real SignalR
(IndFusion.Ember) edges + a shared-storage filesystem handoff:
- **Downloader (Orion)** — `Prisma.Orion.Worker` hosts `/hubs/ingestion`
  (`Orion.Worker/Program.cs:222`); broadcasts `DocumentDownloadedEvent` via `SignalRIngestionBroadcaster`
  (`:137`). Drives `SiaraWatchLoop` → `IngestionOrchestrator.IngestCaseAsync`.
- **Extractor (Athena)** — `Prisma.Athena.Worker` is a SignalR **client** of Orion's hub
  (`SiaraIngestionHubClient`, `Athena.Worker/Program.cs:70`) AND **hosts** `/hubs/reconciliation` (`:238`);
  runs Quality→OCR→Fusion, writes `{id}.fusion.json` to shared storage, broadcasts `ExtractionCompletedEvent`
  (`SignalRReconciliationBroadcaster`, `:198`).
- **Reconciliator** — `Prisma.Reconciliator.Worker` is a SignalR **client** of Athena's reconciliation hub
  (`ReconciliationHubClient`, `Reconciliator.Worker/Program.cs:52`); loads the handoff, runs
  Classification→Export (SIRO XML + Datos-Carga xlsx).

The seam exchanges Ember events + a shared-volume artifact (not shared raw DB access) — matches A4 intent.

### Ember actually used? — **YES (referenced AND used).**
`IndFusion.Ember` is a real `PackageReference` in 3 production projects (`Orion.Ingestion`, `Athena.Processing`,
`Web.UI` csproj) and consumed: both worker hubs derive from Ember's `ExxerHub<T>` and both broadcasters
implement `IExxerHub<T>` and send on Ember's `"ReceiveMessage"` protocol. The known Ember gotcha (a DI-resolved
`Hub` has null `Clients`) is respected — broadcast goes via `IHubContext`, never a resolved hub. Not
referenced-but-unused.

### IngestionOrchestrator placeholder status — **REMOVED / not applicable.**
The `Task.CompletedTask` `StartAsync` placeholder (formerly `IngestionOrchestrator.cs:268-273`) is **gone**.
The real poll/watch loop lives in `SiaraWatchLoop.RunAsync` (`SiaraWatchLoop.cs:123`) — a singleton
BackgroundService-driven loop that discovers cases, creates a fresh DI scope per case, calls
`IngestionOrchestrator.IngestCaseAsync`, dedups via SHA-256 journal, and is resilient (never throws out of the
loop). `IngestionOrchestrator` is now a host-agnostic logic class (case ingestion, ROP, audit, broadcast).

---

## A1–A6 / component matrix

| Item | Intent | Built-state class | Evidence (file:line) | Gap to E2E-readiness | Dependency |
|------|--------|-------------------|----------------------|----------------------|------------|
| **A1** Real SIARA downloader → `IDocumentDownloader` | Pull real docs from SIARA (not stub) | **Real + Wired + E2E** | `Orion.Worker/Program.cs:64` (real registered); `SiaraDocumentDownloader.cs`; live Playwright pull in `MaxFidelityGateE2EBase` (`DiscoverFullCompanionCaseAsync`/real download). Stub fails-closed (`StubDocumentDownloader.cs:36-39`), registered nowhere. | Auth seam is **session-passthrough** vs production SIARA TLS/login (sim has self-signed cert + `IgnoreHttpsErrors`). Real SIARA portal behavior unverified (no prod portal access). | none |
| **A2** Real poll/watch ingestion loop | Discover new docs, ingest per-doc, dedup, resilient | **Real + Wired + E2E** | `SiaraWatchLoop.cs:123-179` (loop, per-case scope, resilient); placeholder removed; `Orion.Worker/Program.cs:201-202` (singleton + hosted). E2E exercises discovery→ingest in `MaxFidelityGate`. | Cadence/back-pressure under real ≤2k/day volume untested; one-warm-session longevity across long runs unproven. | A1 |
| **A3** Real Ember transport in workers | Replace `StubExxerHub`; worker-published event reaches subscriber | **Real + Wired + E2E** | `Orion.Worker/Program.cs:135-137`; `Athena.Worker/Program.cs:196-198`; cross-process receive proven in `AllRealWireThreeHostE2ETests` + `MaxFidelityGate`. `StubExxerHub` registered nowhere. | Transport proven over **in-memory TestServer handler**, never real TCP/WebSocket between separate processes on a network. (ADR-012 limitation #4.) | none |
| **A4** Split into 3 hosted processes | 3 separately-hosted Ember-coordinated actors; doc crosses all three | **Real + Wired + E2E (in-memory wire)** | 3 distinct `*.Worker.csproj` + `Program.cs`; 2 hubs + shared-storage handoff; `AllRealWireThreeHostE2ETests` (3 WAF hosts, both wires) + `MaxFidelityGateFullPipelineE2ETests` (real pipeline across all 3). | Same in-memory-transport caveat as A3; never deployed/proven as 3 OS processes over a network. No deploy artifacts (compose/k8s) for the 3-host topology. | A3 |
| **A5** Per-stage authz + data minimization | Each process under own clearance; reject out-of-clearance work; pass references not full content | **Real + Wired + E2E** | JWT clearance: `AddProcessIdentity` in all 3 workers; connection-level bearer policies (`Orion:121-124` RequireExtract, `Athena:172-175` RequireReconcile); per-message validation in forwarders + replay guard (`InMemoryClearanceReplayGuard`); handoff passes a storage-relative path artifact, not raw content. E2E refuses wrong-clearance/no-token connects. | **Symmetric HMAC, one shared secret** — clearance separation is "honesty-by-configuration within one trust boundary," NOT cryptographic per-process isolation (ADR-012 #5). `jti` minted but **not enforced** → within-lifetime replay not blocked (#1). `file_id=Guid.Empty` connection-scope edge (#2). *defer-to-security-review* | A4 |
| **A6** Per-process access audit | Each process logs which identity/process touched which doc; queryable | **Real + Wired + E2E** | `AuditRecord.ProcessId` column + EF migration; `LogAuditAsync(…, processId)`; audit call sites in all 3 paths (`IngestionOrchestrator.EmitAuditAsync`, Extraction/Reconciliation pipeline services); `MaxFidelityGate` asserts ≥2 distinct `ProcessId` rows in **real SQL** (Testcontainers). | Audit is **fail-open** (any audit failure swallowed so pipeline never blocks) — acceptable for availability, but a regulator wants audit-write guarantees / immutability proof. Audit→FileMetadata FK was dropped for immutability (by design). *defer-to-security-review* (immutability, retention, tamper-evidence) | A4 |
| **DocumentIngestionService** (UI scraping path) | Adapt the demoed UI scraper into the headless port | **Superseded / not the path** | The headless port is served by `SiaraDocumentDownloader` + `SiaraDocumentSource` (discovery) behind the ADR-010 seam, not by adapting `Application/Services/DocumentIngestionService`. The headless ingestion path is independent and real. | n/a — the headless path is built; the UI service is a separate (older) surface. | — |

---

## Per-class tally (A1–A6 + key components)

| Class | Count | Items |
|-------|-------|-------|
| **Real + Wired + E2E** | 6 | A1, A2, A3, A4, A5, A6 (all proven by `MaxFidelityGate`/`AllRealWire` E2E) |
| Real-unwired | 0 | — |
| Partial | 0 | — |
| Stub (present, not wired) | 2 | `StubDocumentDownloader`, `StubExxerHub` (dead — registered nowhere, fail-closed/log-only) |
| Missing | 0 | — |
| Unknown | 0 | — |

> Nuance on "E2E": A3/A4 carry an honest qualifier — the cross-process SignalR wire is exercised over the
> ASP.NET in-memory **TestServer handler**, not real TCP between OS processes on a network. Production hub +
> auth + pipeline code all execute; only the transport substrate differs. This is the single biggest "is it
> *really* E2E" caveat and is logged in ADR-012 #4.

---

## Biggest readiness gaps (≤6, ordered)

1. **No real-network 3-process transport proof.** Every 3-host E2E (`AllRealWireThreeHostE2ETests`,
   `MaxFidelityGate*`) routes both SignalR edges through an **in-memory TestServer handler**, never real
   TCP/WebSocket between separate OS processes. Reconnect/auth/back-pressure over a real network is unverified.
   **Blocks-production** (it is *the* claim the split is meant to deliver).
2. **No deploy artifacts for the 3-host topology.** No compose/k8s/host manifests wiring 3 processes + shared
   volume + the 2 hub URLs + a shared JWT secret as separate deployables; config is supplied only by test
   harnesses. Plus the known hardcoded `Server=DESKTOP-FB2ES22\SQL2022` class in app config. **Blocks-production.**
3. **Clearance is symmetric-HMAC honesty-by-config, not crypto isolation.** One shared `JwtSecret` signs every
   process's tokens; any process holding the secret can mint any clearance. `jti` replay not enforced.
   Real per-process key isolation (asymmetric) is future hardening. **Degrades / Blocks for a regulated gate.**
   *defer-to-security-review.*
4. **SIARA auth/portal fidelity vs production.** The whole chain is proven against the local
   `tools/Siara.Simulator` (self-signed TLS, `IgnoreHttpsErrors=true`). Real SIARA login flow, session
   longevity, rate limits, and document-link shape are unverified against the live portal. **Degrades** (could
   block A1/A2 on first prod contact). *No SIARA API exists — scraping is the deliberate approach.*
5. **Hub endpoints are internal/network-bounded with JWT only.** `/hubs/ingestion` + `/hubs/reconciliation`
   carry connection-level clearance policies but assume a trusted internal network (no mTLS, no transport
   encryption asserted). **Degrades.** *defer-to-security-review.*
6. **Audit is fail-open; immutability/retention unproven.** Audit failures are swallowed to protect pipeline
   availability; the FK to FileMetadata was dropped for immutability but tamper-evidence, 7-yr retention, and
   write-guarantee are not demonstrated. **Degrades** for CNBV. *defer-to-security-review.*

---

## Bottom line

Workstream-1 (ingestion + the A1–A6 security spine) is **genuinely built and wired** — a complete reversal of
the GAP-MATRIX "Planned/Stub" baseline. A real SIARA case provably traverses all three real processes with real
OCR / fusion / SIRO export / SQL audit (`MaxFidelityGateFullPipelineE2ETests`, owner's MVP gate). The remaining
ingestion gaps are **operational, not algorithmic**: prove the split over a **real network** (not in-memory),
produce **3-host deploy artifacts + externalized config**, and harden the clearance/audit/transport security
posture (deferred to a dedicated security review). The single biggest MVP-blocking ingestion gap is **#1 — the
3-process split has never run as 3 real OS processes over a real wire.**
