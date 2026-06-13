using ExxerCube.Prisma.Domain.Interfaces;
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
            discoverySource.DiscoverDocumentIdsAsync(Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<string>>.Success(System.Array.Empty<string>()));
            services.AddSingleton(discoverySource);
        });
    }
}
