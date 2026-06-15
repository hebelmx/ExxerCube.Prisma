using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Calendar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification.DependencyInjection;

/// <summary>
/// Extension methods for registering classification services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds classification services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for matching policy options).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClassificationServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddScoped<IFileClassifier, FileClassifierService>();

        // Register matching policy service (general) and name-specific policy
        services.AddScoped<IMatchingPolicy, MatchingPolicyService>();
        services.AddScoped<NameMatchingPolicy>();
        // Expose the name-aware policy through its Domain abstraction so the Application orchestrator can route
        // name fields to it (homonym disambiguation) without referencing Infrastructure. Same scoped instance.
        services.AddScoped<INameMatchingPolicy>(sp => sp.GetRequiredService<NameMatchingPolicy>());

        // Register identity resolution service
        services.AddScoped<IPersonIdentityResolver, PersonIdentityResolverService>();

        // Register Ollama LLM client (typed HttpClient + options).
        // OllamaHttpClient is always registered; the SemanticAnalyzerService checks OllamaOptions.Enabled
        // at call time, so environments without a running Ollama instance are unaffected.
        if (configuration != null)
        {
            var ollamaSection = configuration.GetSection(OllamaOptions.SectionName);
            if (ollamaSection.Exists())
            {
                services.Configure<OllamaOptions>(ollamaSection);
            }
            else
            {
                services.Configure<OllamaOptions>(_ => { }); // defaults (Enabled=false)
            }
        }
        else
        {
            services.Configure<OllamaOptions>(_ => { }); // defaults (Enabled=false)
        }

        services.AddHttpClient<OllamaHttpClient>();
        services.AddScoped<IOllamaClient>(sp => sp.GetRequiredService<OllamaHttpClient>());

        // Register semantic analyzer service with fuzzy phrase matching and optional LLM enrichment.
        // IOllamaClient is resolved from DI; when Enabled=false in OllamaOptions the service no-ops.
        services.AddScoped<ISemanticAnalyzer>(sp =>
        {
            var textComparer = sp.GetRequiredService<ITextComparer>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SemanticAnalyzerService>>();
            var ollamaClient = sp.GetRequiredService<IOllamaClient>();
            var ollamaOptions = sp.GetRequiredService<IOptions<OllamaOptions>>();
            return new SemanticAnalyzerService(textComparer, logger, ollamaClient, ollamaOptions);
        });

        // Register adapter that bridges ILegalDirectiveClassifier → ISemanticAnalyzer
        // This allows DecisionLogicService to use the new fuzzy matching implementation
        services.AddScoped<ILegalDirectiveClassifier, SemanticAnalyzerAdapter>();

        // Register holiday-aware business-day calculator (Mexico federal holidays via PublicHoliday package).
        // TryAddSingleton: MexicoPublicHoliday is stateless calendar math, one instance per host. Using
        // TryAdd avoids a double-registration when AddDatabaseServices also runs in the same host.
        services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();

        // Register data fusion services — FusionExpedienteService receives IBusinessDayCalculator via DI.
        services.AddScoped<IFusionExpediente>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FusionExpedienteService>>();
            var calculator = sp.GetRequiredService<IBusinessDayCalculator>();
            return new FusionExpedienteService(logger, coefficients: null, businessDayCalculator: calculator);
        });
        services.AddScoped<IExpedienteClasifier, ExpedienteClasifierService>();

        // Register matching policy options from configuration
        if (configuration != null)
        {
            var section = configuration.GetSection("MatchingPolicy");
            if (section.Exists())
            {
                services.Configure<MatchingPolicyOptions>(section);
            }
            else
            {
                services.Configure<MatchingPolicyOptions>(_ => { });
            }
            var nameSection = configuration.GetSection("NameMatching");
            if (nameSection.Exists())
            {
                services.Configure<NameMatchingOptions>(nameSection);
            }
            else
            {
                services.Configure<NameMatchingOptions>(_ => { });
            }
        }
        else
        {
            // Use default options if no configuration provided
            services.Configure<MatchingPolicyOptions>(_ => { });
            services.Configure<NameMatchingOptions>(_ => { });
        }

        return services;
    }
}

