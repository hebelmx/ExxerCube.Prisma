using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;
using Shouldly;
using Xunit;

namespace Prisma.Orion.Worker.Tests;

/// <summary>
/// TDD tests for Orion Worker health endpoints (liveness/readiness).
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - /health endpoint returns 200 when worker is running
/// - /health returns 503 when orchestrator is not started
/// - Liveness reflects orchestrator start state
/// - Readiness reflects processing capability
/// </remarks>
public sealed class HealthEndpointTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_WorkerRunning_Returns200()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_OrchestratorStarted_ReturnsHealthy()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Give orchestrator time to start
        await Task.Delay(100);

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_Liveness_AlwaysHealthyWhenProcessRunning()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_Readiness_HealthyWhenOrchestratorReady()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Give orchestrator time to become ready
        await Task.Delay(100);

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}

/// <summary>
/// Test application factory for Orion Worker with health endpoints.
/// </summary>
internal class OrionWorkerApplication : WebApplicationFactory<Program>
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
