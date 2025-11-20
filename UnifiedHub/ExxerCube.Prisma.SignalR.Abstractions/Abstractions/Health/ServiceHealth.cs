using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;

/// <summary>
/// Service health monitoring implementation with real-time updates via SignalR.
/// Provides type-safe health status tracking following Railway-Oriented Programming patterns.
/// </summary>
/// <typeparam name="T">The type of health data structure.</typeparam>
public class ServiceHealth<T> : IServiceHealth<T>
{
    private readonly ILogger<ServiceHealth<T>> _logger;
    private HealthStatus _status;
    private T? _data;
    private DateTime _lastUpdated;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHealth{T}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ServiceHealth(ILogger<ServiceHealth<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _status = HealthStatus.Healthy;
        _lastUpdated = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public HealthStatus Status => _status;

    /// <inheritdoc />
    public T? Data => _data;

    /// <inheritdoc />
    public DateTime LastUpdated => _lastUpdated;

    /// <inheritdoc />
    public event EventHandler<HealthStatusChangedEventArgs<T>>? HealthStatusChanged;

    /// <inheritdoc />
    public Task<Result> UpdateHealthAsync(HealthStatus status, T? data = default, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled before updating health status");
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        try
        {
            var previousStatus = _status;
            _status = status;
            _data = data;
            _lastUpdated = DateTime.UtcNow;

            _logger.LogDebug("Health status updated from {PreviousStatus} to {NewStatus}", previousStatus, status);

            // Raise event if status changed
            if (previousStatus != status)
            {
                var args = new HealthStatusChangedEventArgs<T>(previousStatus, status, data);
                HealthStatusChanged?.Invoke(this, args);
                _logger.LogInformation("Health status changed from {PreviousStatus} to {NewStatus}", previousStatus, status);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health status");
            return Task.FromResult(Result.WithFailure($"Failed to update health status: {ex.Message}"));
        }
    }
}

