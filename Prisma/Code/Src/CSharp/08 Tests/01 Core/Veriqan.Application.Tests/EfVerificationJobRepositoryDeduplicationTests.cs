using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using IndQuestResults;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for the idempotent conflict-retry behaviour added to
/// <see cref="EfVerificationJobRepository.AddAsync"/> in VERIQAN-E2-S16.
/// No Docker / Testcontainers — a fault-injecting EF Core interceptor simulates a
/// unique-constraint <see cref="DbUpdateException"/> so the test runs fully in-process.
/// </summary>
public sealed class EfVerificationJobRepositoryDeduplicationTests
{
    // ---------------------------------------------------------------------------
    // Fault-injecting interceptor — always throws DbUpdateException (used for
    // non-constraint failure scenarios where no pre-seeded row exists).
    // ---------------------------------------------------------------------------

    /// <summary>
    /// An EF Core <see cref="ISaveChangesInterceptor"/> that always throws a
    /// <see cref="DbUpdateException"/>, simulating a non-duplicate failure such as
    /// an FK violation or deadlock (no committed row will match the ContentHash).
    /// </summary>
    private sealed class AlwaysThrowDbUpdateInterceptor : ISaveChangesInterceptor
    {
        public ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException(
                "Simulated non-constraint DB failure (e.g. FK violation).",
                new InvalidOperationException("Foreign key constraint violated."));
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result) => result;
        public void SaveChangesFailed(DbContextErrorEventData eventData) { }
        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => result;
        public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    // ---------------------------------------------------------------------------
    // Fault-injecting interceptor — throws DbUpdateException on the FIRST
    // SaveChangesAsync call, then passes through on subsequent calls.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// An EF Core <see cref="ISaveChangesInterceptor"/> that throws a
    /// <see cref="DbUpdateException"/> the first time <c>SavingChangesAsync</c> is
    /// reached, simulating a unique-constraint violation from the database layer.
    /// Subsequent calls are forwarded to the base implementation.
    /// </summary>
    private sealed class ThrowOnceDbUpdateInterceptor : ISaveChangesInterceptor
    {
        private int _callCount;

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                // Simulate the unique-constraint violation the DB would produce.
                throw new DbUpdateException(
                    "Simulated unique constraint violation on ContentHash.",
                    new InvalidOperationException("Unique index IX_VerificationJobs_ContentHash violated."));
            }

