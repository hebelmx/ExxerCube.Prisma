namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Represents the status of a circuit breaker.
/// </summary>
internal enum CircuitBreakerStatus
{
    /// <summary>
    /// Circuit is closed - operations are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - operations are blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - one operation is allowed to test recovery.
    /// </summary>
    HalfOpen
}