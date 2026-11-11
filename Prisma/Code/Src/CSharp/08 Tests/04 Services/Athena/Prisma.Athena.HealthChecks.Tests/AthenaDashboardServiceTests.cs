namespace ExxerCube.Prisma.Athena.HealthChecks.Tests;

/// <summary>
/// Unit tests for AthenaDashboardService.
/// </summary>
/// <remarks>
/// Verifies:
/// - Dashboard returns worker name / status
/// - Documents processed count starts at zero and increments via RecordDocumentProcessed
/// - Last event timestamp is null until the first RecordDocumentProcessed call
/// - Last heartbeat is updated on each GetStatsAsync call
/// - Queue depth is non-negative
/// - After N calls to RecordDocumentProcessed, DocumentsProcessed == N (core story assertion)
/// </remarks>
public sealed class AthenaDashboardServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsWorkerName()
    {
        // Arrange
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert
        stats.ShouldNotBeNull();
        stats.WorkerName.ShouldBe("Athena Processing Worker");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetStatsAsync_ReturnsDocumentsProcessed_ZeroInitially()
    {
        // Arrange
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);
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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);
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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

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
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

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
        // Arrange — simulates pipeline calling RecordDocumentProcessed after each extraction
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);
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
        // Arrange — fresh service, no documents processed
        var service = new AthenaDashboardService(NullLogger<AthenaDashboardService>.Instance);

        // Act
        var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        // Assert — zero is expected when nothing has been processed, not a stub lie
        stats.DocumentsProcessed.ShouldBe(0);
    }
}