            // Subsequent calls proceed normally.
            return ValueTask.FromResult(result);
        }

        // All remaining interception points delegate to the default pass-through.
        public int SavedChanges(SaveChangesCompletedEventData eventData, int result) => result;
        public void SaveChangesFailed(DbContextErrorEventData eventData) { }
        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => result;
        public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    // ---------------------------------------------------------------------------
    // Helper: build a VeriqanDbContext + EfVerificationJobRepository wired with the
    // fault-injecting interceptor.  The interceptor fires on the first SaveChangesAsync
    // call (the attempted insert); the pre-seeded row is found on the subsequent query.
    // ---------------------------------------------------------------------------

    private static (EfVerificationJobRepository Repo, VeriqanDbContext Context) BuildSutWithInterceptor(
        string dbName,
        ISaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        var context = new VeriqanDbContext(options);
        var logger = XUnitLogger.CreateLogger<EfVerificationJobRepository>();
        var repo = new EfVerificationJobRepository(context, logger);
        return (repo, context);
    }

    /// <summary>
    /// Helper: build a plain InMemory context (no fault injection) for pre-seeding.
    /// </summary>
    private static VeriqanDbContext BuildPlainContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new VeriqanDbContext(options);
    }

    private static VerificationJob MakeJob(string contentHash) =>
        new(
            id: Guid.NewGuid(),
            contentHash: contentHash,
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

    // ---------------------------------------------------------------------------
    // Test: DbUpdateException on insert → existing job re-queried and returned
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When <see cref="EfVerificationJobRepository.AddAsync"/> receives a
    /// <see cref="DbUpdateException"/> during <c>SaveChangesAsync</c> (simulating a
    /// unique-constraint violation on <c>ContentHash</c>), the repository must
    /// re-query the database, find the already-committed row, and return it as
    /// <see cref="Result{T}.WithSuccess"/> — the same GUID that was seeded, not the
    /// duplicate's GUID.
    /// </summary>
    [Fact]
    public async Task AddAsync_DbUpdateExceptionOnInsert_ReturnsExistingJob()
    {
        var ct = TestContext.Current.CancellationToken;

        // Shared EF InMemory database name so both contexts see the same data.
        var dbName = Guid.NewGuid().ToString("N");

        const string sharedHash = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

        // --- Pre-seed: commit the "already existing" job using a plain context. ---
        var existingJob = MakeJob(sharedHash);
        await using (var seedContext = BuildPlainContext(dbName))
        {
            // xUnit1030: test methods must not call ConfigureAwait(false).
            await seedContext.VerificationJobs.AddAsync(existingJob, ct);
            await seedContext.SaveChangesAsync(ct);
        }

        // --- SUT context with fault-injecting interceptor. ---
        // The interceptor will throw DbUpdateException on the first SaveChangesAsync
        // (the duplicate insert attempt).  The repo must then re-query and find
        // existingJob.
        var interceptor = new ThrowOnceDbUpdateInterceptor();
        var (repo, context) = BuildSutWithInterceptor(dbName, interceptor);
        await using (context)
        {
            var duplicateJob = MakeJob(sharedHash); // different GUID, same hash

            var result = await repo.AddAsync(duplicateJob, ct);

            // Must succeed — not a failure.
            result.IsSuccess.ShouldBeTrue(
                "AddAsync must handle DbUpdateException and return the existing job as success.");

            result.Value.ShouldNotBeNull();

            // Must return the pre-seeded job's Id, not the duplicate's new GUID.
            result.Value!.Id.ShouldBe(existingJob.Id,
                "The returned job must be the already-committed row, not the failed-to-insert duplicate.");

            result.Value!.ContentHash.ShouldBe(sharedHash);
        }
    }

    // ---------------------------------------------------------------------------
    // Test: DbUpdateException with NO matching row → honest failure, no false dup claim
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When <see cref="EfVerificationJobRepository.AddAsync"/> receives a
    /// <see cref="DbUpdateException"/> but no row exists for the ContentHash (simulating
    /// a non-duplicate failure such as an FK violation or deadlock), the repository must
    /// return a failure result — not a spurious <see cref="Result{T}.WithSuccess"/> — and
    /// the failure message must not falsely claim a duplicate/constraint violation.
    /// </summary>
    [Fact]
    public async Task AddAsync_DbUpdateExceptionWithNoExistingRow_ReturnsHonestFailure()
    {
        var ct = TestContext.Current.CancellationToken;

        // Unique DB name: no rows are pre-seeded, so the re-query finds nothing.
        var dbName = Guid.NewGuid().ToString("N");

        const string hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Use the always-throw interceptor: no committed row will match the ContentHash.
        var interceptor = new AlwaysThrowDbUpdateInterceptor();
        var (repo, context) = BuildSutWithInterceptor(dbName, interceptor);
        await using (context)
        {
            var job = MakeJob(hash);

            var result = await repo.AddAsync(job, ct);

            // Must be a failure — not a wrong success with null or a phantom row.
            result.IsSuccess.ShouldBeFalse(
                "AddAsync must return failure when DbUpdateException fires but no existing row matches.");

            // The error message must not falsely blame a duplicate/constraint violation.
            result.Error.ShouldNotBeNullOrEmpty();
            result.Error.ShouldNotContain(
                "constraint violation",
                Case.Insensitive,
                "The failure message must not falsely claim a constraint/duplicate violation when no row was found.");
            result.Error.ShouldNotContain(
                "duplicate",
                Case.Insensitive,
                "The failure message must not falsely claim a duplicate when no row was found.");
        }
    }
}
