namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Custom WebApplicationFactory for testing the ExxerCube.Prisma.Web.UI application.
/// Configures the application for testing with test-specific settings and isolated localdb instances.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
{
    /// <summary>
    /// Disposes the factory and ensures all resources are cleaned up.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Ensure all services are properly disposed
            // WebApplicationFactory base class handles disposal of the host and services
            // This override ensures we don't leave any SQL Server connections open
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Configures the web host builder for testing.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure minimal Serilog for testing (avoid file/seq sinks in tests)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Configure test database connection string
            var testConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb_" + Guid.NewGuid() + ";Trusted_Connection=True;MultipleActiveResultSets=true";

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", testConnectionString },
                { "ConnectionStrings:ApplicationConnection", testConnectionString },
                { "ApiBaseUrl", "https://localhost:7062/" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Guard: remove any accidental InMemory EF provider registrations to avoid dual-provider errors.
            // These tests must exercise the real SQL Server provider.
            var inMemoryProviderDescriptors = services
                .Where(d =>
                    d.ImplementationType?.Assembly?.GetName().Name?.Contains("Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase) == true ||
                    d.ServiceType.Assembly.GetName().Name?.Contains("Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase) == true ||
                    (d.ImplementationFactory?.Method?.DeclaringType?.Assembly?.GetName().Name?.Contains("Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase) == true))
                .ToList();

            foreach (var descriptor in inMemoryProviderDescriptors)
            {
                services.Remove(descriptor);
            }

            // Ensure IPrismaDbContext resolves to the concrete PrismaDbContext when requested
            services.AddScoped<IPrismaDbContext>(sp => sp.GetRequiredService<PrismaDbContext>());

            // Disable selected hosted services that might create SQL connections
            // while keeping core audit processor registrations intact for DI expectations.
            // Remove other hosted services that might create connections
            var hostedServiceDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    (d.ImplementationType == typeof(AuditRetentionBackgroundService) ||
                     d.ImplementationType == typeof(SLAUpdateBackgroundService) ||
                     d.ImplementationType == typeof(SignalREventBroadcaster)))
                .ToList();

            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            // Remove SignalR event broadcaster if registered separately
            var broadcasterDescriptor = services
                .FirstOrDefault(d => d.ImplementationType == typeof(SignalREventBroadcaster));
            if (broadcasterDescriptor is not null)
            {
                services.Remove(broadcasterDescriptor);
            }

            // Ensure adaptive DOCX extraction is registered for DI validation (matches production Program.ConfigureServices)
            services.AddAdaptiveDocxExtraction();
            services.AddScoped<IAdaptiveDocxStrategy, StructuredDocxStrategy>();
            services.AddScoped<IAdaptiveDocxStrategy, ContextualDocxStrategy>();
            services.AddScoped<IAdaptiveDocxStrategy, TableBasedDocxStrategy>();
            services.AddScoped<IAdaptiveDocxStrategy, ComplementExtractionStrategy>();
            services.AddScoped<IAdaptiveDocxStrategy, SearchExtractionStrategy>();
            services.AddScoped<IReadOnlyList<IAdaptiveDocxStrategy>>(sp => sp.GetServices<IAdaptiveDocxStrategy>().ToList());

            // Configure test-specific services if needed
            // For example, you could replace real services with mocks here
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
        });

        builder.UseEnvironment("Testing");
    }
}
