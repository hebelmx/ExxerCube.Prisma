using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ExxerCube.Prisma.Web.UI;

namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Custom WebApplicationFactory for testing the ExxerCube.Prisma.Web.UI application.
/// Configures the application for testing with in-memory database and test-specific settings.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
{
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
                { "ApiBaseUrl", "https://localhost:7062/" }
            });
        });

        builder.ConfigureServices(services =>
        {
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

