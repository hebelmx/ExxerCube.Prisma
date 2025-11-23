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
        services.AddScoped<IFieldExtractor<DocxSource>, DocxFieldExtractor>();
        services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();
        // Register dummy XML field extractor (temporary placeholder until full implementation is added)
        services.AddScoped<IFieldExtractor<XmlSource>, XmlFieldExtractor>();

        // Register OCR executor - using GOT-OCR2 as default implementation
        // TODO: Add Tesseract implementation and use keyed services for runtime selection
        services.AddScoped<IOcrExecutor, GotOcr2.GotOcr2OcrExecutor>();

        return services;
    }
}

