using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database.Resilience;

/// <summary>
/// Resilient wrapper for SLA Enforcer service, providing circuit breaker, retry, and timeout protection.
/// </summary>
public class ResilientSLAEnforcerService : ISLAEnforcer
{
    private readonly ISLAEnforcer _innerService;
    private readonly ILogger<ResilientSLAEnforcerService> _logger;
    private readonly SLAResilienceOptions _options;
    private readonly IAsyncPolicy _circuitBreakerPolicy;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IAsyncPolicy _timeoutPolicy;
    private readonly IAsyncPolicy _combinedPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientSLAEnforcerService"/> class.
    /// </summary>
    /// <param name="innerService">The inner SLA enforcer service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The resilience options.</param>
    public ResilientSLAEnforcerService(
        ISLAEnforcer innerService,
        ILogger<ResilientSLAEnforcerService> logger,
        IOptions<SLAResilienceOptions> options)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Build resilience policies
        _circuitBreakerPolicy = BuildCircuitBreakerPolicy();
        _retryPolicy = BuildRetryPolicy();
        _timeoutPolicy = BuildTimeoutPolicy();

        // Combine policies: Timeout -> Retry -> Circuit Breaker
        _combinedPolicy = Policy.WrapAsync(_timeoutPolicy, _retryPolicy, _circuitBreakerPolicy);
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus>> CalculateSLAStatusAsync(
        string fileId,
        DateTime intakeDate,
        int daysPlazo,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, cancellationToken),
            nameof(CalculateSLAStatusAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus>> UpdateSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.UpdateSLAStatusAsync(fileId, cancellationToken),
            nameof(UpdateSLAStatusAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus?>> GetSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync<SLAStatus?>(
            () => _innerService.GetSLAStatusAsync(fileId, cancellationToken),
            nameof(GetSLAStatusAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<System.Collections.Generic.List<SLAStatus>>> GetActiveCasesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.GetActiveCasesAsync(cancellationToken),
            nameof(GetActiveCasesAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<System.Collections.Generic.List<SLAStatus>>> GetAtRiskCasesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.GetAtRiskCasesAsync(cancellationToken),
            nameof(GetAtRiskCasesAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<System.Collections.Generic.List<SLAStatus>>> GetBreachedCasesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.GetBreachedCasesAsync(cancellationToken),
            nameof(GetBreachedCasesAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> EscalateCaseAsync(
        string fileId,
        EscalationLevel escalationLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _combinedPolicy.ExecuteAsync(async ct =>
            {
                var operationResult = await _innerService.EscalateCaseAsync(fileId, escalationLevel, ct).ConfigureAwait(false);

                // If operation failed with a transient error, throw to trigger retry
                if (operationResult.IsFailure && IsTransientError(operationResult.Error))
                {
                    _logger.LogWarning(
                        "Transient error in {OperationName}: {Error}. Will retry.",
                        nameof(EscalateCaseAsync), operationResult.Error);
                    throw new TransientException(operationResult.Error ?? "Transient error");
                }

                return operationResult;
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open for {OperationName}", nameof(EscalateCaseAsync));
            return Result.WithFailure(
                $"Circuit breaker is open. SLA service temporarily unavailable. Operation: {nameof(EscalateCaseAsync)}",
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Operation {OperationName} timed out after {Timeout}s",
                nameof(EscalateCaseAsync), _options.OperationTimeout.TotalSeconds);
            return Result.WithFailure(
                $"Operation timed out after {_options.OperationTimeout.TotalSeconds} seconds. Operation: {nameof(EscalateCaseAsync)}",
                ex);
        }
        catch (TransientException ex)
        {
            _logger.LogError(ex, "Transient error persisted after retries for {OperationName}", nameof(EscalateCaseAsync));
            return Result.WithFailure(
                $"Operation failed after retries: {ex.Message}. Operation: {nameof(EscalateCaseAsync)}",
                ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation {OperationName} was cancelled", nameof(EscalateCaseAsync));
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {OperationName}", nameof(EscalateCaseAsync));
            return Result.WithFailure(
                $"Unexpected error: {ex.Message}. Operation: {nameof(EscalateCaseAsync)}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> CalculateBusinessDaysAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithResilienceAsync(
            () => _innerService.CalculateBusinessDaysAsync(startDate, endDate, cancellationToken),
            nameof(CalculateBusinessDaysAsync),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with resilience policies (timeout, retry, circuit breaker).
    /// </summary>
    private async Task<Result<T>> ExecuteWithResilienceAsync<T>(
        Func<Task<Result<T>>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _combinedPolicy.ExecuteAsync(async ct =>
            {
                var operationResult = await operation().ConfigureAwait(false);

                // If operation failed with a transient error, throw to trigger retry
                if (operationResult.IsFailure && IsTransientError(operationResult.Error))
                {
                    _logger.LogWarning(
                        "Transient error in {OperationName}: {Error}. Will retry.",
                        operationName, operationResult.Error);
                    throw new TransientException(operationResult.Error ?? "Transient error");
                }

                return operationResult;
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open for {OperationName}", operationName);
            return Result<T>.WithFailure(
                $"Circuit breaker is open. SLA service temporarily unavailable. Operation: {operationName}",
                default,
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Operation {OperationName} timed out after {Timeout}s",
                operationName, _options.OperationTimeout.TotalSeconds);
            return Result<T>.WithFailure(
                $"Operation timed out after {_options.OperationTimeout.TotalSeconds} seconds. Operation: {operationName}",
                default,
                ex);
        }
        catch (TransientException ex)
        {
            _logger.LogError(ex, "Transient error persisted after retries for {OperationName}", operationName);
            return Result<T>.WithFailure(
                $"Operation failed after retries: {ex.Message}. Operation: {operationName}",
                default,
                ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation {OperationName} was cancelled", operationName);
            return ResultExtensions.Cancelled<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {OperationName}", operationName);
            return Result<T>.WithFailure(
                $"Unexpected error: {ex.Message}. Operation: {operationName}",
                default,
                ex);
        }
    }

    /// <summary>
    /// Determines if an error is transient and should be retried.
    /// </summary>
    private static bool IsTransientError(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        // Common transient database errors
        var transientKeywords = new[]
        {
            "timeout",
            "connection",
            "network",
            "temporarily",
            "deadlock",
            "lock",
            "busy",
            "unavailable"
        };

        var errorLower = error.ToLowerInvariant();
        return Array.Exists(transientKeywords, keyword => errorLower.Contains(keyword));
    }

    /// <summary>
    /// Builds the circuit breaker policy.
    /// </summary>
    private IAsyncPolicy BuildCircuitBreakerPolicy()
    {
        return Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: _options.CircuitBreakerFailureThreshold,
                durationOfBreak: _options.CircuitBreakerResetTimeout,
                onBreak: (exception, duration) =>
                {
                    if (exception is OperationCanceledException)
                    {
                        return;
                    }

                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration}s due to: {Exception}",
                        duration.TotalSeconds,
                        exception.Message ?? "Unknown error");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset - service is available again");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half‑open - testing service availability");
                });
    }

    /// <summary>
    /// Builds the retry policy with exponential backoff.
    /// </summary>
    private IAsyncPolicy BuildRetryPolicy()
    {
        return Policy
            .Handle<TransientException>()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt =>
                {
                    var delay = TimeSpan.FromMilliseconds(
                        _options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1));
                    return delay > _options.RetryMaxDelay ? _options.RetryMaxDelay : delay;
                },
                onRetry: (exception, delay, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}ms for operation: {Exception}",
                        retryCount, _options.MaxRetryAttempts, delay.TotalMilliseconds, exception.Message);
                });
    }

    /// <summary>
    /// Builds the timeout policy.
    /// </summary>
    private IAsyncPolicy BuildTimeoutPolicy()
    {
        return Policy.TimeoutAsync(
            _options.OperationTimeout,
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogWarning("Operation timed out after {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Exception used to indicate transient errors that should trigger retries.
    /// </summary>
    private class TransientException : Exception
    {
        public TransientException(string message) : base(message)
        {
        }
    }
}
