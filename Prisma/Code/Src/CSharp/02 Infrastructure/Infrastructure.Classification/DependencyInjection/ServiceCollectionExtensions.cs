using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // IBusinessDayCalculator (MexicoBusinessDayCalculator) is registered by the host / composition root
        // via AddCalendarServices() — see ADR-023. Any host that wires AddClassificationServices must also
        // call AddCalendarServices() so the FusionExpedienteService factory below can resolve the port.

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

        // ----------------------------------------------------------------
        // LLM provider subsystem — registered DARK (flags default false).
        // S1: OllamaProvider + LlmProviderFactory + ConfigSecretProvider.
        // Registering the types is always safe; nothing changes at runtime
        // until LlmProviders:TextExtractorEnabled or VisionExtractorEnabled=true.
        // ----------------------------------------------------------------
        if (configuration != null)
        {
            var llmSection = configuration.GetSection(LlmProvidersOptions.SectionName);
            if (llmSection.Exists())
            {
                services.Configure<LlmProvidersOptions>(llmSection);
            }
            else
            {
                services.Configure<LlmProvidersOptions>(_ => { });
            }
        }
        else
        {
            services.Configure<LlmProvidersOptions>(_ => { });
        }

        // Named HttpClient for OllamaProvider (base address is resolved per-call from options).
        services.AddHttpClient(OllamaProvider.HttpClientName);

        // Named HttpClient for GeminiProvider (base URL is hard-coded in the provider).
        services.AddHttpClient(GeminiProvider.HttpClientName);

        // Register the provider implementations (singleton — stateless HTTP adapters).
        services.AddSingleton<ILlmProvider, OllamaProvider>();

        // S2: Gemini provider — registered DARK alongside Ollama so the factory sees both.
        // The active provider remains "Ollama" (config default) until LlmProviders:Active is flipped.
        services.AddSingleton<ILlmProvider, GeminiProvider>();

        // Singleton factory that holds all registered providers.
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Secret resolution from IConfiguration (user-secrets / env vars).
        services.AddSingleton<ISecretProvider, ConfigSecretProvider>();

        return services;
    }
}

