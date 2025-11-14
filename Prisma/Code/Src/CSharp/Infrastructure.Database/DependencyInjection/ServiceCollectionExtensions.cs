using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

namespace ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;

/// <summary>
/// Extension methods for configuring database dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<PrismaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IDownloadTracker, DownloadTrackerService>();
        services.AddScoped<IFileMetadataLogger, FileMetadataLoggerService>();

        return services;
    }
}

