# ADR-012 — Per-Process Security Spine (clearance, identity, audit, hub auth, SIRO export)

**Status:** Accepted · **Date:** 2026-06-13 · **Branch:** `Kt2`
**Supersedes/extends:** ADR-011 (cross-process ingestion transport), ADR-010 (SIARA auth — `ISiaraActorIdentityProvider` S8.3)
**Implements:** MVP-PATH-2026-06-11 items **1.5 (A5)** + **1.6 (A6)** + owner-approved follow-ups (hub auth, SmartEnum handoff fidelity, real SIRO-XML export).
**Design anchors:** `DESIGN-2026-06-12-mvp-path-1.5-clearance-identity.md`, `DESIGN-2026-06-12-mvp-path-1.6-process-audit.md`, `DESIGN-2026-06-13-mvp-path-8-siro-export-reconciliator.md`.

## Context

The 3-process security split (ADR-011: Orion **Downloader** → Athena **Extractor** → **Reconciliator**, two Ember/SignalR edges) created the inter-process boundaries that MVP criteria **A5** (per-stage authorization + data minimization) and **A6** (per-process access audit) require. Both were unbuilt. Two existing seams were candidates but mis-fit as-is:

- `EfCoreIdentityAdapter` (`ITokenService`) — a **user**/JWT adapter requiring `UserManager` + the Identity DB; inappropriate to drag into background workers.
- `ISiaraActorIdentityProvider`/`SiaraActor` (ADR-010 S8.3) — a **config-bound, trustworthy service identity**, already wired in Orion. The right identity source for unattended workers.
- `IAuditLogger`/`AuditRecord` — real and used in the Web.UI/Application path, but **registered in none of the three worker hosts**, so worker-path audit was a no-op.

## Decisions

1. **Per-process identity & clearance = config-bound service identity (owner ruling).** Each worker reads its trustworthy actor from `ISiaraActorIdentityProvider`/`SiaraActor` (config `Siara:Actor:*`) and its clearance from `ProcessIdentityOptions.Clearance` (`Download`/`Extract`/`Reconcile`). Not a user-DB principal, not a vault (deferred).

2. **Clearance token modelled on — not literally — `EfCoreIdentityAdapter`.** A new domain port `IProcessClearanceTokenService` → `JwtProcessClearanceTokenService` reuses the *JWT-signing half* (HMAC-SHA256 over a shared `ProcessIdentityOptions.JwtSecret`, ClockSkew 30s, fail-closed validate) with **no Identity DB**. The A5 DoD wording in `MVP-PATH-2026-06-11.md` was updated to match (literal `EfCoreIdentityAdapter` reuse is infeasible in workers).

3. **Two layers of enforcement.** (a) **Per-message** (1.5c): the broadcaster mints + stamps a clearance token (bound to the document `file_id`) on each handoff event; the receiving forwarder validates signature + expected sender clearance + `file_id` and **rejects out-of-clearance work fail-closed** (logs Warning, never publishes, never throws). (b) **Per-connection** (hub auth): both `/hubs/ingestion` and `/hubs/reconciliation` carry `[Authorize]` policies requiring the expected downstream clearance claim (`Extract`, `Reconcile`); clients present a connection-scope token (`file_id = Guid.Empty`) via the SignalR `access_token`. The same `JwtSecret` signs both.

4. **Data minimization preserved.** Handoffs continue to carry only the derived expediente reference (storage path), never the raw document; the token adds only identity claims.

5. **Per-process audit = direct shared-audit-DB (owner ruling A) + first-class `ProcessId` column (owner ruling B).** Each worker registers `AddDatabaseServices` (blank/placeholder connection ⇒ skip + Warning, workers stay bootable). `AuditRecord.ProcessId` (nullable, indexed) + an optional trailing `IAuditLogger.LogAuditAsync(..., processId)` parameter (placed after `cancellationToken` deliberately so the ~8 existing positional-`ct` callers stay source-compatible). The three pipeline paths emit audit (`userId` + `processId` = `SiaraActor.ActorId`, `actionDetails` = process-identity JSON) on happy + failure branches, **fail-open** (audit failure never breaks the pipeline; logged at Warning). Identity from `ISiaraActorIdentityProvider`.

6. **Audit is an immutable compliance trail — the `AuditRecords.FileId` FK to `FileMetadata` was dropped** (owner-approved migration `DropAuditFileMetadataFk`). Worker audit records (which carry the document GUID before any `FileMetadata` row exists) now persist and are queryable by `FileId` instead of being silently dropped on FK violation; audit survives `FileMetadata` deletion.

7. **SIRO export is produced by `SiroXmlExporter` via `IResponseExporter`, not the template-DB `AdaptiveExporter`.** The Reconciliator's Stage 5 emits `<SiroResponse>` XML. `AddExportServices` was split into `AddSiroExportServices` (minimal, DB-free SIRO path; lifetime-parameterized) + `AddPdfRequirementSummarizer` to avoid pulling `IMetadataExtractor` into the workers. The Extractor (Athena) registers **no** exporter (it does not export). A Reconciliator host-DI test (`ValidateOnBuild`) guards that the export graph actually resolves.

8. **Handoff JSON SmartEnum fidelity** is fixed by `EnumModelJsonConverterFactory` (serialize by `Name`, deserialize via `FromName` to the singleton) on the handoff store's `JsonSerializerOptions`.

## Consequences

- A5 and A6 are met and adversarially verified; the two SignalR edges are authenticated; SIRO XML is produced end-to-end and proven by an all-real-wire 3-host E2E.
- All three workers now depend on a shared `ProcessIdentity:JwtSecret` (manage as a bearer secret) and a `ConnectionStrings:DefaultConnection` for audit; the Reconciliator additionally needs an export connection only for the template-DB path (the SIRO path itself is DB-free).
- **Accepted limitations (logged, not hidden):** `jti` is minted but not enforced → same-document within-lifetime replay is not blocked (idempotent reprocess; mitigated by short lifetime + `file_id`); connection-scope tokens (`file_id=Guid.Empty`) would pass the per-message `file_id` check only if the event's FileId were also `Guid.Empty` (never true for real documents); SIRO XSD schema validation hook (`XmlSchemaSet`) is dormant pending the Banamex XSD; the all-real-wire E2E uses in-memory TestServer transport (real SignalR protocol + auth + pipeline, not TCP). Symmetric HMAC means any process holding the shared secret can mint any clearance — clearance separation is honesty-by-configuration within a single trust boundary (asymmetric per-process keys are a future hardening).

## Verification

Build 0/0. Green (verified from ground truth incl. Docker/Testcontainers): Tests.Domain.Interfaces 339 · Tests.Infrastructure.BrowserAutomation 199 · Athena.Processing 70 · Orion/Athena Worker 24 each · Reconciliator.Worker 3 (host-DI) · Tests.System.Storage 43 (A6, by FileId) · Tests.Infrastructure.FileSystem 54 · Tests.AllRealWireE2E 1. One **pre-existing** unrelated failure remains: `EfCoreRepositoryIntegrationTests.FileMetadataRepository_RemoveAsync_ShouldDeleteEntity` (reproduced on clean HEAD; out of scope — flagged for a separate fix).
