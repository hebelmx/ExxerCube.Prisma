using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Prisma.Athena.Worker.Tests;

/// <summary>
/// Test application factory for Athena Worker with health endpoints.
/// </summary>
internal class AthenaWorkerApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Register mock dependencies for ProcessingOrchestrator
            services.AddSingleton(Substitute.For<IEventPublisher>());
        });
    }
}