using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.Imaging;

/// <summary>
/// Extension methods for registering imaging services with dependency injection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds imaging infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImagingInfrastructure(this IServiceCollection services)
    {
        // Register individual filters
        services.AddSingleton<PilSimpleEnhancementFilter>();
        services.AddSingleton<OpenCvAdvancedEnhancementFilter>();
        services.AddSingleton<NoOpEnhancementFilter>();

        // Register keyed services for filter selection
        services.AddKeyedSingleton<IImageEnhancementFilter, PilSimpleEnhancementFilter>(
            ImageFilterType.PilSimple);
        services.AddKeyedSingleton<IImageEnhancementFilter, OpenCvAdvancedEnhancementFilter>(
            ImageFilterType.OpenCvAdvanced);
        services.AddKeyedSingleton<IImageEnhancementFilter, NoOpEnhancementFilter>(
            ImageFilterType.None);

        // Register adaptive filter (requires IImageQualityAnalyzer)
        services.AddSingleton<AdaptiveEnhancementFilter>();
        services.AddKeyedSingleton<IImageEnhancementFilter, AdaptiveEnhancementFilter>(
            ImageFilterType.Adaptive);

        // Register filter selection strategy
        services.AddSingleton<IFilterSelectionStrategy, DefaultFilterSelectionStrategy>();

        return services;
    }
}
