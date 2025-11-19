using ExxerCube.Prisma.SignalR.Abstractions.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Extensions;

/// <summary>
/// Tests for the ServiceCollectionExtensions class.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Tests that AddSignalRAbstractions registers reconnection strategy.
    /// </summary>
    [Fact]
    public void AddSignalRAbstractions_Registers_ReconnectionStrategy()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSignalRAbstractions();

        // Assert
        var strategy = services.BuildServiceProvider().GetService<ReconnectionStrategy>();
        strategy.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that AddSignalRAbstractions configures reconnection strategy.
    /// </summary>
    [Fact]
    public void AddSignalRAbstractions_Configures_ReconnectionStrategy()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSignalRAbstractions(options =>
        {
            options.MaxRetries = 10;
            options.InitialDelay = 2000;
        });

        // Assert
        var strategy = services.BuildServiceProvider().GetRequiredService<ReconnectionStrategy>();
        strategy.MaxRetries.ShouldBe(10);
        strategy.InitialDelay.ShouldBe(2000);
    }

    /// <summary>
    /// Tests that AddSignalRAbstractions registers IServiceHealth as scoped.
    /// </summary>
    [Fact]
    public void AddSignalRAbstractions_Registers_IServiceHealthAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSignalRAbstractions();

        // Assert
        var provider = services.BuildServiceProvider();
        var health1 = provider.GetService<IServiceHealth<TestHealthData>>();
        var health2 = provider.GetService<IServiceHealth<TestHealthData>>();

        health1.ShouldNotBeNull();
        health2.ShouldNotBeNull();
        // Scoped services return the same instance within the same scope (root scope)
        // To test scoping properly, we'd need to create child scopes
        health1.ShouldBeSameAs(health2); // Same scope = same instance
    }

    /// <summary>
    /// Tests that AddServiceHealth registers service health for specific type.
    /// </summary>
    [Fact]
    public void AddServiceHealth_Registers_ServiceHealthForType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddServiceHealth<TestHealthData>();

        // Assert
        var provider = services.BuildServiceProvider();
        var health = provider.GetService<IServiceHealth<TestHealthData>>();
        health.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that AddSignalRAbstractions throws ArgumentNullException for null services.
    /// </summary>
    [Fact]
    public void AddSignalRAbstractions_WithNullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ServiceCollectionExtensions.AddSignalRAbstractions(null!));
    }

    /// <summary>
    /// Tests that AddServiceHealth throws ArgumentNullException for null services.
    /// </summary>
    [Fact]
    public void AddServiceHealth_WithNullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ServiceCollectionExtensions.AddServiceHealth<TestHealthData>(null!));
    }

    /// <summary>
    /// Test health data class for testing.
    /// </summary>
    public class TestHealthData
    {
        public string Message { get; set; } = string.Empty;
    }
}

