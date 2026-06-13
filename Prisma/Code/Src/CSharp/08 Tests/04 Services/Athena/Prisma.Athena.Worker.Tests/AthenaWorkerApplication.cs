using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Athena.Worker.Tests;

/// <summary>
/// Test application factory for Athena Worker with all pipeline services mocked.
/// Removes real infrastructure registrations (which have unresolvable dependencies)
/// and replaces them with NSubstitute mocks to pass DI validation.
/// </summary>
internal class AthenaWorkerApplication : WebApplicationFactory<global::Prisma.Athena.Worker.Program>
{
    /// <summary>
    /// The JWT secret used by both the hub host (server-side validation) and the test clients
    /// (token minting). Chosen as a fixed test value that is long enough for HMAC-SHA256 (32+ chars).
    /// </summary>
    internal const string TestJwtSecret = "ATHENA-WORKER-TESTS-JWT-SECRET-FOR-TESTS-ONLY-32+";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Inject the ProcessIdentity config values so the JWT bearer auth setup in Program.cs can read
        // the signing key, issuer, and audience before the DI container is built.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProcessIdentity:JwtSecret"] = TestJwtSecret,
                ["ProcessIdentity:JwtIssuer"] = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"] = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"] = "00:05:00",
                ["ProcessIdentity:Clearance"] = "Extract",
                ["Siara:Actor:ActorId"] = "athena-extractor-test",
                ["Siara:Actor:DisplayName"] = "Athena Extractor (Test)",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real infrastructure registrations that have unresolvable
            // dependencies (e.g., AdaptiveExporter requires ITemplateRepository)
            services.RemoveAll<IEventPublisher>();
            services.RemoveAll<IFileLoader>();
            services.RemoveAll<IImageQualityAnalyzer>();
            services.RemoveAll<IOcrExecutor>();
            services.RemoveAll<IFusionExpediente>();
            services.RemoveAll<IFileClassifier>();
            services.RemoveAll<IAdaptiveExporter>();

            // Replace with NSubstitute mocks
            var mockEventPublisher = Substitute.For<IEventPublisher>();
            mockEventPublisher.GetEventStream<ExxerCube.Prisma.Domain.Events.DocumentDownloadedEvent>()
                .Returns(System.Reactive.Linq.Observable.Empty<ExxerCube.Prisma.Domain.Events.DocumentDownloadedEvent>());

            services.AddSingleton(mockEventPublisher);
            services.AddSingleton(Substitute.For<IFileLoader>());
            services.AddSingleton(Substitute.For<IImageQualityAnalyzer>());
            services.AddSingleton(Substitute.For<IOcrExecutor>());
            services.AddSingleton(Substitute.For<IFusionExpediente>());
            services.AddSingleton(Substitute.For<IFileClassifier>());
            services.AddSingleton(Substitute.For<IAdaptiveExporter>());
        });
    }
}
