// VERIQAN-E3-S3 — Durable BatchExceptionLog for the batch-processing dead-letter queue.
// Tests run against a real SQL Server Testcontainer; each test provisions its own isolated DB.

using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.Repositories;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="EfBatchExceptionLogRepository"/> exercised against a real
/// SQL Server Testcontainer.
/// </summary>
/// <remarks>
/// Each test provisions its own isolated database via
/// <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/> and applies EF migrations
/// before running. Tests may be run in parallel without shared-state collisions.
/// </remarks>
public sealed class BatchExceptionLogIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initialises the test class.
    /// <paramref name="fixture"/> is injected by the xUnit v3 assembly-fixture mechanism.
    /// </summary>
    public BatchExceptionLogIntegrationTests(
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
    /// Builds a fresh <see cref="IServiceProvider"/> wired to <paramref name="connectionString"/>
    /// with <see cref="EfBatchExceptionLogRepository"/> registered. Each call produces an
    /// independent DI container (and therefore independent <see cref="VeriqanDbContext"/>
    /// instances created inside the repository's own scopes) so tests can prove durability
    /// across process boundaries rather than relying on in-process DbContext caching.
    /// </summary>
    private IServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new XUnitLoggerProvider(_output)));

        services.AddDbContext<VeriqanDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));

        services.AddSingleton<IBatchExceptionLogRepository, EfBatchExceptionLogRepository>();

        return services.BuildServiceProvider();
    }

    private async Task<string> ProvisionMigratedDatabaseAsync(string dbName, CancellationToken ct)
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName, ct);

        var migrateOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var ctx = new VeriqanDbContext(migrateOptions))
        {
            await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
        }

        return connectionString;
    }

    private static BatchExceptionLogEntry MakeEntry(
        Guid batchId,
        string statementHash = "aabbccddeeff001122334455667788990011223344556677889900aabbccddee",
        string institutionId = "Bank A",
        string failureReason = "extraction failed",
        DateTimeOffset? failedAt = null) =>
        new(
            Id: Guid.NewGuid(),
            BatchId: batchId,
            StatementHash: statementHash,
            InstitutionId: institutionId,
            FailureReason: failureReason,
            FailedAt: failedAt ?? DateTimeOffset.UtcNow);

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// AppendAsync then GetByBatchIdAsync round-trips a single dead-letter row through a real
    /// SQL Server database, using a SECOND, independently-constructed
    /// <see cref="IServiceProvider"/> (and therefore a second <see cref="VeriqanDbContext"/>
    /// instance) for the read — proving the row survives beyond the writer's DI scope /
    /// DbContext instance, not merely cached in EF's first-level change tracker.
    /// </summary>
    [Fact]
    public async Task EfBatchExceptionLogRepository_AppendThenQueryFromSecondProvider_RoundTripsRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await ProvisionMigratedDatabaseAsync("batchexlog_roundtrip", ct);

        var batchId = Guid.NewGuid();
        var entry = MakeEntry(batchId, failureReason: "PDF extraction timed out");

        // Act — append via provider #1
        await using (var writerProvider = (ServiceProvider)BuildProvider(connectionString))
        {
            var writerRepo = writerProvider.GetRequiredService<IBatchExceptionLogRepository>();
            var appendResult = await writerRepo.AppendAsync(entry, ct);
            appendResult.IsSuccess.ShouldBeTrue($"AppendAsync failed: {appendResult.Error}");
        }

        // Act — query via a brand-new provider #2 (fresh DbContext instance, same database).
        await using var readerProvider = (ServiceProvider)BuildProvider(connectionString);
        var readerRepo = readerProvider.GetRequiredService<IBatchExceptionLogRepository>();
        var getResult = await readerRepo.GetByBatchIdAsync(batchId, ct);

        // Assert
        getResult.IsSuccess.ShouldBeTrue($"GetByBatchIdAsync failed: {getResult.Error}");
        var rows = getResult.Value!;
        rows.Count.ShouldBe(1, "The row appended by provider #1 must be visible to provider #2.");
        rows[0].Id.ShouldBe(entry.Id);
        rows[0].BatchId.ShouldBe(batchId);
        rows[0].StatementHash.ShouldBe(entry.StatementHash);
        rows[0].InstitutionId.ShouldBe(entry.InstitutionId);
        rows[0].FailureReason.ShouldBe("PDF extraction timed out");
        rows[0].RetryCount.ShouldBe(0);
        rows[0].LastRetryAt.ShouldBeNull();
        rows[0].ResolvedAt.ShouldBeNull();
    }

    /// <summary>
    /// GetByBatchIdAsync returns an empty (not failed) list when no rows exist for the given
    /// BatchId.
    /// </summary>
    [Fact]
    public async Task EfBatchExceptionLogRepository_GetByBatchIdAsync_NoRows_ReturnsEmptySuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await ProvisionMigratedDatabaseAsync("batchexlog_empty", ct);

        await using var provider = (ServiceProvider)BuildProvider(connectionString);
        var repo = provider.GetRequiredService<IBatchExceptionLogRepository>();

        var getResult = await repo.GetByBatchIdAsync(Guid.NewGuid(), ct);

        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value.ShouldNotBeNull();
        getResult.Value!.Count.ShouldBe(0);
    }

    /// <summary>
    /// Multiple rows for the same BatchId are returned ordered by <c>FailedAt</c> ascending, and
    /// rows for a different BatchId are excluded.
    /// </summary>
    [Fact]
    public async Task EfBatchExceptionLogRepository_MultipleRowsSameBatch_ReturnedOrderedByFailedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await ProvisionMigratedDatabaseAsync("batchexlog_ordering", ct);

        var batchId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var first = MakeEntry(batchId, statementHash: new string('1', 64), failureReason: "first", failedAt: baseTime);
        var second = MakeEntry(batchId, statementHash: new string('2', 64), failureReason: "second", failedAt: baseTime.AddMinutes(3));
        var otherBatch = MakeEntry(otherBatchId, statementHash: new string('3', 64), failureReason: "other batch", failedAt: baseTime.AddMinutes(1));

        await using (var provider = (ServiceProvider)BuildProvider(connectionString))
        {
            var repo = provider.GetRequiredService<IBatchExceptionLogRepository>();

            // Insert out of chronological order to prove the read applies the ORDER BY, not insert order.
            (await repo.AppendAsync(second, ct)).IsSuccess.ShouldBeTrue();
            (await repo.AppendAsync(first, ct)).IsSuccess.ShouldBeTrue();
            (await repo.AppendAsync(otherBatch, ct)).IsSuccess.ShouldBeTrue();
        }

        await using var readerProvider = (ServiceProvider)BuildProvider(connectionString);
        var readerRepo = readerProvider.GetRequiredService<IBatchExceptionLogRepository>();
        var getResult = await readerRepo.GetByBatchIdAsync(batchId, ct);

        getResult.IsSuccess.ShouldBeTrue();
        var rows = getResult.Value!;
        rows.Count.ShouldBe(2, "Only rows for the requested BatchId must be returned.");
        rows[0].Id.ShouldBe(first.Id, "Earlier FailedAt must be first.");
        rows[1].Id.ShouldBe(second.Id, "Later FailedAt must be second.");
    }

    /// <summary>
    /// A FailureReason at the schema's maximum length (2000 chars, matching
    /// <see cref="BatchExceptionLogEntry.MaxFailureReasonLength"/>) round-trips without
    /// truncation or a database error — the column width matches the domain constant.
    /// </summary>
    [Fact]
    public async Task EfBatchExceptionLogRepository_MaxLengthFailureReason_RoundTripsExactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await ProvisionMigratedDatabaseAsync("batchexlog_maxlen", ct);

        var batchId = Guid.NewGuid();
        var maxLengthReason = new string('z', BatchExceptionLogEntry.MaxFailureReasonLength);
        var entry = MakeEntry(batchId, failureReason: maxLengthReason);

        await using (var provider = (ServiceProvider)BuildProvider(connectionString))
        {
            var repo = provider.GetRequiredService<IBatchExceptionLogRepository>();
            var appendResult = await repo.AppendAsync(entry, ct);
            appendResult.IsSuccess.ShouldBeTrue($"AppendAsync failed: {appendResult.Error}");
        }

        await using var readerProvider = (ServiceProvider)BuildProvider(connectionString);
        var readerRepo = readerProvider.GetRequiredService<IBatchExceptionLogRepository>();
        var getResult = await readerRepo.GetByBatchIdAsync(batchId, ct);

        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value!.Count.ShouldBe(1);
        getResult.Value![0].FailureReason.Length.ShouldBe(BatchExceptionLogEntry.MaxFailureReasonLength);
        getResult.Value![0].FailureReason.ShouldBe(maxLengthReason);
    }
}
