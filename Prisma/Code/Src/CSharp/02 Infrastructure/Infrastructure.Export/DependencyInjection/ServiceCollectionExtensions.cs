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
    /// Adds the SIRO XML export services to the service collection (minimal, no
    /// <see cref="IPdfRequirementSummarizer"/> / <see cref="IMetadataExtractor"/> dependency).
    /// Use this in worker hosts that run the SIRO export stage (Reconciliator).
    /// </summary>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="SiroXmlExporter"/> (concrete — consumed by <see cref="CompositeResponseExporter"/>)</item>
    ///   <item><see cref="DigitalPdfSigner"/> (concrete)</item>
    ///   <item><see cref="IResponseExporter"/> → <see cref="CompositeResponseExporter"/></item>
    ///   <item><see cref="ILayoutGenerator"/> → <see cref="ExcelLayoutGenerator"/></item>
    ///   <item><see cref="ICriterionMapper"/> → <see cref="CriterionMapperService"/></item>
    ///   <item><see cref="CertificateOptions"/> (configured from <paramref name="configuration"/>)</item>
    /// </list>
    /// Does NOT register <see cref="IPdfRequirementSummarizer"/> — that service depends on
    /// <see cref="IMetadataExtractor"/> which is not available in worker hosts.  Callers that
    /// need PDF summarisation must additionally register <see cref="IMetadataExtractor"/> and
    /// call <see cref="AddPdfRequirementSummarizer"/>.
    /// <para>
    /// <paramref name="lifetime"/> controls the service lifetime.  Worker hosts (Reconciliator)
    /// must use <see cref="ServiceLifetime.Singleton"/> because the orchestrators that consume
    /// <see cref="IResponseExporter"/> are themselves singletons — resolving a scoped service from
    /// a singleton's root provider violates scope validation.  The SIRO exporters are stateless, so
    /// Singleton is safe.  The Web UI (which uses request-scoped pipelines) passes
    /// <see cref="ServiceLifetime.Scoped"/> (the default) via <see cref="AddExportServices"/>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for certificate options).</param>
    /// <param name="lifetime">
    /// Service lifetime for the export registrations.
    /// Defaults to <see cref="ServiceLifetime.Scoped"/> (Web UI / request-scoped pipelines).
    /// Pass <see cref="ServiceLifetime.Singleton"/> in worker hosts whose orchestrators are singletons.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiroExportServices(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        // SiroXmlExporter and DigitalPdfSigner are stateless — safe at any lifetime.
        services.Add(ServiceDescriptor.Describe(typeof(SiroXmlExporter), typeof(SiroXmlExporter), lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(DigitalPdfSigner), typeof(DigitalPdfSigner), lifetime));

        // CompositeResponseExporter delegates to the two concrete exporters above.
        services.Add(ServiceDescriptor.Describe(
            typeof(IResponseExporter), typeof(CompositeResponseExporter), lifetime));

        // Supporting export services — also stateless.
        services.Add(ServiceDescriptor.Describe(
            typeof(ILayoutGenerator), typeof(ExcelLayoutGenerator), lifetime));
        services.Add(ServiceDescriptor.Describe(
            typeof(ICriterionMapper), typeof(CriterionMapperService), lifetime));

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

    /// <summary>
    /// Adds the PDF requirement summarizer service.  Requires <see cref="IMetadataExtractor"/>
    /// to already be registered (e.g. via <c>AddExtractionServices()</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfRequirementSummarizer(this IServiceCollection services)
    {
        services.AddScoped<IPdfRequirementSummarizer, PdfRequirementSummarizerService>();
        return services;
    }

    /// <summary>
    /// Adds the full set of export services including PDF summarisation.
    /// Requires <see cref="IMetadataExtractor"/> to already be registered (e.g. via
    /// <c>AddExtractionServices()</c>).  Used by the Web UI host which registers the
    /// full extraction + export stack.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for certificate options).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExportServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddSiroExportServices(configuration);
        services.AddPdfRequirementSummarizer();
        return services;
    }
}

