namespace Prisma.Sentinel.Monitor.Tests;

/// <summary>
/// TDD tests for heartbeat monitoring logic.
/// </summary>
/// <remarks>
/// Stage 5 Requirements:
/// - Track worker heartbeats with timestamps
/// - Detect workers that missed 3+ consecutive heartbeats
/// - Forgive transient missed heartbeats (if heartbeat resumes)
/// - Return list of workers needing restart
/// </remarks>
public sealed class HeartbeatMonitorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordHeartbeat_NewWorker_StoresHeartbeat()
    {
        // Arrange
        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance);
        var heartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow,
            Status: WorkerStatus.Running,
            DocumentsProcessed: 0,
            LastEventTime: null);

        // Act
        await monitor.RecordHeartbeatAsync(heartbeat, TestContext.Current.CancellationToken);
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        failedWorkers.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetFailedWorkers_NoMissedHeartbeats_ReturnsEmpty()
    {
        // Arrange
        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance);
        var heartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow,
            Status: WorkerStatus.Running,
            DocumentsProcessed: 10,
            LastEventTime: DateTime.UtcNow.AddMinutes(-1));

        await monitor.RecordHeartbeatAsync(heartbeat, TestContext.Current.CancellationToken);

        // Act
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        failedWorkers.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetFailedWorkers_MissedThreeHeartbeats_ReturnsWorkerId()
    {
        // Arrange - Configure 30-second timeout, check for 3 missed heartbeats
        var config = Substitute.For<ISentinelConfiguration>();
        config.HeartbeatTimeout.Returns(TimeSpan.FromSeconds(30));
        config.MissedHeartbeatThreshold.Returns(3);

        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance, config);

        // Record old heartbeat (more than 90 seconds ago = 3 missed cycles)
        var oldHeartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-95),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 10,
            LastEventTime: DateTime.UtcNow.AddMinutes(-5));

        await monitor.RecordHeartbeatAsync(oldHeartbeat, TestContext.Current.CancellationToken);

        // Act
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        failedWorkers.ShouldContain("orion-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetFailedWorkers_MissedTwoHeartbeats_ReturnsEmpty()
    {
        // Arrange - Only 2 missed heartbeats, threshold is 3
        var config = Substitute.For<ISentinelConfiguration>();
        config.HeartbeatTimeout.Returns(TimeSpan.FromSeconds(30));
        config.MissedHeartbeatThreshold.Returns(3);

        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance, config);

        // Record heartbeat 65 seconds ago (2 missed cycles, not 3)
        var heartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-65),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 10,
            LastEventTime: DateTime.UtcNow.AddMinutes(-2));

        await monitor.RecordHeartbeatAsync(heartbeat, TestContext.Current.CancellationToken);

        // Act
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert - Should NOT trigger restart yet
        failedWorkers.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetFailedWorkers_ForgiveMissedHeartbeat_ReturnsEmpty()
    {
        // Arrange - Worker missed heartbeats but then recovered
        var config = Substitute.For<ISentinelConfiguration>();
        config.HeartbeatTimeout.Returns(TimeSpan.FromSeconds(30));
        config.MissedHeartbeatThreshold.Returns(3);

        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance, config);

        // Record old heartbeat
        var oldHeartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-95),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 10,
            LastEventTime: DateTime.UtcNow.AddMinutes(-5));

        await monitor.RecordHeartbeatAsync(oldHeartbeat, TestContext.Current.CancellationToken);

        // Worker recovers - send recent heartbeat
        var recentHeartbeat = new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-5),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 15,
            LastEventTime: DateTime.UtcNow.AddSeconds(-10));

        await monitor.RecordHeartbeatAsync(recentHeartbeat, TestContext.Current.CancellationToken);

        // Act
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert - Should forgive previous missed heartbeats
        failedWorkers.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetFailedWorkers_MultipleWorkers_ReturnsOnlyFailed()
    {
        // Arrange
        var config = Substitute.For<ISentinelConfiguration>();
        config.HeartbeatTimeout.Returns(TimeSpan.FromSeconds(30));
        config.MissedHeartbeatThreshold.Returns(3);

        var monitor = new HeartbeatMonitor(NullLogger<HeartbeatMonitor>.Instance, config);

        // Healthy worker
        await monitor.RecordHeartbeatAsync(new WorkerHeartbeat(
            WorkerId: "orion-1",
            WorkerName: "Orion Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-10),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 10,
            LastEventTime: DateTime.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);

        // Failed worker
        await monitor.RecordHeartbeatAsync(new WorkerHeartbeat(
            WorkerId: "athena-1",
            WorkerName: "Athena Worker 1",
            Timestamp: DateTime.UtcNow.AddSeconds(-95),
            Status: WorkerStatus.Running,
            DocumentsProcessed: 5,
            LastEventTime: DateTime.UtcNow.AddMinutes(-5)), TestContext.Current.CancellationToken);

        // Act
        var failedWorkers = await monitor.GetFailedWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        failedWorkers.ShouldContain("athena-1");
        failedWorkers.ShouldNotContain("orion-1");
    }
}
