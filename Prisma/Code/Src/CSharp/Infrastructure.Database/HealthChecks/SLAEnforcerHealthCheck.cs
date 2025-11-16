namespace ExxerCube.Prisma.Infrastructure.Database.HealthChecks;

/// <summary>
/// Health check for SLA Enforcer service, verifying database connectivity and SLA calculation functionality.
/// </summary>
public class SLAEnforcerHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SLAEnforcerHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAEnforcerHealthCheck"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    public SLAEnforcerHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<SLAEnforcerHealthCheck> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();
            var slaEnforcer = scope.ServiceProvider.GetRequiredService<ISLAEnforcer>();

            var data = new Dictionary<string, object>();

            // Check 1: Database connectivity
            var dbCheckResult = await CheckDatabaseConnectivityAsync(dbContext, cancellationToken).ConfigureAwait(false);
            data["database_connected"] = dbCheckResult.IsHealthy;
            if (!dbCheckResult.IsHealthy)
            {
                _logger.LogWarning("SLA health check: Database connectivity check failed - {Error}", dbCheckResult.Error);
                return HealthCheckResult.Unhealthy(
                    "SLA database connectivity check failed",
                    dbCheckResult.Exception,
                    data);
            }

            // Check 2: SLA calculation functionality
            var calculationCheckResult = await CheckSLACalculationAsync(slaEnforcer, cancellationToken).ConfigureAwait(false);
            data["sla_calculation_working"] = calculationCheckResult.IsHealthy;
            data["sla_calculation_time_ms"] = calculationCheckResult.DurationMs;

            if (!calculationCheckResult.IsHealthy)
            {
                _logger.LogWarning("SLA health check: Calculation check failed - {Error}", calculationCheckResult.Error);
                return HealthCheckResult.Degraded(
                    "SLA calculation check failed",
                    calculationCheckResult.Exception,
                    data);
            }

            // Check 3: Performance threshold (should complete within 200ms)
            if (calculationCheckResult.DurationMs > 200)
            {
                _logger.LogWarning(
                    "SLA health check: Calculation performance degraded - {Duration}ms exceeds 200ms threshold",
                    calculationCheckResult.DurationMs);
                return HealthCheckResult.Degraded(
                    $"SLA calculation performance degraded: {calculationCheckResult.DurationMs}ms exceeds 200ms threshold",
                    data: data);
            }

            // Check 4: Query active cases (verify query functionality)
            var queryCheckResult = await CheckQueryFunctionalityAsync(slaEnforcer, cancellationToken).ConfigureAwait(false);
            data["query_functionality_working"] = queryCheckResult.IsHealthy;

            if (!queryCheckResult.IsHealthy)
            {
                _logger.LogWarning("SLA health check: Query functionality check failed - {Error}", queryCheckResult.Error);
                return HealthCheckResult.Degraded(
                    "SLA query functionality check failed",
                    queryCheckResult.Exception,
                    data);
            }

            _logger.LogDebug("SLA health check passed: Database={DbOk}, Calculation={CalcOk}ms, Query={QueryOk}",
                dbCheckResult.IsHealthy, calculationCheckResult.DurationMs, queryCheckResult.IsHealthy);

            return HealthCheckResult.Healthy("SLA Enforcer service is healthy", data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SLA health check cancelled");
            return HealthCheckResult.Unhealthy("Health check was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SLA health check encountered an exception");
            return HealthCheckResult.Unhealthy("SLA health check failed with exception", ex);
        }
    }

    /// <summary>
    /// Checks database connectivity by attempting to query the SLAStatus table.
    /// </summary>
    private async Task<HealthCheckSubResult> CheckDatabaseConnectivityAsync(
        PrismaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Simple query to verify database connectivity
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            
            if (!canConnect)
            {
                return new HealthCheckSubResult
                {
                    IsHealthy = false,
                    Error = "Cannot connect to database"
                };
            }

            // Verify SLAStatus table exists and is queryable
            var count = await dbContext.SLAStatus.CountAsync(cancellationToken).ConfigureAwait(false);
            
            return new HealthCheckSubResult
            {
                IsHealthy = true
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckSubResult
            {
                IsHealthy = false,
                Error = ex.Message,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Checks SLA calculation functionality by performing a test calculation.
    /// </summary>
    private async Task<HealthCheckSubResult> CheckSLACalculationAsync(
        ISLAEnforcer slaEnforcer,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Perform a test calculation with known values
            var testFileId = $"health-check-{Guid.NewGuid():N}";
            var testIntakeDate = DateTime.UtcNow;
            var testDaysPlazo = 5;

            var result = await slaEnforcer.CalculateSLAStatusAsync(
                testFileId,
                testIntakeDate,
                testDaysPlazo,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            if (result.IsFailure)
            {
                return new HealthCheckSubResult
                {
                    IsHealthy = false,
                    Error = result.Error ?? "SLA calculation failed",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Clean up test data
            try
            {
                // Note: In a real scenario, you might want to clean up the test record
                // For health checks, it's acceptable to leave it as it's minimal data
            }
            catch
            {
                // Ignore cleanup errors in health check
            }

            return new HealthCheckSubResult
            {
                IsHealthy = true,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckSubResult
            {
                IsHealthy = false,
                Error = ex.Message,
                Exception = ex,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Checks query functionality by querying active cases.
    /// </summary>
    private async Task<HealthCheckSubResult> CheckQueryFunctionalityAsync(
        ISLAEnforcer slaEnforcer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await slaEnforcer.GetActiveCasesAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return new HealthCheckSubResult
                {
                    IsHealthy = false,
                    Error = result.Error ?? "Query functionality failed"
                };
            }

            return new HealthCheckSubResult
            {
                IsHealthy = true
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckSubResult
            {
                IsHealthy = false,
                Error = ex.Message,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Represents a sub-result of a health check component.
    /// </summary>
    private class HealthCheckSubResult
    {
        public bool IsHealthy { get; set; }
        public string? Error { get; set; }
        public Exception? Exception { get; set; }
        public long DurationMs { get; set; }
    }
}

