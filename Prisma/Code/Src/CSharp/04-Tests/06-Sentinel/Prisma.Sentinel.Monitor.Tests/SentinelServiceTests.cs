namespace Prisma.Sentinel.Monitor.Tests;

/// <summary>
/// TDD tests for Sentinel monitoring service orchestration.
/// </summary>
/// <remarks>
/// Stage 5 Requirements:
/// - Poll heartbeat monitor for failed workers
/// - Trigger restart for workers exceeding threshold
/// - Log restart results
/// - Run continuously with configurable interval
/// </remarks>
public sealed class SentinelServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckWorkers_NoFailures_NoRestartsTriggered()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        await service.CheckWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        await restarter.DidNotReceive().RestartAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckWorkers_FailedWorker_TriggersRestart()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(new[] { "orion-1" });
        restarter.RestartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        await service.CheckWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        await restarter.Received(1).RestartAsync(
            "orion-1",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckWorkers_MultipleFailedWorkers_TriggersMultipleRestarts()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(new[] { "orion-1", "athena-1" });
        restarter.RestartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        await service.CheckWorkersAsync(TestContext.Current.CancellationToken);

        // Assert
        await restarter.Received(1).RestartAsync("orion-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await restarter.Received(1).RestartAsync("athena-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckWorkers_RestartFails_ContinuesProcessing()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(new[] { "orion-1" });
        restarter.RestartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act & Assert - Should not throw
        await service.CheckWorkersAsync(TestContext.Current.CancellationToken);

        // Restart was attempted
        await restarter.Received(1).RestartAsync("orion-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MonitorAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();
        config.CheckInterval.Returns(TimeSpan.FromMilliseconds(100));

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(250));

        // Act & Assert - Should complete gracefully when cancelled
        await service.MonitorAsync(cts.Token);

        // Should have checked at least once
        await monitor.Received().GetFailedWorkersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MonitorAsync_RunsContinuously_ChecksAtInterval()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();
        config.CheckInterval.Returns(TimeSpan.FromMilliseconds(50));

        var callCount = 0;
        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            callCount++;
            return Array.Empty<string>();
        });

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(180));

        // Act
        await service.MonitorAsync(cts.Token);

        // Assert - Should have checked multiple times (at least 2-3 times in 180ms with 50ms interval)
        callCount.ShouldBeGreaterThanOrEqualTo(2);
    }
}
