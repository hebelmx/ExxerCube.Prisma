// DEFERRED: This test requires a running SQL Server container (Docker).
// Docker is NOT available on this machine. The test is authored and compiled
// but cannot be executed until Docker is available (CI or a Docker-capable dev box).
//
// To run: docker must be available, then:
//   dotnet test ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests.csproj
//
// Story: VERIQAN-E1-S5 — Persist stage (Stage 8: JobVerdict + Findings persistence).

using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Services;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="EfVerdictPersistenceService"/> exercised against a real
/// SQL Server container (Testcontainers).
/// </summary>
/// <remarks>
/// <para>
/// <b>DEFERRED — Docker required.</b> These tests spin up a SQL Server container via
/// Testcontainers. They must NOT be run on machines without Docker (including the machine
/// on which the story was authored). On a CI or Docker-capable box:
/// <c>dotnet test ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests.csproj</c>
/// </para>
/// <para>
/// Each test provisions its own isolated database via
/// <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/> so tests run in
/// parallel without colliding on shared state.
/// </para>
/// </remarks>
public sealed class VerdictPersistenceIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<VerdictPersistenceIntegrationTests> _logger;

    /// <summary>
    /// Initialises the test class.
    /// <paramref name="fixture"/> is injected by the xUnit v3 assembly-fixture mechanism declared
    /// in <c>AssemblyFixtures.cs</c>.
    /// </summary>
    public VerdictPersistenceIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = XUnitLogger.CreateLogger<VerdictPersistenceIntegrationTests>(output);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates and migrates an isolated <see cref="VeriqanDbContext"/> on a fresh database,
    /// then returns a DI scope containing a real <see cref="EfVerdictPersistenceService"/>.
    /// </summary>
    private async Task<(AsyncServiceScope Scope, string ConnectionString)> BuildScopeAsync(
        string dbName,
        CancellationToken ct)
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName, ct);

        _logger.LogInformation(
            "Isolated database {DbName} created for VerdictPersistence test.",
            dbName);

        // Migrate the schema so JobVerdicts / Findings tables exist.
        var migrateOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var ctx = new VeriqanDbContext(migrateOptions))
        {
            await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("VeriqanDbContext migration completed for {DbName}.", dbName);
        }

        // Build a DI scope so EfVerdictPersistenceService is resolved the same way
        // it would be in production (Scoped lifetime + scoped DbContext).
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new XUnitLoggerProvider(_output)));

        services.AddDbContext<VeriqanDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));

        services.AddScoped<EfVerdictPersistenceService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateAsyncScope();

        return (scope, connectionString);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Happy path: <see cref="EfVerdictPersistenceService.PersistAsync"/> writes a
    /// <see cref="Domain.Entities.JobVerdict"/> row and at least one <see cref="Domain.Entities.Finding"/>
    /// row to the database, including both a Pass (green) and a Fail (red) finding.
    /// </summary>
    [Fact]
    public async Task PersistAsync_HappyPath_WritesVerdictAndFindings()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_happy", ct);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();
            const string engineVersion = "1.0.0";

            // Two findings: one Pass (green) and one Fail (red).
            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Pass("CL-PERSIST-P", TechniqueClass.Deterministic, engineVersion, "ok"),
                RuleFinding.Fail(
                    "CL-PERSIST-F",
                    TechniqueClass.Deterministic,
                    FindingSeverity.Critical,
                    engineVersion,
                    expected: "expected-value",
                    observed: "observed-value"),
            ];

            // Act
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Red,
                findings: ruleFindings,
                engineVersion: engineVersion,
                cancellationToken: ct);

            // Assert — PersistAsync succeeded
            result.IsSuccess.ShouldBeTrue(
                $"PersistAsync returned failure: {result.Error}");
            result.Value.ShouldNotBeNull();
            result.Value.VerificationJobId.ShouldBe(jobId);
            result.Value.Signal.ShouldBe(VerdictSignal.Red);

            // Assert — JobVerdict row exists in the database
            var storedVerdict = await ctx.JobVerdicts
                .AsNoTracking()
                .Where(v => v.VerificationJobId == jobId)
                .SingleOrDefaultAsync(ct);

            storedVerdict.ShouldNotBeNull("A JobVerdict row must exist for the given jobId.");
            storedVerdict.Signal.ShouldBe(VerdictSignal.Red);

            // Assert — Finding rows: at least one Pass and at least one Fail
            var storedFindings = await ctx.Findings
                .AsNoTracking()
                .Where(f => f.VerificationJobId == jobId)
                .ToListAsync(ct);

            storedFindings.Count.ShouldBeGreaterThanOrEqualTo(
                2,
                $"Expected at least 2 Finding rows for jobId {jobId}; got {storedFindings.Count}.");

            storedFindings
                .Any(f => f.Verdict == FindingVerdict.Pass)
                .ShouldBeTrue("Expected at least one Pass (green) finding in the database.");

            storedFindings
                .Any(f => f.Verdict == FindingVerdict.Fail)
                .ShouldBeTrue("Expected at least one Fail (red) finding in the database.");

            _logger.LogInformation(
                "VerdictPersistence happy-path passed: {VerdictId} | {FindingCount} findings.",
                storedVerdict.Id,
                storedFindings.Count);
        }
    }

    /// <summary>
    /// Cancellation guard: when the <see cref="CancellationToken"/> is already cancelled before
    /// <see cref="EfVerdictPersistenceService.PersistAsync"/> is called, the method returns
    /// a cancelled <c>Result</c> and writes NO rows to the database.
    /// </summary>
    [Fact]
    public async Task PersistAsync_PreCancelledToken_ReturnsCancelledWithoutWriting()
    {
        // Arrange
        var parentCt = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_cancel", parentCt);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
            cts.Cancel(); // pre-cancel

            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Pass("CL-CANCEL", TechniqueClass.Deterministic, "1.0.0"),
            ];

            // Act
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Green,
                findings: ruleFindings,
                engineVersion: "1.0.0",
                cancellationToken: cts.Token);

            // Assert — returned non-success (cancelled) and not success
            result.IsSuccess.ShouldBeFalse(
                "PersistAsync with a pre-cancelled token must not return success.");

            // Assert — no rows written
            var rowCount = await ctx.JobVerdicts
                .AsNoTracking()
                .CountAsync(v => v.VerificationJobId == jobId, parentCt);

            rowCount.ShouldBe(
                0,
                "No JobVerdict rows must be written when the token was pre-cancelled.");
        }
    }
}
