using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Infrastructure.Calendar.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the Calendar adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the holiday-aware <see cref="IBusinessDayCalculator"/> implementation
    /// (<see cref="MexicoBusinessDayCalculator"/> — Mexican federal holidays via the PublicHoliday package).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Per ADR-023 this registration lives with the Calendar adapter and is invoked by the host /
    /// composition root, so no other infrastructure adapter needs a project reference to Calendar.
    /// The registration is idempotent (TryAddSingleton) so multiple composition paths may call it safely.
    /// </remarks>
    public static IServiceCollection AddCalendarServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();
        return services;
    }
}
