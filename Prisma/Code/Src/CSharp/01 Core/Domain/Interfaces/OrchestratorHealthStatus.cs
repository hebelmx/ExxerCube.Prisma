namespace ExxerCube.Prisma.Domain.Interfaces;

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