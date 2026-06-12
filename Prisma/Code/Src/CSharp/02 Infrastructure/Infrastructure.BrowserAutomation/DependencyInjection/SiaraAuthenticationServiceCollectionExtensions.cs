using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;

/// <summary>
/// Registers the SIARA authentication seam (ADR-010): the three keyed
/// <see cref="ISiaraSessionProvider"/> strategies, the fail-closed
/// <see cref="ISiaraSessionProviderResolver"/>, and the supporting credential source and lockout-safety
/// circuit-breaker.
/// </summary>
public static class SiaraAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SIARA authentication services. The client selects the mode at deployment time via
    /// <see cref="SiaraAuthOptions.AuthMode"/>; the resolver honors it fail-closed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// Optional configuration to bind <see cref="SiaraAuthOptions"/> from the <c>Siara</c> section (the
    /// secret store the credential source reads is also expected here for <c>AutomatedLogin</c>).
    /// </param>
    /// <param name="configure">Optional code-based override applied after binding.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Call alongside <c>AddBrowserAutomationServices</c>, which provides the
    /// <see cref="IBrowserAutomationAgent"/>, <see cref="IBrowserSessionContext"/>, and
    /// <see cref="ISiaraLoginService"/> these providers depend on. Lifetimes: the providers and resolver
    /// are <strong>scoped</strong> (they ride the scoped live browser); the credential source and the
    /// <see cref="SiaraLoginCircuitBreaker"/> are <strong>singletons</strong> so the P3 failure/backoff
    /// state persists across acquisition attempts (a scoped provider depending on a singleton is safe — no
    /// captive dependency).
    /// </remarks>
    public static IServiceCollection AddSiaraAuthentication(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<SiaraAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<SiaraAuthOptions>();
        if (configuration is not null)
        {
            optionsBuilder.Bind(configuration.GetSection(SiaraAuthOptions.SectionName));
        }

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        // Singletons: the clock, the lockout-safety state (P3 must persist across attempts), and the
        // credential source (stateless, reads the secret store per call).
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SiaraLoginCircuitBreaker>();
        services.TryAddSingleton<ISiaraCredentialSource, ConfiguredSiaraCredentialSource>();

        // The three strategies, keyed by mode so only the configured one is constructed by the resolver.
        services.AddKeyedScoped<ISiaraSessionProvider, SessionPassthroughSiaraSessionProvider>(
            SiaraAuthMode.SessionPassthrough);
        services.AddKeyedScoped<ISiaraSessionProvider, InteractiveLoginSiaraSessionProvider>(
            SiaraAuthMode.InteractiveLogin);
        services.AddKeyedScoped<ISiaraSessionProvider, AutomatedLoginSiaraSessionProvider>(
            SiaraAuthMode.AutomatedLogin);

        services.TryAddScoped<ISiaraSessionProviderResolver, SiaraSessionProviderResolver>();

        return services;
    }
}
