namespace ExxerCube.Prisma.Athena.HealthChecks.Tests;

/// <summary>
/// Tests for <see cref="AthenaHealthCheckService"/> (MVP-PATH 4.2 / E1).
/// </summary>
/// <remarks>
/// - Liveness always returns Healthy when the process is running.
/// - Readiness reflects the real <see cref="IReadinessProbe"/> (the Extractor pipeline's started state):
///   Healthy once started, Unhealthy before — no longer the "ready if not null" placeholder.
/// - Health combines liveness and readiness status.
/// </remarks>
public sealed class AthenaHealthCheckServiceTests
{
    private static AthenaHealthCheckService CreateService(bool ready)
    {
        var probe = Substitute.For<IReadinessProbe>();
        probe.IsReady.Returns(ready);
        return new AthenaHealthCheckService(probe, NullLogger<AthenaHealthCheckService>.Instance);
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
    public async Task GetReadinessAsync_WhenStarted_ReturnsHealthy()
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
    public async Task GetReadinessAsync_WhenNotStarted_ReturnsUnhealthy()
    {
        var service = CreateService(ready: false);

        var result = await service.GetReadinessAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(OrchestratorHealthState.Unhealthy);
        result.Data.ShouldNotBeNull();
        result.Data["orchestratorReady"].ShouldBe(false);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHealthAsync_WhenStarted_ReturnsHealthy()
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
    public async Task GetHealthAsync_WhenNotStarted_ReturnsDegraded()
    {
        var service = CreateService(ready: false);

        var result = await service.GetHealthAsync(TestContext.Current.CancellationToken);

        // Liveness is healthy but readiness is not → overall degraded.
        result.Status.ShouldBe(OrchestratorHealthState.Degraded);
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
}
