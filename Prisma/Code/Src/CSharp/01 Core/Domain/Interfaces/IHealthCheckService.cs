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