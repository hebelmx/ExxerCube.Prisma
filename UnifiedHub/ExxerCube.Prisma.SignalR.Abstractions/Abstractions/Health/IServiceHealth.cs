namespace ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;

/// <summary>
/// Interface for service health monitoring with real-time updates via SignalR.
/// Provides type-safe health status tracking with support for multiple health check types.
/// </summary>
/// <typeparam name="T">The type of health data structure.</typeparam>
public interface IServiceHealth<T>
{
    /// <summary>
    /// Gets the current health status.
    /// </summary>
    HealthStatus Status { get; }

    /// <summary>
    /// Gets the health data.
    /// </summary>
    T? Data { get; }

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    DateTime LastUpdated { get; }

    /// <summary>
    /// Updates the health status.
    /// </summary>
    /// <param name="status">The new health status.</param>
    /// <param name="data">The health data.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> UpdateHealthAsync(HealthStatus status, T? data = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when health status changes.
    /// </summary>
    event EventHandler<HealthStatusChangedEventArgs<T>>? HealthStatusChanged;
}

/// <summary>
/// Event arguments for health status change events.
/// </summary>
/// <typeparam name="T">The type of health data structure.</typeparam>
public class HealthStatusChangedEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the previous health status.
    /// </summary>
    public HealthStatus PreviousStatus { get; }

    /// <summary>
    /// Gets the new health status.
    /// </summary>
    public HealthStatus NewStatus { get; }

    /// <summary>
    /// Gets the health data.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthStatusChangedEventArgs{T}"/> class.
    /// </summary>
    /// <param name="previousStatus">The previous health status.</param>
    /// <param name="newStatus">The new health status.</param>
    /// <param name="data">The health data.</param>
    public HealthStatusChangedEventArgs(HealthStatus previousStatus, HealthStatus newStatus, T? data)
    {
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Data = data;
    }
}

