using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Shouldly;
using Xunit;

namespace Prisma.Athena.HealthChecks.Tests;

/// <summary>
/// TDD tests for AthenaDashboardService.
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - Dashboard returns worker name
/// - Dashboard returns documents processed count
/// - Dashboard returns last event timestamp
/// - Dashboard returns last heartbeat timestamp
/// - Dashboard returns queue depth
/// </remarks>
public sealed class AthenaDashboardServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsWorkerName()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.ShouldNotBeNull();
        stats.WorkerName.ShouldBe("Athena Processing Worker");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsDocumentsProcessed()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.DocumentsProcessed.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsLastHeartbeat()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);
        var beforeCall = DateTime.UtcNow;

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.LastHeartbeat.ShouldNotBeNull();
        stats.LastHeartbeat.Value.ShouldBeGreaterThanOrEqualTo(beforeCall.AddSeconds(-1));
        stats.LastHeartbeat.Value.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_LastEventTimeNull_WhenNoEventsProcessed()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.LastEventTime.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordDocumentProcessed_IncrementsCount()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        service.RecordDocumentProcessed();
        service.RecordDocumentProcessed();
        service.RecordDocumentProcessed();
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.DocumentsProcessed.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordDocumentProcessed_UpdatesLastEventTime()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);
        var beforeEvent = DateTime.UtcNow;

        // Act
        service.RecordDocumentProcessed();
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.LastEventTime.ShouldNotBeNull();
        stats.LastEventTime.Value.ShouldBeGreaterThanOrEqualTo(beforeEvent);
        stats.LastEventTime.Value.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsQueueDepth()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.QueueDepth.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsStatus()
    {
        // Arrange
        var orchestrator = new ProcessingOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ProcessingOrchestrator>.Instance);
        var service = new AthenaDashboardService(orchestrator, NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.Status.ShouldNotBeNullOrWhiteSpace();
    }
}
