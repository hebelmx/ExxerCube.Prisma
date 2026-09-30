namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
internal class CircuitBreakerState
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private CircuitBreakerStatus _status;
    private DateTime _lastFailureTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerState"/> class.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before opening the circuit.</param>
    /// <param name="resetTimeout">The timeout before attempting to close the circuit.</param>
    public CircuitBreakerState(int failureThreshold, TimeSpan resetTimeout)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout;
        _status = CircuitBreakerStatus.Closed;
        _failureCount = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the circuit breaker can execute operations.
    /// </summary>
    /// <returns>True if operations can be executed; otherwise, false.</returns>
    public bool CanExecute()
    {
        switch (_status)
        {
            case CircuitBreakerStatus.Closed:
                return true;
            case CircuitBreakerStatus.Open:
                if (DateTime.UtcNow - _lastFailureTime >= _resetTimeout)
                {
                    _status = CircuitBreakerStatus.HalfOpen;
                    return true;
                }
                return false;
            case CircuitBreakerStatus.HalfOpen:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Called when an operation succeeds.
    /// </summary>
    public void OnSuccess()
    {
        switch (_status)
        {
            case CircuitBreakerStatus.Closed:
                _failureCount = 0;
                break;
            case CircuitBreakerStatus.HalfOpen:
                _status = CircuitBreakerStatus.Closed;
                _failureCount = 0;
                break;
        }
    }

    /// <summary>
    /// Called when an operation fails.
    /// </summary>
    public void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_status == CircuitBreakerStatus.Closed && _failureCount >= _failureThreshold)
        {
            _status = CircuitBreakerStatus.Open;
        }
        else if (_status == CircuitBreakerStatus.HalfOpen)
        {
            _status = CircuitBreakerStatus.Open;
        }
    }
}