using System;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Fail-closed resilience decorator for <see cref="IVerificationPipeline"/>.
/// Wraps the inner gate with a Polly v8 <see cref="ResiliencePipeline{TResult}"/> that
/// enforces a per-request entry timeout and a circuit breaker, both configured from
/// <see cref="GateResilienceOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-closed policy:</b> for a compliance verification gate, when the inner pipeline
/// faults (throws), times out, or the circuit breaker is open, the host MUST NOT return
/// a passing/success verdict.  Any of these conditions causes the decorator to return a
/// non-success <see cref="Result{T}"/> whose <see cref="Result.Error"/> starts with the
/// <c>"Gate."</c> prefix, allowing callers to distinguish system-unavailability from a
/// normal business-failure verdict and map to HTTP 503.
/// </para>
/// <para>
/// <b>Resilience pipeline order (Polly v8 — outermost to innermost):</b>
/// <list type="number">
///   <item>Timeout — enforces a wall-clock limit for the entire operation.</item>
///   <item>Circuit breaker — opens after repeated failures, blocking further calls until
///         the configured break duration elapses.</item>
/// </list>
/// </para>
/// <para>
/// <b>Circuit-breaker predicate:</b> counts both thrown exceptions (excluding
/// <see cref="OperationCanceledException"/>, which indicates user-initiated cancellation
/// rather than infra fault) and <see cref="Result.IsFailure"/> return values that are not
/// cancelled.  This means repeated business-logic failures (e.g. DB unreachable at persist)
/// also contribute to the failure ratio, protecting downstream systems from hammering.
/// </para>
/// <para>
/// <b>Registration:</b> scoped (one instance per DI scope / HTTP request), while the
/// shared <see cref="ResiliencePipeline{TResult}"/> that holds circuit-breaker state is
/// registered as a singleton so the breaker tracks failures across all requests.
/// </para>
/// </remarks>
internal sealed class ResilientVerificationPipeline : IVerificationPipeline
{
    private readonly IVerificationPipeline _inner;
    private readonly ResiliencePipeline<Result<VerificationOutcome>> _resiliencePipeline;
    private readonly GateResilienceOptions _options;
    private readonly ILogger<ResilientVerificationPipeline> _logger;

    /// <summary>
    /// Initializes a new <see cref="ResilientVerificationPipeline"/> with an inner pipeline,
    /// a shared (singleton-lifetime) Polly resilience pipeline, and the configured options.
    /// </summary>
    /// <param name="inner">The real <see cref="IVerificationPipeline"/> to delegate to.</param>
    /// <param name="resiliencePipeline">
    /// Polly pipeline that carries the circuit-breaker state (singleton-scoped so state is
    /// shared across all requests).
    /// </param>
    /// <param name="options">Resolved gate resilience options (timeout, circuit-breaker thresholds).</param>
    /// <param name="logger">Logger for gate-level diagnostics.</param>
    public ResilientVerificationPipeline(
        IVerificationPipeline inner,
        ResiliencePipeline<Result<VerificationOutcome>> resiliencePipeline,
        GateResilienceOptions options,
        ILogger<ResilientVerificationPipeline> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _resiliencePipeline = resiliencePipeline ?? throw new ArgumentNullException(nameof(resiliencePipeline));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Wraps the inner pipeline call in a Polly resilience pipeline.  The following
    /// fail-closed mappings apply:
    /// </para>
    /// <list type="table">
    ///   <listheader><term>Condition</term><description>Returned Result error prefix</description></listheader>
    ///   <item><term>Entry timeout exceeded</term><description><c>Gate.Timeout:</c></description></item>
    ///   <item><term>Circuit breaker open</term><description><c>Gate.CircuitOpen:</c></description></item>
    ///   <item><term>Unhandled exception from inner</term><description><c>Gate.Error:</c></description></item>
    ///   <item><term><paramref name="ct"/> signalled</term><description>Cancelled Result</description></item>
    /// </list>
    /// </remarks>
    public async Task<Result<VerificationOutcome>> ProcessAsync(
        StatementSubmission submission,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationOutcome>();

        try
        {
            return await _resiliencePipeline
                .ExecuteAsync(
                    async innerCt => await _inner.ProcessAsync(submission, innerCt).ConfigureAwait(false),
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Outer CT was cancelled — propagate as a cancelled Result; do NOT treat as an
            // error that should trip the circuit breaker.
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(
                ex,
                "Gate.Timeout: verification gate exceeded the {TimeoutSeconds:F0} s timeout for {FileName}",
                _options.TimeoutPerRequest.TotalSeconds,
                submission.FileName);

            return Result<VerificationOutcome>.WithFailure(
                $"Gate.Timeout: verification gate exceeded {_options.TimeoutPerRequest.TotalSeconds:F0} s timeout");
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(
                ex,
                "Gate.CircuitOpen: verification gate circuit breaker is open for {FileName}",
                submission.FileName);

            return Result<VerificationOutcome>.WithFailure(
                "Gate.CircuitOpen: verification gate is temporarily unavailable — circuit breaker open");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Gate.Error: unexpected exception from inner verification gate for {FileName}",
                submission.FileName);

            return Result<VerificationOutcome>.WithFailure(
                $"Gate.Error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the shared Polly <see cref="ResiliencePipeline{TResult}"/> that enforces the
    /// timeout and circuit-breaker policies derived from <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pipeline strategy order (outermost → innermost):
    /// <list type="number">
    ///   <item>Timeout — enforces <see cref="GateResilienceOptions.TimeoutPerRequest"/>.</item>
    ///   <item>Circuit breaker — opens after the configured failure ratio / throughput
    ///         threshold is exceeded within the sampling window.</item>
    /// </list>
    /// </para>
    /// <para>
    /// This method is <c>internal</c> so the test project (granted
    /// <c>[InternalsVisibleTo]</c>) can construct isolated resilience pipelines with
    /// test-friendly thresholds without going through the DI container.
    /// </para>
    /// </remarks>
    /// <param name="options">Options that control timeout and circuit-breaker behaviour.</param>
    /// <param name="logger">Logger for circuit-breaker state-change events.</param>
    /// <returns>
    /// A fully built <see cref="ResiliencePipeline{TResult}"/> ready for use.
    /// </returns>
    internal static ResiliencePipeline<Result<VerificationOutcome>> BuildResiliencePipeline(
        GateResilienceOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        return new ResiliencePipelineBuilder<Result<VerificationOutcome>>()
            // ── Strategy 1 (outermost): Entry timeout ────────────────────────────
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.TimeoutPerRequest,
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Gate timeout strategy fired after {Timeout}",
                        args.Timeout);
                    return default;
                },
            })
            // ── Strategy 2 (innermost): Circuit breaker ───────────────────────────
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Result<VerificationOutcome>>
            {
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = options.SamplingDuration,
                BreakDuration = options.BreakDuration,

                // Trip on:
                //  • Any exception except user-initiated cancellation.
                //  • A Result.IsFailure return that is not a cancellation result.
                ShouldHandle = args => new ValueTask<bool>(
                    (args.Outcome.Exception is not null &&
                     args.Outcome.Exception is not OperationCanceledException) ||
                    (args.Outcome.Result is { IsFailure: true } r && !r.IsCancelled())),

                OnOpened = args =>
                {
                    logger.LogError(
                        "Gate circuit breaker OPENED. Break duration: {BreakDuration}. Reason: {Reason}",
                        args.BreakDuration,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.Error ?? "failure ratio exceeded");
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Gate circuit breaker CLOSED (recovered)");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("Gate circuit breaker HALF-OPEN (sending probe request)");
                    return default;
                },
            })
            .Build();
    }
}
