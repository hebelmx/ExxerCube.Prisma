using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        services.AddScoped<IDispositionRepository, EfDispositionRepository>();

        // Crypto key provider — reads from configuration key "Veriqan:LegalBaseline:EncryptionKey"
        services.AddSingleton<ILegalBaselineCryptoKeyProvider>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new ConfigurationCryptoKeyProvider(config);
        });

        // AES converter — singleton; shares key-material for the process lifetime.
        // Also registered as the concrete type so VeriqanDbContext can receive it via DI.
        services.AddSingleton(sp =>
        {
            var keyProvider = sp.GetRequiredService<ILegalBaselineCryptoKeyProvider>();
            return new AesEncryptedDecimalConverter(keyProvider.GetKey());
        });

        // Legal-baseline store and SQL tolerance provider
        services.AddScoped<ILegalBaselineStore, SqlLegalBaselineStore>();
        services.AddScoped<SqlLegalToleranceProvider>();

        return services;
    }
}
