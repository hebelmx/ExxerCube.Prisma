// Story: VERIQAN-E1-S5 — Persist stage (Stage 8: JobVerdict + Findings persistence).
// Story 1.4 — Persist + expose two-tier verdict (BankTierVerdict, CondusefTierVerdict, Finding.Tier).
// Story 4.1 — Persist + round-trip extraction confidence (JobVerdict.Confidence, Finding.Confidence).
// Docker IS available in this session; these tests run LIVE on a SQL Server Testcontainer.

using System.Collections.Generic;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
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

            // Seed the parent VerificationJob first. JobVerdict and Finding are children of the
            // VerificationJob aggregate (FK_JobVerdicts/Findings_VerificationJobs_VerificationJobId,
            // configured in VerificationJobConfiguration with cascade delete), so real SQL rejects an
            // orphan verdict/finding insert. In production the job row always exists before the Stage-8
            // verdict is persisted; the test must reproduce that precondition. (InMemory does not enforce
            // FKs, which is why this gap only surfaces against a real SQL container.)
            var parentJob = new VerificationJob(
                id: jobId,
                contentHash: jobId.ToString("N"),
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending); // matches the real ingestion precondition (job created Pending before Stage-8 verdict)
            await ctx.VerificationJobs.AddAsync(parentJob, ct);
            await ctx.SaveChangesAsync(ct);

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
    /// VERIQAN-E3-S4 round-trip: <see cref="EfVerdictPersistenceService.PersistAsync"/> stamps
    /// <see cref="JobVerdict.EngineVersion"/> (non-null, always supplied by the pipeline) and
    /// <see cref="JobVerdict.ReferenceBundleVersion"/> (round-trips exactly when the caller
    /// supplies a bundle version — mirroring the pipeline's catalog-pre-resolve success path).
    /// </summary>
    [Fact]
    public async Task PersistAsync_Provenance_EngineVersionNonNullAndReferenceBundleVersionRoundTrips()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_provenance", ct);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();
            const string engineVersion = "9.9.9.9";
            const string referenceBundleVersion = "1.0.0";

            // Seed the parent VerificationJob (FK required by real SQL).
            var parentJob = new VerificationJob(
                id: jobId,
                contentHash: jobId.ToString("N") + "prov",
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending);
            await ctx.VerificationJobs.AddAsync(parentJob, ct);
            await ctx.SaveChangesAsync(ct);

            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Pass("CL-PROV-P", TechniqueClass.Deterministic, engineVersion, "ok"),
            ];

            // Act
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Green,
                findings: ruleFindings,
                engineVersion: engineVersion,
                cancellationToken: ct,
                referenceBundleVersion: referenceBundleVersion);

            // Assert — service call succeeded
            result.IsSuccess.ShouldBeTrue(
                $"PersistAsync returned failure: {result.Error}");
            result.Value.ShouldNotBeNull();
            result.Value.EngineVersion.ShouldBe(engineVersion);
            result.Value.ReferenceBundleVersion.ShouldBe(referenceBundleVersion);

            // Assert — JobVerdict provenance columns round-trip from the database
            var storedVerdict = await ctx.JobVerdicts
                .AsNoTracking()
                .Where(v => v.VerificationJobId == jobId)
                .SingleOrDefaultAsync(ct);

            storedVerdict.ShouldNotBeNull("A JobVerdict row must exist for the given jobId.");
            storedVerdict.EngineVersion.ShouldNotBeNull(
                "EngineVersion must never be null on a persisted JobVerdict row (VERIQAN-E3-S4).");
            storedVerdict.EngineVersion.ShouldBe(engineVersion);
            storedVerdict.ReferenceBundleVersion.ShouldBe(
                referenceBundleVersion,
                "ReferenceBundleVersion must round-trip when the caller supplies a resolved bundle version.");

            _logger.LogInformation(
                "Provenance round-trip passed: VerdictId={VerdictId} EngineVersion={EngineVersion} ReferenceBundleVersion={ReferenceBundleVersion}",
                storedVerdict.Id,
                storedVerdict.EngineVersion,
                storedVerdict.ReferenceBundleVersion);
        }
    }

    /// <summary>
    /// VERIQAN-E3-S4 graceful-degradation path: when the caller does not supply a
    /// <c>referenceBundleVersion</c> (mirrors the pipeline's catalog-pre-resolve failure path,
    /// where <c>catalogBundle</c> is <see langword="null"/>), the persisted
    /// <see cref="JobVerdict.ReferenceBundleVersion"/> is <see langword="null"/> while
    /// <see cref="JobVerdict.EngineVersion"/> is still stamped and non-null.
    /// </summary>
    [Fact]
    public async Task PersistAsync_NoReferenceBundleVersionSupplied_PersistsNullReferenceBundleVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_no_bundle", ct);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();
            const string engineVersion = "9.9.9.9";

            var parentJob = new VerificationJob(
                id: jobId,
                contentHash: jobId.ToString("N") + "nobundle",
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending);
            await ctx.VerificationJobs.AddAsync(parentJob, ct);
            await ctx.SaveChangesAsync(ct);

            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Pass("CL-NOBUNDLE-P", TechniqueClass.Deterministic, engineVersion, "ok"),
            ];

            // Act — referenceBundleVersion intentionally omitted (defaults to null).
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Green,
                findings: ruleFindings,
                engineVersion: engineVersion,
                cancellationToken: ct);

            // Assert
            result.IsSuccess.ShouldBeTrue(
                $"PersistAsync returned failure: {result.Error}");
            result.Value.ShouldNotBeNull();
            result.Value.EngineVersion.ShouldBe(engineVersion);
            result.Value.ReferenceBundleVersion.ShouldBeNull();

            var storedVerdict = await ctx.JobVerdicts
                .AsNoTracking()
                .Where(v => v.VerificationJobId == jobId)
                .SingleOrDefaultAsync(ct);

            storedVerdict.ShouldNotBeNull("A JobVerdict row must exist for the given jobId.");
            storedVerdict.EngineVersion.ShouldNotBeNull(
                "EngineVersion must never be null even on the no-bundle-resolved path.");
            storedVerdict.ReferenceBundleVersion.ShouldBeNull(
                "ReferenceBundleVersion must persist as null on the graceful-degradation path.");
        }
    }

    /// <summary>
    /// Story 1.4 round-trip: <see cref="EfVerdictPersistenceService.PersistAsync"/> persists
    /// <see cref="JobVerdict.BankTierVerdict"/> + <see cref="JobVerdict.CondusefTierVerdict"/>
    /// and each <see cref="Finding.Tier"/> is stamped from the supplied <c>checklistTiers</c> map.
    /// Reads back both the verdict columns and the finding tier to confirm the DB stores them
    /// exactly as passed (no silent drop, no default override).
    /// </summary>
    [Fact]
    public async Task PersistAsync_TwoTierVerdict_RoundTripsCorrectly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_two_tier", ct);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();
            const string engineVersion = "1.0.0";

            // Seed the parent VerificationJob (FK required by real SQL).
            var parentJob = new VerificationJob(
                id: jobId,
                contentHash: jobId.ToString("N") + "tt",
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending);
            await ctx.VerificationJobs.AddAsync(parentJob, ct);
            await ctx.SaveChangesAsync(ct);

            // Two findings: one Bank-tier (CL-BANK-01) and one Condusef-tier (CL-COND-01).
            // Both fail so both tier verdicts become non-green.
            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Fail(
                    "CL-BANK-01",
                    TechniqueClass.Deterministic,
                    FindingSeverity.Warning,
                    engineVersion,
                    expected: "bank-expected",
                    observed: "bank-observed"),
                RuleFinding.Fail(
                    "CL-COND-01",
                    TechniqueClass.Deterministic,
                    FindingSeverity.Critical,
                    engineVersion,
                    expected: "condusef-expected",
                    observed: "condusef-observed"),
            ];

            // Tier map: CL-BANK-01 is Bank-only; CL-COND-01 is Condusef.
            IReadOnlyDictionary<string, ChecklistTier> tierMap = new Dictionary<string, ChecklistTier>
            {
                ["CL-BANK-01"] = ChecklistTier.Bank,
                ["CL-COND-01"] = ChecklistTier.Condusef,
            };

            // The expected overall verdict: Bank=Yellow (bank-tier fail), Condusef=Red (condusef-tier fail).
            // Under the two-tier combination rule Condusef Red overrides → overall Red.

            // Act
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Red,
                findings: ruleFindings,
                engineVersion: engineVersion,
                bankTierVerdict: VerdictSignal.Yellow,
                condusefTierVerdict: VerdictSignal.Red,
                checklistTiers: tierMap,
                cancellationToken: ct);

            // Assert — service call succeeded
            result.IsSuccess.ShouldBeTrue(
                $"PersistAsync returned failure: {result.Error}");
            result.Value.ShouldNotBeNull();

            // Assert — JobVerdict tier columns round-trip from the database
            var storedVerdict = await ctx.JobVerdicts
                .AsNoTracking()
                .Where(v => v.VerificationJobId == jobId)
                .SingleOrDefaultAsync(ct);

            storedVerdict.ShouldNotBeNull("A JobVerdict row must exist for the given jobId.");
            storedVerdict.Signal.ShouldBe(VerdictSignal.Red, "overall signal must be Red");
            storedVerdict.BankTierVerdict.ShouldBe(
                VerdictSignal.Yellow,
                "BankTierVerdict must persist as Yellow (Story 1.4)");
            storedVerdict.CondusefTierVerdict.ShouldBe(
                VerdictSignal.Red,
                "CondusefTierVerdict must persist as Red (Story 1.4)");

            // Assert — Finding.Tier stamped correctly from the tier map
            var storedFindings = await ctx.Findings
                .AsNoTracking()
                .Where(f => f.VerificationJobId == jobId)
                .ToListAsync(ct);

            storedFindings.Count.ShouldBe(2, "Expected exactly 2 Finding rows.");

            var bankFinding = storedFindings.SingleOrDefault(f => f.CheckId == "CL-BANK-01");
            bankFinding.ShouldNotBeNull("CL-BANK-01 finding must exist");
            bankFinding!.Tier.ShouldBe(ChecklistTier.Bank,
                "CL-BANK-01 must be stamped as Bank tier (Story 1.4)");

            var condusefFinding = storedFindings.SingleOrDefault(f => f.CheckId == "CL-COND-01");
            condusefFinding.ShouldNotBeNull("CL-COND-01 finding must exist");
            condusefFinding!.Tier.ShouldBe(ChecklistTier.Condusef,
                "CL-COND-01 must be stamped as Condusef tier (Story 1.4)");

            _logger.LogInformation(
                "Two-tier round-trip passed: VerdictId={VerdictId} Bank={Bank} Condusef={Condusef} Findings={Count}",
                storedVerdict.Id,
                storedVerdict.BankTierVerdict,
                storedVerdict.CondusefTierVerdict,
                storedFindings.Count);
        }
    }

    /// <summary>
    /// Story 4.1 round-trip: non-default <see cref="JobVerdict.Confidence"/> and
    /// <see cref="Finding.Confidence"/> survive a write-then-read against SQL Server.
    /// Persists findings with <c>Confidence = 0.82</c>, reads the JobVerdict and Finding rows
    /// back, and asserts both equal <c>0.82</c>.
    /// </summary>
    [Fact]
    public async Task PersistAsync_ConfidenceRoundTrip_StoredAndReadBackCorrectly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (scope, _) = await BuildScopeAsync("verdict_persist_confidence", ct);

        await using (scope)
        {
            var svc = scope.ServiceProvider.GetRequiredService<EfVerdictPersistenceService>();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var jobId = Guid.NewGuid();
            const string engineVersion = "1.0.0";
            const double expectedConfidence = 0.82;

            // Seed the parent VerificationJob (FK required by real SQL).
            var parentJob = new VerificationJob(
                id: jobId,
                contentHash: jobId.ToString("N") + "conf",
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending);
            await ctx.VerificationJobs.AddAsync(parentJob, ct);
            await ctx.SaveChangesAsync(ct);

            // Two findings both carrying Confidence = 0.82 (non-default).
            // Story 4.1: confidence is set as a RuleFinding init property — the factory methods
            // leave it at the 1.0 default, so we use the object-initialiser to override.
            IReadOnlyList<RuleFinding> ruleFindings =
            [
                RuleFinding.Pass("CL-CONF-P", TechniqueClass.Deterministic, engineVersion, "ok")
                    with { Confidence = expectedConfidence },
                RuleFinding.Fail(
                    "CL-CONF-F",
                    TechniqueClass.Deterministic,
                    FindingSeverity.Critical,
                    engineVersion,
                    expected: "exp",
                    observed: "obs")
                    with { Confidence = expectedConfidence },
            ];

            // Act
            var result = await svc.PersistAsync(
                jobId: jobId,
                signal: VerdictSignal.Red,
                findings: ruleFindings,
                engineVersion: engineVersion,
                cancellationToken: ct);

            // Assert — service call succeeded
            result.IsSuccess.ShouldBeTrue(
                $"PersistAsync returned failure: {result.Error}");

            // Assert — JobVerdict.Confidence round-trips from the database
            var storedVerdict = await ctx.JobVerdicts
                .AsNoTracking()
                .Where(v => v.VerificationJobId == jobId)
                .SingleOrDefaultAsync(ct);

            storedVerdict.ShouldNotBeNull("A JobVerdict row must exist.");
            storedVerdict.Confidence.ShouldBe(
                expectedConfidence,
                $"JobVerdict.Confidence must persist as {expectedConfidence} (Story 4.1).");

            // Assert — Finding.Confidence round-trips from the database (both rows)
            var storedFindings = await ctx.Findings
                .AsNoTracking()
                .Where(f => f.VerificationJobId == jobId)
                .ToListAsync(ct);

            storedFindings.Count.ShouldBe(2, "Expected exactly 2 Finding rows.");

            foreach (var finding in storedFindings)
            {
                finding.Confidence.ShouldBe(
                    expectedConfidence,
                    $"Finding {finding.CheckId} Confidence must persist as {expectedConfidence} (Story 4.1).");
            }

            _logger.LogInformation(
                "Confidence round-trip passed: VerdictId={VerdictId} VerdictConf={VConf} FindingCount={Count}",
                storedVerdict.Id,
                storedVerdict.Confidence,
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
