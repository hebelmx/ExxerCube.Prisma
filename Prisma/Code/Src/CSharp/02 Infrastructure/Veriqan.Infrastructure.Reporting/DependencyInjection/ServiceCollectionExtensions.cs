using System;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;

/// <summary>
/// Registers Veriqan reporting services (marked-PDF generator, and future story-7.3 email alert).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IMarkedPdfGenerator"/> (and, in Story 7.3, the email-alert service)
    /// to the DI container.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same <paramref name="services"/> for method chaining.</returns>
    public static IServiceCollection AddVeriqanReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMarkedPdfGenerator, MarkedPdfGenerator>();

        return services;
    }
}
