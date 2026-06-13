namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Unit tests for <see cref="SLAMetricsCollector"/> gauge tracking (MVP-PATH 4.1 / D2).
/// Pin the fix for the previously-hardcoded <c>GetCurrentGaugeValue</c> (always returned 0), which made
/// the UpDownCounter deltas wrong and the metrics snapshot meaningless.
/// </summary>
public sealed class SLAMetricsCollectorTests
{
    private readonly ITestOutputHelper _output;

    public SLAMetricsCollectorTests(ITestOutputHelper output) => _output = output;

    private SLAMetricsCollector CreateCollector() =>
        new(XUnitLogger.CreateLogger<SLAMetricsCollector>(_output));

    [Fact]
    public void GetMetricsSnapshot_ReflectsLastReportedGaugeValues()
    {
        // Arrange
        var collector = CreateCollector();

        // Act - report absolute counts; the collector converts each to a delta internally.
        collector.UpdateAtRiskCases(7);
        collector.UpdateBreachedCases(2);
        collector.UpdateActiveCases(10);

        // Assert - before the fix every value was hardcoded 0.
        var snapshot = collector.GetMetricsSnapshot();
        snapshot["at_risk_cases"].ShouldBe(7L);
        snapshot["breached_cases"].ShouldBe(2L);
        snapshot["active_cases"].ShouldBe(10L);
    }

    [Fact]
    public void UpdateGauge_WhenCountDrops_SnapshotTracksTheNewAbsoluteValue()
    {
        // Arrange
        var collector = CreateCollector();

        // Act - a rise then a drop. An UpDownCounter is delta-based, so the collector must emit +10 then
        // -6 (net 4); the snapshot reads the same internally-tracked absolute value.
        collector.UpdateActiveCases(10);
        collector.UpdateActiveCases(4);

        // Assert
        collector.GetMetricsSnapshot()["active_cases"].ShouldBe(4L);
    }

    [Fact]
    public void UpdateGauge_WhenCountUnchanged_KeepsTheReportedValue()
    {
        // Arrange
        var collector = CreateCollector();

        // Act - reporting the same count twice is a no-op delta but must not reset the tracked value.
        collector.UpdateAtRiskCases(5);
        collector.UpdateAtRiskCases(5);

        // Assert
        collector.GetMetricsSnapshot()["at_risk_cases"].ShouldBe(5L);
    }
}
