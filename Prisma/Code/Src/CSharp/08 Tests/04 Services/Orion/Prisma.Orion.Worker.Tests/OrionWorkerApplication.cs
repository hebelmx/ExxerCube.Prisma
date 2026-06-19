using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// Test application factory for Orion Worker with health endpoints.
/// </summary>
internal class OrionWorkerApplication : WebApplicationFactory<global::Prisma.Orion.Worker.Program>
{
    /// <summary>
    /// The JWT secret used by both the hub host (server-side validation) and the test clients
    /// (token minting). Chosen as a fixed test value that is long enough for HMAC-SHA256 (32+ chars).
    /// </summary>
    internal const string TestJwtSecret = "ORION-WORKER-TESTS-JWT-SECRET-FOR-TESTS-ONLY-32+";

    /// <summary>
    /// Ensure SEQ_URL is set to a valid URL before the host starts.
    /// Serilog.Settings.Configuration v10 calls Environment.ExpandEnvironmentVariables on the
    /// serverUrl string from appsettings.json (%SEQ_URL%) at logger-creation time, which happens
    /// synchronously at the top of Program.cs — before ConfigureAppConfiguration can inject
    /// in-memory overrides. When SEQ_URL is absent the literal "%SEQ_URL%" is passed to
    /// SeqIngestionApiClient which rejects it with UriFormatException. Setting the env var here
    /// (once, idempotently) gives Serilog a valid URL so the host boots; no actual Seq server
    /// is needed — the sink will silently fail to connect and that is acceptable in tests.
    /// </summary>
    public OrionWorkerApplication()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEQ_URL")))
            Environment.SetEnvironmentVariable("SEQ_URL", "http://localhost:5341");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Inject the ProcessIdentity config values so the JWT bearer auth setup in Program.cs can read
        // the signing key, issuer, and audience. The builder.Configuration.GetSection() call in Program.cs
        // happens at startup — WebApplicationFactory applies this before the host builds.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProcessIdentity:JwtSecret"] = TestJwtSecret,
                ["ProcessIdentity:JwtIssuer"] = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"] = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"] = "00:05:00",
                ["ProcessIdentity:Clearance"] = "Download",
                ["Siara:Actor:ActorId"] = "orion-downloader-test",
                ["Siara:Actor:DisplayName"] = "Orion Downloader (Test)",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Register mock dependencies for IngestionOrchestrator
            services.AddSingleton(Substitute.For<IIngestionJournal>());
            services.AddSingleton(Substitute.For<IDocumentDownloader>());
            services.AddSingleton(Substitute.For<IEventPublisher>());

            // Override the real SIARA discovery source so the background watch loop stays quiet (no live
            // browser) during these health/dashboard endpoint tests: it presents nothing each cycle.
            var discoverySource = Substitute.For<ISiaraDocumentSource>();
            discoverySource.DiscoverCasesAsync(Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<SiaraCase>>.Success(System.Array.Empty<SiaraCase>()));
            services.AddSingleton(discoverySource);
        });
    }
}
