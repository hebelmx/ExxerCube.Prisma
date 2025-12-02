namespace Prisma.Composition.Tests;

/// <summary>
/// ITDD tests proving DI composition and service resolution works correctly.
/// </summary>
/// <remarks>
/// These tests verify:
/// - All orchestration services register correctly
/// - IServiceProvider can resolve all required services
/// - No circular dependencies exist
/// - Singleton lifetimes configured correctly (IEventPublisher/IEventSubscriber share instance)
/// - ValidateScopes and ValidateOnBuild pass
/// </remarks>
public sealed class ServiceResolutionTests
{
    [Fact]
    public void ServiceProvider_ResolvesInMemoryEventBus_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Act
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var subscriber = provider.GetRequiredService<IEventSubscriber>();
        var eventBus = provider.GetRequiredService<InMemoryEventBus>();

        // Assert
        publisher.ShouldNotBeNull();
        subscriber.ShouldNotBeNull();
        eventBus.ShouldNotBeNull();

        // CRITICAL: IEventPublisher and IEventSubscriber must be the same instance (singleton pattern)
        publisher.ShouldBeSameAs(subscriber, "IEventPublisher and IEventSubscriber must share the same InMemoryEventBus instance");
        publisher.ShouldBeSameAs(eventBus, "IEventPublisher must be the InMemoryEventBus instance");
    }

    [Fact]
    public void ServiceProvider_RegistersEventBusAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        var provider = services.BuildServiceProvider();

        // Act - resolve multiple times
        var publisher1 = provider.GetRequiredService<IEventPublisher>();
        var publisher2 = provider.GetRequiredService<IEventPublisher>();
        var subscriber1 = provider.GetRequiredService<IEventSubscriber>();
        var subscriber2 = provider.GetRequiredService<IEventSubscriber>();

        // Assert - all references should be the same instance (singleton)
        publisher1.ShouldBeSameAs(publisher2, "IEventPublisher should be singleton");
        subscriber1.ShouldBeSameAs(subscriber2, "IEventSubscriber should be singleton");
        publisher1.ShouldBeSameAs(subscriber1, "Both interfaces should resolve to the same instance");
    }

    [Fact]
    public void ServiceProvider_NoCircularDependencies_EventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act & Assert - should not throw on build with validation
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Additional check: resolve all registered services
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var subscriber = provider.GetRequiredService<IEventSubscriber>();
        var eventBus = provider.GetRequiredService<InMemoryEventBus>();

        // If we got here without exception, composition is valid
        publisher.ShouldNotBeNull();
        subscriber.ShouldNotBeNull();
        eventBus.ShouldNotBeNull();
    }

    [Fact]
    public void ServiceProvider_EventBusRegistration_IsIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - register multiple times (simulates multiple AddInMemoryEventBus calls)
        services.AddInMemoryEventBus();
        services.AddInMemoryEventBus();
        services.AddInMemoryEventBus();

        var provider = services.BuildServiceProvider();

        // Assert - should still work, should not register duplicates
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var subscriber = provider.GetRequiredService<IEventSubscriber>();

        publisher.ShouldNotBeNull();
        subscriber.ShouldNotBeNull();
        publisher.ShouldBeSameAs(subscriber);
    }

    [Fact]
    public void ServiceProvider_AllOrchestrationServices_RegisterCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Register all orchestration services (as they become available)
        services.AddInMemoryEventBus();
        // TODO: Add services.AddOrionIngestion() when implemented
        // TODO: Add services.AddAthenaProcessing() when implemented

        // Act - validate composition
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Assert - all core services resolve
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var subscriber = provider.GetRequiredService<IEventSubscriber>();

        publisher.ShouldNotBeNull();
        subscriber.ShouldNotBeNull();

        // TODO: Assert Orion/Athena services when implemented
    }

    [Fact]
    public void ServiceProvider_WithoutLogging_ThrowsOnBuild()
    {
        // Arrange - some environments may not have logging configured
        var services = new ServiceCollection();
        services.AddInMemoryEventBus();

        // Act & Assert - should fail because InMemoryEventBus requires ILogger
        // .NET 9+ wraps validation errors in AggregateException
        Should.Throw<AggregateException>(() =>
        {
            var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        });
    }
}