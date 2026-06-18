using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Veriqan.Application.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering the Veriqan human
/// disposition application service (FR-18, AR-9).
/// </summary>
public static class VeriqanDispositionExtensions
{
    /// <summary>
    /// Registers <see cref="IDispositionService"/> and its dependencies into the service
    /// collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers must also register an <see cref="IDispositionRepository"/> implementation
    /// (provided by <c>AddVeriqanPersistence</c> in the Infrastructure layer) and a
    /// <see cref="TimeProvider"/> (defaults to <see cref="TimeProvider.System"/> when not
    /// already registered).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanDisposition(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDispositionService, DispositionService>();

        return services;
    }
}
