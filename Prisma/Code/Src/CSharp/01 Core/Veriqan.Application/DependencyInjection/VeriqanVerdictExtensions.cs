using System;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Application.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering Veriqan verdict-aggregation
/// services (FR-15; Story 7.1).
/// </summary>
public static class VeriqanVerdictExtensions
{
    /// <summary>
    /// Registers <see cref="IVerdictAggregator"/> (implemented by <see cref="VerdictAggregator"/>)
    /// as a singleton — the aggregator is stateless and safe for concurrent use.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanVerdict(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IVerdictAggregator, VerdictAggregator>();

        return services;
    }
}
