namespace ExxerCube.Prisma.Tests.EndToEnd.DependencyValidation;

/// <summary>
/// Validates DI configuration end-to-end using WebApplicationFactory as a host, ensuring critical services resolve.
/// </summary>
public class DependencyValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ILogger<DependencyValidationTests> _logger;

    public DependencyValidationTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _logger = Meziantou.Extensions.Logging.Xunit.v3.XUnitLogger.CreateLogger<DependencyValidationTests>(output);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void AllCriticalServices_ShouldBeResolvable()
    {
        using var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        scopedProvider.GetService<DocumentIngestionService>().ShouldNotBeNull();
        scopedProvider.GetService<FileMetadataQueryService>().ShouldNotBeNull();
        scopedProvider.GetService<FileDownloadService>().ShouldNotBeNull();
        scopedProvider.GetService<MetadataExtractionService>().ShouldNotBeNull();
        scopedProvider.GetService<FieldMatchingService>().ShouldNotBeNull();
        scopedProvider.GetService<DecisionLogicService>().ShouldNotBeNull();
        scopedProvider.GetService<SLATrackingService>().ShouldNotBeNull();
        scopedProvider.GetService<ExportService>().ShouldNotBeNull();
        scopedProvider.GetService<AuditReportingService>().ShouldNotBeNull();

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

        var httpClientFactory = scopedProvider.GetService<IHttpClientFactory>();
        httpClientFactory.ShouldNotBeNull();
        var httpClient = httpClientFactory.CreateClient("api");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void ServiceLifetimes_ShouldBeCorrect()
    {
        using var client = _factory.CreateClient();

        var singleton1 = _factory.Services.GetRequiredService<IHttpClientFactory>();
        var singleton2 = _factory.Services.GetRequiredService<IHttpClientFactory>();
        singleton1.ShouldBeSameAs(singleton2);

        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();
        var scoped1 = scope1.ServiceProvider.GetRequiredService<DocumentIngestionService>();
        var scoped2 = scope2.ServiceProvider.GetRequiredService<DocumentIngestionService>();
        scoped1.ShouldNotBeSameAs(scoped2);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void ShouldNotHaveCircularDependencies()
    {
        using var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

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

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public async Task HealthChecks_ShouldBeRegistered()
    {
        using var client = _factory.CreateClient();

        var healthCheckService = _factory.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        healthCheckService.ShouldNotBeNull();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        response.ShouldNotBeNull();
        _ = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void FieldMatchers_ShouldBeRegistered()
    {
        using var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        scopedProvider.GetService<IFieldMatcher<DocxSource>>().ShouldNotBeNull();
        scopedProvider.GetService<IFieldMatcher<PdfSource>>().ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void HostedServices_ShouldResolveWithoutScopeViolations()
    {
        using var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var hostedServices = provider.GetServices<IHostedService>().ToList();

        hostedServices.ShouldNotBeNull();
        hostedServices.ShouldNotBeEmpty("at least one hosted service is expected (e.g., SignalREventBroadcaster)");
        hostedServices.ShouldAllBe(hs => hs != null, "hosted services should resolve without scope violations");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void ShouldValidateScopesAndBuild()
    {
        using var client = _factory.CreateClient();

        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        provider.ShouldNotBeNull("Service provider should be available from WebApplicationFactory");
        provider.GetRequiredService<IServiceProviderIsService>().ShouldNotBeNull("Provider should support service validation checks");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void NamedHttpClients_ShouldBeRegistered()
    {
        using var client = _factory.CreateClient();

        var factory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        factory.ShouldNotBeNull();

        var apiClient = factory.CreateClient("api");
        apiClient.ShouldNotBeNull();
        apiClient.BaseAddress.ShouldNotBeNull("named client 'api' should have a BaseAddress");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void Options_ShouldBeBindable()
    {
        using var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var browserOptions = sp.GetRequiredService<IOptionsMonitor<BrowserAutomationOptions>>().CurrentValue;
        browserOptions.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "DI")]
    [Trait("Category", "WebApplicationFactory")]
    public void ShouldNotInjectHttpClientDirectly()
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

        offenders.ShouldBeEmpty("HttpClient should be injected via IHttpClientFactory instead of directly");
    }
}
