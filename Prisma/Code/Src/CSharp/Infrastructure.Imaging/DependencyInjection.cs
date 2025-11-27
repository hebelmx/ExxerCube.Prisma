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
    /// Uses analytical filter selection strategy by default (based on NSGA-II optimization results).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useAnalyticalStrategy">True to use analytical strategy (default), false for simple default strategy.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImagingInfrastructure(
        this IServiceCollection services,
        bool useAnalyticalStrategy = true)
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
        // Use analytical strategy by default (based on 820 OCR baseline testing runs)
        if (useAnalyticalStrategy)
        {
            services.AddSingleton<IFilterSelectionStrategy, AnalyticalFilterSelectionStrategy>();
        }
        else
        {
            services.AddSingleton<IFilterSelectionStrategy, DefaultFilterSelectionStrategy>();
        }

        return services;
    }
}
