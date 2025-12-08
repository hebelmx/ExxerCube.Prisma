using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing;

namespace Prisma.Athena.HealthChecks;

/// <summary>
/// Health check service for Athena processing worker.
/// </summary>
/// <remarks>
/// Tracks orchestrator state to provide liveness/readiness information:
/// - Liveness: Process is running (always true if service is called)
/// - Readiness: Orchestrator is started and ready to process
/// </remarks>
public sealed class AthenaHealthCheckService : IHealthCheckService
{
    private readonly ProcessingOrchestrator _orchestrator;
    private readonly ILogger<AthenaHealthCheckService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AthenaHealthCheckService"/> class.
    /// </summary>
    /// <param name="orchestrator">Processing orchestrator.</param>
    /// <param name="logger">Logger.</param>
    public AthenaHealthCheckService(
        ProcessingOrchestrator orchestrator,
        ILogger<AthenaHealthCheckService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<OrchestratorHealthStatus> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        // Liveness: Process is running (if we can execute this, we're alive)
        _logger.LogTrace("Liveness check requested");

        var result = new OrchestratorHealthStatus(
            OrchestratorHealthState.Healthy,
            "Athena worker process is running",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<OrchestratorHealthStatus> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        // Readiness: Orchestrator is started and ready to process
        _logger.LogTrace("Readiness check requested");

        // TODO: Check orchestrator.IsStarted or similar state
        // For now, assume ready if orchestrator is not null
        var isReady = _orchestrator != null;

        var result = new OrchestratorHealthStatus(
            isReady ? OrchestratorHealthState.Healthy : OrchestratorHealthState.Unhealthy,
            isReady ? "Athena orchestrator is ready" : "Athena orchestrator is not ready",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["orchestratorReady"] = isReady
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task<OrchestratorHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        // Overall health: Combine liveness and readiness
        _logger.LogTrace("Health check requested");

        var liveness = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        var readiness = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);

        // Overall health is degraded if readiness is not healthy
        var overallStatus = readiness.Status == OrchestratorHealthState.Healthy
            ? OrchestratorHealthState.Healthy
            : OrchestratorHealthState.Degraded;

        var result = new OrchestratorHealthStatus(
            overallStatus,
            $"Athena worker: {overallStatus}",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["liveness"] = liveness.Status.ToString(),
                ["readiness"] = readiness.Status.ToString()
            });

        return result;
    }
}
