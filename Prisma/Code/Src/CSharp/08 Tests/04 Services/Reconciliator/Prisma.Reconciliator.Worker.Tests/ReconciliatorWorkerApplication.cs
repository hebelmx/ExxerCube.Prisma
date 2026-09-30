using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Reconciliator.Worker.Tests;

/// <summary>
/// Test application factory for the Reconciliator Worker.
/// Removes real infrastructure registrations that require external resources
/// (SignalR hub connection, shared-volume mounts, DB) and replaces them with
/// NSubstitute mocks so the host DI graph builds and validates cleanly.
/// Export services (SiroXmlExporter / CompositeResponseExporter / IResponseExporter)
/// are left untouched — this factory exists precisely to prove the export DI graph
/// resolves without IMetadataExtractor.
/// </summary>
internal sealed class ReconciliatorWorkerApplication
    : WebApplicationFactory<global::Prisma.Reconciliator.Worker.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Minimal ProcessIdentity config so AddProcessIdentity does not throw on startup.
                ["ProcessIdentity:JwtSecret"] = "RECONCILIATOR-WORKER-TESTS-JWT-SECRET-FOR-TESTS-ONLY-32+",
                ["ProcessIdentity:JwtIssuer"] = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"] = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"] = "00:05:00",
                ["ProcessIdentity:Clearance"] = "Reconcile",
                ["Siara:Actor:ActorId"] = "reconciliator-test",
                ["Siara:Actor:DisplayName"] = "Reconciliator (Test)",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real infrastructure registrations that have external dependencies
            // (SignalR hub client, shared-volume FS, event publisher, classifier) and
            // replace with mocks so the host builds in-process for DI validation.
            // NOTE: export services (SiroXmlExporter, CompositeResponseExporter, IResponseExporter)
            // are intentionally NOT removed — testing that they resolve is the point of this factory.
            services.RemoveAll<IEventPublisher>();
            services.RemoveAll<IFileClassifier>();
            services.RemoveAll<IExpedienteHandoffStore>();

            var mockEventPublisher = Substitute.For<IEventPublisher>();
            mockEventPublisher
                .GetEventStream<ExxerCube.Prisma.Domain.Events.ExtractionCompletedEvent>()
                .Returns(System.Reactive.Linq.Observable
                    .Empty<ExxerCube.Prisma.Domain.Events.ExtractionCompletedEvent>());

            services.AddSingleton(mockEventPublisher);
            services.AddSingleton(Substitute.For<IFileClassifier>());
            services.AddSingleton(Substitute.For<IExpedienteHandoffStore>());
        });
    }
}
