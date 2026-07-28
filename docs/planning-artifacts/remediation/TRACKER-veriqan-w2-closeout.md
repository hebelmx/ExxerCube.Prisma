# TRACKER — Veriqan W2 close-out epic (O2 + O3 + O4)

**Started:** 2026-07-27 · **Branch:** `Liv` · **Base HEAD:** `d7f5b480`
**Scope source:** `OPEN-BACKLOG-2026-07-25.md` §1 (O2/O3/O4) — the last engineering OPEN items after O1 closed.
**Intended-solution specs:** `VERIQAN-REMEDIATION-EPICS.md` E3-S3 (:993), E3-S4 (:1024), E3-S5 (:1054).

## Ground-truth facts (scouted 2026-07-27, do not re-derive)

- Batch failure today: `BatchProcessor.cs:109` `ConcurrentBag<ExceptionQueueEntry>`; failure adds at
  `:240-252` (Result failure) / `:254-269` (exception); surfaced only in `BatchReport.ExceptionQueue`
  (`:346`) — in-memory only, not even returned by `POST /batch` (`Worker/Program.cs:319-324`).
- `VeriqanDbContext` (`Veriqan.Infrastructure.Persistence/EntityFramework/VeriqanDbContext.cs:15`),
  schema `veriqan`, migrations in `Veriqan.Infrastructure.Persistence/Migrations/`,
  latest `20260630185219_AddImmutabilityTriggers`, design-time factory `Design/VeriqanDbContextFactory.cs`.
- Dead-letter precedent: `ReprocessAuditLogEntity` + `EfReprocessAuditRepository` + config + migration
  `20260630182922_AddDurableStores` (+ immutability interceptor/trigger pattern in `20260630185219`).
- JobVerdict entity (`Veriqan.Domain/Entities/JobVerdict.cs`) lacks EngineVersion/ReferenceBundleVersion;
  persisted by `EfVerdictPersistenceService.cs:63-91`; `VerificationPipeline.cs:58` has
  `const EngineVersion = "1.0.0"` passed to `PersistAsync` (reaches Finding only).
- `--migrate` CLI exists (`Worker/MigrateCommand.cs`, dispatch `Program.cs:28-39`) — the spec's
  `--migrate-only` half of E3-S5 is DONE under this flag name. Remaining O4 = CI wiring only.
- CI: `Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` — NO Veriqan docker build/publish,
  NO migrate step. Athena `--ocr-smoke` step (:174-178) is the in-container smoke precedent.

## Task status

| # | Task | Status | Commit | Verification |
|---|------|--------|--------|--------------|
| 1 | O3 JobVerdict provenance (E3-S4) | PENDING | — | — |
| 2 | O2 BatchExceptionLog dead-letter (E3-S3) | PENDING | — | — |
| 3 | O4 CI migrate wiring (E3-S5 remainder) | PENDING | — | — |
| 4 | Adversarial review + close-out | PENDING | — | — |

## Orchestrator decisions (pre-delegation)

- Serial execution O3 → O2 → O4: both O3 and O2 generate EF migrations against the same ModelSnapshot.
- O4 scope: CI wiring for migrations only (migrate step + `ef migrations bundle` artifact). A full
  docker-publish matrix entry for Veriqan is a separate gap — log as follow-up, do not silently expand.
- O3 open question for the dev: confirm `BundleMetadata.Version` is reachable at the Persist stage;
  if not, make the column nullable and report back before inventing plumbing.

## Log

- 2026-07-27: Epic opened. Scout complete (facts above). No implementation yet.
