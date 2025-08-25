using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Python.Wrappers;
using CSnakes.Runtime;

namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Extension methods for configuring Python environment services.
/// </summary>
public static class PythonEnvironmentExtensions
{
    /// <summary>
    /// Adds CSnakes Python environment services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPrismaPythonEnvironment(this IServiceCollection services)
    {
        // Register CSnakes Python environment
        services.AddSingleton<IPythonEnvironment>(provider =>
        {
            // Initialize the Python environment
            var env = PrismaPythonEnvironment.Env;
            return env;
        });

        // Register CSnakes wrapper services
        services.AddScoped<ExxerCube.Prisma.Infrastructure.Python.Wrappers.IPrismaOcrWrapper>(provider =>
        {
            return PrismaOcrWrapper.Create();
        });

        // Register the main CSnakes OCR processing adapter
        services.AddScoped<IPythonInteropService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CSnakesOcrProcessingAdapter>>();
            return new CSnakesOcrProcessingAdapter(logger);
        });

        // Register other interfaces with the same adapter
        services.AddScoped<IImagePreprocessor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CSnakesOcrProcessingAdapter>>();
            return new CSnakesOcrProcessingAdapter(logger);
        });

        services.AddScoped<IOcrExecutor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CSnakesOcrProcessingAdapter>>();
            return new CSnakesOcrProcessingAdapter(logger);
        });

        services.AddScoped<IFieldExtractor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CSnakesOcrProcessingAdapter>>();
            return new CSnakesOcrProcessingAdapter(logger);
        });

        return services;
    }
}
