// <copyright file="MigrateOnlyPathIntegrationTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Infrastructure.Database.Startup;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Integration tests for the <c>--migrate-only</c> K8s init-container path
/// (<see cref="PrismaDbMigrationRunner"/>).
///
/// Verifies that running <see cref="PrismaDbMigrationRunner.RunMigrationsAsync"/>
/// against a fresh SQL Server database:
/// <list type="bullet">
///   <item>Returns exit code 0.</item>
///   <item>Leaves no pending migrations on <see cref="PrismaDbContext"/>.</item>
/// </list>
///
/// NOTE: Requires Docker (Testcontainers SQL Server).  This test is Docker-deferred on
/// boxes without Docker; it COMPILES cleanly so the build gate is green everywhere.
/// </summary>
public class MigrateOnlyPathIntegrationTests : IAsyncDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateOnlyPathIntegrationTests"/> class.
    /// Creates an isolated database on the shared container so this class runs in parallel
    /// with other test classes without colliding on a shared database.
    /// </summary>
    public MigrateOnlyPathIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.EnsureAvailable();

        // Parallel-safe: each class gets its own isolated DB on the shared container.
        _connectionString = _fixture
            .CreateIsolatedDatabaseAsync(nameof(MigrateOnlyPathIntegrationTests))
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// <c>PrismaDbMigrationRunner.RunMigrationsAsync</c> on a fresh database must return
    /// exit code 0 and leave zero pending migrations on <see cref="PrismaDbContext"/>.
    /// </summary>
    [Fact]
    public async Task RunMigrationsAsync_FreshDatabase_ReturnsZeroAndNoPendingMigrations()
    {
        // Arrange — build a minimal host that registers PrismaDbContext,
        // mirroring what AddDatabaseServices does in the real workers.
        var ct = TestContext.Current.CancellationToken;

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(lb => lb.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddDbContext<PrismaDbContext>(opts =>
                    opts.UseSqlServer(_connectionString));
                // Wire console logging so PrismaDbMigrationRunner can resolve ILogger<T>.
                services.AddLogging(lb => lb.AddConsole());
            })
            .Build();

        // Act
        var exitCode = await PrismaDbMigrationRunner.RunMigrationsAsync(host, ct);

        // Assert: runner reports success
        exitCode.ShouldBe(0, "RunMigrationsAsync must return exit code 0 on a clean database");

        // Assert: no pending migrations remain after the runner ran
        var opts = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        await using var verifyContext = new PrismaDbContext(opts);
        var pending = await verifyContext.Database.GetPendingMigrationsAsync(ct);

        pending.ShouldBeEmpty(
            "after RunMigrationsAsync there should be no pending migrations on PrismaDbContext");
    }

    /// <summary>
    /// <c>PrismaDbMigrationRunner.RunMigrationsAsync</c> on a host that has no
    /// <see cref="PrismaDbContext"/> registered (blank/DEV-PLACEHOLDER connection-string
    /// scenario) must still return exit code 0 (no-op is intentional in dev/test).
    /// </summary>
    [Fact]
    public async Task RunMigrationsAsync_NoContextRegistered_ReturnsZeroNoOp()
    {
        // Arrange — host with NO DbContext registration (simulates blank connection string path)
        var ct = TestContext.Current.CancellationToken;

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(lb => lb.ClearProviders())
            .ConfigureServices(services =>
            {
                // Intentionally NO AddDbContext call — mirrors worker dev/test no-DB path.
                services.AddLogging(lb => lb.AddConsole());
            })
            .Build();

        // Act
        var exitCode = await PrismaDbMigrationRunner.RunMigrationsAsync(host, ct);

        // Assert: no-op returns success, not an error
        exitCode.ShouldBe(0, "no-context runner must return 0 (dev/test no-DB path is valid)");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
