// Story 6.8 — Migrate-only CLI entrypoint: integration tests.
// DOCKER REQUIRED: these tests spin a SQL Server Testcontainer. Run on a Docker-capable machine.

using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Startup;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using ExxerCube.Prisma.Veriqan.Worker;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="MigrateCommand.RunMigrateAsync(string, IConfiguration, CancellationToken)"/>
/// against a real SQL Server Testcontainer.
/// </summary>
/// <remarks>
/// <para>
/// Each test provisions its own isolated database via
/// <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/> so tests run in
/// parallel without colliding on shared state.
/// </para>
/// <para>
/// The Serilog static logger (<c>Log.Logger</c>) defaults to <c>Logger.None</c> (no-op) when
/// not configured — <see cref="MigrateCommand"/> uses it for console output, which is silenced
/// harmlessly in the test runner.  No Serilog setup is needed here.
/// </para>
/// </remarks>
public sealed class MigrateCommandIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MigrateCommandIntegrationTests> _logger;

    /// <summary>
    /// 32-byte zero test key (same convention as <see cref="LegalBaselineEncryptedStoreTests"/>).
    /// </summary>
    private static readonly string TestEncryptionKeyBase64 = Convert.ToBase64String(new byte[32]);

    /// <summary>
    /// Minimum configuration required by <c>AddVeriqanPersistence</c>: the AES-256 key for the
    /// encrypted legal-baseline store.
    /// </summary>
    private IConfiguration BuildTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veriqan:LegalBaseline:EncryptionKey"] = TestEncryptionKeyBase64,
            })
            .Build();

    /// <summary>
    /// Initialises the test class.
    /// <paramref name="fixture"/> is injected by the xUnit v3 assembly-fixture mechanism
    /// declared in <c>AssemblyFixtures.cs</c>.
    /// </summary>
    public MigrateCommandIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = XUnitLogger.CreateLogger<MigrateCommandIntegrationTests>(output);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Happy path: <see cref="MigrateCommand.RunMigrateAsync(string, IConfiguration, CancellationToken)"/>
    /// against an empty isolated database returns exit code 0 and leaves the schema in the
    /// expected state:
    /// <list type="bullet">
    ///   <item><c>veriqan.__EFMigrationsHistory</c> has at least one row.</item>
    ///   <item><c>veriqan.LegalBaselineTolerances</c> has exactly 19 seeded rows.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task RunMigrateAsync_HappyPath_Returns0AndSchemaIsReady()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync("migrate_cmd_happy", ct);
        var config = BuildTestConfiguration();

        _logger.LogInformation(
            "MigrateCommand happy-path test — isolated DB: migrate_cmd_happy.");

        // ── Act ──────────────────────────────────────────────────────────────────
        var exitCode = await MigrateCommand.RunMigrateAsync(connectionString, config, ct);

        // ── Assert: exit code ─────────────────────────────────────────────────────
        _logger.LogInformation("MigrateCommand exit code: {ExitCode}.", exitCode);
        exitCode.ShouldBe(0,
            "MigrateCommand.RunMigrateAsync must return 0 on success.");

        // ── Assert: EF migrations history table exists and has rows ───────────────
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var histCmd = conn.CreateCommand();
        histCmd.CommandText =
            "SELECT COUNT(*) FROM [veriqan].[__EFMigrationsHistory]";
        var histCount = (int)(await histCmd.ExecuteScalarAsync(ct))!;

        _logger.LogInformation(
            "veriqan.__EFMigrationsHistory row count: {Count}.", histCount);
        histCount.ShouldBeGreaterThan(0,
            "At least one EF migration must be recorded in veriqan.__EFMigrationsHistory.");

        // ── Assert: legal-baseline tolerance table seeded with 19 rows ────────────
        await using var seedCmd = conn.CreateCommand();
        seedCmd.CommandText =
            "SELECT COUNT(*) FROM [veriqan].[LegalBaselineTolerances]";
        var seedCount = (int)(await seedCmd.ExecuteScalarAsync(ct))!;

        _logger.LogInformation(
            "veriqan.LegalBaselineTolerances row count: {Count}.", seedCount);
        seedCount.ShouldBe(19,
            "LegalBaselineTolerances must contain exactly 19 seeded rows " +
            "after RunMigrateAsync (see LegalBaselineSeeder.BuildSeedRecords).");

        _logger.LogInformation("MigrateCommand happy-path integration test passed.");
    }

    /// <summary>
    /// Idempotency: calling
    /// <see cref="MigrateCommand.RunMigrateAsync(string, IConfiguration, CancellationToken)"/>
    /// a second time on an already-migrated and seeded database returns 0 and does not
    /// duplicate rows.
    /// </summary>
    [Fact]
    public async Task RunMigrateAsync_Idempotent_Returns0AndDoesNotDuplicateRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync("migrate_cmd_idempotent", ct);
        var config = BuildTestConfiguration();

        // First run
        var first = await MigrateCommand.RunMigrateAsync(connectionString, config, ct);
        first.ShouldBe(0, "First call must return 0.");

        // Second run — must be a no-op (seed is idempotent, migrations already applied)
        var second = await MigrateCommand.RunMigrateAsync(connectionString, config, ct);
        second.ShouldBe(0, "Second call must also return 0 (idempotent).");

        // Row count must not double
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM [veriqan].[LegalBaselineTolerances]";
        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;

        count.ShouldBe(19,
            "LegalBaselineTolerances must still have exactly 19 rows after two RunMigrateAsync calls.");

        _logger.LogInformation(
            "Idempotent migration test passed — {Count} rows after two runs.", count);
    }

    // ── Story 6.8 corrected-design tests: applyMigrationsAndSeed=false path ──────────

    /// <summary>
    /// Verifies the corrected design (part a): when <c>applyMigrationsAndSeed=false</c> is
    /// passed to <see cref="VeriqanDbInitialiser.InitialiseAsync"/> against a database that was
    /// ALREADY migrated and seeded (via <see cref="MigrateCommand.RunMigrateAsync"/>), the
    /// provider cache is still warmed — <see cref="SqlLegalToleranceProvider.IsWarm"/> must be
    /// <c>true</c> after the call, and DDL is NOT re-applied.
    /// </summary>
    /// <remarks>
    /// This proves the host-startup path when CI has already run <c>--migrate</c>: the hosted
    /// service skips migrate+seed but still warms the cache so <c>For(checkId)</c> does not throw.
    /// </remarks>
    [Fact]
    public async Task InitialiseAsync_FalseFlagOnPremigratedDb_WarmsProviderCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "init_false_premigrated", ct);
        var config = BuildTestConfiguration();

        // ── Step 1: pre-migrate + seed via MigrateCommand (simulates CI `--migrate` step) ─
        var exitCode = await MigrateCommand.RunMigrateAsync(connectionString, config, ct);
        exitCode.ShouldBe(0, "Pre-migration step must succeed.");

        // ── Step 2: simulate host boot with applyMigrationsAndSeed=false ─────────────────
        // Build a minimal DI container (same pattern as MigrateCommand uses internally).
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(lb => lb.AddProvider(new XUnitLoggerProvider(_output)));
        services.AddVeriqanPersistence(connectionString);

        await using var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var provider = sp.GetRequiredService<SqlLegalToleranceProvider>();

        provider.IsWarm.ShouldBeFalse("Provider must be cold before InitialiseAsync.");

        // Act: warm-only (skip DDL)
        await VeriqanDbInitialiser.InitialiseAsync(
            scopeFactory,
            provider,
            applyMigrationsAndSeed: false,
            cancellationToken: ct);

        // ── Assert: cache is warm despite skipping DDL ────────────────────────────────────
        provider.IsWarm.ShouldBeTrue(
            "SqlLegalToleranceProvider must be warm after InitialiseAsync " +
            "even when applyMigrationsAndSeed=false (cache warming is unconditional).");

        // Spot-check: For() must not throw
        var tol = provider.For("CL-21");
        tol.LegalDefault.ShouldBe(0.50m,
            "For(CL-21) must return the seeded legal-default tolerance after warming.");

        _logger.LogInformation(
            "applyMigrationsAndSeed=false on pre-migrated DB: provider is warm. Test passed.");
    }

    /// <summary>
    /// Verifies the corrected design (part b): when <c>applyMigrationsAndSeed=false</c> is
    /// passed against an EMPTY database (no migration, no seed), the fail-loud contract holds —
    /// <see cref="VeriqanDbInitialiser.InitialiseAsync"/> throws because the tolerance table
    /// does not exist / is empty.
    /// </summary>
    /// <remarks>
    /// This is the safety net: if CI forgets to run <c>--migrate</c>, host startup fails loudly
    /// rather than silently serving cold-cache data.
    /// </remarks>
    [Fact]
    public async Task InitialiseAsync_FalseFlagOnEmptyDb_ThrowsFailLoud()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "init_false_empty", ct);
        var config = BuildTestConfiguration();

        // Build a minimal DI container — no MigrateCommand called, so the DB is empty.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(lb => lb.AddProvider(new XUnitLoggerProvider(_output)));
        services.AddVeriqanPersistence(connectionString);

        await using var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var provider = sp.GetRequiredService<SqlLegalToleranceProvider>();

        // Act + Assert: must throw because the schema doesn't exist.
        // (SqlException or InvalidOperationException depending on whether the table is absent
        //  vs. present-but-empty — either way it propagates as a non-OCE exception.)
        await Should.ThrowAsync<Exception>(async () =>
            await VeriqanDbInitialiser.InitialiseAsync(
                scopeFactory,
                provider,
                applyMigrationsAndSeed: false,
                cancellationToken: ct));

        provider.IsWarm.ShouldBeFalse(
            "Provider must remain cold when InitialiseAsync throws on an empty DB.");

        _logger.LogInformation(
            "applyMigrationsAndSeed=false on empty DB: fail-loud contract verified. Test passed.");
    }
}
