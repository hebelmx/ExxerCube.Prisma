namespace ExxerCube.Prisma.Infrastructure.Metrics.DependencyInjection;

/// <summary>
/// Extension methods for registering metrics services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds metrics services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxConcurrency">The maximum number of concurrent processing operations. Defaults to 5.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricsServices(this IServiceCollection services, int maxConcurrency = 5)
    {
        services.AddSingleton<IProcessingMetricsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ProcessingMetricsService>>();
            return new ProcessingMetricsService(logger, maxConcurrency);
        });

        return services;
    }
}

