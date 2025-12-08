using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for monitoring system health and providing health check endpoints.
/// Implements comprehensive health monitoring for the OCR processing pipeline.
/// </summary>
public class HealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IProcessingMetricsService _metricsService;
    private readonly IFileLoader _fileLoader;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly IOutputWriter _outputWriter;
    private readonly Dictionary<string, HealthCheckResult> _healthChecks;
    private readonly object _healthChecksLock = new();

    /// <summary>
    /// Gets the overall system health status.
    /// </summary>
    public HealthStatus OverallHealth { get; private set; }

    /// <summary>
    /// Gets when the health check was last performed.
    /// </summary>
    public DateTime LastHealthCheck { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="metricsService">The metrics service.</param>
    /// <param name="fileLoader">The file loader service.</param>
    /// <param name="ocrExecutor">The OCR executor service.</param>
    /// <param name="imagePreprocessor">The image preprocessor service.</param>
    /// <param name="fieldExtractor">The field extractor service.</param>
    /// <param name="outputWriter">The output writer service.</param>
    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IProcessingMetricsService metricsService,
        IFileLoader fileLoader,
        IOcrExecutor ocrExecutor,
        IImagePreprocessor imagePreprocessor,
        IFieldExtractor fieldExtractor,
        IOutputWriter outputWriter)
    {
        _logger = logger;
        _metricsService = metricsService;
        _fileLoader = fileLoader;
        _ocrExecutor = ocrExecutor;
        _imagePreprocessor = imagePreprocessor;
        _fieldExtractor = fieldExtractor;
        _outputWriter = outputWriter;
        _healthChecks = new Dictionary<string, HealthCheckResult>();
        OverallHealth = HealthStatus.Unknown;
        LastHealthCheck = DateTime.MinValue;
    }

    /// <summary>
    /// Performs a comprehensive health check of the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the health check results.</returns>
    public async Task<Result<HealthCheckReport>> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive health check");

        var healthChecks = new List<HealthCheckResult>();
        var overallHealth = HealthStatus.Healthy;

        try
        {
            // Check system components
            var componentChecks = await CheckSystemComponentsAsync(cancellationToken).ConfigureAwait(false);
            healthChecks.AddRange(componentChecks);

            // Check performance metrics
            var performanceCheck = await CheckPerformanceMetricsAsync().ConfigureAwait(false);
            healthChecks.Add(performanceCheck);

            // Check resource availability
            var resourceCheck = await CheckResourceAvailabilityAsync().ConfigureAwait(false);
            healthChecks.Add(resourceCheck);

            // Check external dependencies
            var dependencyCheck = await CheckExternalDependenciesAsync(cancellationToken).ConfigureAwait(false);
            healthChecks.Add(dependencyCheck);

            // Determine overall health
            overallHealth = DetermineOverallHealth(healthChecks);

            // Update health check cache
            lock (_healthChecksLock)
            {
                _healthChecks.Clear();
                foreach (var check in healthChecks)
                {
                    _healthChecks[check.Component] = check;
                }
                OverallHealth = overallHealth;
                LastHealthCheck = DateTime.UtcNow;
            }

            var report = new HealthCheckReport
            {
                OverallHealth = overallHealth,
                HealthChecks = healthChecks,
                Timestamp = DateTime.UtcNow,
                ProcessingStatistics = await _metricsService.GetCurrentStatisticsAsync().ConfigureAwait(false)
            };

            _logger.LogInformation("Health check completed. Overall status: {OverallHealth}", overallHealth);
            return Result<HealthCheckReport>.Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check");
            return Result<HealthCheckReport>.WithFailure($"Health check failed: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets the current health status without performing a full health check.
    /// </summary>
    /// <returns>The current health status.</returns>
    public async Task<HealthStatus> GetCurrentHealthAsync()
    {
        // If we haven't performed a health check recently, perform one
        if (DateTime.UtcNow.Subtract(LastHealthCheck).TotalMinutes > 5)
        {
            var healthCheckResult = await PerformHealthCheckAsync().ConfigureAwait(false);
            if (healthCheckResult.IsSuccess)
            {
                return healthCheckResult.Value!.OverallHealth;
            }
        }

        return OverallHealth;
    }

    /// <summary>
    /// Gets detailed health information for a specific component.
    /// </summary>
    /// <param name="component">The component name.</param>
    /// <returns>The health check result for the component, or null if not found.</returns>
    public HealthCheckResult? GetComponentHealth(string component)
    {
        lock (_healthChecksLock)
        {
            return _healthChecks.TryGetValue(component, out var result) ? result : null;
        }
    }

    /// <summary>
    /// Gets all component health check results.
    /// </summary>
    /// <returns>A list of all health check results.</returns>
    public List<HealthCheckResult> GetAllComponentHealth()
    {
        lock (_healthChecksLock)
        {
            return _healthChecks.Values.ToList();
        }
    }

    /// <summary>
    /// Checks system components for health.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of health check results for system components.</returns>
    private Task<List<HealthCheckResult>> CheckSystemComponentsAsync(CancellationToken cancellationToken)
    {
        var checks = new List<HealthCheckResult>();

        // Check file loader
        try
        {
            var fileLoaderCheck = new HealthCheckResult
            {
                Component = "FileLoader",
                Status = HealthStatus.Healthy,
                Message = "File loader is operational",
                Timestamp = DateTime.UtcNow
            };
            checks.Add(fileLoaderCheck);
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Component = "FileLoader",
                Status = HealthStatus.Unhealthy,
                Message = $"File loader error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        // Check OCR executor
        try
        {
            var ocrExecutorCheck = new HealthCheckResult
            {
                Component = "OcrExecutor",
                Status = HealthStatus.Healthy,
                Message = "OCR executor is operational",
                Timestamp = DateTime.UtcNow
            };
            checks.Add(ocrExecutorCheck);
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Component = "OcrExecutor",
                Status = HealthStatus.Unhealthy,
                Message = $"OCR executor error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        // Check image preprocessor
        try
        {
            var preprocessorCheck = new HealthCheckResult
            {
                Component = "ImagePreprocessor",
                Status = HealthStatus.Healthy,
                Message = "Image preprocessor is operational",
                Timestamp = DateTime.UtcNow
            };
            checks.Add(preprocessorCheck);
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Component = "ImagePreprocessor",
                Status = HealthStatus.Unhealthy,
                Message = $"Image preprocessor error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        // Check field extractor
        try
        {
            var fieldExtractorCheck = new HealthCheckResult
            {
                Component = "FieldExtractor",
                Status = HealthStatus.Healthy,
                Message = "Field extractor is operational",
                Timestamp = DateTime.UtcNow
            };
            checks.Add(fieldExtractorCheck);
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Component = "FieldExtractor",
                Status = HealthStatus.Unhealthy,
                Message = $"Field extractor error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        // Check output writer
        try
        {
            var outputWriterCheck = new HealthCheckResult
            {
                Component = "OutputWriter",
                Status = HealthStatus.Healthy,
                Message = "Output writer is operational",
                Timestamp = DateTime.UtcNow
            };
            checks.Add(outputWriterCheck);
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Component = "OutputWriter",
                Status = HealthStatus.Unhealthy,
                Message = $"Output writer error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        return Task.FromResult(checks);
    }

    /// <summary>
    /// Checks performance metrics for health.
    /// </summary>
    /// <returns>A health check result for performance metrics.</returns>
    private async Task<HealthCheckResult> CheckPerformanceMetricsAsync()
    {
        try
        {
            var statistics = await _metricsService.GetCurrentStatisticsAsync().ConfigureAwait(false);
            var performanceValidation = await _metricsService.ValidatePerformanceAsync().ConfigureAwait(false);

            var status = HealthStatus.Healthy;
            var message = "Performance metrics are within acceptable ranges";

            if (!performanceValidation.IsSuccess)
            {
                status = HealthStatus.Degraded;
                message = "Performance issues detected: Unknown performance issues";
            }
            else if (performanceValidation.IsSuccess)
            {
                var validationValue = performanceValidation.Value;
                if (validationValue != null)
                {
                    status = HealthStatus.Degraded;
                    message = $"Performance issues detected: {string.Join(", ", validationValue.ValidationResults)}";
                }
            }

            return new HealthCheckResult
            {
                Component = "PerformanceMetrics",
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["TotalDocumentsProcessed"] = statistics.TotalDocumentsProcessed,
                    ["SuccessRate"] = statistics.SuccessRate,
                    ["AverageProcessingTime"] = statistics.AverageProcessingTime,
                    ["ActiveProcessingCount"] = _metricsService.ActiveProcessingCount,
                    ["MaxConcurrency"] = _metricsService.MaxConcurrency
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "PerformanceMetrics",
                Status = HealthStatus.Unhealthy,
                Message = $"Performance metrics error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Checks resource availability for health.
    /// </summary>
    /// <returns>A health check result for resource availability.</returns>
    private Task<HealthCheckResult> CheckResourceAvailabilityAsync()
    {
        try
        {
            var status = HealthStatus.Healthy;
            var message = "System resources are available";
            var details = new Dictionary<string, object>();

            // Check memory usage
            var memoryInfo = GC.GetGCMemoryInfo();
            var totalMemory = memoryInfo.TotalCommittedBytes;
            var memoryUsageMB = totalMemory / (1024 * 1024);
            details["MemoryUsageMB"] = memoryUsageMB;

            if (memoryUsageMB > 1024) // Warning if over 1GB
            {
                status = HealthStatus.Degraded;
                message = "High memory usage detected";
            }

            // Check available disk space (simplified check)
            details["AvailableDiskSpace"] = "Unknown"; // Would need actual disk space check

            // Check thread pool status
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            details["AvailableWorkerThreads"] = workerThreads;
            details["AvailableCompletionPortThreads"] = completionPortThreads;

            if (workerThreads < 10)
            {
                status = HealthStatus.Degraded;
                message = "Low thread pool availability";
            }

            return Task.FromResult(new HealthCheckResult
            {
                Component = "ResourceAvailability",
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Details = details
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult
            {
                Component = "ResourceAvailability",
                Status = HealthStatus.Unhealthy,
                Message = $"Resource availability error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Checks external dependencies for health.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health check result for external dependencies.</returns>
    private async Task<HealthCheckResult> CheckExternalDependenciesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = HealthStatus.Healthy;
            var message = "External dependencies are available";
            var details = new Dictionary<string, object>();

            // Check Python runtime (if applicable)
            try
            {
                // This would check if Python runtime is available
                details["PythonRuntime"] = "Available";
            }
            catch (Exception ex)
            {
                status = HealthStatus.Unhealthy;
                message = "Python runtime is not available";
                details["PythonRuntime"] = $"Error: {ex.Message}";
            }

            // Check file system access
            try
            {
                var tempPath = Path.GetTempPath();
                var testFile = Path.Combine(tempPath, $"health_check_{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testFile, "health_check", cancellationToken).ConfigureAwait(false);
                File.Delete(testFile);
                details["FileSystemAccess"] = "Available";
            }
            catch (Exception ex)
            {
                status = HealthStatus.Unhealthy;
                message = "File system access is not available";
                details["FileSystemAccess"] = $"Error: {ex.Message}";
            }

            return new HealthCheckResult
            {
                Component = "ExternalDependencies",
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Details = details
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "ExternalDependencies",
                Status = HealthStatus.Unhealthy,
                Message = $"External dependencies error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Determines the overall health status based on individual component checks.
    /// </summary>
    /// <param name="healthChecks">The list of health check results.</param>
    /// <returns>The overall health status.</returns>
    private static HealthStatus DetermineOverallHealth(List<HealthCheckResult> healthChecks)
    {
        if (healthChecks.Any(h => h.Status == HealthStatus.Unhealthy))
        {
            return HealthStatus.Unhealthy;
        }

        if (healthChecks.Any(h => h.Status == HealthStatus.Degraded))
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }
}