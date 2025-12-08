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

    // ========================================================================
    // NEW: Railway-Oriented Programming Tests (Stage 5.5)
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "5.5")]
    public async Task CheckWorkersWithResult_NoFailures_ReturnsSuccessWithZeroStats()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        var result = await service.CheckWorkersWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.WorkersChecked.ShouldBe(0);
        result.Value!.WorkersRestarted.ShouldBe(0);
        result.Value!.WorkersFailed.ShouldBe(0);
        result.Value!.FailedWorkerIds.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "5.5")]
    public async Task CheckWorkersWithResult_WhenCancelled_ReturnsCancelled()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.CheckWorkersWithResultAsync(cts.Token);

        // Assert - Railway-Oriented: cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "5.5")]
    public async Task CheckWorkersWithResult_AllRestartsSucceed_ReturnsSuccessWithCorrectStats()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(new[] { "orion-1", "athena-1" });
        restarter.RestartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        var result = await service.CheckWorkersWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.WorkersChecked.ShouldBe(2);
        result.Value!.WorkersRestarted.ShouldBe(2);
        result.Value!.WorkersFailed.ShouldBe(0);
        result.Value!.FailedWorkerIds.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "5.5")]
    public async Task CheckWorkersWithResult_SomeRestartsFail_TracksFailedWorkersInResult()
    {
        // Arrange
        var monitor = Substitute.For<IHeartbeatMonitor>();
        var restarter = Substitute.For<IProcessRestarter>();
        var config = Substitute.For<ISentinelConfiguration>();

        monitor.GetFailedWorkersAsync(Arg.Any<CancellationToken>()).Returns(new[] { "orion-1", "athena-1", "sentinel-1" });
        restarter.RestartAsync("orion-1", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        restarter.RestartAsync("athena-1", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        restarter.RestartAsync("sentinel-1", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var service = new SentinelService(monitor, restarter, config, NullLogger<SentinelService>.Instance);

        // Act
        var result = await service.CheckWorkersWithResultAsync(TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.WorkersChecked.ShouldBe(3);
        result.Value!.WorkersRestarted.ShouldBe(1);
        result.Value!.WorkersFailed.ShouldBe(2);
        result.Value!.FailedWorkerIds.ShouldContain("athena-1");
        result.Value!.FailedWorkerIds.ShouldContain("sentinel-1");
    }
}
