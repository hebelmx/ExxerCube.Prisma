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
    /// <param name="pythonModulesPath">The path to the Python modules.</param>
    /// <param name="maxConcurrency">The maximum number of concurrent processing operations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOcrProcessingServices(this IServiceCollection services, string pythonModulesPath, int maxConcurrency = 5)
    {
        // Register metrics and monitoring services
        services.AddSingleton<ProcessingMetricsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ProcessingMetricsService>>();
            return new ProcessingMetricsService(logger, maxConcurrency);
        });

        services.AddSingleton<HealthCheckService>();

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
        
        // Register Python adapter as multiple interfaces
        services.AddScoped<IOcrExecutor>(provider => 
        {
            var logger = provider.GetRequiredService<ILogger<PythonOcrProcessingAdapter>>();
            return new PythonOcrProcessingAdapter(logger, pythonModulesPath);
        });
        
        services.AddScoped<IImagePreprocessor>(provider => 
        {
            var logger = provider.GetRequiredService<ILogger<PythonOcrProcessingAdapter>>();
            return new PythonOcrProcessingAdapter(logger, pythonModulesPath);
        });
        
        services.AddScoped<IFieldExtractor>(provider => 
        {
            var logger = provider.GetRequiredService<ILogger<PythonOcrProcessingAdapter>>();
            return new PythonOcrProcessingAdapter(logger, pythonModulesPath);
        });

        // Register file system adapters
        services.AddScoped<IFileLoader, FileSystemLoader>();
        services.AddScoped<IOutputWriter, FileSystemOutputWriter>();

        return services;
    }
}
