using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Infrastructure.Python;

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

        // Register metrics and monitoring services
        services.AddSingleton<ProcessingMetricsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ProcessingMetricsService>>();
            return new ProcessingMetricsService(logger, pythonConfiguration.MaxConcurrency);
        });

        services.AddScoped<HealthCheckService>();

        // Register domain interfaces with their implementations
        services.AddScoped<IOcrProcessingService>(provider =>
        {
            var imagePreprocessor = provider.GetRequiredService<IImagePreprocessor>();
            var ocrExecutor = provider.GetRequiredService<IOcrExecutor>();
            var fieldExtractor = provider.GetRequiredService<IFieldExtractor>();
            var logger = provider.GetRequiredService<ILogger<OcrProcessingService>>();
            var metricsService = provider.GetRequiredService<ProcessingMetricsService>();
            
            return new OcrProcessingService(imagePreprocessor, ocrExecutor, fieldExtractor, logger, metricsService);
        });
        
        // Register Python interop service (CSnakes-based with circuit breaker)
        services.AddScoped<IPythonInteropService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CSnakesOcrProcessingAdapter>>();
            var config = provider.GetRequiredService<PythonConfiguration>();
            var innerService = new CSnakesOcrProcessingAdapter(logger, config.ModulesPath);
            
            var circuitBreakerLogger = provider.GetRequiredService<ILogger<CircuitBreakerPythonInteropService>>();
            return new CircuitBreakerPythonInteropService(circuitBreakerLogger, innerService);
        });

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
