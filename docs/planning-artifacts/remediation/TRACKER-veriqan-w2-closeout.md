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
| 1 | O3 JobVerdict provenance (E3-S4) | DONE | `5f0b9794` | build 0/0; Orchestration.Tests 265/265; Persistence.IntegrationTests 33/33 (Testcontainers); migration `20260727233432_AddJobVerdictProvenance` |
| 2 | O2 BatchExceptionLog dead-letter (E3-S3) | DONE | `368ec43c` | build 0/0 (Worker); Orchestration.Tests 271/271 (+6); Persistence.IntegrationTests 37/37 (+4, Testcontainers second-DbContext durability proof); migration `20260728155959_AddBatchExceptionLog` |
| 3 | O4 CI migrate wiring (E3-S5 remainder) | DONE | — | `veriqan-migrations` CI job (bundle + from-zero verify); every YAML command executed locally: dotnet-ef 10.0.8 restored, bundle built, all 10 migrations applied to fresh SQL 2022 container (exit 0) + idempotent re-run; runbook §8 extended |
| 4 | Adversarial review + close-out | PENDING | — | — |

## Orchestrator decisions (pre-delegation)

- Serial execution O3 → O2 → O4: both O3 and O2 generate EF migrations against the same ModelSnapshot.
- O4 scope: CI wiring for migrations only (migrate step + `ef migrations bundle` artifact). A full
  docker-publish matrix entry for Veriqan is a separate gap — log as follow-up, do not silently expand.
- O3 open question for the dev: confirm `BundleMetadata.Version` is reachable at the Persist stage;
  if not, make the column nullable and report back before inventing plumbing.

## Log

- 2026-07-27: Epic opened. Scout complete (facts above). No implementation yet.
- 2026-07-27: O3 DONE (`5f0b9794`). Fork resolved by orchestrator: spec's `BundleMetadata.Version`
  doesn't exist; dev used `SchemaVersion` (a constant — zero discriminating value); orchestrator
  changed the stamp to `BundleId ?? SchemaVersion` (helper `BundleProvenanceVersion`, 50-char cap).
  `ReferenceBundleVersion` nullable — catalog pre-resolve degrades gracefully to null bundle.
  EngineVersion = Orchestration assembly version (spec said Infrastructure assembly; the stamping
  pipeline lives in Orchestration — deviation accepted, flagged for adversarial review).
  Note: `docs/qa/calibration/*.md` regenerate as a Veriqan test side effect — revert before commit.
- 2026-07-28: O2 DONE. Scaffold (entry record, port, in-memory + EF entity/config) completed with:
  `EfBatchExceptionLogRepository` (scope-factory pattern, mirrors `EfReprocessAuditRepository`,
  same Orchestration/Repositories placement precedent), DbSet + migration `AddBatchExceptionLog`
  (composite index BatchId+FailedAt; deliberately NO immutability trigger — triage columns mutable),
  per-run `BatchId` stamped on `BatchReport` + `POST /batch` response, `WriteDeadLetterAsync`
  never-abort guard at both failure sites (reason truncated to 2000), `GET /exceptions?batchId=`
  (auth-required; 400 bad guid; 499 cancelled), DI in both EF and in-memory branches.
  Verified from ground truth by orchestrator: build 0/0, both suites green, diff reviewed.
- 2026-07-28: O4 DONE. `veriqan-migrations` job in quality-gates.yml: tool-manifest-pinned
  dotnet-ef 10.0.8 (matches EF Core 10.0.8), `ef migrations bundle --self-contained -r linux-x64`
  (no --startup-project needed — VeriqanDbContextFactory is IDesignTimeDbContextFactory),
  upload-artifact, from-zero apply against ephemeral SQL 2022 + lineage assert (>=1, non-stale)
  + always() teardown. All commands proven locally (CI-dormancy caveat below). Runbook §8 got the
  bundle/DBA path subsection. `.gitignore` excludes `/veriqan-efbundle`.
- 2026-07-28: **FINDING (epic-external, surface to owner):** there is NO repo-root
  `.github/workflows/` — `quality-gates.yml` lives only under `Prisma/Code/Src/CSharp/.github/`,
  where GitHub Actions never executes it. ALL CI (including O1's --ocr-smoke step and the
  E2-S8 publish matrix) is dormant. Activating it = move to repo root + fix stale paths
  (root-less `dotnet restore`, e2e job paths) — several jobs would go red immediately.
  Deliberately NOT fixed inside this epic (scope + risk); needs its own decision/pass.
