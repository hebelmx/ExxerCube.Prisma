namespace ExxerCube.Prisma.Infrastructure.Events;

/// <summary>
/// Extension methods for registering event infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory event bus to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Registers InMemoryEventBus as a singleton implementing both IEventPublisher and IEventSubscriber.
    /// This ensures a single event bus instance coordinates all pub/sub operations within the process.
    /// </remarks>
    public static IServiceCollection AddInMemoryEventBus(this IServiceCollection services)
    {
        // Register as singleton so all publishers and subscribers share the same instance
        services.TryAddSingleton<InMemoryEventBus>();

        // Register both interfaces to the same singleton instance
        services.TryAddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventBus>());
        services.TryAddSingleton<IEventSubscriber>(sp => sp.GetRequiredService<InMemoryEventBus>());

        return services;
    }
}
