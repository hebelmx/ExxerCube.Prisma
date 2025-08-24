using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SnakeWorker.Core.Interfaces;
using System.Text.Json;

namespace SnakeWorker.Host.Services;

/// <summary>
/// Service that periodically reports metrics
/// </summary>
public class MetricsReportingService : BackgroundService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<MetricsReportingService> _logger;
    private readonly MetricsReportingOptions _options;

    public MetricsReportingService(
        IMetricsCollector metricsCollector,
        ILogger<MetricsReportingService> logger,
        IOptions<MetricsReportingOptions> options)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics reporting service started (interval: {IntervalSeconds}s)", 
            _options.ReportingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportMetricsAsync();
                await Task.Delay(TimeSpan.FromSeconds(_options.ReportingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while reporting metrics");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Metrics reporting service stopped");
    }

    private async Task ReportMetricsAsync()
    {
        try
        {
            var metrics = _metricsCollector.GetMetricsSnapshot();
            
            if (metrics.Count == 0)
            {
                _logger.LogDebug("No metrics to report");
                return;
            }

            // Log metrics summary
            _logger.LogInformation("=== Metrics Report ===");
            
            // Group metrics by type
            var counters = metrics.Where(m => m.Key.StartsWith("counter.")).ToDictionary(m => m.Key, m => m.Value);
            var gauges = metrics.Where(m => m.Key.StartsWith("gauge.")).ToDictionary(m => m.Key, m => m.Value);
            var histograms = metrics.Where(m => m.Key.StartsWith("histogram.")).GroupBy(m => m.Key.Split('.')[1]).ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Key, m => m.Value));

            // Report counters
            if (counters.Count > 0)
            {
                _logger.LogInformation("Counters:");
                foreach (var counter in counters)
                {
                    _logger.LogInformation("  {Name}: {Value}", counter.Key.Substring(8), counter.Value);
                }
            }

            // Report gauges
            if (gauges.Count > 0)
            {
                _logger.LogInformation("Gauges:");
                foreach (var gauge in gauges)
                {
                    _logger.LogInformation("  {Name}: {Value}", gauge.Key.Substring(6), gauge.Value);
                }
            }

            // Report histogram summaries
            if (histograms.Count > 0)
            {
                _logger.LogInformation("Histograms:");
                foreach (var histogram in histograms)
                {
                    var stats = histogram.Value;
                    var count = stats.GetValueOrDefault($"histogram.{histogram.Key}.count", 0);
                    var avg = stats.GetValueOrDefault($"histogram.{histogram.Key}.avg", 0);
                    var p95 = stats.GetValueOrDefault($"histogram.{histogram.Key}.p95", 0);
                    
                    _logger.LogInformation("  {Name}: count={Count}, avg={Avg:F2}, p95={P95:F2}", 
                        histogram.Key, count, avg, p95);
                }
            }

            // Save to file if configured
            if (!string.IsNullOrEmpty(_options.OutputFilePath))
            {
                await SaveMetricsToFileAsync(metrics);
            }

            // Send to external system if configured
            if (!string.IsNullOrEmpty(_options.ExternalEndpoint))
            {
                await SendMetricsToExternalSystemAsync(metrics);
            }

            _logger.LogInformation("=== End Metrics Report ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report metrics");
        }
    }

    private async Task SaveMetricsToFileAsync(Dictionary<string, object> metrics)
    {
        try
        {
            var report = new
            {
                timestamp = DateTime.UtcNow,
                hostname = Environment.MachineName,
                process_id = Environment.ProcessId,
                metrics = metrics
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            var directory = Path.GetDirectoryName(_options.OutputFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_options.OutputFilePath, json);
            _logger.LogDebug("Metrics saved to {FilePath}", _options.OutputFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save metrics to file {FilePath}", _options.OutputFilePath);
        }
    }

    private async Task SendMetricsToExternalSystemAsync(Dictionary<string, object> metrics)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var payload = new
            {
                timestamp = DateTime.UtcNow,
                source = "SnakeWorker",
                hostname = Environment.MachineName,
                metrics = metrics
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_options.ExternalEndpoint, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Metrics sent to external system successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send metrics to external system. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send metrics to external system {Endpoint}", _options.ExternalEndpoint);
        }
    }
}

/// <summary>
/// Configuration options for metrics reporting
/// </summary>
public class MetricsReportingOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "MetricsReporting";

    /// <summary>
    /// How often to report metrics (seconds)
    /// </summary>
    public int ReportingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// File path to save metrics reports (optional)
    /// </summary>
    public string? OutputFilePath { get; set; }

    /// <summary>
    /// External endpoint to send metrics to (optional)
    /// </summary>
    public string? ExternalEndpoint { get; set; }

    /// <summary>
    /// Whether to include detailed histogram data
    /// </summary>
    public bool IncludeHistogramDetails { get; set; } = true;
}