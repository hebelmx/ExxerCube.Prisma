using Microsoft.Extensions.DependencyInjection;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Extraction;

/// <summary>
/// Extension methods for registering extraction services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds extraction services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExtractionServices(this IServiceCollection services)
    {
        services.AddScoped<IFileTypeIdentifier, FileTypeIdentifierService>();
        services.AddScoped<IXmlNullableParser<Domain.Entities.Expediente>, XmlExpedienteParser>();
        
        // Register format-specific extractors
        services.AddScoped<XmlMetadataExtractor>();
        services.AddScoped<DocxMetadataExtractor>();
        services.AddScoped<PdfMetadataExtractor>();
        
        // Register composite extractor that delegates to format-specific ones
        services.AddScoped<IMetadataExtractor, CompositeMetadataExtractor>();

        // Register generic field extractors for Story 1.3
        services.AddScoped<IFieldExtractor<Domain.Entities.DocxSource>, DocxFieldExtractor>();
        services.AddScoped<IFieldExtractor<Domain.Entities.PdfSource>, PdfOcrFieldExtractor>();
        // Note: IFieldExtractor<XmlSource> can be added when XML field extractor is implemented

        return services;
    }
}

