namespace ExxerCube.Prisma.Orion.HealthChecks.Tests;

/// <summary>
/// Tests for <see cref="OrionHealthCheckService"/> (MVP-PATH 4.2 / E1).
/// </summary>
/// <remarks>
/// - Liveness always returns Healthy when the process is running.
/// - Readiness reflects the real <see cref="IReadinessProbe"/> (the SIARA watch loop's running state):
///   Healthy once the loop is polling, Unhealthy before — no longer the "ready if not null" placeholder.
/// - Health combines liveness and readiness status.
/// </remarks>
public sealed class OrionHealthCheckServiceTests
{
    private static OrionHealthCheckService CreateService(bool ready)
    {
        var probe = Substitute.For<IReadinessProbe>();
        probe.IsReady.Returns(ready);
        return new OrionHealthCheckService(probe, NullLogger<OrionHealthCheckService>.Instance);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_ProcessRunning_ReturnsHealthy()
    {
        var service = CreateService(ready: false);

        var result = await service.GetLivenessAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(OrchestratorHealthState.Healthy);
        result.Description.ShouldContain("running", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenRunning_ReturnsHealthy()
    {
        var service = CreateService(ready: true);

        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(OrchestratorHealthState.Healthy);
        result.Description.ShouldContain("ready", Case.Insensitive);
        result.Data.ShouldNotBeNull();
        result.Data["orchestratorReady"].ShouldBe(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReadinessAsync_WhenNotRunning_ReturnsUnhealthy()
    {
        var service = CreateService(ready: false);

        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
        result.Data.ShouldNotBeNull();
        result.Data["orchestratorReady"].ShouldBe(false);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHealthAsync_WhenRunning_ReturnsHealthy()
    {
        var service = CreateService(ready: true);

        var result = await service.GetHealthAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(OrchestratorHealthState.Healthy);
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("liveness");
        result.Data.ShouldContainKey("readiness");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLivenessAsync_IncludesTimestamp()
    {
        var service = CreateService(ready: true);
        var beforeCall = DateTime.UtcNow;

        var result = await service.GetLivenessAsync(TestContext.Current.CancellationToken);

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
        var service = CreateService(ready: true);

        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        result.Data.ShouldNotBeNull();
        result.Data.ShouldContainKey("orchestratorReady");
        result.Data["orchestratorReady"].ShouldBeOfType<bool>();
    }

    // ========================================================================
    // Railway-Oriented Programming variants
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetLivenessWithResult_ProcessRunning_ReturnsSuccessWithHealthy()
    {
        var service = CreateService(ready: true);

        var result = await service.GetLivenessWithResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(OrchestratorHealthState.Healthy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetReadinessWithResult_WhenRunning_ReturnsSuccessWithHealthy()
    {
        var service = CreateService(ready: true);

        var result = await service.GetReadinessWithResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(OrchestratorHealthState.Healthy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetHealthWithResult_WhenRunning_ReturnsSuccessWithHealthy()
    {
        var service = CreateService(ready: true);

        var result = await service.GetHealthWithResultAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Status.ShouldBe(OrchestratorHealthState.Healthy);
        result.Value.Data.ShouldNotBeNull();
        result.Value!.Data.ShouldContainKey("liveness");
        result.Value!.Data.ShouldContainKey("readiness");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "4.5")]
    public async Task GetLivenessWithResult_WhenCancelled_ReturnsCancelled()
    {
        var service = CreateService(ready: true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.GetLivenessWithResultAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
