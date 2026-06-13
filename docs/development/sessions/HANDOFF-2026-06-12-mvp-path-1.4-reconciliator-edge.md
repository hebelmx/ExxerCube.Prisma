# Handoff — MVP-PATH 1.4 Reconciliator edge (3-process split lynchpin) — DONE

**Date:** 2026-06-12 · **Branch:** `Kt2` (pushed) · **DoD:** `MVP-PATH-2026-06-11.md` item 1.4 (A4)
**Design anchor:** `docs/development/sessions/DESIGN-2026-06-12-mvp-path-1.4-reconciliator-edge.md`
**Supersedes the "Reconciliator edge" deferred item in:** `HANDOFF-2026-06-12-mvp-path-1.4-shared-storage.md`

---

## TL;DR

The **3-process security split is complete** (DoD A4). The second cross-process edge —
**Extractor (Athena) → Reconciliator** — is built, so a document now flows across all three actors:

```
Orion Downloader ──/hubs/ingestion (1.3)──▶ Athena Extractor ──/hubs/reconciliation (1.4)──▶ Reconciliator (NEW)
  download + store {id}.pdf                   Quality→OCR→Fusion                                load {id}.fusion.json
  broadcast DocumentDownloaded                save {id}.fusion.json @ shared vol               Classification→Export
                                              broadcast ExtractionCompleted                    emit DocumentProcessingCompleted
```

**Owner decisions (AskUserQuestion):** handoff = **shared-storage reference** (not payload-on-event);
scope = **full new Reconciliator host**.

**Everything green, full solution build 0/0:** Domain.Interfaces 329 · Infrastructure.FileSystem 38 ·
Athena.Processing 53 · Athena.Worker 15 · Architecture 22 · EndToEnd 27.

### Commits (on `Kt2`, pushed)
| Commit | What |
|---|---|
| `3defd09` | (1) Domain handoff contract: `ExtractionCompletedEvent` + `IExpedienteHandoffStore` + ITDD |
| `216c361` | (2) Real `FileSystemExpedienteHandoffStore` + contract inheritor |
| `7117cf1` | (3) Decompose `ProcessingOrchestrator` → `ExtractionOrchestrator` + `ReconciliationOrchestrator` (composes) |
| `e799f78` | (4) Athena Extractor hosts `/hubs/reconciliation` (hub + broadcaster + `ExtractionPipelineService`) |
| `41b675b` | (5) New `Prisma.Reconciliator.Worker` host + forwarder + pipeline + client |
| `d2b3d76` | (6) 3-process E2E (`ThreeProcessPipelineEndToEndTests`) |
| *(this)* | (7) ADR-011 addendum + handoff + memory |

---

## What landed (by layer)

- **Domain.** `ExtractionCompletedEvent : DomainEvent` (FileId, storage-relative `Path`, fusion counts;
  registered on the `[JsonDerivedType]` allow-list). `IExpedienteHandoffStore` port (Save/Load, fail-closed).
- **Infrastructure.** `FileSystemExpedienteHandoffStore` (JSON on the shared volume, traversal guard delegated
  to `IStoragePathResolver`).
- **Processing lib.** `ExtractionOrchestrator` (1–3) + `ReconciliationOrchestrator` (4–5); `ProcessingOrchestrator`
  composes them (monolith preserved). `ExtractionPipelineService` (Extractor driver: extract → save handoff →
  broadcast). `Reconciliation/ReconciliationEventForwarder` + `Reconciliation/ReconciliationPipelineService`
  (Reconciliator driver: load handoff → classify → export → complete).
- **Athena Worker = Extractor.** Hosts `ReconciliationHub` + `SignalRReconciliationBroadcaster` at
  `/hubs/reconciliation`; `AthenaWorkerService` now drives `ExtractionPipelineService` (no longer the monolith).
- **New Reconciliator Worker.** `Prisma.Reconciliator.Worker` (own `Program.cs`): SignalR client of
  `/hubs/reconciliation` (`ReconciliationHubClient` + `ReconciliationClientOptions`), `ReconciliatorWorkerService`
  drives `ReconciliationPipelineService`; classifier wired, exporter optional; minimal health endpoints. Added to `.sln`.
- **ITDD/tests.** `ExpedienteHandoffStoreContract` (fake + real inheritors); broadcaster unit + real SignalR
  wire test (`ReconciliationHubWireTests`); `ExtractionPipelineService`/`ReconciliationPipelineService`/forwarder
  unit tests; the 3-process E2E.

