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

        return services;
    }
}

