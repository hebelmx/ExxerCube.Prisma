using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.DependencyInjection;

/// <summary>
/// Extension methods for configuring text field extraction services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds text field extraction services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the following services:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="IFieldExtractor{TxtSource}"/> → <see cref="AdaptiveTxtFieldExtractor"/> (Scoped)</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddTxtFieldExtraction(this IServiceCollection services)
    {
        // Deterministic extractor — remains the active IFieldExtractor<TxtSource> binding.
        services.AddScoped<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();

        // LLM extractor — registered as a CONCRETE scoped service so it can be resolved
        // directly (e.g. for demo override or future A/B routing) without replacing the
        // deterministic binding above.  Ships DARK: LlmProviders:TextExtractorEnabled=false.
        services.AddScoped<LlmTxtFieldExtractor>();

        return services;
    }
}
