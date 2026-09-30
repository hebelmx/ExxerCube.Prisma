using Microsoft.Extensions.Time.Testing;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Tests for the P3 lockout-safety control <see cref="SiaraLoginCircuitBreaker"/> (ADR-010): exponential
/// backoff between failures and a hard stop-and-alert before a retry storm can lock the SIARA account.
/// </summary>
public sealed class SiaraLoginCircuitBreakerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 11, 8, 0, 0, TimeSpan.Zero);

    private static (SiaraLoginCircuitBreaker Breaker, FakeTimeProvider Clock) Build(
        int maxFailures = 3,
        TimeSpan? initialBackoff = null,
        double multiplier = 2.0,
        TimeSpan? maxBackoff = null)
    {
        var clock = new FakeTimeProvider(Start);
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.AutomatedLogin,
            Automated = new SiaraAutomatedOptions
            {
                MaxConsecutiveFailures = maxFailures,
                InitialBackoff = initialBackoff ?? TimeSpan.FromSeconds(30),
                BackoffMultiplier = multiplier,
                MaxBackoff = maxBackoff ?? TimeSpan.FromMinutes(15),
            },
        });

        var breaker = new SiaraLoginCircuitBreaker(
            options, clock, Substitute.For<ILogger<SiaraLoginCircuitBreaker>>());
        return (breaker, clock);
    }

    [Fact]
    public void CheckCanAttempt_Initially_AllowsAttempt()
    {
        var (breaker, _) = Build();

        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void RecordFailure_BelowThreshold_BlocksUntilBackoffElapses()
    {
        var (breaker, clock) = Build(initialBackoff: TimeSpan.FromSeconds(30));

        breaker.RecordFailure();

        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();

        // Just before the window elapses it is still blocked; once it elapses, an attempt is allowed again.
        clock.Advance(TimeSpan.FromSeconds(29));
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();

        clock.Advance(TimeSpan.FromSeconds(1));
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void RecordFailure_BackoffGrowsExponentially()
    {
        var (breaker, clock) = Build(maxFailures: 5, initialBackoff: TimeSpan.FromSeconds(30), multiplier: 2.0);

        // 1st failure → 30s window.
        breaker.RecordFailure();
        clock.Advance(TimeSpan.FromSeconds(30));
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();

        // 2nd failure → 60s window: 30s is not enough, 60s is.
        breaker.RecordFailure();
        clock.Advance(TimeSpan.FromSeconds(30));
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(30));
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ComputeBackoff_IsCappedAtMaxBackoff()
    {
        var (breaker, clock) = Build(
            maxFailures: 10,
            initialBackoff: TimeSpan.FromMinutes(10),
            multiplier: 10.0,
            maxBackoff: TimeSpan.FromMinutes(15));

        breaker.RecordFailure(); // window = 10m
        clock.Advance(TimeSpan.FromMinutes(10));
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();

        breaker.RecordFailure(); // 10m * 10 = 100m, capped to 15m
        clock.Advance(TimeSpan.FromMinutes(15));
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void RecordFailure_AtThreshold_HardStopsRegardlessOfTime()
    {
        var (breaker, clock) = Build(maxFailures: 3);

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();

        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();
        // A hard stop is not a backoff: time passing does not reopen the circuit.
        clock.Advance(TimeSpan.FromHours(24));
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCountAndBackoff()
    {
        var (breaker, _) = Build();

        breaker.RecordFailure();
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();

        breaker.RecordSuccess();

        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void RecordSuccess_AfterHardStop_ReopensCircuit()
    {
        var (breaker, _) = Build(maxFailures: 2);

        breaker.RecordFailure();
        breaker.RecordFailure(); // hard stop
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();

        breaker.RecordSuccess();

        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }
}
