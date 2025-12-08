using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;
using ExxerCube.Prisma.Infrastructure.Imaging.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.Imaging;

/// <summary>
/// Filter selection strategy type.
/// </summary>
public enum FilterSelectionStrategyType
{
    /// <summary>
    /// Simple default strategy with fixed parameters.
    /// </summary>
    Default,

    /// <summary>
    /// Analytical strategy using threshold-based decision trees (NSGA-II optimized).
    /// </summary>
    Analytical,

    /// <summary>
    /// Advanced polynomial regression strategy for continuous parameter prediction.
    /// Requires trained models from filtering study results.
    /// </summary>
    Polynomial
}

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
    [Obsolete("Use AddImagingInfrastructure(services, FilterSelectionStrategyType) instead")]
    public static IServiceCollection AddImagingInfrastructure(
        this IServiceCollection services,
        bool useAnalyticalStrategy = true)
    {
        // Register individual filters
        services.AddSingleton<PilSimpleEnhancementFilter>();
        services.AddSingleton<OpenCvAdvancedEnhancementFilter>();
        services.AddSingleton<NoOpEnhancementFilter>();
        services.AddSingleton<PolynomialEnhancementFilter>();

        // Register keyed services for filter selection
        services.AddKeyedSingleton<IImageEnhancementFilter, PilSimpleEnhancementFilter>(
            ImageFilterType.PilSimple);
        services.AddKeyedSingleton<IImageEnhancementFilter, OpenCvAdvancedEnhancementFilter>(
            ImageFilterType.OpenCvAdvanced);
        services.AddKeyedSingleton<IImageEnhancementFilter, NoOpEnhancementFilter>(
            ImageFilterType.None);
        services.AddKeyedSingleton<IImageEnhancementFilter, PolynomialEnhancementFilter>(
            ImageFilterType.Polynomial);

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

        // Register default IImageEnhancementFilter (uses adaptive filter)
        services.AddSingleton<IImageEnhancementFilter, AdaptiveEnhancementFilter>();

        // Register IImageQualityAnalyzer (Emgu.CV-based implementation using OpenCV)
        services.AddSingleton<IImageQualityAnalyzer, EmguCvImageQualityAnalyzer>();

        // Register polynomial analyzer (for polynomial enhancement filter)
        services.AddSingleton<PolynomialImageQualityAnalyzer>();

        // Register ITextComparer (Levenshtein distance-based text comparison)
        services.AddSingleton<ITextComparer, LevenshteinTextComparer>();

        return services;
    }

    /// <summary>
    /// Adds imaging infrastructure services to the service collection with specified strategy type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="strategyType">The filter selection strategy to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImagingInfrastructure(
        this IServiceCollection services,
        FilterSelectionStrategyType strategyType = FilterSelectionStrategyType.Analytical)
    {
        // Register individual filters
        services.AddSingleton<PilSimpleEnhancementFilter>();
        services.AddSingleton<OpenCvAdvancedEnhancementFilter>();
        services.AddSingleton<NoOpEnhancementFilter>();
        services.AddSingleton<PolynomialEnhancementFilter>();

        // Register keyed services for filter selection
        services.AddKeyedSingleton<IImageEnhancementFilter, PilSimpleEnhancementFilter>(
            ImageFilterType.PilSimple);
        services.AddKeyedSingleton<IImageEnhancementFilter, OpenCvAdvancedEnhancementFilter>(
            ImageFilterType.OpenCvAdvanced);
        services.AddKeyedSingleton<IImageEnhancementFilter, NoOpEnhancementFilter>(
            ImageFilterType.None);
        services.AddKeyedSingleton<IImageEnhancementFilter, PolynomialEnhancementFilter>(
            ImageFilterType.Polynomial);

        // Register adaptive filter (requires IImageQualityAnalyzer)
        services.AddSingleton<AdaptiveEnhancementFilter>();
        services.AddKeyedSingleton<IImageEnhancementFilter, AdaptiveEnhancementFilter>(
            ImageFilterType.Adaptive);

        // Register filter selection strategy based on type
        switch (strategyType)
        {
            case FilterSelectionStrategyType.Polynomial:
                services.AddSingleton<IFilterSelectionStrategy, PolynomialFilterSelectionStrategy>();
                break;

            case FilterSelectionStrategyType.Analytical:
                services.AddSingleton<IFilterSelectionStrategy, AnalyticalFilterSelectionStrategy>();
                break;

            case FilterSelectionStrategyType.Default:
            default:
                services.AddSingleton<IFilterSelectionStrategy, DefaultFilterSelectionStrategy>();
                break;
        }

        // Register default IImageEnhancementFilter (uses adaptive filter)
        services.AddSingleton<IImageEnhancementFilter, AdaptiveEnhancementFilter>();

        // Register IImageQualityAnalyzer (Emgu.CV-based implementation using OpenCV)
        services.AddSingleton<IImageQualityAnalyzer, EmguCvImageQualityAnalyzer>();

        // Register polynomial analyzer (for polynomial enhancement filter)
        services.AddSingleton<PolynomialImageQualityAnalyzer>();

        // Register ITextComparer (Levenshtein distance-based text comparison)
        services.AddSingleton<ITextComparer, LevenshteinTextComparer>();

        return services;
    }
}
