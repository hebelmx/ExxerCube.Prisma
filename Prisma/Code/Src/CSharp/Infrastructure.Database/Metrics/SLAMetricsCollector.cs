using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database.Metrics;

/// <summary>
/// Metrics collector for SLA tracking and escalation operations.
/// Collects metrics for monitoring SLA service performance and health.
/// </summary>
public class SLAMetricsCollector
{
    private readonly Meter _meter;
    private readonly ILogger<SLAMetricsCollector> _logger;
    
    // Counters
    private readonly Counter<long> _slaCalculationCounter;
    private readonly Counter<long> _slaUpdateCounter;
    private readonly Counter<long> _escalationCounter;
    private readonly Counter<long> _errorCounter;
    
    // Histograms for duration measurements
    private readonly Histogram<double> _calculationDurationHistogram;
    private readonly Histogram<double> _updateDurationHistogram;
    private readonly Histogram<double> _queryDurationHistogram;
    
    // Gauges for current state
    private readonly UpDownCounter<long> _atRiskCasesCounter;
    private readonly UpDownCounter<long> _breachedCasesCounter;
    private readonly UpDownCounter<long> _activeCasesCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAMetricsCollector"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SLAMetricsCollector(ILogger<SLAMetricsCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _meter = new Meter("ExxerCube.Prisma.SLA", "1.0.0");
        
        // Initialize counters
        _slaCalculationCounter = _meter.CreateCounter<long>(
            "sla_calculation_total",
            "count",
            "Total number of SLA calculations performed");
        
        _slaUpdateCounter = _meter.CreateCounter<long>(
            "sla_update_total",
            "count",
            "Total number of SLA status updates");
        
        _escalationCounter = _meter.CreateCounter<long>(
            "sla_escalation_total",
            "count",
            "Total number of SLA escalations");
        
        _errorCounter = _meter.CreateCounter<long>(
            "sla_errors_total",
            "count",
            "Total number of SLA operation errors");
        
        // Initialize histograms for duration measurements
        _calculationDurationHistogram = _meter.CreateHistogram<double>(
            "sla_calculation_duration_ms",
            "milliseconds",
            "Duration of SLA calculation operations in milliseconds");
        
        _updateDurationHistogram = _meter.CreateHistogram<double>(
            "sla_update_duration_ms",
            "milliseconds",
            "Duration of SLA update operations in milliseconds");
        
        _queryDurationHistogram = _meter.CreateHistogram<double>(
            "sla_query_duration_ms",
            "milliseconds",
            "Duration of SLA query operations in milliseconds");
        
        // Initialize gauges for current state
        _atRiskCasesCounter = _meter.CreateUpDownCounter<long>(
            "sla_at_risk_cases",
            "count",
            "Current number of SLA cases at risk");
        
        _breachedCasesCounter = _meter.CreateUpDownCounter<long>(
            "sla_breached_cases",
            "count",
            "Current number of SLA cases that have breached their deadline");
        
        _activeCasesCounter = _meter.CreateUpDownCounter<long>(
            "sla_active_cases",
            "count",
            "Current number of active SLA cases");
        
