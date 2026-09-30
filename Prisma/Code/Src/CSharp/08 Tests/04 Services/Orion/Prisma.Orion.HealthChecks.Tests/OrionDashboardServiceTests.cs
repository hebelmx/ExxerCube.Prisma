namespace ExxerCube.Prisma.Orion.HealthChecks.Tests;

/// <summary>
/// Unit tests for OrionDashboardService.
/// </summary>
/// <remarks>
/// Verifies:
/// - Dashboard returns worker name / status
/// - Documents processed count starts at zero and increments via RecordDocumentProcessed
/// - Last event timestamp is null until the first RecordDocumentProcessed call
/// - Last heartbeat is updated on each GetStatsAsync call
/// - Queue depth is non-negative
/// - After N calls to RecordDocumentProcessed, DocumentsProcessed == N (core story assertion)
/// - Railway-Oriented Programming variant (GetStatsWithResultAsync) propagates correctly
/// </remarks>
public sealed class OrionDashboardServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsWorkerName()
    {
        // Arrange
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.ShouldNotBeNull();
        stats.WorkerName.ShouldBe("Orion Ingestion Worker");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsDocumentsProcessed_ZeroInitially()
    {
        // Arrange
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);
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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);
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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.Status.ShouldNotBeNullOrWhiteSpace();
    }

    // ========================================================================
    // PRISMA-E1-S7: Dashboard returns real throughput, not hardcoded zero
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Story", "PRISMA-E1-S7")]
    public async Task RecordDocumentProcessed_CalledNTimes_DocumentsProcessedEqualsN()
    {
        // Arrange — simulates pipeline calling RecordDocumentProcessed after each ingestion
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);
        const int n = 5;

        // Act
        for (var i = 0; i < n; i++)
        {
            service.RecordDocumentProcessed();
        }

        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert — dashboard no longer returns hardcoded zero; it reflects actual pipeline throughput
        stats.DocumentsProcessed.ShouldBe(n);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Story", "PRISMA-E1-S7")]
    public async Task WithNoRecordCalls_DocumentsProcessedIsZero()
    {
        // Arrange — fresh service, no documents ingested
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert — zero is expected when nothing has been ingested, not a stub lie
        stats.DocumentsProcessed.ShouldBe(0);
    }

    // ========================================================================
    // Railway-Oriented Programming Tests (Stage 4.5)
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetStatsWithResult_ReturnsSuccessWithStats()
    {
        // Arrange
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

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
        var service = new OrionDashboardService(NullLogger<OrionDashboardService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.GetStatsWithResultAsync(cts.Token);

        // Assert - Railway-Oriented: cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();
    }
}