## Deployment notes
- Run **all three** processes. Set `Storage:BasePath` on Extractor + Reconciliator to the same shared volume
  (mount path may differ). Set `Reconciliation:HubUrl` on the Reconciliator to the Athena hub
  (`http://athena:.../hubs/reconciliation`); blank ⇒ idle. Same for `Ingestion:HubUrl` on Athena → Orion (1.3).
- **SIRO export** in the Reconciliator is opt-in: register `AddAdaptiveExportServices(connectionString)` (it is
  template-DB-backed). Without it, the Reconciliator classifies + completes but skips Stage 5.

## Known limitations / deferred (honest — see ADR-011 §Reconciliator edge)
1. **Export wired-but-optional** in the Reconciliator host (DB-backed exporter). Full SIRO export = the separate
   MVP gate item F1/5.2.
2. **3-process E2E** simulates the two edges via their real forwarders; each edge's real SignalR wire is proven
   separately (`IngestionHubWireTests` + `ReconciliationHubWireTests`). A single all-real-wire 3-host E2E is a
   future hardening.
3. Handoff JSON: SmartEnum fidelity is best-effort (converter is a follow-up).
4. `/hubs/reconciliation` has no auth (same as `/hubs/ingestion`) — harden before untrusted-network exposure.
5. Per-stage authz (1.5/A5) + per-process audit (1.6/A6) are the next Workstream-1 items, now unblocked by the split.

## HARD CONSTRAINTS (carry forward)
xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions) · `TestContext.Current.CancellationToken` ·
`Result<T>` + `CancellationToken` · ITDD per ADR-005 · broadcast via `IHubContext` (DI-resolved hub has null
`Clients`) · `dotnet test <csproj>` no extra flags · commit code+tests separately from docs · push `Kt2`.

---

## Short prompt for the next agent (Workstream 1 → 1.5 + 1.6)

> The **3-process security split is DONE** on `Kt2` (Downloader/Orion → Extractor/Athena → new
> `Prisma.Reconciliator.Worker`); MVP-PATH 1.1–1.4 complete, build 0/0, all suites green. Read this handoff +
> the auto-memory `mvp-path-1.4-reconciliator-edge.md` first, then drive the next two Workstream-1 items the
> split just unblocked (`docs/planning/gap-analysis/MVP-PATH-2026-06-11.md`):
>
> - **1.5 — Per-stage authorization + data minimization (A5).** Each of the three processes runs under its own
>   role/clearance; cross-stage handoffs already pass *derived* data (the fused expediente reference, not the raw
>   doc — keep that). Register + use the existing `EfCoreIdentityAdapter` (`04 Services/Auth`, real but wired
>   nowhere) to enforce per-process authorization; a test proves a stage rejects work outside its clearance.
> - **1.6 — Per-process access audit (A6).** Each process logs which process/identity touched which document via
>   `IAuditLogger` (extend `LogAuditAsync` with a process-identity dimension); add audit calls on the
>   Downloader/Extractor/Reconciliator paths (today audit is Web.UI-only); an audit query answers
>   "who/which-process touched doc X". Reuse the mandatory `ISiaraActorIdentityProvider` (ADR-010 S8.3) as the
>   trustworthy actor-identity source where applicable.
>
> **Decision to settle first (checkpoint with the owner):** where each process's role/clearance + identity comes
> from in an unattended deployment (config-bound service identity vs the SIARA actor identity vs a vault). Pick a
> recommended option before coding 1.5.
>
> **Also worth surfacing as candidate follow-ups** (from the 1.4 honest-limitations list — owner-gate before
> doing): wire real SIRO export in the Reconciliator (`AddAdaptiveExportServices(connStr)` + template DB) =
> overlaps the MVP gate F1/5.2; a single all-real-wire 3-host E2E; a SmartEnum JSON converter for full handoff
> fidelity; `/hubs/ingestion` + `/hubs/reconciliation` auth.
>
> **Constraints:** ITDD per ADR-005; `Result<T>`+`CancellationToken`; xUnit v3 + Shouldly + NSubstitute (no
> Moq/FluentAssertions); `TestContext.Current.CancellationToken`; `dotnet test <csproj>` no extra flags;
> broadcast via `IHubContext`; verify every chunk from ground truth (build + the touched test project); commit
> code+tests separately from docs; push `Kt2`. Use the BMAD orchestrator (delegate chunks to isolated subagents,
> verify from ground truth, periodic adversarial-review gate) — bigger quota is available this session.
