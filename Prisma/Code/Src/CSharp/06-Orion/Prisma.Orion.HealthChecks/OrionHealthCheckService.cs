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
    public Task<HealthCheckResult> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        // Liveness: Process is running (if we can execute this, we're alive)
        _logger.LogTrace("Liveness check requested");

        var result = new HealthCheckResult(
            HealthStatus.Healthy,
            "Orion worker process is running",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        // Readiness: Orchestrator is started and ready to process
        _logger.LogTrace("Readiness check requested");

        // TODO: Check orchestrator.IsStarted or similar state
        // For now, assume ready if orchestrator is not null
        var isReady = _orchestrator != null;

        var result = new HealthCheckResult(
            isReady ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            isReady ? "Orion orchestrator is ready" : "Orion orchestrator is not ready",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["orchestratorReady"] = isReady
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        // Overall health: Combine liveness and readiness
        _logger.LogTrace("Health check requested");

        var liveness = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        var readiness = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);

        // Overall health is degraded if readiness is not healthy
        var overallStatus = readiness.Status == HealthStatus.Healthy
            ? HealthStatus.Healthy
            : HealthStatus.Degraded;

        var result = new HealthCheckResult(
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
    /// Returns Result&lt;HealthCheckResult&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing HealthCheckResult on success.</returns>
    public async Task<Result<HealthCheckResult>> GetLivenessWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<HealthCheckResult>();
        }

        var healthCheckResult = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        return Result<HealthCheckResult>.Success(healthCheckResult);
    }

    /// <summary>
    /// Gets the readiness status using Railway-Oriented Programming.
    /// Returns Result&lt;HealthCheckResult&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing HealthCheckResult on success.</returns>
    public async Task<Result<HealthCheckResult>> GetReadinessWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<HealthCheckResult>();
        }

        var healthCheckResult = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);
        return Result<HealthCheckResult>.Success(healthCheckResult);
    }

    /// <summary>
    /// Gets the overall health status using Railway-Oriented Programming.
    /// Returns Result&lt;HealthCheckResult&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing HealthCheckResult on success.</returns>
    public async Task<Result<HealthCheckResult>> GetHealthWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<HealthCheckResult>();
        }

        var healthCheckResult = await GetHealthAsync(cancellationToken).ConfigureAwait(false);
        return Result<HealthCheckResult>.Success(healthCheckResult);
    }
}
