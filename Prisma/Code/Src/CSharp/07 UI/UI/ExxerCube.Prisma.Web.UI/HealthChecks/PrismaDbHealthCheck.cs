using MsHealthChecks = Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExxerCube.Prisma.Web.UI.HealthChecks;

/// <summary>
/// ASP.NET Core <see cref="MsHealthChecks.IHealthCheck"/> that validates database connectivity for the Web.UI host.
/// </summary>
/// <remarks>
/// Wired into <c>services.AddHealthChecks()</c> so that <c>MapHealthChecks("/health")</c> returns a real
/// Unhealthy state (HTTP 503) when the Identity/application database is unreachable, rather than a
/// hardcoded 200 OK. MVP-PATH E1-S4.
/// </remarks>
public sealed class PrismaDbHealthCheck : MsHealthChecks.IHealthCheck
{
    private readonly IDbContextFactory<PrismaIdentityDbContext> _dbContextFactory;
    private readonly ILogger<PrismaDbHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaDbHealthCheck"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory used to create a short-lived <see cref="PrismaIdentityDbContext"/> per check.</param>
    /// <param name="logger">Logger.</param>
    public PrismaDbHealthCheck(
        IDbContextFactory<PrismaIdentityDbContext> dbContextFactory,
        ILogger<PrismaDbHealthCheck> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<MsHealthChecks.HealthCheckResult> CheckHealthAsync(
        MsHealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return MsHealthChecks.HealthCheckResult.Unhealthy("Health check cancelled");
        }

        try
        {
            await using var dbContext = await _dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            var canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            if (canConnect)
            {
                _logger.LogTrace("Database connectivity check passed");
                return MsHealthChecks.HealthCheckResult.Healthy("Database is reachable");
            }

            _logger.LogWarning("Database connectivity check failed: CanConnectAsync returned false");
            return MsHealthChecks.HealthCheckResult.Unhealthy("Database is unreachable");
        }
        catch (OperationCanceledException)
        {
            return MsHealthChecks.HealthCheckResult.Unhealthy("Health check cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connectivity check threw an unexpected exception");
            return MsHealthChecks.HealthCheckResult.Unhealthy(
                $"Database connectivity check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
