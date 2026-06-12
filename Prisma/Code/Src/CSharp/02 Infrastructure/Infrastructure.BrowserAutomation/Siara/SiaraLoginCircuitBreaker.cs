namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// The P3 lockout-safety control for <see cref="Domain.Enum.SiaraAuthMode.AutomatedLogin"/> (ADR-010):
/// counts consecutive login failures, applies exponential backoff between attempts, and
/// <strong>hard-stops and alerts a human</strong> after a threshold — so an unattended loop can never
/// become a retry storm that locks out the bank's real SIARA account.
/// </summary>
/// <remarks>
/// <para>
/// This is a stateful, thread-safe collaborator, <strong>not</strong> a domain port: it holds the
/// failure/backoff state that must persist <em>across</em> login attempts, so it is registered as a
/// singleton (S7) while the provider that consults it stays scoped. The clock is injected via
/// <see cref="TimeProvider"/> so the backoff windows are deterministically testable.
/// </para>
/// <para>
/// Only genuine login <em>rejections</em> should be recorded as failures (a submitted login SIARA refused);
/// infrastructure faults (browser launch, navigation, capture) do not count toward the lockout threshold
/// because they never reach SIARA's auth.
/// </para>
/// </remarks>
public sealed class SiaraLoginCircuitBreaker
{
    private readonly SiaraAutomatedOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiaraLoginCircuitBreaker> _logger;
    private readonly object _gate = new();

    private int _consecutiveFailures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;
    private bool _hardStopped;

    /// <summary>Initializes a new instance of the <see cref="SiaraLoginCircuitBreaker"/> class.</summary>
    /// <param name="options">The SIARA auth options (the automated section supplies the thresholds).</param>
    /// <param name="timeProvider">The clock used for the backoff windows.</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraLoginCircuitBreaker(
        IOptions<SiaraAuthOptions> options,
        TimeProvider timeProvider,
        ILogger<SiaraLoginCircuitBreaker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value.Automated;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Decides whether a login attempt may proceed now. Returns a failure (without attempting) when the
    /// circuit is backing off or has hard-stopped — so the caller never drives the form during a storm.
    /// </summary>
    /// <returns>Success when an attempt is allowed; otherwise a failure describing why it is blocked.</returns>
    public Result CheckCanAttempt()
    {
        lock (_gate)
        {
            if (_hardStopped)
            {
                return Result.WithFailure(
                    $"SIARA automated login is halted after {_consecutiveFailures} consecutive failures; " +
                    "human intervention is required to avoid account lockout.");
            }

            var now = _timeProvider.GetUtcNow();
            if (now < _openUntil)
            {
                return Result.WithFailure(
                    $"SIARA automated login is backing off until {_openUntil:O} after {_consecutiveFailures} failure(s).");
            }

            return Result.Success();
        }
    }

    /// <summary>Records a successful login, clearing the failure count and any backoff/hard-stop state.</summary>
    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openUntil = DateTimeOffset.MinValue;
            _hardStopped = false;
        }
    }

    /// <summary>
    /// Records a rejected login: increments the failure count and either opens the circuit for an
    /// exponentially-growing backoff window, or hard-stops and alerts once the threshold is reached.
    /// </summary>
    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;

            if (_consecutiveFailures >= _options.MaxConsecutiveFailures)
            {
                _hardStopped = true;
                _logger.LogCritical(
                    "SIARA automated login halted after {Failures} consecutive failures; manual intervention " +
                    "required to avoid locking out the SIARA account.",
                    _consecutiveFailures);
                return;
            }

            var backoff = ComputeBackoff(_consecutiveFailures);
            _openUntil = _timeProvider.GetUtcNow().Add(backoff);
            _logger.LogWarning(
                "SIARA automated login failed ({Failures}); backing off {Backoff} until {OpenUntil}.",
                _consecutiveFailures,
                backoff,
                _openUntil);
        }
    }

    private TimeSpan ComputeBackoff(int failures)
    {
        var factor = Math.Pow(_options.BackoffMultiplier, failures - 1);
        var milliseconds = _options.InitialBackoff.TotalMilliseconds * factor;
        var capped = Math.Min(milliseconds, _options.MaxBackoff.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}
