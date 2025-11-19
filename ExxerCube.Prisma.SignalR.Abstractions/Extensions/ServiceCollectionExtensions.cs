using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Extensions;

/// <summary>
/// Extension methods for dependency injection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SignalR abstractions services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for reconnection strategy.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSignalRAbstractions(
        this IServiceCollection services,
        Action<ReconnectionStrategy>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Configure reconnection strategy
        var reconnectionStrategy = new ReconnectionStrategy();
        configure?.Invoke(reconnectionStrategy);
        services.AddSingleton(reconnectionStrategy);

        // Register service health as scoped (can be overridden per service)
        services.AddScoped(typeof(IServiceHealth<>), typeof(ServiceHealth<>));

        return services;
    }

    /// <summary>
    /// Adds a service health monitor for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of health data.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServiceHealth<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.Add(new ServiceDescriptor(
            typeof(IServiceHealth<T>),
            sp => new ServiceHealth<T>(sp.GetRequiredService<ILogger<ServiceHealth<T>>>()),
            lifetime));

        return services;
    }
}

