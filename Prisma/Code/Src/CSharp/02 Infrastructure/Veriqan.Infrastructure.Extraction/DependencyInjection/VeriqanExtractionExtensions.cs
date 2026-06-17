using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for the Veriqan extraction infrastructure.
/// </summary>
public static class VeriqanExtractionExtensions
{
    /// <summary>
    /// Registers the PdfPig-based <see cref="IStatementFieldExtractor"/> implementation
    /// and any supporting services required by the Veriqan extraction pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVeriqanExtraction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStatementFieldExtractor, PdfPigStatementFieldExtractor>();

        return services;
    }
}
