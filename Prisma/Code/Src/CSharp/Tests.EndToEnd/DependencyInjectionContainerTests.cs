namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Tests to validate the dependency injection container configuration.
/// Ensures all services are properly registered and can be resolved.
/// </summary>
public class DependencyInjectionContainerTests
{
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
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();

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

        await using var context = await factory.CreateDbContextAsync();
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
        });

        exception.ShouldBeNull();
    }

    /// <summary>
    /// Builds a service collection matching Program.cs configuration for testing.
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

        // Add MudBlazor services
        builder.Services.AddMudServices();

        // Add SignalR for real-time updates
        builder.Services.AddSignalR();

        // Register SignalR hub as scoped
        builder.Services.AddScoped<ProcessingHub>();

        // Add OCR processing services with test configuration
        var pythonModulesPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Python", "ocr_modules");
        var pythonConfig = new ExxerCube.Prisma.Infrastructure.Python.PythonConfiguration
        {
            ModulesPath = pythonModulesPath,
            PythonExecutablePath = "python",
            MaxConcurrency = 5,
            OperationTimeoutSeconds = 30,
            EnableDebugging = false
        };
        builder.Services.AddOcrProcessingServices(pythonConfig);

        // Add services to the container
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add API controllers
        builder.Services.AddControllers();

        // Add HttpClient for API calls
        builder.Services.AddHttpClient("api", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7062/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        // Use in-memory database for testing
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb_" + Guid.NewGuid() + ";Trusted_Connection=True;MultipleActiveResultSets=true";
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        // Add Story 1.1 services: Browser Automation, File Storage, and Database services
        builder.Services.AddDatabaseServices(connectionString, builder.Configuration);
        builder.Services.AddBrowserAutomationServices(options =>
        {
            builder.Configuration.GetSection("BrowserAutomation").Bind(options);
        });
        builder.Services.AddFileStorageServices(options =>
        {
            builder.Configuration.GetSection("FileStorage").Bind(options);
        });
        builder.Services.AddScoped<DocumentIngestionService>();
        builder.Services.AddScoped<FileMetadataQueryService>();
        builder.Services.AddScoped<FileDownloadService>();

        // Add Story 1.2 services: Extraction, Classification, and Metadata Extraction
        builder.Services.AddExtractionServices();
        builder.Services.AddClassificationServices(builder.Configuration);
        builder.Services.AddScoped<MetadataExtractionService>();

        // Add Story 1.3 services: Field Matching and Unified Metadata Generation
        builder.Services.AddScoped<FieldMatchingService>();
        builder.Services.AddScoped(typeof(IFieldMatcher<DocxSource>), typeof(ExxerCube.Prisma.Infrastructure.Classification.FieldMatcherService<DocxSource>));
        builder.Services.AddScoped(typeof(IFieldMatcher<PdfSource>), typeof(ExxerCube.Prisma.Infrastructure.Classification.FieldMatcherService<PdfSource>));

        // Add Story 1.4 services: Decision Logic
        builder.Services.AddScoped<DecisionLogicService>();

        // Add Story 1.5 services: SLA Tracking and Escalation
        builder.Services.AddScoped<SLATrackingService>();

        // Add Story 1.7 & 1.8 services: Export Generation
        builder.Services.AddExportServices(builder.Configuration);
        builder.Services.AddScoped<ExportService>();

        // Add Story 1.9 services: Audit Reporting
        builder.Services.AddScoped<AuditReportingService>();

        // Add SLA health checks
        builder.Services.AddHealthChecks()
            .AddCheck<SLAEnforcerHealthCheck>(
                "sla_enforcer",
                tags: new[] { "sla", "database", "ready" })
            .AddCheck<SLABackgroundJobHealthCheck>(
                "sla_background_job",
                tags: new[] { "sla", "background", "ready" });

        return builder.Services;
    }
}

