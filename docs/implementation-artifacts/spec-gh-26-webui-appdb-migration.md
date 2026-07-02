---
title: 'GH#26 — Web.UI app DB never migrated: OutboxEvents missing → OutboxRetryWorker SQL error-loop'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'b8417136'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Seq shows a continuous SQL error-loop from `OutboxRetryWorker`
(`Invalid object name 'OutboxEvents'`). Root cause: the Web.UI startup migrates only
the **Identity** DB (`MigratePrismaIdentityAsync`). The application DB (`ApplicationConnection`
→ `Prisma`) is only ever touched by `TemplateSeeder.SeedAllTemplatesAsync`, which calls
`TemplateDbContext.EnsureCreatedAsync()`. `EnsureCreated` creates the database with **only**
the `Templates`/`FieldMappings` tables and **no `__EFMigrationsHistory`** — so none of the
`PrismaDbContext` tables (`OutboxEvents`, audit ledger, review cases, SLA, unified metadata…)
are ever created. `OutboxRetryWorker` (a hosted service registered via `AddDatabaseServices`)
then polls `OutboxEvents` on a timer and errors forever.

**Approach:** After template seeding, run `PrismaDbContext` EF Core migrations at startup via
the existing tested `PrismaDbMigrationRunner.RunMigrationsAsync(app)`. `PrismaDbContext` and
`TemplateDbContext` own **disjoint** tables against the same physical DB, so on a fresh DB the
sequence `EnsureCreated (Template tables) → MigrateAsync (Prisma tables)` produces both sets
with no conflict; on the already-broken container DB (Templates only, no history) the migration
runner applies all Prisma migrations and **heals it in place** — no manual DB drop required.

## Boundaries & Constraints

**Always:** Run the Prisma migration **after** `SeedTemplatesAsync` (EnsureCreated must create
the DB + Template tables first; if Migrate ran first on a fresh DB, EnsureCreated would see the
DB already exists, no-op, and the Templates tables would be missing). Run it **before**
`app.Run()` so the schema exists before hosted services (`OutboxRetryWorker`) start their first
poll. Reuse `PrismaDbMigrationRunner` (already covered by tests) rather than a new MigrateAsync
call. Keep the block fail-open (log, do not throw) — consistent with the Identity/template blocks.

**Ask First:** N/A — matches the existing startup-migration pattern already used for Identity.

**Never:** Do NOT drop `TemplateSeeder.EnsureCreatedAsync` — `TemplateDbContext` has no
migrations, so EnsureCreated is the only thing that creates the `Templates`/`FieldMappings`
tables; removing it would break export-template seeding. Do NOT fold `Templates` into
`PrismaDbContext` (out of scope). Do NOT call the runner before `SeedTemplatesAsync`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh DB (first boot) | `Prisma` DB absent | EnsureCreated → Templates tables; Migrate → all Prisma tables incl `OutboxEvents`; both present | fail-open log |
| Already-broken DB | `Prisma` exists: Templates only, no `__EFMigrationsHistory` | Migrate applies all migrations → `OutboxEvents` created; error-loop stops next boot | fail-open log |
| Healthy DB (re-boot) | migrations already applied | Migrate is a no-op (history current) | fail-open log |
| DB unreachable | bad/absent connection | Runner returns 1; startup continues; logged | fail-open log |

</frozen-after-approval>

## Code Map

- `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs:205-214` — template-seeding block; add the Prisma-migration block immediately after it (before `app.Run()`).
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Database/Startup/PrismaDbMigrationRunner.cs` — reused as-is; `RunMigrationsAsync(IHost)` migrates `PrismaDbContext`.
- `TemplateSeeder.cs:43` / `AddDatabaseServices` (OutboxRetryWorker reg :282) — context only; unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `Program.cs` — after the `SeedTemplatesAsync` try/catch, add a fail-open block that calls
  `await ExxerCube.Prisma.Infrastructure.Database.Startup.PrismaDbMigrationRunner.RunMigrationsAsync(app)`
  and logs a warning when it returns non-zero.

**Verified 2026-07-02 (in-place heal on the running container):**
- Before: `Prisma` DB had only `FieldMapping` + `Templates`, no `__EFMigrationsHistory`.
- After rebuild + recreate: log shows `PrismaDbContext migrations applied successfully`; DB now has
  `OutboxEvents`, `AuditRecords`, `FileMetadata`, `Persona`, `RequirementTypeDictionary`, `ReviewCases`,
  `ReviewDecisions`, `SLAStatus` + `__EFMigrationsHistory` (8 migrations) + the disjoint `Templates`/`FieldMapping`.
- `Invalid object name 'OutboxEvents'` count in logs: **0**. `GET /` → 200, `/health` → Healthy (prisma-db reachable).
- Build: 0 warnings / 0 errors.

**Known-adjacent (NOT this issue — filed separately):** migration
`20260613200000_AddUnifiedMetadataRecords` has no `.Designer.cs` → no `[Migration]` attribute → excluded
from the migrations assembly → `UnifiedMetadataRecords` table not created. On-demand path (ManualReviewer/
DecisionLogic), not a background poller, so no error-loop. Tracked as its own GitHub issue + deferred-work.

**Acceptance Criteria:**
- Given the rebuilt Web.UI on a fresh `Prisma` DB, when it starts, then `OutboxEvents` and the
  other `PrismaDbContext` tables exist and the export `Templates` table also exists.
- Given the previously-broken container DB, when the updated image boots, then no new
  `Invalid object name 'OutboxEvents'` errors appear in Seq.
- Given `dotnet build` of the Web.UI project, then it succeeds (warnings-as-errors).

## Verification

**Commands:**
- `dotnet build "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj"` — expected 0/0.
- Recreate web-ui; `docker exec prisma-sqlserver … "SELECT name FROM sys.tables"` in `Prisma` DB shows `OutboxEvents` + `Templates`.
- Tail Seq / web-ui logs after boot — no `OutboxEvents` SQL errors.
