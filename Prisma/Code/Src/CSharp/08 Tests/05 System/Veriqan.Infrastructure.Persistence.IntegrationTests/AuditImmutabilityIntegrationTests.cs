// Story 6.2 — DB-enforced audit immutability.
// Verifies defense-in-depth: DB triggers block raw SQL mutations; EF interceptor blocks
// EF SaveChanges mutations.  VerificationOutcomeSnapshots must remain mutable.

using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using ExxerCube.Prisma.Veriqan.Orchestration.Repositories;
using ExxerCube.Prisma.Veriqan.Orchestration.Stores;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for Story 6.2 — DB-enforced audit immutability.
/// All tests run against an isolated SQL Server Testcontainer database that has EF migrations
/// applied (including the <c>AddImmutabilityTriggers</c> migration).
/// </summary>
/// <remarks>
/// Two defence layers are tested:
/// <list type="bullet">
///   <item><b>DB trigger</b> — raw SQL UPDATE/DELETE bypasses the EF interceptor and hits the trigger
///   directly; the test asserts a <see cref="SqlException"/> is raised.</item>
///   <item><b>EF interceptor</b> (<see cref="ImmutableEntityInterceptor"/>) — changing an entity's
///   tracked state to <see cref="EntityState.Modified"/> or <see cref="EntityState.Deleted"/> and
///   calling <c>SaveChangesAsync</c> throws <see cref="InvalidOperationException"/> before any SQL
///   reaches the database.</item>
/// </list>
/// </remarks>
public sealed class AuditImmutabilityIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initialises the test class.
    /// <paramref name="fixture"/> is injected by the xUnit v3 assembly-fixture mechanism.
    /// </summary>
    public AuditImmutabilityIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates an isolated database, applies EF migrations, and returns a
    /// <see cref="VeriqanDbContext"/> WITHOUT the immutability interceptor registered.
    /// Used by trigger tests: the raw SQL bypasses EF entirely, so we only need the schema.
    /// </summary>
    private async Task<(VeriqanDbContext Ctx, string ConnectionString)> BuildPlainContextAsync(
        string dbName,
        CancellationToken ct)
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName, ct)
            ;

        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        // Apply all migrations (including AddImmutabilityTriggers).
        var ctx = new VeriqanDbContext(options);
        await ctx.Database.MigrateAsync(ct);

        return (ctx, connectionString);
    }

    /// <summary>
    /// Creates an isolated database, applies EF migrations, and returns a
    /// <see cref="VeriqanDbContext"/> WITH the <see cref="ImmutableEntityInterceptor"/> registered
    /// in <see cref="DbContextOptionsBuilder.AddInterceptors"/>.
    /// Used by interceptor tests.
    /// </summary>
    private async Task<VeriqanDbContext> BuildInterceptorContextAsync(
        string dbName,
        CancellationToken ct)
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName, ct)
            ;

        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .AddInterceptors(new ImmutableEntityInterceptor())
            .Options;

        var ctx = new VeriqanDbContext(options);
        await ctx.Database.MigrateAsync(ct);

        return ctx;
    }

    /// <summary>
    /// Builds a DI service provider with the durable Orchestration stores registered,
    /// using the given <paramref name="connectionString"/>.
    /// </summary>
    private IServiceProvider BuildStoreProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new XUnitLoggerProvider(_output)));

        services.AddDbContext<VeriqanDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));

        services.AddSingleton<IVerificationResultStore, EfVerificationResultStore>();
        services.AddSingleton<IReprocessAuditRepository, EfReprocessAuditRepository>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds a minimal <see cref="Disposition"/> with all required fields.
    /// </summary>
    private static Disposition BuildDisposition() =>
        new(
            id: Guid.NewGuid(),
            verificationJobId: Guid.NewGuid(),
            findingId: null,
            action: DispositionAction.Accept,
            actor: "test-actor",
            dispositionedAtUtc: DateTimeOffset.UtcNow);

    /// <summary>
    /// Builds a minimal <see cref="ReprocessAuditLogEntity"/> with all required fields.
    /// </summary>
    private static ReprocessAuditLogEntity BuildAuditEntity() =>
        new()
        {
            Id = Guid.NewGuid(),
            ContentHash = Guid.NewGuid().ToString("N"),
            Actor = "test-system",
            Reason = "unit test",
            BeforeSignal = null,
            AfterSignal = (int)VerdictSignal.Green,
            ReprocessedAtUtc = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Builds a minimal <see cref="VerificationOutcome"/> used for snapshot mutability test.
    /// </summary>
    private static VerificationOutcome BuildOutcome(string contentHash, TimeSpan duration)
    {
        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: contentHash,
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Completed);

        IReadOnlyList<RuleFinding> findings =
        [
            RuleFinding.Pass("CL-IMM-P", TechniqueClass.Deterministic, "1.0.0", "ok"),
        ];

        var summary = VerdictSummary.Green(
            passCount: 1,
            insufficientDataCount: 0,
            insufficientDataCheckIds: []);

        return new VerificationOutcome(job, summary, findings, duration);
    }

    // -----------------------------------------------------------------------
    // Test 1: DB trigger blocks UPDATE on Dispositions
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raw SQL UPDATE on <c>veriqan.Dispositions</c> is rejected by the DB trigger
    /// with a <see cref="SqlException"/> whose message contains "immutable".
    /// </summary>
    [Fact]
    public async Task Trigger_Dispositions_RawUpdate_ThrowsSqlExceptionWithImmutableMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ctx, _) = await BuildPlainContextAsync("imm_disp_trigger_update", ct);
        await using (ctx)
        {
            // Seed one row (INSERT — must succeed).
            var disposition = BuildDisposition();
            await ctx.Dispositions.AddAsync(disposition, ct);
            await ctx.SaveChangesAsync(ct);

            var id = disposition.Id;

            // Act — raw SQL UPDATE bypasses the EF interceptor and hits the DB trigger.
            var ex = await Should.ThrowAsync<SqlException>(async () =>
                await ctx.Database
                    .ExecuteSqlAsync(
                        $"UPDATE veriqan.Dispositions SET Actor = 'tampered' WHERE Id = {id}",
                        ct)
                    );

            ex.Message.ShouldContain("immutable",
                Case.Insensitive,
                "DB trigger must surface the immutability error in the exception message.");
        }
    }

    // -----------------------------------------------------------------------
    // Test 2: DB trigger blocks DELETE on Dispositions
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raw SQL DELETE on <c>veriqan.Dispositions</c> is rejected by the DB trigger
    /// with a <see cref="SqlException"/> whose message contains "immutable".
    /// </summary>
    [Fact]
    public async Task Trigger_Dispositions_RawDelete_ThrowsSqlExceptionWithImmutableMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ctx, _) = await BuildPlainContextAsync("imm_disp_trigger_delete", ct);
        await using (ctx)
        {
            var disposition = BuildDisposition();
            await ctx.Dispositions.AddAsync(disposition, ct);
            await ctx.SaveChangesAsync(ct);

            var id = disposition.Id;

            var ex = await Should.ThrowAsync<SqlException>(async () =>
                await ctx.Database
                    .ExecuteSqlAsync(
                        $"DELETE FROM veriqan.Dispositions WHERE Id = {id}",
                        ct)
                    );

            ex.Message.ShouldContain("immutable", Case.Insensitive);
        }
    }

    // -----------------------------------------------------------------------
    // Test 3: DB trigger blocks UPDATE on ReprocessAuditLog
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raw SQL UPDATE on <c>veriqan.ReprocessAuditLog</c> is rejected by the DB trigger
    /// with a <see cref="SqlException"/> whose message contains "immutable".
    /// </summary>
    [Fact]
    public async Task Trigger_ReprocessAuditLog_RawUpdate_ThrowsSqlExceptionWithImmutableMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ctx, connectionString) = await BuildPlainContextAsync("imm_audit_trigger_update", ct);
        await using (ctx)
        {
            // Seed via the Orchestration-level EfReprocessAuditRepository.
            var sp = BuildStoreProvider(connectionString);
            await using var scope = ((ServiceProvider)sp).CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IReprocessAuditRepository>();

            var entry = new ReprocessAuditEntry(
                Id: Guid.NewGuid(),
                ContentHash: Guid.NewGuid().ToString("N"),
                Actor: "seed-actor",
                Reason: "trigger-test",
                BeforeSignal: null,
                AfterSignal: VerdictSignal.Green,
                ReprocessedAtUtc: DateTimeOffset.UtcNow);

            var appendResult = await repo.AppendAsync(entry, ct);
            appendResult.IsSuccess.ShouldBeTrue($"AppendAsync failed: {appendResult.Error}");

            // Act — raw UPDATE bypasses the EF interceptor.
            var ex = await Should.ThrowAsync<SqlException>(async () =>
                await ctx.Database
                    .ExecuteSqlAsync(
                        $"UPDATE veriqan.ReprocessAuditLog SET Actor = 'tampered' WHERE Id = {entry.Id}",
                        ct)
                    );

            ex.Message.ShouldContain("immutable", Case.Insensitive);
        }
    }

    // -----------------------------------------------------------------------
    // Test 4: DB trigger blocks DELETE on ReprocessAuditLog
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raw SQL DELETE on <c>veriqan.ReprocessAuditLog</c> is rejected by the DB trigger
    /// with a <see cref="SqlException"/> whose message contains "immutable".
    /// </summary>
    [Fact]
    public async Task Trigger_ReprocessAuditLog_RawDelete_ThrowsSqlExceptionWithImmutableMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ctx, connectionString) = await BuildPlainContextAsync("imm_audit_trigger_delete", ct);
        await using (ctx)
        {
            var sp = BuildStoreProvider(connectionString);
            await using var scope = ((ServiceProvider)sp).CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IReprocessAuditRepository>();

            var entry = new ReprocessAuditEntry(
                Id: Guid.NewGuid(),
                ContentHash: Guid.NewGuid().ToString("N"),
                Actor: "seed-actor",
                Reason: "trigger-delete-test",
                BeforeSignal: null,
                AfterSignal: VerdictSignal.Red,
                ReprocessedAtUtc: DateTimeOffset.UtcNow);

            var appendResult = await repo.AppendAsync(entry, ct);
            appendResult.IsSuccess.ShouldBeTrue($"AppendAsync failed: {appendResult.Error}");

            var ex = await Should.ThrowAsync<SqlException>(async () =>
                await ctx.Database
                    .ExecuteSqlAsync(
                        $"DELETE FROM veriqan.ReprocessAuditLog WHERE Id = {entry.Id}",
                        ct)
                    );

            ex.Message.ShouldContain("immutable", Case.Insensitive);
        }
    }

    // -----------------------------------------------------------------------
    // Test 5: EF interceptor blocks Modified state on Disposition
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marking a <see cref="Disposition"/> entity as <see cref="EntityState.Modified"/> in EF
    /// and calling <c>SaveChangesAsync</c> throws <see cref="InvalidOperationException"/>
    /// before any SQL is emitted (app-level fast rejection via <see cref="ImmutableEntityInterceptor"/>).
    /// </summary>
    [Fact]
    public async Task Interceptor_Disposition_ModifiedState_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = await BuildInterceptorContextAsync("imm_disp_interceptor_mod", ct);
        await using (ctx)
        {
            // Seed — INSERT is allowed; interceptor only blocks Modified/Deleted.
            var disposition = BuildDisposition();
            await ctx.Dispositions.AddAsync(disposition, ct);
            await ctx.SaveChangesAsync(ct);

            // Detach and re-attach as Modified to simulate an update attempt.
            ctx.ChangeTracker.Clear();
            ctx.Dispositions.Attach(disposition);
            ctx.Entry(disposition).State = EntityState.Modified;

            // Act + Assert — interceptor fires before any SQL.
            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await ctx.SaveChangesAsync(ct));

            ex.Message.ShouldContain("immutable",
                Case.Insensitive,
                "Interceptor must surface the immutability error in the exception message.");
            ex.Message.ShouldContain(nameof(Disposition), Case.Sensitive,
                "Exception message must name the offending entity type.");
        }
    }

    // -----------------------------------------------------------------------
    // Test 6: EF interceptor blocks Deleted state on Disposition
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marking a <see cref="Disposition"/> entity as <see cref="EntityState.Deleted"/> in EF
    /// and calling <c>SaveChangesAsync</c> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public async Task Interceptor_Disposition_DeletedState_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = await BuildInterceptorContextAsync("imm_disp_interceptor_del", ct);
        await using (ctx)
        {
            var disposition = BuildDisposition();
            await ctx.Dispositions.AddAsync(disposition, ct);
            await ctx.SaveChangesAsync(ct);

            ctx.ChangeTracker.Clear();
            ctx.Dispositions.Attach(disposition);
            ctx.Entry(disposition).State = EntityState.Deleted;

            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await ctx.SaveChangesAsync(ct));

            ex.Message.ShouldContain("immutable", Case.Insensitive);
        }
    }

    // -----------------------------------------------------------------------
    // Test 7: EF interceptor blocks Modified state on ReprocessAuditLogEntity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marking a <see cref="ReprocessAuditLogEntity"/> as <see cref="EntityState.Modified"/> in EF
    /// and calling <c>SaveChangesAsync</c> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public async Task Interceptor_ReprocessAuditLogEntity_ModifiedState_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = await BuildInterceptorContextAsync("imm_audit_interceptor_mod", ct);
        await using (ctx)
        {
            var entity = BuildAuditEntity();
            await ctx.ReprocessAuditLog.AddAsync(entity, ct);
            await ctx.SaveChangesAsync(ct);

            ctx.ChangeTracker.Clear();
            ctx.ReprocessAuditLog.Attach(entity);
            ctx.Entry(entity).State = EntityState.Modified;

            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await ctx.SaveChangesAsync(ct));

            ex.Message.ShouldContain("immutable", Case.Insensitive);
            ex.Message.ShouldContain(nameof(ReprocessAuditLogEntity), Case.Sensitive,
                "Exception message must name the offending entity type.");
        }
    }

    // -----------------------------------------------------------------------
    // Test 8: EF interceptor blocks Deleted state on ReprocessAuditLogEntity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marking a <see cref="ReprocessAuditLogEntity"/> as <see cref="EntityState.Deleted"/> in EF
    /// and calling <c>SaveChangesAsync</c> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public async Task Interceptor_ReprocessAuditLogEntity_DeletedState_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = await BuildInterceptorContextAsync("imm_audit_interceptor_del", ct);
        await using (ctx)
        {
            var entity = BuildAuditEntity();
            await ctx.ReprocessAuditLog.AddAsync(entity, ct);
            await ctx.SaveChangesAsync(ct);

            ctx.ChangeTracker.Clear();
            ctx.ReprocessAuditLog.Attach(entity);
            ctx.Entry(entity).State = EntityState.Deleted;

            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await ctx.SaveChangesAsync(ct));

            ex.Message.ShouldContain("immutable", Case.Insensitive);
        }
    }

    // -----------------------------------------------------------------------
    // Test 9: INSERT regression — Dispositions and ReprocessAuditLog still accept INSERTs
    // -----------------------------------------------------------------------

    /// <summary>
    /// INSERT (AddAsync) on both protected tables succeeds — only UPDATE and DELETE are blocked.
    /// </summary>
    [Fact]
    public async Task Insert_BothProtectedTables_Succeeds_ProvesTriggerIsUpdateDeleteOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = await BuildInterceptorContextAsync("imm_insert_regression", ct);
        await using (ctx)
        {
            // Disposition INSERT must succeed.
            var disposition = BuildDisposition();
            await ctx.Dispositions.AddAsync(disposition, ct);
            await ctx.SaveChangesAsync(ct);

            var dispositionCount = await ctx.Dispositions.CountAsync(ct);
            dispositionCount.ShouldBe(1, "Disposition INSERT must succeed; trigger only fires on UPDATE/DELETE.");

            // ReprocessAuditLogEntity INSERT must succeed.
            var auditEntity = BuildAuditEntity();
            await ctx.ReprocessAuditLog.AddAsync(auditEntity, ct);
            await ctx.SaveChangesAsync(ct);

            var auditCount = await ctx.ReprocessAuditLog.CountAsync(ct);
            auditCount.ShouldBe(1, "ReprocessAuditLog INSERT must succeed; trigger only fires on UPDATE/DELETE.");
        }
    }

    // -----------------------------------------------------------------------
    // Test 10: VerificationOutcomeSnapshots remains mutable (no over-protection)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>ReplaceOutcomeAsync</c> (which UPDATEs <c>veriqan.VerificationOutcomeSnapshots</c>)
    /// continues to succeed — that table must NOT be protected by triggers or the interceptor.
    /// </summary>
    [Fact]
    public async Task VerificationOutcomeSnapshots_ReplaceOutcome_Succeeds_NotOverProtected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, connectionString) = await BuildPlainContextAsync("imm_snapshots_mutable", ct);

        var sp = BuildStoreProvider(connectionString);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();

        const string hash = "aabbccddeeff0011aabbccddeeff0011aabbccddeeff0011aabbccddeeff0011";

        // SaveOutcomeAsync must succeed (INSERT).
        var original = BuildOutcome(hash, TimeSpan.FromSeconds(2));
        var saveResult = await store.SaveOutcomeAsync(hash, original, ct);
        saveResult.IsSuccess.ShouldBeTrue($"SaveOutcomeAsync failed: {saveResult.Error}");

        // ReplaceOutcomeAsync must succeed (UPDATE) — VerificationOutcomeSnapshots is NOT protected.
        var replacement = BuildOutcome(hash, TimeSpan.FromSeconds(9));
        var replaceResult = await store.ReplaceOutcomeAsync(hash, replacement, ct);
        replaceResult.IsSuccess.ShouldBeTrue(
            $"ReplaceOutcomeAsync must succeed; VerificationOutcomeSnapshots must NOT be protected by the immutability trigger or interceptor. Error: {replaceResult.Error}");

        // Confirm the update actually took effect.
        var getResult = await store.GetOutcomeAsync(hash, ct);
        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value!.ProcessingDuration.ShouldBe(
            TimeSpan.FromSeconds(9),
            "ReplaceOutcomeAsync must overwrite the snapshot — the table must remain mutable.");
    }
}
