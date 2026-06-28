using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Calendar;
using ExxerCube.Prisma.Infrastructure.Calendar.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Tests.Infrastructure.Calendar;

/// <summary>
/// Guard tests for <see cref="ServiceCollectionExtensions.AddCalendarServices"/> (ADR-023).
/// The Calendar adapter owns the <see cref="IBusinessDayCalculator"/> registration so the Database and
/// Classification adapters no longer reference it. A host that wires those adapters but forgets to call
/// AddCalendarServices() would fail to resolve SLAEnforcerService / FusionExpedienteService (both resolve
/// the calculator via GetRequiredService), so this pins the registration contract.
/// </summary>
public sealed class CalendarServiceCollectionExtensionsTests
{
    /// <summary>
    /// AddCalendarServices registers <see cref="MexicoBusinessDayCalculator"/> for the
    /// <see cref="IBusinessDayCalculator"/> port as a singleton.
    /// </summary>
    [Fact]
    public void AddCalendarServices_RegistersMexicoBusinessDayCalculator_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddCalendarServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IBusinessDayCalculator));
        descriptor.ImplementationType.ShouldBe(typeof(MexicoBusinessDayCalculator));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// The registered <see cref="IBusinessDayCalculator"/> resolves to a usable
    /// <see cref="MexicoBusinessDayCalculator"/> instance from the provider.
    /// </summary>
    [Fact]
    public void AddCalendarServices_ResolvesIBusinessDayCalculator()
    {
        var services = new ServiceCollection();
        services.AddCalendarServices();
        using var provider = services.BuildServiceProvider();

        var calculator = provider.GetService<IBusinessDayCalculator>();

        calculator.ShouldNotBeNull();
        calculator.ShouldBeOfType<MexicoBusinessDayCalculator>();
    }

    /// <summary>
    /// Calling AddCalendarServices twice is idempotent (TryAddSingleton) — a single registration remains.
    /// </summary>
    [Fact]
    public void AddCalendarServices_IsIdempotent()
    {
        var services = new ServiceCollection();

        services.AddCalendarServices();
        services.AddCalendarServices();

        services.Count(d => d.ServiceType == typeof(IBusinessDayCalculator)).ShouldBe(1);
    }
}
