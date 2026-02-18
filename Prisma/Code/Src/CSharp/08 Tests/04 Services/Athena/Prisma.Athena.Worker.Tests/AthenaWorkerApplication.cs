using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
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
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
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
