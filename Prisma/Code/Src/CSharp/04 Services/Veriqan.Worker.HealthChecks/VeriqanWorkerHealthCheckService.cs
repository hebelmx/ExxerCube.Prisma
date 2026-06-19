using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Worker.HealthChecks;

/// <summary>
/// Health check service for the Veriqan VEC worker.
/// </summary>
/// <remarks>
/// <para>
/// Liveness reports that the process is running (always <see cref="OrchestratorHealthState.Healthy"/>
/// when the service is callable).
/// </para>
/// <para>
/// Readiness runs three checks:
/// <list type="number">
///   <item><description>
///     <b>Durable SQL persistence</b> — probes <see cref="VeriqanDbContext"/> via
///     <c>Database.CanConnectAsync</c>. Reports NOT ready both when the DB is unreachable and
///     when no SQL context is registered at all (the in-memory fallback, i.e. no
///     <c>ConnectionStrings:VeriqanDb</c>). An in-memory worker stays live but is never ready —
///     a regulated verdict pipeline must not take traffic without durable storage.
///   </description></item>
///   <item><description>
///     <b>CSV reference-data root</b> — verifies that <c>CsvReferenceData:RootDirectory</c> exists
///     on disk and contains at least one entry. This confirms the reference bundle is mounted.
///   </description></item>
///   <item><description>
///     <b>Tolerance-cache warm</b> — confirms that <see cref="SqlLegalToleranceProvider.IsWarm"/>
///     is <c>true</c>, meaning <c>VeriqanLegalBaselineStartupService</c> completed its
///     migrate→seed→<see cref="SqlLegalToleranceProvider.InitialiseAsync"/> sequence.
///     When running with in-memory persistence (no SQL provider) this probe is skipped and always
///     reports Healthy.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class VeriqanWorkerHealthCheckService : IHealthCheckService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqlLegalToleranceProvider? _toleranceProvider;
    private readonly CsvReferenceDataOptions _csvOptions;
    private readonly ILogger<VeriqanWorkerHealthCheckService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanWorkerHealthCheckService"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a short-lived scope for the scoped <see cref="VeriqanDbContext"/>.</param>
    /// <param name="toleranceProvider">
    /// Optional SQL tolerance provider. <see langword="null"/> when running with in-memory persistence
    /// (no connection string configured); the cache-warm check is skipped in that case.
    /// </param>
    /// <param name="csvOptions">Options containing the CSV reference-data root directory path.</param>
    /// <param name="logger">Structured logger.</param>
    public VeriqanWorkerHealthCheckService(
        IServiceScopeFactory scopeFactory,
        SqlLegalToleranceProvider? toleranceProvider,
        IOptions<CsvReferenceDataOptions> csvOptions,
        ILogger<VeriqanWorkerHealthCheckService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _toleranceProvider = toleranceProvider;
        _csvOptions = (csvOptions ?? throw new ArgumentNullException(nameof(csvOptions))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<OrchestratorHealthStatus> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Veriqan worker liveness check requested");

        var result = new OrchestratorHealthStatus(
            OrchestratorHealthState.Healthy,
            "Veriqan worker process is running",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow
            });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task<OrchestratorHealthStatus> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Veriqan worker readiness check requested");

        var details = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow
        };

        // Check 1: SQL connectivity
        var sqlHealthy = await CheckSqlConnectivityAsync(cancellationToken).ConfigureAwait(false);
        details["sqlConnectivity"] = sqlHealthy;

        // Check 2: CSV reference-data root
        var csvHealthy = CheckCsvReferenceDataRoot();
        details["csvReferenceDataRoot"] = csvHealthy;

        // Check 3: Tolerance-cache warm
        var cacheWarm = CheckToleranceCacheWarm();
        details["toleranceCacheWarm"] = cacheWarm;

        var allHealthy = sqlHealthy && csvHealthy && cacheWarm;

        if (!allHealthy)
        {
            _logger.LogWarning(
                "Veriqan worker readiness check FAILED — sql={Sql}, csv={Csv}, cache={Cache}",
                sqlHealthy, csvHealthy, cacheWarm);
        }

        var result = new OrchestratorHealthStatus(
            allHealthy ? OrchestratorHealthState.Healthy : OrchestratorHealthState.Unhealthy,
            allHealthy ? "Veriqan worker is ready" : "Veriqan worker is not ready — one or more readiness checks failed",
            details);

        return result;
    }

    /// <inheritdoc/>
    public async Task<OrchestratorHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Veriqan worker overall health check requested");

        var liveness = await GetLivenessAsync(cancellationToken).ConfigureAwait(false);
        var readiness = await GetReadinessAsync(cancellationToken).ConfigureAwait(false);

        // Degraded = live but not ready (readiness is not fully healthy).
        var overallStatus = readiness.Status == OrchestratorHealthState.Healthy
            ? OrchestratorHealthState.Healthy
            : OrchestratorHealthState.Degraded;

        var result = new OrchestratorHealthStatus(
            overallStatus,
            $"Veriqan worker: {overallStatus}",
            new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["liveness"] = liveness.Status.ToString(),
                ["readiness"] = readiness.Status.ToString()
            });

        return result;
    }

    // ── private check helpers ────────────────────────────────────────────────

    /// <summary>
    /// Probes durable SQL persistence using the scoped <see cref="VeriqanDbContext"/>.
    /// Returns <c>false</c> when no SQL context is registered (in-memory fallback): an
    /// in-memory worker loses verdicts on restart and is therefore NOT ready to accept
    /// production traffic (it remains live). This closes the silent-config trap (U2/U4)
    /// where a worker with no connection string would otherwise report ready.
    /// </summary>
    private async Task<bool> CheckSqlConnectivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetService<VeriqanDbContext>();

            // No SQL context registered — running on the in-memory fallback (no
            // ConnectionStrings:VeriqanDb). Durable persistence is required for readiness;
            // report NOT ready so the worker stays out of the load-balancer until a real
            // database is configured.
            if (dbContext is null)
            {
                _logger.LogWarning(
                    "Veriqan SQL connectivity check: no durable persistence configured "
                    + "(in-memory fallback) — worker is live but NOT ready for production traffic");
                return false;
            }

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            if (!canConnect)
            {
                _logger.LogWarning("Veriqan SQL connectivity check: CanConnectAsync returned false");
            }

            return canConnect;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Veriqan SQL connectivity check threw an exception");
            return false;
        }
    }

    /// <summary>
    /// Checks that <see cref="CsvReferenceDataOptions.RootDirectory"/> exists and contains at least one entry.
    /// </summary>
    private bool CheckCsvReferenceDataRoot()
    {
        var root = _csvOptions.RootDirectory;

        if (string.IsNullOrWhiteSpace(root))
        {
            _logger.LogWarning(
                "Veriqan CSV reference-data root check: CsvReferenceData:RootDirectory is not configured");
            return false;
        }

        if (!Directory.Exists(root))
        {
            _logger.LogWarning(
                "Veriqan CSV reference-data root check: directory does not exist — {Root}", root);
            return false;
        }

        // Non-empty: at least one file or sub-directory present.
        var hasEntries = Directory.EnumerateFileSystemEntries(root).Any();
        if (!hasEntries)
        {
            _logger.LogWarning(
                "Veriqan CSV reference-data root check: directory is empty — {Root}", root);
        }

        return hasEntries;
    }

    /// <summary>
    /// Reports whether the <see cref="SqlLegalToleranceProvider"/> cache has been warmed.
    /// Returns <c>true</c> when no SQL provider is registered (in-memory persistence path).
    /// </summary>
    private bool CheckToleranceCacheWarm()
    {
        // No SQL provider registered — in-memory path; the DefaultLegalToleranceProvider
        // is always ready; skip the check.
        if (_toleranceProvider is null)
            return true;

        var isWarm = _toleranceProvider.IsWarm;

        if (!isWarm)
        {
            _logger.LogWarning(
                "Veriqan tolerance-cache warm check: SqlLegalToleranceProvider has not yet been initialised");
        }

        return isWarm;
    }
}
