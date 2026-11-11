using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;

/// <summary>
/// Registers the per-process JWT clearance token service used to stamp and verify cross-process handoff
/// events with a short-lived process identity token (MVP-PATH 1.5, A5 DoD).
/// </summary>
public static class ProcessIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds the process identity services:
    /// <list type="bullet">
    ///   <item><see cref="ProcessIdentityOptions"/> bound from the <c>ProcessIdentity</c> configuration section.</item>
    ///   <item><see cref="IProcessClearanceTokenService"/> → <see cref="JwtProcessClearanceTokenService"/> (singleton; stateless).</item>
    ///   <item><see cref="ISiaraActorIdentityProvider"/> → <see cref="ConfiguredSiaraActorIdentityProvider"/> (singleton; no-op where <c>AddSiaraAuthentication</c> has already registered it).</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The configuration root used to bind <see cref="ProcessIdentityOptions"/> from the
    /// <c>ProcessIdentity</c> section and <see cref="SiaraAuthOptions"/> (Actor sub-section) from the
    /// <c>Siara</c> section. Both sections are required in each worker's <c>appsettings.json</c>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Call this in each worker's <c>Program.cs</c>. In Orion (where <c>AddSiaraAuthentication</c> is
    /// also called), the <see cref="ISiaraActorIdentityProvider"/> registration is a no-op because
    /// <c>TryAddSingleton</c> does not replace an existing registration. In Athena and the Reconciliator
    /// the actor identity provider is registered for the first time here.
    /// </para>
    /// <para>
    /// The <c>ProcessIdentity:JwtSecret</c> must be provided at deployment time via an environment
    /// variable or a Key Vault config provider — never commit a real secret to source control. The three
    /// processes must share the same secret value. See ADR-011 and deployment docs.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddProcessIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ProcessIdentityOptions>()
            .Bind(configuration.GetSection(ProcessIdentityOptions.SectionName));

        // Bind SiaraAuthOptions so ConfiguredSiaraActorIdentityProvider can read the Actor sub-section.
        // In Orion this is already done by AddSiaraAuthentication; AddOptions is idempotent.
        services.AddOptions<SiaraAuthOptions>()
            .Bind(configuration.GetSection(SiaraAuthOptions.SectionName));

        // Singleton: stateless JWT signer/validator.
        services.TryAddSingleton<IProcessClearanceTokenService, JwtProcessClearanceTokenService>();

        // Singleton: reads Siara:Actor:ActorId from config. TryAdd so Orion (which calls
        // AddSiaraAuthentication first) keeps its existing registration.
        services.TryAddSingleton<ISiaraActorIdentityProvider, ConfiguredSiaraActorIdentityProvider>();

        return services;
    }
}