        _logger.LogInformation("SLA Metrics Collector initialized");
    }

    /// <summary>
    /// Records an SLA calculation operation.
    /// </summary>
    /// <param name="duration">The duration of the calculation in milliseconds.</param>
    /// <param name="success">Whether the calculation was successful.</param>
    public void RecordCalculation(double duration, bool success = true)
    {
        _slaCalculationCounter.Add(1, new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
        _calculationDurationHistogram.Record(duration);
        
        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("operation", "calculation"));
        }
        
        _logger.LogDebug("SLA calculation recorded: Duration={Duration}ms, Success={Success}", duration, success);
    }

    /// <summary>
    /// Records an SLA update operation.
    /// </summary>
    /// <param name="duration">The duration of the update in milliseconds.</param>
    /// <param name="success">Whether the update was successful.</param>
    public void RecordUpdate(double duration, bool success = true)
    {
        _slaUpdateCounter.Add(1, new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
        _updateDurationHistogram.Record(duration);
        
        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("operation", "update"));
        }
        
        _logger.LogDebug("SLA update recorded: Duration={Duration}ms, Success={Success}", duration, success);
    }

    /// <summary>
    /// Records an SLA query operation.
    /// </summary>
    /// <param name="duration">The duration of the query in milliseconds.</param>
    /// <param name="queryType">The type of query (e.g., "active", "at_risk", "breached").</param>
    /// <param name="success">Whether the query was successful.</param>
    public void RecordQuery(double duration, string queryType, bool success = true)
    {
        _queryDurationHistogram.Record(duration, new KeyValuePair<string, object?>("query_type", queryType));
        
        if (!success)
        {
            _errorCounter.Add(1, 
                new KeyValuePair<string, object?>("operation", "query"),
                new KeyValuePair<string, object?>("query_type", queryType));
        }
        
        _logger.LogDebug("SLA query recorded: Type={QueryType}, Duration={Duration}ms, Success={Success}", 
            queryType, duration, success);
    }

    /// <summary>
    /// Records an escalation event.
    /// </summary>
    /// <param name="escalationLevel">The escalation level.</param>
    public void RecordEscalation(EscalationLevel escalationLevel)
    {
        _escalationCounter.Add(1, new KeyValuePair<string, object?>("level", escalationLevel.ToString()));
        _logger.LogInformation("SLA escalation recorded: Level={Level}", escalationLevel);
    }

    /// <summary>
    /// Records an error occurrence.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="errorType">The type of error.</param>
    public void RecordError(string operation, string errorType)
    {
        _errorCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
        
        _logger.LogWarning("SLA error recorded: Operation={Operation}, ErrorType={ErrorType}", operation, errorType);
    }

    /// <summary>
    /// Updates the current count of at-risk cases.
    /// </summary>
    /// <param name="count">The current count of at-risk cases.</param>
    public void UpdateAtRiskCases(long count)
    {
        var delta = count - GetCurrentGaugeValue("at_risk");
        if (delta != 0)
        {
            _atRiskCasesCounter.Add(delta);
            _logger.LogDebug("At-risk cases updated: Count={Count}", count);
        }
    }

    /// <summary>
    /// Updates the current count of breached cases.
    /// </summary>
    /// <param name="count">The current count of breached cases.</param>
    public void UpdateBreachedCases(long count)
    {
        var delta = count - GetCurrentGaugeValue("breached");
        if (delta != 0)
        {
            _breachedCasesCounter.Add(delta);
            _logger.LogDebug("Breached cases updated: Count={Count}", count);
        }
    }

    /// <summary>
    /// Updates the current count of active cases.
    /// </summary>
    /// <param name="count">The current count of active cases.</param>
    public void UpdateActiveCases(long count)
    {
        var delta = count - GetCurrentGaugeValue("active");
        if (delta != 0)
        {
            _activeCasesCounter.Add(delta);
            _logger.LogDebug("Active cases updated: Count={Count}", count);
        }
    }

    /// <summary>
    /// Creates a timer for measuring operation duration.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <returns>A disposable timer that records duration when disposed.</returns>
    public IDisposable StartTimer(string operationName)
    {
        return new OperationTimer(this, operationName, Stopwatch.StartNew());
    }

    /// <summary>
    /// Gets the current metrics snapshot for monitoring.
    /// </summary>
    /// <returns>A dictionary containing current metric values.</returns>
    public Dictionary<string, object> GetMetricsSnapshot()
    {
        return new Dictionary<string, object>
        {
            ["at_risk_cases"] = GetCurrentGaugeValue("at_risk"),
            ["breached_cases"] = GetCurrentGaugeValue("breached"),
            ["active_cases"] = GetCurrentGaugeValue("active")
        };
    }

    /// <summary>
    /// Gets the current value of a gauge metric (simplified - in production, use proper metric reading).
    /// </summary>
    private long GetCurrentGaugeValue(string gaugeName)
    {
        // Note: In a real implementation, you would read the actual current value from the metric
        // For now, we'll track it internally. In production, use OpenTelemetry's metric reading APIs.
        return 0; // Placeholder - actual implementation would read from metric system
    }

    /// <summary>
    /// Timer helper class for measuring operation duration.
    /// </summary>
    private class OperationTimer : IDisposable
    {
        private readonly SLAMetricsCollector _collector;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public OperationTimer(SLAMetricsCollector collector, string operationName, Stopwatch stopwatch)
        {
            _collector = collector;
            _operationName = operationName;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _stopwatch.Stop();
            var duration = _stopwatch.Elapsed.TotalMilliseconds;

            switch (_operationName.ToLowerInvariant())
            {
                case "calculation":
                    _collector.RecordCalculation(duration, true);
                    break;
                case "update":
                    _collector.RecordUpdate(duration, true);
                    break;
                default:
                    _collector._logger.LogDebug("Operation {Operation} completed in {Duration}ms", 
                        _operationName, duration);
                    break;
            }

            _disposed = true;
        }
    }
}

