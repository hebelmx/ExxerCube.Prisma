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
/// TDD tests for OrionDashboardService.
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - Dashboard returns worker name
/// - Dashboard returns documents processed count
/// - Dashboard returns last event timestamp
/// - Dashboard returns last heartbeat timestamp
/// - Dashboard returns queue depth
/// </remarks>
public sealed class OrionDashboardServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsWorkerName()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.ShouldNotBeNull();
        stats.WorkerName.ShouldBe("Orion Ingestion Worker");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsDocumentsProcessed()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);
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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);
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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

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
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.Status.ShouldNotBeNullOrWhiteSpace();
    }

    // ========================================================================
    // NEW: Railway-Oriented Programming Tests (Stage 4.5)
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetStatsWithResult_ReturnsSuccessWithStats()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

        // Act
        var result = await service.GetStatsWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.WorkerName.ShouldBe("Orion Ingestion Worker");
        result.Value!.Status.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetStatsWithResult_WhenCancelled_ReturnsCancelled()
    {
        // Arrange
        var orchestrator = new IngestionOrchestrator(
            Substitute.For<IIngestionJournal>(),
            Substitute.For<IDocumentDownloader>(),
            Substitute.For<IExxerHub<DocumentDownloadedEvent>>(),
            NullLogger<IngestionOrchestrator>.Instance);
        var service = new OrionDashboardService(orchestrator, NullLogger<OrionDashboardService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.GetStatsWithResultAsync(cts.Token);

        // Assert - Railway-Oriented: cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();
    }
}
