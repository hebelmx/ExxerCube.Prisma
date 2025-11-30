namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.DependencyInjection;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering adaptive DOCX extraction services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds adaptive DOCX extraction services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers all 5 extraction strategies, the orchestrator, and the migration adapter.
    /// </para>
    /// <para>
    /// <strong>Migration Usage:</strong>
    /// </para>
    /// <code>
    /// // In your DI configuration, call this method:
    /// services.AddAdaptiveDocxExtraction();
    ///
    /// // This registers:
    /// // - 5 IAdaptiveDocxStrategy implementations
    /// // - IAdaptiveDocxExtractor (orchestrator)
    /// // - IFieldMergeStrategy
    /// // - IFieldExtractor&lt;DocxSource&gt; (adapter for backward compatibility)
    /// </code>
    /// <para>
    /// <strong>Rollback:</strong> To revert to old DocxFieldExtractor, simply remove this call
    /// and re-register the old extractor.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAdaptiveDocxExtraction(this IServiceCollection services)
    {
        // Register all 5 extraction strategies
        services.AddScoped<IAdaptiveDocxStrategy, StructuredDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, ContextualDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, TableBasedDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, ComplementExtractionStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, SearchExtractionStrategy>();

        // Register orchestrator (receives all strategies via IEnumerable<IAdaptiveDocxStrategy>)
        services.AddScoped<IAdaptiveDocxExtractor, AdaptiveDocxExtractor>();
        services.AddScoped<IReadOnlyList<IAdaptiveDocxStrategy>>(sp => sp.GetServices<IAdaptiveDocxStrategy>().ToList());

        // Register merge strategy
        services.AddScoped<IFieldMergeStrategy, EnhancedFieldMergeStrategy>();

        // Register migration adapter (replaces old DocxFieldExtractor)
        // This enables transparent migration: consumers continue using IFieldExtractor<DocxSource>
        services.AddScoped<IFieldExtractor<DocxSource>, AdaptiveDocxFieldExtractorAdapter>();

        return services;
    }

    /// <summary>
    /// Adds adaptive DOCX extraction services WITHOUT replacing the old IFieldExtractor registration.
    /// Use this for side-by-side comparison or gradual migration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers only the new adaptive extraction system WITHOUT replacing
    /// the old <c>IFieldExtractor&lt;DocxSource&gt;</c> registration. Use this when you want to:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Run old and new systems side-by-side for comparison</description></item>
    ///   <item><description>Gradually migrate specific consumers</description></item>
    ///   <item><description>Test new system in production without full migration</description></item>
    /// </list>
    /// <para>
    /// <strong>Usage:</strong>
    /// </para>
    /// <code>
    /// // Register new system alongside old:
    /// services.AddAdaptiveDocxExtractionOnly();
    ///
    /// // Consumers can inject IAdaptiveDocxExtractor directly:
    /// public class MyService
    /// {
    ///     public MyService(IAdaptiveDocxExtractor adaptiveExtractor) { }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddAdaptiveDocxExtractionOnly(this IServiceCollection services)
    {
        // Register all 5 extraction strategies
        services.AddScoped<IAdaptiveDocxStrategy, StructuredDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, ContextualDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, TableBasedDocxStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, ComplementExtractionStrategy>();
        services.AddScoped<IAdaptiveDocxStrategy, SearchExtractionStrategy>();

        // Register orchestrator
        services.AddScoped<IAdaptiveDocxExtractor, AdaptiveDocxExtractor>();
        services.AddScoped<IReadOnlyList<IAdaptiveDocxStrategy>>(sp => sp.GetServices<IAdaptiveDocxStrategy>().ToList());

        // Register merge strategy
        services.AddScoped<IFieldMergeStrategy, EnhancedFieldMergeStrategy>();

        // NOTE: Does NOT register IFieldExtractor<DocxSource> adapter
        // Old DocxFieldExtractor registration remains active

        return services;
    }
}
