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

        // Note: PrismaOcrWrapperAdapter has been removed - these registrations need to be updated with a new implementation
        // Register the main Prisma OCR wrapper adapter
        // services.AddScoped<IPythonInteropService>(provider =>
        // {
        //     var logger = provider.GetRequiredService<ILogger<PrismaOcrWrapperAdapter>>();
        //     return new PrismaOcrWrapperAdapter(logger);
        // });

        // Register other interfaces with the same adapter
        // services.AddScoped<IImagePreprocessor>(provider =>
        // {
        //     var logger = provider.GetRequiredService<ILogger<PrismaOcrWrapperAdapter>>();
        //     return new PrismaOcrWrapperAdapter(logger);
        // });

        // services.AddScoped<IOcrExecutor>(provider =>
        // {
        //     var logger = provider.GetRequiredService<ILogger<PrismaOcrWrapperAdapter>>();
        //     return new PrismaOcrWrapperAdapter(logger);
        // });

        // services.AddScoped<IFieldExtractor>(provider =>
        // {
        //     var logger = provider.GetRequiredService<ILogger<PrismaOcrWrapperAdapter>>();
        //     return new PrismaOcrWrapperAdapter(logger);
        // });

        return services;
    }
}