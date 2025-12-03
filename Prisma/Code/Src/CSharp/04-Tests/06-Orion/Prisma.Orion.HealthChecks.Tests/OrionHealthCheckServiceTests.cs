using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Shared.Contracts;
using Shouldly;
using Xunit;

namespace Prisma.Orion.HealthChecks.Tests;

/// <summary>
/// TDD tests for OrionHealthCheckService.
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - Liveness always returns Healthy when process is running
/// - Readiness returns Healthy when orchestrator is ready
/// - Health combines liveness and readiness status
/// </remarks>
public sealed class OrionHealthCheckServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_ProcessRunning_ReturnsHealthy()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);
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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

        // Act
        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("orchestratorReady");
        result.Data["orchestratorReady"].ShouldBeOfType<bool>();
    }

    // ========================================================================
    // NEW: Railway-Oriented Programming Tests (Stage 4.5)
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetLivenessWithResult_ProcessRunning_ReturnsSuccessWithHealthy()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

        // Act
        var result = await service.GetLivenessWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetReadinessWithResult_OrchestratorReady_ReturnsSuccessWithHealthy()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

        // Act
        var result = await service.GetReadinessWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetHealthWithResult_AllHealthy_ReturnsSuccessWithHealthy()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

        // Act
        var result = await service.GetHealthWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(HealthStatus.Healthy);
        result.Value.Data.ShouldNotBeNull();
        result.Value!.Data.ShouldContainKey("liveness");
        result.Value!.Data.ShouldContainKey("readiness");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetLivenessWithResult_WhenCancelled_ReturnsCancelled()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionHealthCheckService(orchestrator, NullLogger<OrionHealthCheckService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.GetLivenessWithResultAsync(cts.Token);

        // Assert - Railway-Oriented: cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();
    }
}
