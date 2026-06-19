namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// xUnit collection that serialises all tests that interact with the global
/// <c>System.Diagnostics.Metrics.Meter</c> named <c>"ExxerCube.Prisma.Veriqan"</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Observability.VeriqanMetrics"/> publishes instruments on a process-global
/// <see cref="System.Diagnostics.Metrics.Meter"/> whose name is
/// <see cref="Observability.VeriqanMetrics.MeterName"/>.
/// A <see cref="System.Diagnostics.Metrics.MeterListener"/> subscribes to all meters with
/// that name, so measurements emitted by ANY <c>VeriqanMetrics</c> instance appear in every
/// active listener.  Running tests that create a <c>VeriqanMetrics</c> instance (e.g.
/// tests that instantiate the real <c>VerificationPipeline</c> via DI) in parallel with
/// tests that assert exact counts of emitted measurements causes non-deterministic failures.
/// </para>
/// <para>
/// Place all test classes that either (a) use <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// or (b) run the real <c>VerificationPipeline</c> (which calls
/// <c>VeriqanMetrics.RecordStatement</c>) into this collection.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MetricsIsolationCollection
{
    /// <summary>Collection name used in <c>[Collection]</c> attributes.</summary>
    public const string Name = "VeriqanMetrics";
}
