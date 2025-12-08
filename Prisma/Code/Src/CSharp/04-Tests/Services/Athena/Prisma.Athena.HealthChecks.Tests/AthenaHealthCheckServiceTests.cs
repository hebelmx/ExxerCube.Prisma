using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Shouldly;
using Xunit;

namespace Prisma.Athena.HealthChecks.Tests;

/// <summary>
/// TDD tests for AthenaHealthCheckService.
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - Liveness always returns Healthy when process is running
/// - Readiness returns Healthy when orchestrator is ready
/// - Health combines liveness and readiness status
/// </remarks>
public sealed class AthenaHealthCheckServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_ProcessRunning_ReturnsHealthy()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaHealthCheckService(orchestrator, NullLogger<AthenaHealthCheckService>.Instance);

        // Act
        var result = await service.GetLivenessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldContain("running", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_OrchestratorReady_ReturnsHealthy()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaHealthCheckService(orchestrator, NullLogger<AthenaHealthCheckService>.Instance);

        // Act
        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldContain("ready", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHealthAsync_AllHealthy_ReturnsHealthy()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaHealthCheckService(orchestrator, NullLogger<AthenaHealthCheckService>.Instance);

        // Act
        var result = await service.GetHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("liveness");
        result.Data.ShouldContainKey("readiness");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_IncludesTimestamp()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaHealthCheckService(orchestrator, NullLogger<AthenaHealthCheckService>.Instance);
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await service.GetLivenessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("timestamp");
        var timestamp = (DateTime)result.Data["timestamp"];
        timestamp.ShouldBeGreaterThanOrEqualTo(beforeCall);
        timestamp.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_IncludesOrchestratorReadyFlag()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaHealthCheckService(orchestrator, NullLogger<AthenaHealthCheckService>.Instance);

        // Act
        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("orchestratorReady");
        result.Data["orchestratorReady"].ShouldBeOfType<bool>();
    }
}
