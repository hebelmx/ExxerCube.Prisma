using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Prisma.Tests.System.E2E.Infrastructure;

/// <summary>
/// Provides an in-memory database for E2E testing.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    private readonly string _databaseName;

    /// <summary>
    /// Creates a test database with a unique name.
    /// </summary>
    public TestDatabaseFixture()
    {
        _databaseName = $"TestDb_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates DbContextOptions for an in-memory database.
    /// </summary>
    public DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(_databaseName)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
    }

    /// <summary>
    /// Connection string for test database (if using real SQL Server via Testcontainers).
    /// </summary>
    public string ConnectionString => $"Data Source=:memory:;";

    public void Dispose()
    {
        // In-memory databases are automatically cleaned up
        // If using Testcontainers, dispose container here
    }
}

/// <summary>
/// Helper for creating test service providers with in-memory database.
/// </summary>
public static class TestServiceProviderFactory
{
    /// <summary>
    /// Creates a service collection configured for E2E testing.
    /// </summary>
    public static IServiceCollection CreateTestServices()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
        });

        // Add in-memory database
        services.AddDbContext<ExxerCube.Prisma.Infrastructure.Database.EntityFramework.PrismaDbContext>(options =>
        {
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid():N}");
            options.EnableSensitiveDataLogging();
        });

        return services;
    }

    /// <summary>
    /// Creates a configured service provider for E2E tests.
    /// </summary>
    public static IServiceProvider BuildTestServiceProvider(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = CreateTestServices();

        // Allow caller to add additional services
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
