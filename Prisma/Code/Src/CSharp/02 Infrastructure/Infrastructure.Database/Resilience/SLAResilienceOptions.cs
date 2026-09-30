namespace ExxerCube.Prisma.Infrastructure.Database.Resilience;

/// <summary>
/// Configuration options for SLA service resilience patterns.
/// </summary>
public class SLAResilienceOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SLA:Resilience";

    /// <summary>
    /// Gets or sets the number of consecutive failures before opening the circuit breaker (default: 5).
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration before attempting to close the circuit breaker (default: 1 minute).
    /// </summary>
    public TimeSpan CircuitBreakerResetTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the number of successful operations required to close the circuit breaker (default: 3).
    /// </summary>
    public int CircuitBreakerSuccessThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts (default: 3).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay for exponential backoff retry (default: 1 second).
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay for exponential backoff retry (default: 8 seconds).
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets or sets the timeout for database operations (default: 5 seconds).
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

