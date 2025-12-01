using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Application.Services;

namespace ExxerCube.Prisma.Web.UI.Controllers;

/// <summary>
/// Metrics controller for dashboard data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly ILogger<MetricsController> _logger;
    private readonly IProcessingMetricsService _metricsService;
    private readonly HealthCheckService _healthCheckService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="metricsService">The metrics service.</param>
    /// <param name="healthCheckService">The health check service.</param>
    public MetricsController(
        ILogger<MetricsController> logger,
        IProcessingMetricsService metricsService,
        HealthCheckService healthCheckService)
    {
        _logger = logger;
        _metricsService = metricsService;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Gets the current dashboard metrics.
    /// </summary>
    /// <returns>The dashboard metrics.</returns>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardMetrics()
    {
        try
        {
            var statistics = await _metricsService.GetCurrentStatisticsAsync();
            var recentEvents = _metricsService.GetRecentEvents(10);
            
            var dashboardMetrics = new DashboardMetrics
            {
                TotalDocumentsProcessed = statistics.TotalDocumentsProcessed,
                SuccessRate = statistics.SuccessRate * 100, // Convert to percentage
                AverageProcessingTime = statistics.AverageProcessingTime,
                AverageConfidence = statistics.AverageConfidence * 100, // Convert to percentage
                DocumentsInQueue = _metricsService.ActiveProcessingCount,
                RecentErrors = recentEvents
                    .Where(e => !e.IsSuccess && !string.IsNullOrEmpty(e.ErrorMessage))
                    .Select(e => e.ErrorMessage!)
                    .Take(5)
                    .ToList()
            };

            return Ok(dashboardMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard metrics");
            return StatusCode(500, "Failed to retrieve dashboard metrics");
        }
    }

    /// <summary>
    /// Gets processing time trends.
    /// </summary>
    /// <returns>The processing time trends.</returns>
    [HttpGet("processing-trends")]
    public IActionResult GetProcessingTrends()
    {
        try
        {
            var recentEvents = _metricsService.GetRecentEvents(50);
            var trends = recentEvents
                .GroupBy(e => e.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    AverageProcessingTime = g.Average(e => e.ProcessingTimeSeconds),
                    DocumentCount = g.Count(),
                    SuccessRate = g.Count(e => e.IsSuccess) / (double)g.Count() * 100
                })
                .OrderBy(t => t.Date)
                .ToList();

            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing trends");
            return StatusCode(500, "Failed to retrieve processing trends");
        }
    }

    /// <summary>
    /// Gets error statistics.
    /// </summary>
    /// <returns>The error statistics.</returns>
    [HttpGet("error-stats")]
    public IActionResult GetErrorStats()
    {
        try
        {
            var recentEvents = _metricsService.GetRecentEvents(100);
            var errorEvents = recentEvents.Where(e => !e.IsSuccess).ToList();
            
            var errorStats = new
            {
                TotalErrors = errorEvents.Count,
                ErrorRate = recentEvents.Count > 0 ? (double)errorEvents.Count / recentEvents.Count * 100 : 0,
                CommonErrors = errorEvents
                    .Where(e => !string.IsNullOrEmpty(e.ErrorMessage))
                    .GroupBy(e => e.ErrorMessage)
                    .Select(g => new { Error = g.Key, Count = g.Count() })
                    .OrderByDescending(e => e.Count)
                    .Take(5)
                    .ToList()
            };

            return Ok(errorStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error statistics");
            return StatusCode(500, "Failed to retrieve error statistics");
        }
    }
}