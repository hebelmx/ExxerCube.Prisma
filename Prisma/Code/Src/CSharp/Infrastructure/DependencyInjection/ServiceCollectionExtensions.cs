using ExxerCube.Prisma.Infrastructure.NoOp;

namespace ExxerCube.Prisma.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OCR processing services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pythonConfiguration">The Python configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOcrProcessingServices(this IServiceCollection services, PythonConfiguration pythonConfiguration)
    {
        // Register Python configuration
        services.AddSingleton(pythonConfiguration);

        // NOTE: Metrics services should be registered in the composition root (Program.cs/Startup.cs)
        // to avoid Infrastructure → Infrastructure.Metrics coupling.
        // services.AddMetricsServices(pythonConfiguration.MaxConcurrency);

        services.AddScoped<HealthCheckService>();

        // Register Application service (does not implement Domain interface for architectural compliance)
        services.AddScoped<OcrProcessingService>(provider =>
        {
            var imagePreprocessor = provider.GetRequiredService<IImagePreprocessor>();
            var ocrExecutor = provider.GetRequiredService<IOcrExecutor>();
            var fieldExtractor = provider.GetRequiredService<IFieldExtractor>();
            var logger = provider.GetRequiredService<ILogger<IOcrProcessingService>>();
            var metricsService = provider.GetRequiredService<IProcessingMetricsService>();

            return new OcrProcessingService(imagePreprocessor, ocrExecutor, fieldExtractor, logger, metricsService);
        });

        // Register Infrastructure adapter that implements Domain interface
        services.AddScoped<IOcrProcessingService>(provider =>
        {
            var ocrProcessingService = provider.GetRequiredService<OcrProcessingService>();
            return new OcrProcessingServiceAdapter(ocrProcessingService);
        });

        // DEPRECATED: All IPythonInteropService-related registrations are commented out.
        // The new Tesseract/GOT-OCR2 implementations in Infrastructure.Extraction do not require Python interop.
        // These old services had low cohesion and poor coupling - they are replaced by:
        // - IOcrExecutor implementations: TesseractOcrExecutor, GotOcr2OcrExecutor (Infrastructure.Extraction)
        // - IFieldExtractor<T> implementations: XmlFieldExtractor, PdfOcrFieldExtractor, DocxFieldExtractor (Infrastructure.Extraction)

        // services.AddScoped<IPythonInteropService, DeprecatedPythonInteropService>();

        // services.AddScoped<IOcrExecutor>(provider =>
        // {
        //     var logger = provider.GetRequiredService<ILogger<OcrProcessingAdapter>>();
        //     var pythonInteropService = provider.GetRequiredService<IPythonInteropService>();
        //     return new OcrProcessingAdapter(logger, pythonInteropService);
        // });

        // Register no-op implementations for IImagePreprocessor and IFieldExtractor
        // These are pass-through implementations used by the legacy OcrProcessingService
        // Modern OCR engines (Tesseract, GOT-OCR2) handle preprocessing internally
        // Field extraction is handled separately by typed extractors (XmlFieldExtractor, etc.)
        services.AddScoped<IImagePreprocessor, NoOpImagePreprocessor>();
        services.AddScoped<IFieldExtractor, NoOpFieldExtractor>();

        // Register file system adapters
        services.AddScoped<IFileLoader, FileSystemLoader>();
        services.AddScoped<IOutputWriter, FileSystemOutputWriter>();

        return services;
    }
}