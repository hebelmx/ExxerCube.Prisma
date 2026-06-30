using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="ResilientVerificationPipeline"/> (Story 6.6).
/// <para>
/// Verifies the fail-closed policy: on inner exceptions, timeout, and circuit-breaker
/// trips the decorator MUST return a non-success <see cref="Result{T}"/> with a
/// <c>"Gate."</c> error prefix and must NEVER throw or return a success result.
/// </para>
/// </summary>
public sealed class ResilientVerificationPipelineTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StatementSubmission MakeSubmission(string name = "test.pdf") =>
        new(
            Pdf: [0x25, 0x50, 0x44, 0x46], // minimal %PDF header
            FileName: name,
            ContextKey: new StatementContextKey("BankA"));

    /// <summary>
    /// Builds the decorator under test with configurable options, a substitute inner pipeline,
    /// and null loggers.
    /// </summary>
    private static (ResilientVerificationPipeline Decorator, IVerificationPipeline Inner)
        BuildDecorator(GateResilienceOptions options)
    {
        var inner = Substitute.For<IVerificationPipeline>();
        var pipeline = ResilientVerificationPipeline.BuildResiliencePipeline(
            options,
            NullLogger.Instance);

        var decorator = new ResilientVerificationPipeline(
            inner,
            pipeline,
            options,
            NullLogger<ResilientVerificationPipeline>.Instance);

        return (decorator, inner);
    }

    private static GateResilienceOptions DefaultOptions() => new()
    {
        TimeoutPerRequest = TimeSpan.FromSeconds(30),
        FailureRatio = 0.8,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromSeconds(60),
        BreakDuration = TimeSpan.FromSeconds(30),
    };

    // -----------------------------------------------------------------------
    // Test 1: happy path — inner success passes through unchanged
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the inner pipeline succeeds the decorator returns the same success result.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_InnerReturnsSuccess_PassesThroughUnchanged()
    {
        // Arrange
        var (decorator, inner) = BuildDecorator(DefaultOptions());
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        var expectedOutcome = new VerificationOutcome(
            Job: new VerificationJob(
                Guid.NewGuid(), "hash", DateTimeOffset.UtcNow,
                VerificationJobStatus.Pending),
            Summary: default!,
            Findings: [],
            ProcessingDuration: TimeSpan.FromMilliseconds(42));

        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(expectedOutcome)));

        // Act
        var result = await decorator.ProcessAsync(submission, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(expectedOutcome);
    }

    // -----------------------------------------------------------------------
    // Test 2: inner throws — decorator is fail-closed (Gate.Error)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the inner pipeline throws an unexpected exception the decorator must NOT throw;
    /// it returns a non-success <c>Result</c> with a <c>"Gate.Error:"</c> prefix.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_InnerThrows_ReturnFailClosedResultWithGateErrorPrefix()
    {
        // Arrange
        var (decorator, inner) = BuildDecorator(DefaultOptions());
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated DB crash"));

        // Act
        // Must NOT throw — fail-closed means the decorator absorbs the exception
        var result = await decorator.ProcessAsync(submission, ct);

        // Assert
        result.IsFailure.ShouldBeTrue("decorator must fail-closed when inner throws");
        result.Error.ShouldNotBeNull();
        result.Error.ShouldStartWith("Gate.Error:");
        result.IsCancelled().ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Test 3: timeout — decorator returns Gate.Timeout
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the inner pipeline exceeds the configured timeout the decorator returns a
    /// non-success <c>Result</c> with a <c>"Gate.Timeout:"</c> prefix and does NOT throw.
    /// </summary>
    [Fact(Timeout = 5_000)] // test must complete within 5 s
    public async Task ProcessAsync_InnerExceedsTimeout_ReturnFailClosedResultWithGateTimeoutPrefix()
    {
        // Arrange — very short timeout so the test does not block
        var options = DefaultOptions();
        options.TimeoutPerRequest = TimeSpan.FromMilliseconds(200);

        var (decorator, inner) = BuildDecorator(options);
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        // Simulate a slow inner that respects the token Polly provides
        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                // The CancellationToken here is Polly's linked token (timeout + outer CT)
                var innerCt = ci.Arg<CancellationToken>();
                await Task.Delay(TimeSpan.FromSeconds(10), innerCt).ConfigureAwait(false);
                return Result<VerificationOutcome>.WithSuccess(default!);
            });

        // Act
        var result = await decorator.ProcessAsync(submission, ct);

        // Assert
        result.IsFailure.ShouldBeTrue("decorator must fail-closed on timeout");
        result.Error.ShouldNotBeNull();
        result.Error.ShouldStartWith("Gate.Timeout:");
        result.IsCancelled().ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Test 4: circuit breaker opens — subsequent call is fail-closed without inner invocation
    // -----------------------------------------------------------------------

    /// <summary>
    /// After enough consecutive failures the circuit breaker opens.  Subsequent calls must
    /// return a non-success <c>Gate.CircuitOpen</c> result and must NOT invoke the inner
    /// pipeline (verified by checking the received-call count is unchanged).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RepeatedFailures_CircuitOpens_SubsequentCallSkipsInner()
    {
        // Arrange — low thresholds so the circuit opens quickly
        var options = new GateResilienceOptions
        {
            TimeoutPerRequest = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,       // open when ≥ 50 % of calls fail
            MinimumThroughput = 3,    // needs at least 3 calls in the window
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(60), // long break so the circuit stays open
        };

        var (decorator, inner) = BuildDecorator(options);
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        // All inner calls throw an infra exception
        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Persistence layer unavailable"));

        // Act — exhaust the minimum throughput (3 calls); all fail → failure ratio = 1.0 > 0.5
        for (var i = 0; i < 3; i++)
            await decorator.ProcessAsync(submission, ct);

        // The circuit should now be OPEN.
        var innerCallCountAfterTrip = inner.ReceivedCalls().Count();

        var circuitOpenResult = await decorator.ProcessAsync(submission, ct);

        // Assert — the circuit-open call did NOT invoke the inner
        circuitOpenResult.IsFailure.ShouldBeTrue("circuit-open call must fail-closed");
        circuitOpenResult.Error.ShouldNotBeNull();
        circuitOpenResult.Error.ShouldStartWith("Gate.CircuitOpen:");
        circuitOpenResult.IsCancelled().ShouldBeFalse();

        // Inner must NOT have been called for the circuit-open request
        inner.ReceivedCalls().Count().ShouldBe(
            innerCallCountAfterTrip,
            "circuit-open call must short-circuit without invoking inner pipeline");
    }

    // -----------------------------------------------------------------------
    // Test 5: cancellation — returns Cancelled result
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the outer <see cref="CancellationToken"/> is already cancelled before calling
    /// <see cref="ResilientVerificationPipeline.ProcessAsync"/> the decorator returns a
    /// cancelled result immediately.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var (decorator, inner) = BuildDecorator(DefaultOptions());
        var submission = MakeSubmission();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cancelledCt = cts.Token;

        // Act
        var result = await decorator.ProcessAsync(submission, cancelledCt);

        // Assert — in IndQuestResults, Cancelled results set IsFailure = true
        // (it is a non-success result), but IsCancelled() distinguishes them from
        // ordinary failures.  The Gate.* prefix must NOT appear on a cancelled result.
        result.IsCancelled().ShouldBeTrue("cancelled token must produce a cancelled result");
        result.Error?.StartsWith("Gate.", StringComparison.Ordinal).ShouldBeFalse(
            "cancelled result must not carry a Gate.* error prefix");

        // Inner must never be called when already cancelled
        await inner.DidNotReceiveWithAnyArgs().ProcessAsync(default!, TestContext.Current.CancellationToken);
    }

    // -----------------------------------------------------------------------
    // Test 7 (A2): RED business verdict must NOT trip the circuit breaker
    // -----------------------------------------------------------------------

    /// <summary>
    /// A business RED verdict (inner returns <c>Result.WithSuccess</c> carrying a RED
    /// <see cref="VerdictSummary"/>) must NEVER count as a circuit-breaker failure.
    /// The breaker's <c>ShouldHandle</c> predicate is keyed on <see cref="Result.IsFailure"/>,
    /// not on the business-verdict signal inside the outcome.  Repeated RED verdicts must not
    /// open the circuit.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_InnerReturnsRedVerdict_CircuitBreakerDoesNotTrip()
    {
        // Arrange — low thresholds: would open quickly if RED verdicts tripped the breaker
        var options = new GateResilienceOptions
        {
            TimeoutPerRequest = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(60),
        };

        var (decorator, inner) = BuildDecorator(options);
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        // Build a RED outcome using the same VerdictAggregator path as production code.
        var job = new VerificationJob(
            Guid.NewGuid(), "hash-red", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        var findings = new List<RuleFinding>
        {
            RuleFinding.Fail(
                "CL-01", TechniqueClass.Deterministic, FindingSeverity.Critical,
                "1.0", "expected value", "actual value"),
        };
        // Pass the test's cancellation token so the aggregator respects test timeout.
        var redSummary = aggregator.Aggregate(findings, ct: ct).Value!;
        var redOutcome = new VerificationOutcome(job, redSummary, findings);

        // Inner always returns a success result wrapping a RED verdict.
        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(redOutcome)));

        // Act — exceed MinimumThroughput to give the circuit breaker the chance to open.
        const int callCount = 8;
        var results = new List<Result<VerificationOutcome>>();
        for (var i = 0; i < callCount; i++)
            results.Add(await decorator.ProcessAsync(submission, ct));

        // Assert — every call must return the red success outcome unchanged.
        // A circuit-open result would be a failure, so checking IsSuccess is sufficient.
        foreach (var r in results)
            r.IsSuccess.ShouldBeTrue(
                "a RED business verdict is a success Result — the circuit must never open for it");

        // Inner must be invoked on every call (circuit never opened).
        inner.ReceivedCalls().Count().ShouldBe(callCount,
            "inner must be called every time when the circuit is not open");
    }

    // -----------------------------------------------------------------------
    // Test 8 (A3): timeout-via-Cancelled-Result — decorator surfaces Gate.Timeout
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the inner pipeline follows the project convention of catching
    /// <see cref="OperationCanceledException"/> and returning a Cancelled
    /// <see cref="Result{T}"/> (rather than letting OCE propagate), Polly's inner Timeout
    /// strategy does NOT throw <see cref="Polly.Timeout.TimeoutRejectedException"/> because
    /// the task completed "successfully".  The decorator's post-execution guard must detect
    /// this case (outer CT not cancelled, but outcome is Cancelled) and surface it as
    /// <c>Gate.Timeout</c>, not as a user-initiated cancellation.
    /// </summary>
    [Fact(Timeout = 5_000)]
    public async Task ProcessAsync_InnerAbsorbsTimeoutCancellationReturningCancelled_SurfacesGateTimeout()
    {
        // Arrange — very short timeout so the test does not block.
        var options = DefaultOptions();
        options.TimeoutPerRequest = TimeSpan.FromMilliseconds(200);

        var (decorator, inner) = BuildDecorator(options);
        var submission = MakeSubmission();

        // Outer CT is NOT cancelled — any cancellation detected by the decorator is from Polly.
        var ct = TestContext.Current.CancellationToken;

        // Inner: follows project convention — absorbs OCE from Polly's linked token and
        // returns a Cancelled Result instead of throwing.
        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var innerCt = ci.Arg<CancellationToken>();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), innerCt).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Project convention: return Cancelled Result rather than throwing.
                    return ResultExtensions.Cancelled<VerificationOutcome>();
                }
                return Result<VerificationOutcome>.WithSuccess(default!);
            });

        // Act
        var result = await decorator.ProcessAsync(submission, ct);

        // Assert — must be Gate.Timeout, not a Cancelled result and not a success.
        result.IsFailure.ShouldBeTrue(
            "decorator must fail-closed when Polly timeout fires even if inner absorbed the cancellation");
        result.Error.ShouldNotBeNull();
        // Must start with Gate.Timeout: — not a cancelled result and not a generic error.
        result.Error.ShouldStartWith("Gate.Timeout:");
        result.IsCancelled().ShouldBeFalse(
            "a Polly-timeout must not be misreported as a user-initiated cancellation");
    }

    // -----------------------------------------------------------------------
    // Test 6: inner returns Result.IsFailure — also trips circuit breaker (Result predicate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the inner pipeline returns a <see cref="Result.IsFailure"/> result (e.g. DB
    /// persist failed) the circuit breaker's result predicate should also count it as a
    /// failure, eventually opening the circuit.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_InnerReturnsPersistFailure_CircuitEventuallyOpens()
    {
        // Arrange — low thresholds
        var options = new GateResilienceOptions
        {
            TimeoutPerRequest = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 3,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(60),
        };

        var (decorator, inner) = BuildDecorator(options);
        var submission = MakeSubmission();
        var ct = TestContext.Current.CancellationToken;

        // Inner returns a non-cancelled failure (e.g. persistence error)
        inner.ProcessAsync(submission, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<VerificationOutcome>.WithFailure("Verdict persistence failed")));

        // Act — exhaust minimum throughput with failures
        for (var i = 0; i < 3; i++)
            await decorator.ProcessAsync(submission, ct);

        var innerCallCountAfterTrip = inner.ReceivedCalls().Count();

        // The circuit should now be OPEN.
        var circuitOpenResult = await decorator.ProcessAsync(submission, ct);

        // Assert — subsequent call is circuit-open, not a pass-through of the inner failure
        circuitOpenResult.IsFailure.ShouldBeTrue();
        circuitOpenResult.Error.ShouldNotBeNull();
        circuitOpenResult.Error.ShouldStartWith("Gate.CircuitOpen:");

        inner.ReceivedCalls().Count().ShouldBe(
            innerCallCountAfterTrip,
            "circuit-open call must not invoke inner when circuit is open");
    }
}
