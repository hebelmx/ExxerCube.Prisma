using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.DependencyInjection;

/// <summary>
/// Extension methods for registering VEC reference-data services.
/// </summary>
public static class VeriqanReferenceDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the VEC reference-data schema validator and the default CSV adapter
    /// as <see cref="IVecReferenceDataProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">Optional delegate to configure <see cref="CsvReferenceDataOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVeriqanReferenceData(
        this IServiceCollection services,
        Action<CsvReferenceDataOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ReferenceBundleSchemaValidator>();

        if (configureOptions is not null)
            services.Configure(configureOptions);
        else
            services.Configure<CsvReferenceDataOptions>(_ => { });

        services.AddTransient<IVecReferenceDataProvider, CsvReferenceDataAdapter>();

        return services;
    }
}
