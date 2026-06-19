using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for the Veriqan extraction infrastructure.
/// </summary>
public static class VeriqanExtractionExtensions
{
    /// <summary>
    /// Registers the PdfPig-based <see cref="IStatementFieldExtractor"/> implementation,
    /// the <see cref="PdfExtractionOptions"/> configuration, the <see cref="IPasswordProvider"/>
    /// (null no-op when no password configuration is present), and any supporting services
    /// required by the Veriqan extraction pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">
    /// Optional application configuration used to bind <c>Veriqan:PdfExtraction</c> options.
    /// When <see langword="null"/> the defaults defined in <see cref="PdfExtractionOptions"/>
    /// are used (50 MB limit, 30 s timeout).
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVeriqanExtraction(
        this IServiceCollection services,
        IConfiguration? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Bind PDF-extraction options from configuration (or use defaults when config is absent).
        if (config is not null)
        {
            services.Configure<PdfExtractionOptions>(
                config.GetSection(PdfExtractionOptions.Section));
        }
        else
        {
            services.Configure<PdfExtractionOptions>(_ => { /* defaults applied by class initialiser */ });
        }

        // Password provider — null (no-op) unless a custom implementation is registered before
        // AddVeriqanExtraction is called (TryAdd semantics: first registration wins).
        services.TryAddSingleton<IPasswordProvider, NullPasswordProvider>();

        services.AddSingleton<IStatementFieldExtractor, PdfPigStatementFieldExtractor>();

        return services;
    }
}
