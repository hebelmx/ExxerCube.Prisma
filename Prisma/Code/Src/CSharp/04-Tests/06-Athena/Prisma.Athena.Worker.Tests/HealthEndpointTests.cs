using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Prisma.Athena.Worker;
using Shouldly;
using Xunit;

namespace Prisma.Athena.Worker.Tests;

/// <summary>
/// TDD tests for Athena Worker health endpoints (liveness/readiness).
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - /health endpoint returns 200 when worker is running
/// - /health/live returns 200 when process is running
/// - /health/ready returns 200 when orchestrator is ready
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
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
