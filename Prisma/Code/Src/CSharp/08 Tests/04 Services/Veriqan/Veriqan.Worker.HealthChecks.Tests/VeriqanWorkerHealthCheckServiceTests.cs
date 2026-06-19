using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Worker.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace ExxerCube.Prisma.Veriqan.Worker.HealthChecks.Tests;

/// <summary>
/// Unit tests for <see cref="VeriqanWorkerHealthCheckService"/>.
/// </summary>
/// <remarks>
/// Three checks are verified at the unit level (no Docker required):
/// <list type="number">
///   <item>SQL connectivity — an unreachable SQL Server yields Unhealthy readiness.</item>
///   <item>CSV reference-data root — a non-existent or empty directory yields Unhealthy.</item>
///   <item>Tolerance-cache warm — an uninitialised <see cref="SqlLegalToleranceProvider"/> yields Unhealthy.</item>
/// </list>
/// </remarks>
public sealed class VeriqanWorkerHealthCheckServiceTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a scope factory that resolves a <see cref="VeriqanDbContext"/> built with the given options.
    /// </summary>
    private static IServiceScopeFactory BuildScopeFactory(DbContextOptions<VeriqanDbContext>? dbContextOptions = null)
    {
        var services = new ServiceCollection();

        if (dbContextOptions is not null)
        {
            // Register the concrete VeriqanDbContext using the provided options.
            services.AddSingleton(dbContextOptions);
            services.AddSingleton<VeriqanDbContext>(sp =>
                new VeriqanDbContext(sp.GetRequiredService<DbContextOptions<VeriqanDbContext>>()));
        }
        // When dbContextOptions is null the container has no VeriqanDbContext registration,
        // so GetService<VeriqanDbContext>() returns null → service treats it as in-memory path.

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>
    /// Builds <see cref="CsvReferenceDataOptions"/> pointing to <paramref name="rootDirectory"/>.
    /// </summary>
    private static IOptions<CsvReferenceDataOptions> CsvOptions(string rootDirectory) =>
        new OptionsWrapper<CsvReferenceDataOptions>(
            new CsvReferenceDataOptions { RootDirectory = rootDirectory });

    /// <summary>
    /// Creates a <see cref="SqlLegalToleranceProvider"/> whose cache has been warmed
    /// by calling <see cref="SqlLegalToleranceProvider.InitialiseAsync"/> with a
    /// mock <see cref="ILegalBaselineStore"/> that returns one tolerance record.
    /// </summary>
    private static async Task<SqlLegalToleranceProvider> BuildWarmProviderAsync()
    {
        var store = Substitute.For<ILegalBaselineStore>();
        store.LoadTolerancesAsync(Arg.Any<CancellationToken>())
             .Returns(new Dictionary<string, Tolerance>
             {
                 // Tolerance(legalDefault, min, max) — min ≤ legalDefault ≤ max
                 ["CL-10"] = new Tolerance(legalDefault: 0.01m, min: 0m, max: 0.05m)
             });

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var provider = new SqlLegalToleranceProvider(scopeFactory);
        await provider.InitialiseAsync(CancellationToken.None);
        return provider;
    }

    // ── Test 1: SQL connectivity — unreachable server → Unhealthy ───────────

    /// <summary>
    /// When the SQL Server is unreachable (port 1 on loopback — always refused),
    /// <see cref="VeriqanWorkerHealthCheckService.GetReadinessAsync"/> must return
    /// <see cref="OrchestratorHealthState.Unhealthy"/> and the <c>sqlConnectivity</c>
    /// detail must be <c>false</c>.
    /// This is the canonical proof that a missing/invalid connection string surfaces as a 503.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenSqlUnreachable_ReturnsUnhealthy()
    {
        // Arrange — connect to 127.0.0.1 port 1 (always refused) with minimum timeout
        var dbOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                "Data Source=127.0.0.1,1;Initial Catalog=test;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True",
                b => b.CommandTimeout(2))
            .Options;

        var scopeFactory = BuildScopeFactory(dbOptions);
        var csvDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(csvDir);
        File.WriteAllText(Path.Combine(csvDir, "dummy.csv"), "data");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: null,   // in-memory path → skip cache-warm check
            csvOptions: CsvOptions(csvDir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
            result.Data.ShouldNotBeNull();
            result.Data["sqlConnectivity"].ShouldBe(false);
        }
        finally
        {
            Directory.Delete(csvDir, recursive: true);
        }
    }

    // ── Test 2: CSV reference-data root — missing/empty directory → Unhealthy ─

    /// <summary>
    /// When <c>CsvReferenceData:RootDirectory</c> points to a non-existent path,
    /// readiness must be <see cref="OrchestratorHealthState.Unhealthy"/> and
    /// <c>csvReferenceDataRoot</c> detail must be <c>false</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenCsvRootMissing_ReturnsUnhealthy()
    {
        // Arrange — no VeriqanDbContext in scope → SQL check skipped (returns true)
        var scopeFactory = BuildScopeFactory();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: null,
            csvOptions: CsvOptions(nonExistentPath),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        // Act
        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
        result.Data.ShouldNotBeNull();
        result.Data["csvReferenceDataRoot"].ShouldBe(false);
    }

    /// <summary>
    /// When <c>CsvReferenceData:RootDirectory</c> exists but is empty,
    /// readiness must be <see cref="OrchestratorHealthState.Unhealthy"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenCsvRootEmpty_ReturnsUnhealthy()
    {
        // Arrange
        var scopeFactory = BuildScopeFactory();
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: null,
            csvOptions: CsvOptions(emptyDir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
            result.Data.ShouldNotBeNull();
            result.Data["csvReferenceDataRoot"].ShouldBe(false);
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }

    /// <summary>
    /// When <c>CsvReferenceData:RootDirectory</c> exists and contains at least one file,
    /// the CSV check passes.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenCsvRootNonEmpty_CsvCheckPasses()
    {
        // Arrange
        var scopeFactory = BuildScopeFactory();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bundle-metadata.csv"), "header\ndata");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: null,
            csvOptions: CsvOptions(dir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert — all three checks pass when SQL + CSV + cache all report healthy
            result.Data.ShouldNotBeNull();
            result.Data["csvReferenceDataRoot"].ShouldBe(true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Test 3: Tolerance-cache warm — uninitialised provider → Unhealthy ─────

    /// <summary>
    /// When <see cref="SqlLegalToleranceProvider.InitialiseAsync"/> has NOT been called
    /// (<see cref="SqlLegalToleranceProvider.IsWarm"/> is <c>false</c>),
    /// readiness must be <see cref="OrchestratorHealthState.Unhealthy"/> and
    /// <c>toleranceCacheWarm</c> detail must be <c>false</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenToleranceCacheNotWarm_ReturnsUnhealthy()
    {
        // Arrange — build an uninitialised provider (IsWarm = false)
        var scopeFactory = BuildScopeFactory();
        var coldProvider = new SqlLegalToleranceProvider(scopeFactory);
        coldProvider.IsWarm.ShouldBeFalse("provider must be cold before InitialiseAsync");

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bundle-metadata.csv"), "header\ndata");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: coldProvider,
            csvOptions: CsvOptions(dir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
            result.Data.ShouldNotBeNull();
            result.Data["toleranceCacheWarm"].ShouldBe(false);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// When <see cref="SqlLegalToleranceProvider.InitialiseAsync"/> has completed successfully
    /// (<see cref="SqlLegalToleranceProvider.IsWarm"/> is <c>true</c>),
    /// the cache-warm check passes.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenToleranceCacheWarm_CacheCheckPasses()
    {
        // Arrange — build a warm provider
        var warmProvider = await BuildWarmProviderAsync();
        warmProvider.IsWarm.ShouldBeTrue("provider should be warm after InitialiseAsync");

        var scopeFactory = BuildScopeFactory();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bundle-metadata.csv"), "header\ndata");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: warmProvider,
            csvOptions: CsvOptions(dir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Data.ShouldNotBeNull();
            result.Data["toleranceCacheWarm"].ShouldBe(true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── AC: no durable persistence (in-memory fallback) → NOT ready (503) ───────

    /// <summary>
    /// The VERIQAN-E1-S7 evidence AC (owner ruling 2026-06-19): a worker started WITHOUT a
    /// SQL connection string runs on the in-memory fallback (no <see cref="VeriqanDbContext"/>
    /// registered) and must report readiness <see cref="OrchestratorHealthState.Unhealthy"/>
    /// even when CSV + cache are fine — durable persistence is required to take traffic.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenNoDurablePersistenceConfigured_ReturnsUnhealthy()
    {
        // Arrange — no VeriqanDbContext registered (in-memory path); CSV + cache otherwise healthy
        var scopeFactory = BuildScopeFactory();
        var warmProvider = await BuildWarmProviderAsync();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bundle-metadata.csv"), "header\ndata");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: warmProvider,
            csvOptions: CsvOptions(dir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert — in-memory fallback is NOT ready, driven by the SQL persistence check
            result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
            result.Data.ShouldNotBeNull();
            result.Data["sqlConnectivity"].ShouldBe(false);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Happy path: all three checks pass → Ready (200) ─────────────────────────

    /// <summary>
    /// When a connectable <see cref="VeriqanDbContext"/> is registered (in-memory EF provider —
    /// <c>CanConnectAsync</c> true), the tolerance cache is warm, and the CSV root is non-empty,
    /// readiness must be <see cref="OrchestratorHealthState.Healthy"/>. Proves the 200 path is
    /// reachable (readiness is not vacuously always-503).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenAllChecksPass_ReturnsHealthy()
    {
        // Arrange — in-memory EF provider: CanConnectAsync returns true
        var dbOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseInMemoryDatabase($"hc-ready-{Guid.NewGuid()}")
            .Options;
        var scopeFactory = BuildScopeFactory(dbOptions);
        var warmProvider = await BuildWarmProviderAsync();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bundle-metadata.csv"), "header\ndata");

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: warmProvider,
            csvOptions: CsvOptions(dir),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        try
        {
            // Act
            var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

            // Assert — all three checks pass → ready
            result.Status.ShouldBe(OrchestratorHealthState.Healthy);
            result.Data.ShouldNotBeNull();
            result.Data["sqlConnectivity"].ShouldBe(true);
            result.Data["csvReferenceDataRoot"].ShouldBe(true);
            result.Data["toleranceCacheWarm"].ShouldBe(true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Liveness always Healthy ────────────────────────────────────────────────

    /// <summary>
    /// Liveness must always return <see cref="OrchestratorHealthState.Healthy"/>
    /// regardless of external dependency state.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_Always_ReturnsHealthy()
    {
        var scopeFactory = BuildScopeFactory();

        var service = new VeriqanWorkerHealthCheckService(
            scopeFactory,
            toleranceProvider: null,
            csvOptions: CsvOptions(string.Empty),
            logger: NullLogger<VeriqanWorkerHealthCheckService>.Instance);

        var result = await service.GetLivenessAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(OrchestratorHealthState.Healthy);
        result.Description.ShouldContain("running", Case.Insensitive);
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("timestamp");
    }
}
