using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Services;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan persistence services.
/// </summary>
public static class VeriqanPersistenceExtensions
{
    /// <summary>
    /// Registers <see cref="VeriqanDbContext"/> and related persistence services for the Veriqan
    /// compliance-verification subsystem, and replaces the default in-code
    /// <see cref="ILegalToleranceProvider"/> with the encrypted SQL-backed
    /// <see cref="SqlLegalToleranceProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The EF Core migrations-history table is placed in the <c>veriqan</c> SQL schema
    /// (<c>veriqan.__EFMigrationsHistory</c>) so it never collides with <c>PrismaDbContext</c>'s
    /// history table in <c>dbo</c>.
    /// </para>
    /// <para>
    /// <b>Provider lifetime:</b> <see cref="SqlLegalToleranceProvider"/> is registered as a
    /// <b>singleton</b> — it populates its in-memory cache once at startup and then serves
    /// synchronous <c>For</c>/<c>Has</c> reads for the process lifetime. Because
    /// <see cref="ILegalBaselineStore"/> is scoped (EF Core context), the provider receives
    /// an <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/> and
    /// creates a short-lived scope only during <see cref="SqlLegalToleranceProvider.InitialiseAsync"/>.
    /// </para>
    /// <para>
    /// <b>Fail-loud startup:</b> <c>VeriqanLegalBaselineStartupService</c> (registered by the
    /// Orchestration layer via <c>AddVeriqan</c>) runs at host startup, applies migrations,
    /// seeds the tolerance table, and calls <see cref="SqlLegalToleranceProvider.InitialiseAsync"/>.
    /// If any of these steps fail the service throws, aborting host startup — the encrypted store
    /// must NEVER silently fall back to the in-code constants.
    /// </para>
    /// <para>
    /// When this method is NOT called (pure unit tests, in-memory composition),
    /// <c>AddVeriqanValidation</c> alone registers <c>DefaultLegalToleranceProvider</c> and
    /// no SQL provider or startup service is involved.
    /// </para>
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
        services.AddScoped<IVerdictPersistenceService, EfVerdictPersistenceService>();
        services.AddScoped<IJobVerdictAlertRepository, EfJobVerdictAlertRepository>();

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

        // Legal-baseline store — scoped because it depends on the scoped VeriqanDbContext.
        services.AddScoped<ILegalBaselineStore, SqlLegalBaselineStore>();

        // SQL tolerance provider — singleton.  It takes IServiceScopeFactory so it can resolve
        // ILegalBaselineStore (scoped) in a short-lived scope during InitialiseAsync only.
        // RegisterAsSelf so the startup service can receive the concrete type directly.
        services.AddSingleton<SqlLegalToleranceProvider>();

        // Replace the DefaultLegalToleranceProvider singleton that AddVeriqanValidation registered
        // with the SQL-backed singleton.  Rules and the resolver both inject ILegalToleranceProvider,
        // so this single Replace makes the encrypted store the live implementation.
        services.Replace(ServiceDescriptor.Singleton<ILegalToleranceProvider>(
            sp => sp.GetRequiredService<SqlLegalToleranceProvider>()));

        // NOTE: the startup hosted service (VeriqanLegalBaselineStartupService) is registered
        // by the Orchestration layer (AddVeriqan / AddVeriqanPersistenceStartup) because the
        // Persistence project does not reference Microsoft.Extensions.Hosting.Abstractions.
        // Callers that embed AddVeriqanPersistence directly (integration tests) must either
        // call SqlLegalToleranceProvider.InitialiseAsync themselves or call the startup hook.
        //
        // NOTE: IVerificationResultStore / IReprocessAuditRepository (durable EF stores, Story 6.1)
        // are also registered by the Orchestration layer (VeriqanOrchestrationExtensions.AddVeriqan)
        // because those interfaces are defined in the Orchestration assembly, and registering them
        // from Persistence would create a circular project reference.

        return services;
    }
}
