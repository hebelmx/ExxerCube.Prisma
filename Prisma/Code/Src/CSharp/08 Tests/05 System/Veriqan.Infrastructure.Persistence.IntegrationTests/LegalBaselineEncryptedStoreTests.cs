using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for the encrypted legal-baseline tolerance store (Story 9.3a Part B).
/// Verifies that:
/// <list type="bullet">
///   <item>Seeded tolerances round-trip through the SQL store correctly (seed then read back via <c>SqlLegalBaselineStore</c>).</item>
///   <item>The raw column values are ciphertext (not plaintext) — proving encryption-at-rest.</item>
///   <item><c>SqlLegalToleranceProvider</c> answers <c>For</c>/<c>Has</c> correctly after <c>InitialiseAsync</c>.</item>
/// </list>
/// </summary>
public sealed class LegalBaselineEncryptedStoreTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<LegalBaselineEncryptedStoreTests> _logger;

    /// <summary>A fixed 32-byte test key (32 zero-bytes).</summary>
    private static readonly byte[] TestKey = new byte[32];

    /// <summary>
    /// Initializes the test class.
    /// </summary>
    public LegalBaselineEncryptedStoreTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = XUnitLogger.CreateLogger<LegalBaselineEncryptedStoreTests>(output);
    }

    /// <summary>
    /// End-to-end test: seed → read-back round-trip is correct, raw SQL column is ciphertext,
    /// and <c>SqlLegalToleranceProvider</c> serves values correctly after initialisation.
    /// </summary>
    [Fact]
    public async Task SeedAndRead_TolerancesRoundTrip_AndRawColumnsAreCiphertext()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── Arrange: isolated database + VeriqanDbContext with real AES key ─────────────
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "veriqan_baseline_enc", ct);

        _logger.LogInformation("Isolated DB connection string obtained.");

        var converter = new AesEncryptedDecimalConverter(TestKey);

        var veriqanOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        // Apply migrations (creates the LegalBaselineTolerances table)
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await ctx.Database.MigrateAsync(ct);
            _logger.LogInformation("Migration applied.");
        }

        // ── Act: seed ──────────────────────────────────────────────────────────────────
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await LegalBaselineSeeder.SeedAsync(ctx, ct);
            _logger.LogInformation("Seeded.");
        }

        // ── Assert 1: SqlLegalBaselineStore round-trips values ─────────────────────────
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            var store = new SqlLegalBaselineStore(ctx);
            var tolerances = await store.LoadTolerancesAsync(ct);

            // Must have all 19 tolerance-bearing rules (Epic 11 added the 5 LAW-§ recompute rules)
            tolerances.Count.ShouldBe(19,
                $"Expected 19 seed records " +
                $"(CL-10/17/18/19/20/21/22/24/25/44/ITEM-58/CL-39/CL-37/CL-36 + " +
                $"LAW-§20-WATERFALL/§19-INTERES/§6-SIMULACION/§8-INDICADORES/§16-OTRASLINEAS). " +
                $"Found {tolerances.Count}.");

            // Spot-check CL-10 (CurrencyMxn)
            tolerances.ShouldContainKey("CL-10");
            var cl10 = tolerances["CL-10"];
            cl10.LegalDefault.ShouldBe(0.50m);
            cl10.Min.ShouldBe(0.00m);
            cl10.Max.ShouldBe(1.00m);

            // Spot-check CL-39 (Points)
            tolerances.ShouldContainKey("CL-39");
            var cl39 = tolerances["CL-39"];
            cl39.LegalDefault.ShouldBe(1.00m);
            cl39.Min.ShouldBe(0.00m);
            cl39.Max.ShouldBe(2.00m);

            // Spot-check CL-37 (ExchangeRate)
            tolerances.ShouldContainKey("CL-37");
            var cl37 = tolerances["CL-37"];
            cl37.LegalDefault.ShouldBe(0.10m);
            cl37.Min.ShouldBe(0.05m);
            cl37.Max.ShouldBe(0.20m);

            // Spot-check CL-36 (RewardsPesosMxn)
            tolerances.ShouldContainKey("CL-36");
            var cl36 = tolerances["CL-36"];
            cl36.LegalDefault.ShouldBe(1.00m);
            cl36.Min.ShouldBe(0.00m);
            cl36.Max.ShouldBe(2.00m);

            _logger.LogInformation("Round-trip assertions passed for all 19 records.");
        }

        // ── Assert 2: Raw column values are ciphertext (not plaintext decimal) ──────────
        // Read the raw nvarchar values directly from SQL to prove they are NOT plain numbers.
        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(ct);

        await using var cmd = sqlConn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 LegalDefault FROM [veriqan].[LegalBaselineTolerances]";
        var rawValue = (await cmd.ExecuteScalarAsync(ct))?.ToString();

        _logger.LogInformation("Raw LegalDefault column value length: {Len}", rawValue?.Length ?? -1);

        rawValue.ShouldNotBeNull("Expected a row in LegalBaselineTolerances after seeding.");

        // The raw value should NOT be a plain decimal string like "0.50"
        var isPlainDecimal = decimal.TryParse(rawValue, out _);
        isPlainDecimal.ShouldBeFalse(
            $"Raw column value parses as a plain decimal — it should be " +
            "AES ciphertext (Base64-encoded). Encryption is not working.");

        // It should be valid Base64
        var isBase64 = IsValidBase64(rawValue);
        isBase64.ShouldBeTrue(
            "Raw column value is not valid Base64. " +
            "The AES converter should produce Base64-encoded ciphertext.");

        _logger.LogInformation("Ciphertext assertion passed — raw column is NOT plaintext decimal.");

        // ── Assert 3: SqlLegalToleranceProvider answers For/Has correctly ──────────────
        // Provider is now a singleton that takes IServiceScopeFactory.  Wire a minimal
        // service provider so the scope factory can resolve ILegalBaselineStore (scoped).
        {
            var testServices = new ServiceCollection();
            testServices.AddDbContext<VeriqanDbContext>(o =>
                o.UseSqlServer(connectionString,
                    b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));
            testServices.AddSingleton(converter);
            testServices.AddScoped<ILegalBaselineStore, SqlLegalBaselineStore>();

            await using var testSp = testServices.BuildServiceProvider();
            var provider = new SqlLegalToleranceProvider(testSp.GetRequiredService<IServiceScopeFactory>());
            await provider.InitialiseAsync(ct);

            provider.Has("CL-10").ShouldBeTrue();
            provider.Has("CL-99").ShouldBeFalse();

            var tol = provider.For("CL-21");
            tol.LegalDefault.ShouldBe(0.50m);

            _logger.LogInformation("SqlLegalToleranceProvider For/Has assertions passed.");
        }

        _logger.LogInformation("All encrypted store assertions passed.");
    }

    /// <summary>
    /// Verifies that seeding is idempotent: running <see cref="LegalBaselineSeeder.SeedAsync"/>
    /// twice does not duplicate rows or throw.
    /// </summary>
    [Fact]
    public async Task SeedAsync_CalledTwice_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "veriqan_baseline_idempotent", ct);

        var converter = new AesEncryptedDecimalConverter(TestKey);

        var veriqanOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await ctx.Database.MigrateAsync(ct);
        }

        // Seed twice
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await LegalBaselineSeeder.SeedAsync(ctx, ct);
        }

        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await LegalBaselineSeeder.SeedAsync(ctx, ct); // should be a no-op
        }

        // Verify count is still 19 (no duplicates)
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            var count = await ctx.LegalBaselineTolerances.CountAsync(ct);
            count.ShouldBe(19, $"Expected exactly 19 rows after two seed calls. Found {count}.");
        }

        _logger.LogInformation("Idempotency assertion passed.");
    }

    /// <summary>
    /// Fix 3.B: <see cref="SqlLegalBaselineStore.LoadTolerancesAsync"/> must propagate
    /// cancellation (throw <see cref="OperationCanceledException"/>) rather than swallowing
    /// a pre-cancelled token and returning an empty dictionary.
    /// </summary>
    [Fact]
    public async Task SqlLegalBaselineStore_LoadTolerancesAsync_PropagatesCancellation()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "veriqan_cancel_test", ct);

        var converter = new AesEncryptedDecimalConverter(TestKey);

        var veriqanOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await ctx.Database.MigrateAsync(ct);
        }

        // Pre-cancel before calling LoadTolerancesAsync — must throw, not return empty dict.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var ctx2 = new VeriqanDbContext(veriqanOptions, converter);
        var store = new SqlLegalBaselineStore(ctx2);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.LoadTolerancesAsync(cts.Token));

        _logger.LogInformation(
            "Cancellation-propagation assertion passed — store throws OperationCanceledException.");
    }

    /// <summary>
    /// Fix A: <see cref="SqlLegalToleranceProvider.InitialiseAsync"/> must throw
    /// <see cref="InvalidOperationException"/> when the loaded cache is empty (DB migrated but
    /// NOT seeded) — proving the fail-loud contract is enforced.
    /// </summary>
    [Fact]
    public async Task SqlLegalToleranceProvider_InitialiseAsync_ThrowsWhenCacheIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "veriqan_empty_cache_test", ct);

        var converter = new AesEncryptedDecimalConverter(TestKey);

        var veriqanOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        // Apply migrations — but do NOT seed, so the table is empty.
        await using (var ctx = new VeriqanDbContext(veriqanOptions, converter))
        {
            await ctx.Database.MigrateAsync(ct);
        }

        // Build a minimal service provider to supply IServiceScopeFactory.
        var services = new ServiceCollection();
        services.AddDbContext<VeriqanDbContext>(o =>
            o.UseSqlServer(connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));
        services.AddSingleton(converter);
        services.AddScoped<ILegalBaselineStore, SqlLegalBaselineStore>();

        await using var sp = services.BuildServiceProvider();
        var provider = new SqlLegalToleranceProvider(sp.GetRequiredService<IServiceScopeFactory>());

        // Empty cache after migration (no seed) → must throw InvalidOperationException.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await provider.InitialiseAsync(ct));

        _logger.LogInformation(
            "Fail-loud assertion passed — InitialiseAsync throws when cache is empty after migration.");
    }

    private static bool IsValidBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s.Length % 4 != 0) return false;
        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
