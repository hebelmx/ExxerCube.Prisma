using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.HealthChecks;

/// <summary>
/// Health check service for Orion ingestion worker.
/// </summary>
/// <remarks>
/// Tracks orchestrator state to provide liveness/readiness information:
/// - Liveness: Process is running (always true if service is called)
/// - Readiness: Orchestrator is started and ready to process
/// </remarks>
public sealed class OrionHealthCheckService : IHealthCheckService
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly ILogger<OrionHealthCheckService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrionHealthCheckService"/> class.
    /// </summary>
    /// <param name="orchestrator">Ingestion orchestrator.</param>
    /// <param name="logger">Logger.</param>
    public OrionHealthCheckService(
        IngestionOrchestrator orchestrator,
        ILogger<OrionHealthCheckService> logger)
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
            "Orion worker process is running",
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
            isReady ? "Orion orchestrator is ready" : "Orion orchestrator is not ready",
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
            $"Orion worker: {overallStatus}",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["liveness"] = liveness.Status.ToString(),
                ["readiness"] = readiness.Status.ToString()
            });

        return result;
    }

    // ========================================================================
    // NEW: Railway-Oriented Programming Methods (Stage 4.5)
    // ========================================================================

    /// <summary>
    /// Gets the liveness status using Railway-Oriented Programming.
    /// Returns Result&lt;OrchestratorHealthStatus&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing OrchestratorHealthStatus on success.</returns>
    public async Task<Result<OrchestratorHealthStatus>> GetLivenessWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<OrchestratorHealthStatus>();
        }

        var healthCheckResult = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        return Result<OrchestratorHealthStatus>.Success(healthCheckResult);
    }

    /// <summary>
    /// Gets the readiness status using Railway-Oriented Programming.
    /// Returns Result&lt;OrchestratorHealthStatus&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing OrchestratorHealthStatus on success.</returns>
    public async Task<Result<OrchestratorHealthStatus>> GetReadinessWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<OrchestratorHealthStatus>();
        }

        var healthCheckResult = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);
        return Result<OrchestratorHealthStatus>.Success(healthCheckResult);
    }

    /// <summary>
    /// Gets the overall health status using Railway-Oriented Programming.
    /// Returns Result&lt;OrchestratorHealthStatus&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing OrchestratorHealthStatus on success.</returns>
    public async Task<Result<OrchestratorHealthStatus>> GetHealthWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<OrchestratorHealthStatus>();
        }

        var healthCheckResult = await GetHealthAsync(cancellationToken).ConfigureAwait(false);
        return Result<OrchestratorHealthStatus>.Success(healthCheckResult);
    }
}
