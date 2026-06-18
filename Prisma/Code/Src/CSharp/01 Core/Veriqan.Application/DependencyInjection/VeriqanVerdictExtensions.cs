using System;
using ExxerCube.Prisma.Veriqan.Application.Tenant;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Application.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan verdict-aggregation
/// and tenant-profile resolution services (FR-15; Story 7.1; Story 9.3b).
/// </summary>
public static class VeriqanVerdictExtensions
{
    /// <summary>
    /// Registers <see cref="IVerdictAggregator"/> (implemented by <see cref="VerdictAggregator"/>),
    /// <see cref="ITenantProfileResolver"/> (implemented by <see cref="TenantProfileResolver"/>),
    /// and the default active <see cref="TenantProfile"/> (the CONDUSEF legal baseline).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="VerdictAggregator"/> and <see cref="TenantProfileResolver"/> are stateless and
    /// registered as singletons — they are safe for concurrent use.
    /// </para>
    /// <para>
    /// The active <see cref="TenantProfile"/> is registered as a singleton
    /// <c>TenantProfile.LegalBaseline()</c> — the CONDUSEF legal baseline with no overrides and
    /// the default confidence threshold (0.8). This default is overridable by configuration in
    /// future stories (FR-39 / Epic 13). Real per-statement profile SELECTION is deferred to
    /// FR-39 / Epic 13; this wiring uses a single configured active profile (the legal baseline).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanVerdict(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IVerdictAggregator, VerdictAggregator>();

        // Tenant-profile resolution — stateless resolver + default legal-baseline profile.
        services.AddSingleton<ITenantProfileResolver, TenantProfileResolver>();
        services.AddSingleton(TenantProfile.LegalBaseline());

        return services;
    }
}
