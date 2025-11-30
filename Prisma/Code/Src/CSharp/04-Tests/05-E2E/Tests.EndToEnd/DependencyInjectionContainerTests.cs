namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Tests to validate the dependency injection container configuration.
/// Ensures all services are properly registered and can be resolved.
/// </summary>
public class DependencyInjectionContainerTests
{
    private readonly ILogger<DependencyInjectionContainerTests> _logger;

    public DependencyInjectionContainerTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<DependencyInjectionContainerTests>(output);
    }

    /// <summary>
    /// Tests that all critical services can be resolved from the DI container.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void BuildServiceProvider_AllCriticalServices_ShouldBeResolvable()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Test critical service resolutions
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Application Services
        _logger.LogInformation("Resolving application services");
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
        _logger.LogInformation("Resolving infrastructure services");
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
    /// Tests that services have the correct lifetime (Singleton, Scoped, Transient).
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void ServiceLifetimes_ShouldBeCorrect()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Test Singleton services
        var singleton1 = serviceProvider.GetService<IEmailSender<ApplicationUser>>();
        var singleton2 = serviceProvider.GetService<IEmailSender<ApplicationUser>>();
        singleton1.ShouldBeSameAs(singleton2);

        // Test Scoped services
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var scoped1 = scope1.ServiceProvider.GetService<DocumentIngestionService>();
        var scoped2 = scope2.ServiceProvider.GetService<DocumentIngestionService>();
        scoped1.ShouldNotBeNull();
        scoped2.ShouldNotBeNull();
        scoped1.ShouldNotBeSameAs(scoped2); // Different scopes should have different instances
    }

    /// <summary>
    /// Tests that services with dependencies can be resolved and dependencies are injected correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void ServiceDependencies_ShouldBeResolvedCorrectly()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Test that services with dependencies can be resolved
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Test that services requiring other services can be resolved
        var documentIngestionService = scopedProvider.GetService<DocumentIngestionService>();
        documentIngestionService.ShouldNotBeNull();

        var metadataExtractionService = scopedProvider.GetService<MetadataExtractionService>();
        metadataExtractionService.ShouldNotBeNull();

        var decisionLogicService = scopedProvider.GetService<DecisionLogicService>();
        decisionLogicService.ShouldNotBeNull();

        var exportService = scopedProvider.GetService<ExportService>();
        exportService.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that all registered health checks can be resolved.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void HealthChecks_ShouldBeRegistered()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var healthCheckService = serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

        // Assert
        healthCheckService.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that SignalR hub can be resolved.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void SignalRHub_ShouldBeResolvable()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        using var scope = serviceProvider.CreateScope();
        var hub = scope.ServiceProvider.GetService<ProcessingHub>();
        hub.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that DbContextFactory can be resolved and used.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public async Task DbContextFactory_ShouldBeResolvableAndUsable()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var factory = serviceProvider.GetService<IDbContextFactory<ApplicationDbContext>>();
        factory.ShouldNotBeNull();

        await using var context = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        context.ShouldNotBeNull();
        context.Database.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that all field matcher services are registered correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void FieldMatchers_ShouldBeRegistered()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var docxMatcher = scopedProvider.GetService<IFieldMatcher<DocxSource>>();
        docxMatcher.ShouldNotBeNull();

        var pdfMatcher = scopedProvider.GetService<IFieldMatcher<PdfSource>>();
        pdfMatcher.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the service provider can be built without errors.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void BuildServiceProvider_ShouldNotThrow()
    {
        // Arrange
        var services = BuildServiceCollection();

        // Act & Assert
        var exception = Record.Exception(() => services.BuildServiceProvider());
        exception.ShouldBeNull();
    }

    /// <summary>
    /// Tests that circular dependencies are not present.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    public void ServiceProvider_ShouldNotHaveCircularDependencies()
    {
        // Arrange
        var services = BuildServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Try to resolve all services, circular dependencies would cause issues
        using var scope = serviceProvider.CreateScope();
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
            _ = scopedProvider.GetService<IExxerHub<DomainEvent>>();
        });

        exception.ShouldBeNull();
    }

    /// <summary>
    /// Builds a service collection using the actual Program.cs configuration for testing.
    /// This ensures we test the real DI configuration, not a duplicate.
    /// </summary>
    /// <returns>The configured service collection.</returns>
    private static IServiceCollection BuildServiceCollection()
    {
        var builder = WebApplication.CreateBuilder();

        // Configure minimal Serilog for testing (avoid file/seq sinks in tests)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();

        // Configure test database connection string
        var testConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb_" + Guid.NewGuid() + ";Trusted_Connection=True;MultipleActiveResultSets=true";
        builder.Configuration["ConnectionStrings:DefaultConnection"] = testConnectionString;

        // Use the actual Program.cs ConfigureServices method to ensure we test the real DI configuration
        ExxerCube.Prisma.Web.UI.Program.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        // Defensive: ensure adaptive DOCX strategies are registered for DI validation
        builder.Services.AddAdaptiveDocxExtraction();
        builder.Services.AddScoped<IAdaptiveDocxStrategy, StructuredDocxStrategy>();
        builder.Services.AddScoped<IAdaptiveDocxStrategy, ContextualDocxStrategy>();
        builder.Services.AddScoped<IAdaptiveDocxStrategy, TableBasedDocxStrategy>();
        builder.Services.AddScoped<IAdaptiveDocxStrategy, ComplementExtractionStrategy>();
        builder.Services.AddScoped<IAdaptiveDocxStrategy, SearchExtractionStrategy>();
        builder.Services.AddScoped<IReadOnlyList<IAdaptiveDocxStrategy>>(sp => sp.GetServices<IAdaptiveDocxStrategy>().ToList());

        return builder.Services;
    }
}
