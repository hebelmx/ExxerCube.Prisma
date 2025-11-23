using ExxerCube.Prisma.Infrastructure.Metrics;

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

        // Register metrics services
        services.AddMetricsServices(pythonConfiguration.MaxConcurrency);

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

        // Register Python interop service (DEPRECATED - using dummy implementation)
        // Note: IPythonInteropService is deprecated and will be removed in a future release.
        // This is a temporary dummy implementation to allow the application to compile.
        services.AddScoped<IPythonInteropService, DeprecatedPythonInteropService>();

        // Register domain interface implementations using the abstract Python interop service
        services.AddScoped<IOcrExecutor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<OcrProcessingAdapter>>();
            var pythonInteropService = provider.GetRequiredService<IPythonInteropService>();
            return new OcrProcessingAdapter(logger, pythonInteropService);
        });

        services.AddScoped<IImagePreprocessor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<OcrProcessingAdapter>>();
            var pythonInteropService = provider.GetRequiredService<IPythonInteropService>();
            return new OcrProcessingAdapter(logger, pythonInteropService);
        });

        services.AddScoped<IFieldExtractor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<OcrProcessingAdapter>>();
            var pythonInteropService = provider.GetRequiredService<IPythonInteropService>();
            return new OcrProcessingAdapter(logger, pythonInteropService);
        });

        // Register file system adapters
        services.AddScoped<IFileLoader, FileSystemLoader>();
        services.AddScoped<IOutputWriter, FileSystemOutputWriter>();

        return services;
    }
}