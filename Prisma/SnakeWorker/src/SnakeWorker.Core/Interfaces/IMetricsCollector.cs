namespace SnakeWorker.Core.Interfaces;

/// <summary>
/// Interface for collecting and reporting metrics
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Increment a counter metric
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to add (default: 1)</param>
    /// <param name="tags">Optional tags</param>
    void IncrementCounter(string name, double value = 1.0, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Record a gauge metric (current value)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Current value</param>
    /// <param name="tags">Optional tags</param>
    void RecordGauge(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Record a histogram metric (distribution)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Optional tags</param>
    void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Start a timer for measuring duration
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="tags">Optional tags</param>
    /// <returns>Disposable timer</returns>
    IDisposable StartTimer(string name, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Get current metrics snapshot
    /// </summary>
    /// <returns>Dictionary of metric names to values</returns>
    Dictionary<string, object> GetMetricsSnapshot();

    /// <summary>
    /// Reset all metrics
    /// </summary>
    void Reset();
}