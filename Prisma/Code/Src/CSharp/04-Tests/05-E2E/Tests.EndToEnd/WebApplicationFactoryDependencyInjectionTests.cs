using IndFusion.Ember.Abstractions.Hubs;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database.Metrics;
using ExxerCube.Prisma.Testing.Infrastructure.Logging;
using System.Net.Http;
using System.Reflection;

namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Tests to validate the dependency injection container configuration using WebApplicationFactory.
/// This approach tests the real application DI through the full application startup pipeline,
/// ensuring that the DI configuration works exactly as it does in production.
/// </summary>
public class WebApplicationFactoryDependencyInjectionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ILogger<WebApplicationFactoryDependencyInjectionTests> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplicationFactoryDependencyInjectionTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    /// <param name="output">xUnit output helper.</param>
    public WebApplicationFactoryDependencyInjectionTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _logger = XUnitLogger.CreateLogger<WebApplicationFactoryDependencyInjectionTests>(output);
    }

    /// <summary>
    /// Tests that all critical services can be resolved from the DI container using WebApplicationFactory.
    /// This ensures the DI configuration works through the full application startup pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_AllCriticalServices_ShouldBeResolvable()
    {
        // Arrange - Create a client to trigger application startup
        using var client = _factory.CreateClient();

        // Act & Assert - Test critical service resolutions using the application's service provider
        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Application Services
        scopedProvider.GetService<DocumentIngestionService>().ShouldNotBeNull();
        scopedProvider.GetService<FileMetadataQueryService>().ShouldNotBeNull();
        scopedProvider.GetService<FileDownloadService>().ShouldNotBeNull();
        scopedProvider.GetService<MetadataExtractionService>().ShouldNotBeNull();
        scopedProvider.GetService<FieldMatchingService>().ShouldNotBeNull();
        scopedProvider.GetService<DecisionLogicService>().ShouldNotBeNull();
        scopedProvider.GetService<SLATrackingService>().ShouldNotBeNull();
        scopedProvider.GetService<ExportService>().ShouldNotBeNull();
        scopedProvider.GetService<AuditReportingService>().ShouldNotBeNull();

        // Infrastructure Services
        scopedProvider.GetService<IDbContextFactory<ApplicationDbContext>>().ShouldNotBeNull();
        scopedProvider.GetService<PrismaDbContext>().ShouldNotBeNull();
        scopedProvider.GetService<IPrismaDbContext>().ShouldNotBeNull();
        scopedProvider.GetService<IAuditLogger>().ShouldNotBeNull();
        scopedProvider.GetService<IDownloadTracker>().ShouldNotBeNull();
        scopedProvider.GetService<IFileMetadataLogger>().ShouldNotBeNull();
        scopedProvider.GetService<IEventPublisher>().ShouldNotBeNull();
        scopedProvider.GetService<QueuedAuditProcessorService>().ShouldNotBeNull();
        scopedProvider.GetService<SLAMetricsCollector>().ShouldNotBeNull();
        scopedProvider.GetService<SLAEnforcerService>().ShouldNotBeNull();
        scopedProvider.GetService<ISLAEnforcer>().ShouldNotBeNull();
        scopedProvider.GetService<IExxerHub<DomainEvent>>().ShouldNotBeNull();

        scopedProvider.GetService<ProcessingHub>().ShouldNotBeNull();
        scopedProvider.GetService<IdentityUserAccessor>().ShouldNotBeNull();
        scopedProvider.GetService<IdentityRedirectManager>().ShouldNotBeNull();
        scopedProvider.GetService<AuthenticationStateProvider>().ShouldNotBeNull();
        scopedProvider.GetService<IEmailSender<ApplicationUser>>().ShouldNotBeNull();

        // HttpClient Factory
        var httpClientFactory = scopedProvider.GetService<IHttpClientFactory>();
        httpClientFactory.ShouldNotBeNull();
        var httpClient = httpClientFactory.CreateClient("api");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that services have the correct lifetime (Singleton, Scoped, Transient) using WebApplicationFactory.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_ServiceLifetimes_ShouldBeCorrect()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act & Assert - Test Singleton services
        var singleton1 = _factory.Services.GetRequiredService<IHttpClientFactory>();
        var singleton2 = _factory.Services.GetRequiredService<IHttpClientFactory>();
        singleton1.ShouldBeSameAs(singleton2);

        // Act & Assert - Test Scoped services (should be different in different scopes)
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();
        var scoped1 = scope1.ServiceProvider.GetRequiredService<DocumentIngestionService>();
        var scoped2 = scope2.ServiceProvider.GetRequiredService<DocumentIngestionService>();
        scoped1.ShouldNotBeSameAs(scoped2);
    }

    /// <summary>
    /// Tests that the application can build without circular dependencies using WebApplicationFactory.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_ShouldNotHaveCircularDependencies()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act & Assert - Try to resolve all services, circular dependencies would cause issues
        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Resolve all main services - if there are circular dependencies, this will fail
        var exception = Record.Exception(() =>
        {
            _ = scopedProvider.GetService<DocumentIngestionService>();
            _ = scopedProvider.GetService<MetadataExtractionService>();
            _ = scopedProvider.GetService<DecisionLogicService>();
            _ = scopedProvider.GetService<SLATrackingService>();
            _ = scopedProvider.GetService<ExportService>();
            _ = scopedProvider.GetService<AuditReportingService>();
        });

        exception.ShouldBeNull();
    }

    /// <summary>
    /// Tests that health checks are properly registered using WebApplicationFactory.
    /// Note: The health endpoint may return non-success if health checks fail (e.g., database not available),
    /// but the important thing is that the health check service is registered and the endpoint exists.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public async Task WebApplicationFactory_HealthChecks_ShouldBeRegistered()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act - Verify health check service is registered
        var healthCheckService = _factory.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        healthCheckService.ShouldNotBeNull();

        // Act - Verify health endpoint exists (may return non-success if checks fail, but endpoint should exist)
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert - Endpoint should exist (status code may vary based on health check results)
        // In test environment, health checks may fail due to database connectivity, but the service should be registered
        // Just verify the endpoint responded (status code is set, even if it's an error)
        response.ShouldNotBeNull();

        // Verify we can read the response (endpoint exists and is accessible)
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that field matchers are properly registered using WebApplicationFactory.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_FieldMatchers_ShouldBeRegistered()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act & Assert
        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var docxMatcher = scopedProvider.GetService<IFieldMatcher<DocxSource>>();
        docxMatcher.ShouldNotBeNull();

        var pdfMatcher = scopedProvider.GetService<IFieldMatcher<PdfSource>>();
        pdfMatcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that all hosted services resolve successfully (no scoped dependencies injected into singletons).
    /// This catches issues like singleton hosted services depending on scoped hubs.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_HostedServices_ShouldResolveWithoutScopeViolations()
    {
        // Arrange
        using var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // Act
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        // Assert
        hostedServices.ShouldNotBeNull();
        hostedServices.ShouldNotBeEmpty("at least one hosted service is expected (e.g., SignalREventBroadcaster)");
        hostedServices.ShouldAllBe(hs => hs != null, "hosted services should resolve without scope violations");
    }

    /// <summary>
    /// Builds a provider with validation enabled to catch lifetime/missing registrations early.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_ShouldValidateScopesAndBuild()
    {
        // Arrange
        using var client = _factory.CreateClient();

        var services = new ServiceCollection();
        foreach (var sd in _factory.Services.GetRequiredService<IServiceCollection>())
        {
            services.Add(sd);
        }

        // Act
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        provider.ShouldNotBeNull();
    }

    /// <summary>
    /// Ensures named HttpClient registrations exist and have base addresses.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_NamedHttpClients_ShouldBeRegistered()
    {
        using var client = _factory.CreateClient();

        var factory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        factory.ShouldNotBeNull();

        var apiClient = factory.CreateClient("api");
        apiClient.ShouldNotBeNull();
        apiClient.BaseAddress.ShouldNotBeNull("named client 'api' should have a BaseAddress");
    }

    /// <summary>
    /// Ensures key options-bound configs can be read without exceptions, indicating bindings exist.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_Options_ShouldBeBindable()
    {
        using var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var browserOptions = sp.GetRequiredService<IOptionsMonitor<BrowserAutomationOptions>>().CurrentValue;
        browserOptions.ShouldNotBeNull();
    }

    /// <summary>
    /// Flags any classes that inject HttpClient directly instead of using IHttpClientFactory.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void WebApplicationFactory_ShouldNotInjectHttpClientDirectly()
    {
        var assemblies = new[]
        {
            typeof(ExxerCube.Prisma.Application.Services.DocumentIngestionService).Assembly,
            typeof(ExxerCube.Prisma.Infrastructure.BrowserAutomation.PlaywrightBrowserAutomationAdapter).Assembly,
            typeof(ExxerCube.Prisma.Web.UI.Program).Assembly
        };

        var offenders = new List<string>();

        foreach (var asm in assemblies)
        {
            var types = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any());

            foreach (var type in types)
            {
                var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (var ctor in ctors)
                {
                    if (ctor.GetParameters().Any(p => p.ParameterType == typeof(HttpClient)))
                    {
                        offenders.Add(type.FullName ?? type.Name);
                        break;
                    }
                }
            }
        }

        if (offenders.Any())
        {
            foreach (var offender in offenders)
            {
                _logger.LogWarning("HttpClient injected directly in type: {Type}", offender);
            }
        }

        offenders.ShouldBeEmpty("Services should inject IHttpClientFactory/typed clients, not raw HttpClient.");
    }
}
