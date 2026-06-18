using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Observability;

/// <summary>
/// Centralised OpenTelemetry-compatible metrics surface for the Veriqan VEC pipeline.
/// </summary>
/// <remarks>
/// <para>
/// All instruments are backed by <see cref="System.Diagnostics.Metrics.Meter"/> — part of the
/// BCL since .NET 6 — so no additional NuGet package is required.  A Prometheus/OTLP exporter
/// can be attached at host startup without changing this class.
/// </para>
/// <para>
/// The meter name is <c>"ExxerCube.Prisma.Veriqan"</c>. Listeners (e.g.
/// <see cref="MeterListener"/>) must subscribe to this name.
/// </para>
/// <para>Registered as <b>Singleton</b> via <c>AddVeriqan</c>.</para>
/// </remarks>
public sealed class VeriqanMetrics : IDisposable
{
    /// <summary>The name of the <see cref="Meter"/> published by this class.</summary>
    public const string MeterName = "ExxerCube.Prisma.Veriqan";

    private readonly Meter _meter;

    /// <summary>
    /// Per-statement processing latency in milliseconds (wall-clock from pipeline entry to
    /// outcome).  NFR-1: p95 ≤ 10 000 ms.
    /// </summary>
    public Histogram<double> StatementDuration { get; }

    /// <summary>
    /// Cumulative count of successfully completed statements.
    /// Tag key <c>"verdict"</c> carries the lowercase signal name (green / red / blocked).
    /// </summary>
    public Counter<long> StatementProcessed { get; }

    /// <summary>
    /// Cumulative count of statements placed on the exception queue (pipeline failure or
    /// unhandled exception).
    /// </summary>
    public Counter<long> StatementExceptions { get; }

    /// <summary>
    /// Initialises the meter and its instruments.
    /// </summary>
    public VeriqanMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        StatementDuration = _meter.CreateHistogram<double>(
            name: "veriqan.statement.duration",
            unit: "ms",
            description: "Wall-clock processing latency per statement (NFR-1: p95 ≤ 10 000 ms).");

        StatementProcessed = _meter.CreateCounter<long>(
            name: "veriqan.statement.processed",
            unit: "{statement}",
            description: "Number of statements that completed the pipeline (tagged by verdict signal).");

        StatementExceptions = _meter.CreateCounter<long>(
            name: "veriqan.statement.exceptions",
            unit: "{statement}",
            description: "Number of statements placed on the exception queue.");
    }

    /// <summary>
    /// Records the outcome of a completed pipeline run.
    /// </summary>
    /// <param name="durationMs">Wall-clock milliseconds from pipeline entry to outcome.</param>
    /// <param name="signal">The verdict signal produced by the pipeline.</param>
    public void RecordStatement(double durationMs, VerdictSignal signal)
    {
        var verdictTag = new KeyValuePair<string, object?>("verdict", signal.ToString().ToLowerInvariant());
        StatementDuration.Record(durationMs, verdictTag);
        StatementProcessed.Add(1, verdictTag);
    }

    /// <summary>
    /// Increments the exception counter for one item that could not complete the pipeline.
    /// </summary>
    public void RecordException() => StatementExceptions.Add(1);

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
