namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.DependencyInjection;

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

        // NOTE: Adaptive DOCX extraction system is registered at the application layer (API/Host)
        // Infrastructure projects should NOT depend on each other.
        // Call services.AddAdaptiveDocxExtraction() in your API/Host Startup/Program.cs after calling AddExtractionServices()

        // Register generic field extractors for Story 1.3
        // OLD (replaced by adaptive extraction): services.AddScoped<IFieldExtractor<DocxSource>, DocxFieldExtractor>();
        services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();
        // Register dummy XML field extractor (temporary placeholder until full implementation is added)
        services.AddScoped<IFieldExtractor<XmlSource>, XmlFieldExtractor>();

        // Register OCR executors with keyed services for runtime selection
        // Tesseract: Fast, traditional OCR (3-6s, 80-93% confidence)
        services.AddKeyedScoped<IOcrExecutor, Teseract.TesseractOcrExecutor>("Tesseract");

        // GOT-OCR2: Transformer-based, slower but more accurate (140s, 88%+ confidence)
        // DISABLED: Requires IPythonEnvironment which is not configured
        // services.AddKeyedScoped<IOcrExecutor, GotOcr2.GotOcr2OcrExecutor>("GotOcr2");

        // Default: Use Tesseract as primary (fast), fallback to GOT-OCR2 for low confidence
        services.AddScoped<IOcrExecutor, Teseract.TesseractOcrExecutor>();

        // Register comparison service
        services.AddScoped<IDocumentComparisonService, DocumentComparisonService>();

        // Register bulk processing service
        services.AddScoped<IBulkProcessingService, BulkProcessingService>();

        // Register OCR processing service (used by BulkProcessingService)
        services.AddScoped<IOcrProcessingService, Execution.OcrProcessingService>();

        // OCR text cleaning (raw + normalized forms retained)
        services.AddSingleton<ITextSanitizer, TextSanitizer>();
        services.AddSingleton<OcrSanitizationService>();

        // OCR session repository for data collection and model retraining
        services.AddSingleton<IOcrSessionRepository, Repositories.OcrSessionRepository>();

        return services;
    }
}