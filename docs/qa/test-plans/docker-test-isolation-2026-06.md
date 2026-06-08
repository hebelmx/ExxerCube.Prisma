# Docker Integration-Test Isolation & Parallelization (2026-06-07)

## Problem
The SQL Server integration tests are **slow** because they are forced to run **serially**.
All four DB test classes (`EventPersistenceWorkerIntegrationTests`,
`DocumentProcessingPipelineIntegrationTests`, `Mission1HappyPathPipelineTests`,
`DatabaseInfrastructureSmokeTests`) share **one container *and* one database** (`PrismaTestDb`)
and each calls `CleanDatabaseAsync()` (a global table-wipe) in setup/teardown. To stop one
class's wipe from corrupting another's data, the collection is declared
`[CollectionDefinition("DatabaseInfrastructure", DisableParallelization = true)]` — so every
DB test runs one-at-a-time.

The container start itself is also expensive (SQL ~10–30s; **Ollama is worse because it
downloads the model on first run** — the observed "very slow to start").

## Goal (two kinds of test)
1. **Share one complete Docker instance** across many tests that *don't collide*, and let them
   run **in parallel**.
2. **Isolate** the tests that *do* collide (writers), without paying for a fresh container each.

## Design

### Lever A — shared container, isolated databases (the key idea)
Keep **one** SQL container for the whole assembly (started once). Give **each writer test class
its own database** on that container — a cheap `CREATE DATABASE`. Writers then never collide,
so they can run in parallel and `DisableParallelization` / `CleanDatabaseAsync` are no longer
needed.

✅ **Implemented (compile-verified):** `SqlServerContainerFixture.CreateIsolatedDatabaseAsync(name)`
creates `PrismaTest_{ClassName}` on the shared container and returns its connection string;
all isolated DBs are dropped on fixture disposal (`DropIsolatedDatabasesAsync`).

### Lever B — share the container across *parallel* collections
xUnit parallelizes across **collections**, not classes within a collection. `ICollectionFixture<T>`
creates **one fixture instance per collection** — so N collections ⇒ N containers (defeats the
purpose). The correct xUnit v3 tool is an **assembly fixture**
(`[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]`): **one** container instance for
the whole test assembly, injectable into any class, while classes/collections run in parallel.

⏳ **Not yet applied** — this introduces an xUnit v3 pattern not currently used anywhere in the
repo, and the DB suite **cannot be runtime-verified here** (Docker daemon is down). Applying it
blind risks a green-build / red-runtime break.

### Target architecture
```
[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]   // one container, whole assembly

// Writer class — isolated DB, parallel-safe, NO [Collection], NO DisableParallelization:
public class EventPersistenceWorkerIntegrationTests
{
    public EventPersistenceWorkerIntegrationTests(SqlServerContainerFixture fx, ITestOutputHelper o)
    {
        var conn = fx.CreateIsolatedDatabaseAsync(nameof(EventPersistenceWorkerIntegrationTests))
                     .GetAwaiter().GetResult();
        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>().UseSqlServer(conn).Options;
        using var ctx = new PrismaDbContext(_dbOptions);
        ctx.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        // NO CleanDatabaseAsync() — the database is already private to this class
    }
}
```
Two "kinds" then fall out naturally:
- **Parallel/shared:** every class that uses an isolated DB (or only reads shared seed data).
- **Serial/isolated:** keep a small `[Collection("DatabaseInfrastructureSerial", DisableParallelization = true)]`
  for any test that genuinely needs the one canonical `PrismaTestDb` (none today).

Ollama: keep a single shared `OllamaContainerFixture` (read-only model inference — no write
collisions), shared via the same assembly-fixture mechanism; it already needs no isolation.

## Migration recipe (per writer class)
1. Remove `[Collection("DatabaseInfrastructure")]`.
2. In the ctor, replace `_fixture.ConnectionString` with
   `_fixture.CreateIsolatedDatabaseAsync(nameof(ThisClass)).GetAwaiter().GetResult()`.
3. `EnsureCreatedAsync()` on the isolated DB.
4. Delete the `CleanDatabaseAsync()` call (no longer needed).
5. Once all classes are migrated, delete the `DisableParallelization` collection (or repurpose
   it as the serial fallback).
6. Add `[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]` (and Ollama) to the
   Storage test project.

## Verification (REQUIRED before declaring done)
- Build: 0/0 (enabler already confirmed).
- With Docker up: run `Tests.System.Storage` and confirm (a) one container starts, (b) classes
  run in parallel, (c) all green, (d) wall-clock drops materially vs the serial baseline.

## Status — ✅ DONE & VERIFIED WITH DOCKER (2026-06-07)
- ✅ `SqlServerContainerFixture.CreateIsolatedDatabaseAsync` + `DropIsolatedDatabasesAsync` disposal.
- ✅ `[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]` (Storage test project) — one
  container for the whole assembly.
- ✅ Migrated the 3 writer classes (`EventPersistenceWorkerIntegrationTests`,
  `DocumentProcessingPipelineIntegrationTests`, `Mission1HappyPathPipelineTests`) to isolated
  databases; removed `[Collection]` and `CleanDatabaseAsync()`.
- ✅ `DatabaseInfrastructureSmokeTests` dropped `[Collection]`, now the sole user of the canonical
  `PrismaTestDb` via the shared assembly fixture (it intentionally exercises the shared-DB ops).
- ✅ Deleted the obsolete `DatabaseInfrastructureCollection` (the `DisableParallelization` serial gate).
- ✅ **Verified with Docker:** all 4 DB classes run in **one process on one shared container**,
  **31/31 green in ~1m10s** (incl. container start), pipeline classes parallel on isolated DBs.
  Solution build 0/0.

**Two kinds, realized:**
- *Shared-instance, parallel:* the 3 writer pipeline classes (isolated DB each).
- *Shared canonical DB:* `DatabaseInfrastructureSmokeTests` (sole user → no collision).

Ollama: left as-is (single shared `OllamaInfrastructureCollection`; read-only inference, no write
collisions). Its first-run slowness is the **model download** (no `ollama` volume cached), not a
sharing issue — orthogonal to this work.
