using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing.Reconciliation;

namespace Prisma.Reconciliator.HealthChecks;

/// <summary>
/// Health check service for the Reconciliator worker.
/// </summary>
/// <remarks>
/// Tracks <see cref="ReconciliationPipelineService"/> state to provide liveness/readiness information:
/// - Liveness: Process is running (always true if service is called).
/// - Readiness: The pipeline has started and subscribed to the <c>ExtractionCompletedEvent</c> stream.
/// </remarks>
public sealed class ReconciliatorHealthCheckService : IHealthCheckService
{
    private readonly IReadinessProbe _readiness;
    private readonly ILogger<ReconciliatorHealthCheckService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciliatorHealthCheckService"/> class.
    /// </summary>
    /// <param name="readiness">
    /// Readiness of the Reconciliation pipeline the Reconciliator worker actually drives (MVP-PATH 1.4 / E1-S4).
    /// Readiness reflects its started state — whether the observable subscription is active.
    /// </param>
    /// <param name="logger">Logger.</param>
    public ReconciliatorHealthCheckService(
        IReadinessProbe readiness,
        ILogger<ReconciliatorHealthCheckService> logger)
    {
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<OrchestratorHealthStatus> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        // Liveness: Process is running (if we can execute this, we're alive).
        _logger.LogTrace("Liveness check requested");

        var result = new OrchestratorHealthStatus(
            OrchestratorHealthState.Healthy,
            "Reconciliator worker process is running",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<OrchestratorHealthStatus> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        // Readiness: the Reconciliation pipeline has started and is subscribed to the ExtractionCompletedEvent stream.
        _logger.LogTrace("Readiness check requested");

        var isReady = _readiness.IsReady;

        var result = new OrchestratorHealthStatus(
            isReady ? OrchestratorHealthState.Healthy : OrchestratorHealthState.Unhealthy,
            isReady ? "Reconciliator orchestrator is ready" : "Reconciliator orchestrator is not ready",
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
        // Overall health: combine liveness and readiness.
        _logger.LogTrace("Health check requested");

        var liveness = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        var readiness = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);

        // Overall health is degraded if readiness is not healthy.
        var overallStatus = readiness.Status == OrchestratorHealthState.Healthy
            ? OrchestratorHealthState.Healthy
            : OrchestratorHealthState.Degraded;

        var result = new OrchestratorHealthStatus(
            overallStatus,
            $"Reconciliator worker: {overallStatus}",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["liveness"] = liveness.Status.ToString(),
                ["readiness"] = readiness.Status.ToString()
            });

        return result;
    }
}
