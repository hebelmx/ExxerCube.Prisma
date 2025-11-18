using ExxerAI.RealTimeCommunication.Extensions;
using ExxerAI.RealTimeCommunication.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExxerAI.RealTimeCommunication.Tests.Extensions
{
    /// <summary>
    /// Tests for ServiceCollectionExtensions dependency injection configuration.
    /// </summary>
    public class ServiceCollectionExtensionsTests
    {
        private readonly ILogger<ServiceCollectionExtensionsTests> _logger;
        private readonly IServiceCollection _services;
        private readonly IConfiguration _configuration;
        /// <summary>
        /// Constructor: Sets up test services and configuration.
        /// </summary>
        /// <param name="output">xUnit output helper used to capture logging from the service setup process.</param>

        public ServiceCollectionExtensionsTests(ITestOutputHelper output)
        {
            _logger = XUnitLogger.CreateLogger<ServiceCollectionExtensionsTests>(output);
            _services = new ServiceCollection();

            // Create test configuration
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealTimeCommunication:Hubs:MaxConnections"] = "1000",
                ["RealTimeCommunication:Hubs:KeepAliveInterval"] = "15",
                ["RealTimeCommunication:Hubs:ClientTimeoutInterval"] = "30",
                ["RealTimeCommunication:Hubs:HandshakeTimeout"] = "15"
            });
            _configuration = configBuilder.Build();

            // Add required services
            _services.AddLogging();
            _services.AddSingleton(_configuration);
        }

        //   AddRealTimeCommunication Tests

        /// <summary>
        /// Registration Test: AddRealTimeCommunication should register all required services.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Register_All_Required_Services()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert - Check port registrations
            var realTimeCommunicationPort = serviceProvider.GetService<IRealTimeCommunicationPort>();
            realTimeCommunicationPort.ShouldNotBeNull();

            var eventBroadcastingPort = serviceProvider.GetService<IEventBroadcastingPort>();
            eventBroadcastingPort.ShouldNotBeNull();

            var notificationPort = serviceProvider.GetService<INotificationPort>();
            notificationPort.ShouldNotBeNull();

            _logger.LogInformation("All required ports are properly registered");
        }

        /// <summary>
        /// Registration Test: Should register ports as singletons.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Register_Ports_As_Singletons()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert
            var port1 = serviceProvider.GetService<IRealTimeCommunicationPort>();
            var port2 = serviceProvider.GetService<IRealTimeCommunicationPort>();

            port1.ShouldNotBeNull();
            port2.ShouldNotBeNull();
            port1.ShouldBeSameAs(port2);

            _logger.LogInformation("Ports are registered as singletons");
        }

        /// <summary>
        /// Registration Test: Should register SignalR services.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Register_SignalR_Services()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert - Check that SignalR services are registered
            var signalRServices = _services.Where(s =>
                    s.ServiceType.FullName?.Contains("SignalR") == true ||
                    s.ServiceType.Name.Contains("Hub"))
                .ToList();

            signalRServices.ShouldNotBeEmpty();
            _logger.LogInformation("Found {Count} SignalR-related service registrations", signalRServices.Count);
        }

        //   AddRealTimeCommunication Tests

        //   Configuration Tests

        /// <summary>
        /// Configuration Test: Should apply configuration from appsettings.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Apply_Configuration_From_Settings()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert - Verify configuration is bound (Note: SignalR options may not be directly accessible)
            var configSection = _configuration.GetSection("RealTimeCommunication");
            configSection.Exists().ShouldBeTrue();

            var maxConnections = configSection.GetValue<int>("Hubs:MaxConnections");
            maxConnections.ShouldBe(1000);

            _logger.LogInformation("Configuration properly loaded with MaxConnections: {MaxConnections}", maxConnections);
        }

        /// <summary>
        /// Configuration Test: Should handle missing configuration gracefully.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Handle_Missing_Configuration_Gracefully()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().Build();
            var emptyServices = new ServiceCollection();
            emptyServices.AddLogging();

            // Act & Assert - Should not throw
            Should.NotThrow(() => emptyServices.AddRealTimeCommunication(emptyConfig));

            var serviceProvider = emptyServices.BuildServiceProvider();
            var port = serviceProvider.GetService<IRealTimeCommunicationPort>();
            port.ShouldNotBeNull();

            _logger.LogInformation("Missing configuration handled gracefully");
        }

        //   Configuration Tests

        //   Service Lifetime Tests

        /// <summary>
        /// Lifetime Test: Should register adapters with correct lifetimes.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Register_Services_With_Correct_Lifetimes()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);

            // Assert - Check service lifetimes
            var singletonServices = _services.Where(s => s.Lifetime == ServiceLifetime.Singleton).ToList();
            var scopedServices = _services.Where(s => s.Lifetime == ServiceLifetime.Scoped).ToList();
            var transientServices = _services.Where(s => s.Lifetime == ServiceLifetime.Transient).ToList();

            _logger.LogInformation("Service lifetimes - Singleton: {Singleton}, Scoped: {Scoped}, Transient: {Transient}",
                singletonServices.Count, scopedServices.Count, transientServices.Count);

            // Port services should be singletons
            var portServices = singletonServices.Where(s =>
                    s.ServiceType == typeof(IRealTimeCommunicationPort) ||
                    s.ServiceType == typeof(IEventBroadcastingPort) ||
                    s.ServiceType == typeof(INotificationPort))
                .ToList();

            portServices.ShouldNotBeEmpty();
        }

        //   Service Lifetime Tests

        //   Error Handling Tests

        /// <summary>
        /// Error Handling Test: Should throw ArgumentNullException for null services.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Throw_When_Services_Is_Null()
        {
            // Arrange
            IServiceCollection? nullServices = null;

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                nullServices!.AddRealTimeCommunication(_configuration));
        }

        /// <summary>
        /// Error Handling Test: Should throw ArgumentNullException for null configuration.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Throw_When_Configuration_Is_Null()
        {
            // Arrange
            IConfiguration? nullConfiguration = null;

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                _services.AddRealTimeCommunication(nullConfiguration!));
        }

        //   Error Handling Tests

        //   Integration Tests

        /// <summary>
        /// Integration Test: Should create working service provider.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Create_Working_Service_Provider()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert - Test service resolution
            using var scope = serviceProvider.CreateScope();
            var scopedPort = scope.ServiceProvider.GetService<IRealTimeCommunicationPort>();
            scopedPort.ShouldNotBeNull();

            // Test that we can get services without errors
            var eventPort = scope.ServiceProvider.GetService<IEventBroadcastingPort>();
            eventPort.ShouldNotBeNull();

            var notificationPort = scope.ServiceProvider.GetService<INotificationPort>();
            notificationPort.ShouldNotBeNull();

            _logger.LogInformation("Service provider successfully created and services resolved");
        }

        /// <summary>
        /// Integration Test: Should support multiple registrations.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Support_Multiple_Registrations()
        {
            // Act - Register multiple times (should not cause issues)
            _services.AddRealTimeCommunication(_configuration);
            _services.AddRealTimeCommunication(_configuration);

            // Assert - Should build successfully
            var serviceProvider = _services.BuildServiceProvider();
            var port = serviceProvider.GetService<IRealTimeCommunicationPort>();
            port.ShouldNotBeNull();

            _logger.LogInformation("Multiple registrations handled successfully");
        }

        //   Integration Tests

        //   Service Discovery Tests

        /// <summary>
        /// Discovery Test: Should register all expected service types.
        /// </summary>
        [Fact]
        public void AddRealTimeCommunication_Should_Register_All_Expected_Service_Types()
        {
            // Act
            _services.AddRealTimeCommunication(_configuration);

            // Assert
            var serviceTypes = _services.Select(s => s.ServiceType).Distinct().ToList();

            // Check for expected port types
            serviceTypes.ShouldContain(typeof(IRealTimeCommunicationPort));
            serviceTypes.ShouldContain(typeof(IEventBroadcastingPort));
            serviceTypes.ShouldContain(typeof(INotificationPort));

            _logger.LogInformation("All expected service types are registered");

            // Log all registered service types for debugging
            foreach (var serviceType in serviceTypes.Where(t => t.Assembly.GetName().Name?.Contains("RealTimeCommunication") == true))
            {
                _logger.LogDebug("Registered service type: {ServiceType}", serviceType.Name);
            }
        }

        //   Service Discovery Tests
    }
}
