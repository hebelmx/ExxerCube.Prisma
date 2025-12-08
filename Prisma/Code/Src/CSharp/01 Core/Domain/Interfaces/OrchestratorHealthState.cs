namespace ExxerCube.Prisma.Domain.Interfaces;

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