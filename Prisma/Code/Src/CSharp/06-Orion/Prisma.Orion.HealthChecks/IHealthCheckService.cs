namespace Prisma.Orion.HealthChecks;

/// <summary>
/// Service for providing health check status of Orion worker.
/// </summary>
/// <remarks>
/// Health checks follow standard patterns:
/// - Liveness: Is the process running?
/// - Readiness: Is the orchestrator ready to process work?
/// </remarks>
public interface IHealthCheckService
{
    /// <summary>
    /// Gets liveness status (process is running).
    /// </summary>
    /// <returns>Health check result with status.</returns>
    Task<HealthCheckResult> GetLivenessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets readiness status (orchestrator is ready).
    /// </summary>
    /// <returns>Health check result with status and details.</returns>
    Task<HealthCheckResult> GetReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overall health status.
    /// </summary>
    /// <returns>Health check result combining liveness and readiness.</returns>
    Task<HealthCheckResult> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check result.
/// </summary>
/// <param name="Status">Health status (Healthy, Degraded, Unhealthy).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Data">Additional diagnostic data.</param>
public record HealthCheckResult(
    HealthStatus Status,
    string Description,
    IReadOnlyDictionary<string, object>? Data = null);

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is degraded but functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is unhealthy.
    /// </summary>
    Unhealthy
}
