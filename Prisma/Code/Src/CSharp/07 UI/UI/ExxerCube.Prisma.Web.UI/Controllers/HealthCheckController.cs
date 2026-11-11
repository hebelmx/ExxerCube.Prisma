using Microsoft.AspNetCore.Mvc;
using MsHealthChecks = Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExxerCube.Prisma.Web.UI.Controllers;

/// <summary>
/// REST controller that exposes health status for backward-compatible API consumers.
/// </summary>
/// <remarks>
/// The authoritative health probes are the ASP.NET Core middleware endpoints:
/// <c>GET /health</c> and <c>GET /health/ready</c> (wired via <c>MapHealthChecks</c>).
/// This controller delegates to the framework's <see cref="MsHealthChecks.HealthCheckService"/> so the
/// response reflects the real registered checks (DB connectivity via <c>PrismaDbHealthCheck</c>),
/// never a hardcoded string. MVP-PATH E1-S4.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    private readonly ILogger<HealthCheckController> _logger;
    private readonly MsHealthChecks.HealthCheckService _healthCheckService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="healthCheckService">The ASP.NET Core health check service.</param>
    public HealthCheckController(
        ILogger<HealthCheckController> logger,
        MsHealthChecks.HealthCheckService healthCheckService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
    }

    /// <summary>
    /// Gets the system health status by running all registered health checks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated health status.</returns>
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        try
        {
            var report = await _healthCheckService
                .CheckHealthAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var statusCode = report.Status == MsHealthChecks.HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;

            var response = new
            {
                Status = report.Status.ToString(),
                Timestamp = DateTime.UtcNow,
                Checks = report.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        Status = kvp.Value.Status.ToString(),
                        Description = kvp.Value.Description
                    })
            };

            _logger.LogTrace(
                "Health check requested via API controller. Aggregate status: {Status}",
                report.Status);

            return StatusCode(statusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check API failed unexpectedly");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Status = MsHealthChecks.HealthStatus.Unhealthy.ToString(),
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
