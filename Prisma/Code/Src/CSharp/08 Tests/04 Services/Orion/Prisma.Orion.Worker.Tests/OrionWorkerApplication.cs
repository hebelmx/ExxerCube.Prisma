using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// Test application factory for Orion Worker with health endpoints.
/// </summary>
internal class OrionWorkerApplication : WebApplicationFactory<global::Prisma.Orion.Worker.Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Register mock dependencies for IngestionOrchestrator
            services.AddSingleton(Substitute.For<IIngestionJournal>());
            services.AddSingleton(Substitute.For<IDocumentDownloader>());
            services.AddSingleton(Substitute.For<IEventPublisher>());
        });
    }
}