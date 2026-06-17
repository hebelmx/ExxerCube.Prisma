using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Application.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan context-binding
/// services (FR-3, FR-20; Story 2.3).
/// </summary>
public static class VeriqanBindingExtensions
{
    /// <summary>
    /// Registers <see cref="IProductResolver"/> and <see cref="IBundleBinder"/> into the
    /// service collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers must separately register an <see cref="IVecReferenceDataProvider"/> implementation
    /// (e.g. via <c>AddVeriqanCsvReferenceData</c> from the Infrastructure layer) — this method
    /// does not provide one.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanBinding(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IProductResolver, ProductResolver>();
        services.AddScoped<IBundleBinder, BundleBinder>();

        return services;
    }
}
