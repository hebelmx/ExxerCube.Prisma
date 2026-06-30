using System;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Configuration options for the resilience wrapper applied to the verification gate
/// (<see cref="IVerificationPipeline"/>).
/// </summary>
/// <remarks>
/// <para>
/// Bind from the <c>Veriqan:Gate:Resilience</c> configuration section
/// (e.g. <c>appsettings.json</c> key <c>Veriqan__Gate__Resilience</c> or the matching
/// environment variable).
/// </para>
/// <para>
/// All <see cref="TimeSpan"/> properties support .NET configuration binding via
/// <c>"HH:mm:ss"</c> or ISO-8601 duration strings (e.g. <c>"00:00:30"</c> for 30 s).
/// </para>
/// </remarks>
public sealed class GateResilienceOptions
{
    /// <summary>Configuration section key used to bind these options.</summary>
    public const string Section = "Veriqan:Gate:Resilience";

    /// <summary>
    /// Per-request wall-clock timeout.  When the inner pipeline does not complete within
    /// this duration the gate returns a fail-closed <c>Gate.Timeout</c> result.
    /// </summary>
    /// <value>Default: 30 seconds.</value>
    public TimeSpan TimeoutPerRequest { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fraction of calls within the sampling window that must fail (exception or
    /// <see cref="IndQuestResults.Result.IsFailure"/>) before the circuit opens.
    /// </summary>
    /// <value>Range 0.0–1.0; default 0.8 (80 %).</value>
    public double FailureRatio { get; set; } = 0.8;

    /// <summary>
    /// Minimum number of calls that must occur within the sampling window before the
    /// failure ratio is evaluated.  Guards against spurious trips on cold-start traffic.
    /// </summary>
    /// <value>Default: 5. Polly v8 minimum accepted value is 2.</value>
    public int MinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Length of the sliding time window over which calls are counted for the failure ratio.
    /// </summary>
    /// <value>Default: 60 seconds. Polly v8 minimum is 500 ms.</value>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Duration for which the circuit stays open (all calls immediately fail-closed) before
    /// transitioning to half-open for a single probe call.
    /// </summary>
    /// <value>Default: 30 seconds.</value>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
