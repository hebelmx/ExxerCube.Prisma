using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Veriqan.Application.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan application
/// ingestion services.
/// </summary>
public static class VeriqanIngestionExtensions
{
    /// <summary>
    /// Registers <see cref="IStatementIngestionService"/> and its dependencies into the
    /// service collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method registers the application-layer service only. Callers must also register
    /// an <see cref="IVerificationJobRepository"/> implementation (typically via
    /// <c>AddVeriqanPersistence</c> from the Infrastructure layer) and a <see cref="TimeProvider"/>
    /// (defaults to <see cref="TimeProvider.System"/> if not already registered).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanIngestion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register TimeProvider.System as a default fallback when nothing else provides it.
        // Production hosts typically register a concrete TimeProvider; this ensures the service
        // can resolve without requiring callers to explicitly register it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IStatementIngestionService, StatementIngestionService>();

        return services;
    }
}
