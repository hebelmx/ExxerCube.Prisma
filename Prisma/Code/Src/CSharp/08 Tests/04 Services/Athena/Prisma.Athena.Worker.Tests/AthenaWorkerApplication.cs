using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
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

    /// <summary>
    /// Optional per-test configuration overrides (e.g. RC6 3.9 rotation-grace scenarios), applied after
    /// the default <see cref="ProcessIdentityOptions.SectionName"/> values below so a test can override
    /// individual keys — such as <c>ProcessIdentity:JwtSecret</c> or
    /// <c>ProcessIdentity:PreviousJwtSecrets:0</c> — without duplicating the whole config block. Null
    /// (the default) preserves existing behaviour exactly.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string?>? _additionalConfig;

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
    /// <param name="additionalConfig">
    /// Optional configuration overrides applied on top of the default in-memory <c>ProcessIdentity</c>
    /// section (see <see cref="_additionalConfig"/>). Omit for default behaviour.
    /// </param>
    public AthenaWorkerApplication(IReadOnlyDictionary<string, string?>? additionalConfig = null)
    {
        _additionalConfig = additionalConfig;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEQ_URL")))
            Environment.SetEnvironmentVariable("SEQ_URL", "http://localhost:5341");
    }

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

            // Applied last so per-test overrides (e.g. a rotated JwtSecret + PreviousJwtSecrets grace
            // list) win over the defaults above for the same keys.
            if (_additionalConfig is not null)
                config.AddInMemoryCollection(_additionalConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real infrastructure registrations that have external dependencies
            // (OCR engine files, imaging native libs, etc.) and replace with mocks so
            // the host-DI graph validates cleanly without I/O.
            // NOTE: export services are intentionally NOT registered in the Athena (Extractor)
            // worker — export is owned exclusively by the Reconciliator process (ADR-011).
            services.RemoveAll<IEventPublisher>();
            services.RemoveAll<IFileLoader>();
            services.RemoveAll<IImageQualityAnalyzer>();
            services.RemoveAll<IOcrExecutor>();
            services.RemoveAll<IFusionExpediente>();
            services.RemoveAll<IFileClassifier>();

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
        });
    }
}
