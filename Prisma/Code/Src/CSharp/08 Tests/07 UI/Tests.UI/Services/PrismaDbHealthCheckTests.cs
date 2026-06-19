using ExxerCube.Prisma.Web.UI.Data;
using ExxerCube.Prisma.Web.UI.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MsHealthChecks = Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExxerCube.Prisma.Tests.UI.Services;

/// <summary>
/// Unit tests for <see cref="PrismaDbHealthCheck"/> (MVP-PATH E1-S4).
/// </summary>
/// <remarks>
/// Uses an EF Core In-Memory database to avoid requiring a live SQL Server.
/// Tests validate the two critical paths:
/// - Returns <see cref="MsHealthChecks.HealthCheckResult.Healthy"/> when the database is reachable.
/// - Returns <see cref="MsHealthChecks.HealthCheckResult.Unhealthy"/> when the factory throws.
/// </remarks>
public sealed class PrismaDbHealthCheckTests
{
    private static MsHealthChecks.HealthCheckContext CreateContext() =>
        new()
        {
            Registration = new MsHealthChecks.HealthCheckRegistration(
                "prisma-db",
                Substitute.For<MsHealthChecks.IHealthCheck>(),
                failureStatus: MsHealthChecks.HealthStatus.Unhealthy,
                tags: null)
        };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckHealthAsync_WhenDatabaseReachable_ReturnsHealthy()
    {
        // Arrange: In-Memory provider always reports CanConnectAsync = true.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PrismaHealthTest_{Guid.NewGuid()}")
            .Options;
        var factory = new TestDbContextFactory(options);
        var check = new PrismaDbHealthCheck(factory, NullLogger<PrismaDbHealthCheck>.Instance);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(MsHealthChecks.HealthStatus.Healthy,
            "DB health check must return Healthy when the database is reachable.");
        result.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsUnhealthy()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PrismaHealthTest_{Guid.NewGuid()}")
            .Options;
        var factory = new TestDbContextFactory(options);
        var check = new PrismaDbHealthCheck(factory, NullLogger<PrismaDbHealthCheck>.Instance);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), cts.Token);

        // Assert
        result.Status.ShouldBe(MsHealthChecks.HealthStatus.Unhealthy,
            "Cancelled health check must return Unhealthy, not throw OperationCanceledException.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        // Arrange: factory that always throws to simulate an unreachable database.
        var factory = new ThrowingDbContextFactory();
        var check = new PrismaDbHealthCheck(factory, NullLogger<PrismaDbHealthCheck>.Instance);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(MsHealthChecks.HealthStatus.Unhealthy,
            "DB health check must return Unhealthy (never throw) when the factory throws.");
        result.Exception.ShouldNotBeNull("Exception should be captured in the result.");
    }

    // ---------------------------------------------------------------------------
    // Test helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Wraps pre-built <see cref="DbContextOptions{TContext}"/> so we can inject In-Memory contexts
    /// via the <see cref="IDbContextFactory{TContext}"/> interface without a real DI container.
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        internal TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) =>
            _options = options;

        public ApplicationDbContext CreateDbContext() => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    /// <summary>
    /// A factory that always throws <see cref="InvalidOperationException"/> to simulate a
    /// completely unreachable database (no connection string, server offline, etc.).
    /// </summary>
    private sealed class ThrowingDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            throw new InvalidOperationException("Simulated database factory failure");

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated database factory failure");
    }
}
