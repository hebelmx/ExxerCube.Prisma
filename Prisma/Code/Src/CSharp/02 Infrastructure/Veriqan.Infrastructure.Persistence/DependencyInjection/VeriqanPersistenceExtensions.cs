using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan persistence services.
/// </summary>
public static class VeriqanPersistenceExtensions
{
    /// <summary>
    /// Registers <see cref="VeriqanDbContext"/> and related persistence services for the Veriqan
    /// compliance-verification subsystem.
    /// </summary>
    /// <remarks>
    /// The EF Core migrations-history table is placed in the <c>veriqan</c> SQL schema
    /// (<c>veriqan.__EFMigrationsHistory</c>) so it never collides with <c>PrismaDbContext</c>'s
    /// history table in <c>dbo</c>.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">SQL Server connection string for the Veriqan database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<VeriqanDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));

        services.AddScoped<IVerificationJobRepository, EfVerificationJobRepository>();

        return services;
    }
}
