namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Service for providing health check status of orchestrator workers.
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result with status.</returns>
    Task<OrchestratorHealthStatus> GetLivenessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets readiness status (orchestrator is ready).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result with status and details.</returns>
    Task<OrchestratorHealthStatus> GetReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overall health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result combining liveness and readiness.</returns>
    Task<OrchestratorHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrator health check result.
/// Renamed from HealthCheckResult to avoid collision with Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.
/// </summary>
/// <param name="Status">Health status (Healthy, Degraded, Unhealthy).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Data">Additional diagnostic data.</param>
public record OrchestratorHealthStatus(
    OrchestratorHealthState Status,
    string Description,
    IReadOnlyDictionary<string, object>? Data = null);

/// <summary>
/// Orchestrator health state enumeration.
/// Renamed from HealthStatus to avoid collision with Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.
/// </summary>
public enum OrchestratorHealthState
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
