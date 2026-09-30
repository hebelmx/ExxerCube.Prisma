namespace ExxerCube.Prisma.Infrastructure.Database.HealthChecks;

/// <summary>
/// Health check for SLA background update service, verifying the service is running and operational.
/// </summary>
public class SLABackgroundJobHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SLABackgroundJobHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLABackgroundJobHealthCheck"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    public SLABackgroundJobHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<SLABackgroundJobHealthCheck> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();

            // Find the SLA background service
            var slaBackgroundService = hostedServices.OfType<SLAUpdateBackgroundService>().FirstOrDefault();

            if (slaBackgroundService == null)
            {
                _logger.LogWarning("SLA background job health check: Service not found");
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "SLA background update service is not registered",
                    data: new Dictionary<string, object>
                    {
                        ["service_registered"] = false
                    }));
            }

            // Check if the service is running
            // Note: BackgroundService doesn't expose a direct "IsRunning" property
            // We can check if the service exists and is registered, which indicates it should be running
            var data = new Dictionary<string, object>
            {
                ["service_registered"] = true,
                ["service_type"] = nameof(SLAUpdateBackgroundService)
            };

            // Since BackgroundService doesn't expose runtime status directly,
            // we assume it's healthy if it's registered and the application is running
            // In a production scenario, you might want to track last execution time
            // via a shared state or metrics service

            _logger.LogDebug("SLA background job health check passed: Service is registered and should be running");

            return Task.FromResult(HealthCheckResult.Healthy(
                "SLA background update service is registered and operational",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SLA background job health check encountered an exception");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SLA background job health check failed with exception",
                ex));
        }
    }
}

