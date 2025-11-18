using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Export;

namespace ExxerCube.Prisma.Infrastructure.Export.DependencyInjection;

/// <summary>
/// Extension methods for configuring export services dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds export services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for certificate options).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExportServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Register specialized exporters
        services.AddScoped<SiroXmlExporter>();
        services.AddScoped<DigitalPdfSigner>();

        // Register composite exporter that delegates to specialized exporters
        services.AddScoped<IResponseExporter, CompositeResponseExporter>();

        // Register other export services
        services.AddScoped<ILayoutGenerator, ExcelLayoutGenerator>();
        services.AddScoped<ICriterionMapper, CriterionMapperService>();

        // Register PDF summarization service
        services.AddScoped<IPdfRequirementSummarizer, PdfRequirementSummarizerService>();

        // Configure certificate options
        services.Configure<CertificateOptions>(options =>
        {
            if (configuration != null)
            {
                var certificateSection = configuration.GetSection(CertificateOptions.SectionName);
                if (certificateSection.Exists())
                {
                    certificateSection.Bind(options);
                }
            }
        });

        return services;
    }
}

