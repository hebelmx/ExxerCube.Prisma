using Microsoft.Extensions.Logging;
using SnakeWorker.Core.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SnakeWorker.Core.Services;

/// <summary>
/// Implementation of metrics collector
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private readonly ConcurrentDictionary<string, double> _counters = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();
    private readonly ConcurrentDictionary<string, List<double>> _histograms = new();
    private readonly ILogger<MetricsCollector> _logger;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void IncrementCounter(string name, double value = 1.0, Dictionary<string, string>? tags = null)
    {
        var key = BuildMetricKey(name, tags);
        _counters.AddOrUpdate(key, value, (k, v) => v + value);
        
        _logger.LogTrace("Counter {MetricName} incremented by {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = BuildMetricKey(name, tags);
        _gauges[key] = value;
        
        _logger.LogTrace("Gauge {MetricName} set to {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = BuildMetricKey(name, tags);
        _histograms.AddOrUpdate(key, new List<double> { value }, (k, list) =>
        {
            lock (list)
            {
                list.Add(value);
                // Keep only last 1000 values to prevent memory bloat
                if (list.Count > 1000)
                {
                    list.RemoveAt(0);
                }
            }
            return list;
        });
        
        _logger.LogTrace("Histogram {MetricName} recorded value {Value}", name, value);
    }

    /// <inheritdoc />
    public IDisposable StartTimer(string name, Dictionary<string, string>? tags = null)
    {
        return new MetricTimer(this, name, tags);
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetMetricsSnapshot()
    {
        var snapshot = new Dictionary<string, object>();

        // Add counters
        foreach (var kvp in _counters)
        {
            snapshot[$"counter.{kvp.Key}"] = kvp.Value;
        }

        // Add gauges
        foreach (var kvp in _gauges)
        {
            snapshot[$"gauge.{kvp.Key}"] = kvp.Value;
        }

        // Add histogram statistics
        foreach (var kvp in _histograms)
        {
            var values = kvp.Value.ToList(); // Copy to avoid concurrent modification
            if (values.Count > 0)
            {
                snapshot[$"histogram.{kvp.Key}.count"] = values.Count;
                snapshot[$"histogram.{kvp.Key}.min"] = values.Min();
                snapshot[$"histogram.{kvp.Key}.max"] = values.Max();
                snapshot[$"histogram.{kvp.Key}.avg"] = values.Average();
                
                // Calculate percentiles
                var sorted = values.OrderBy(v => v).ToList();
                snapshot[$"histogram.{kvp.Key}.p50"] = GetPercentile(sorted, 0.5);
                snapshot[$"histogram.{kvp.Key}.p90"] = GetPercentile(sorted, 0.9);
                snapshot[$"histogram.{kvp.Key}.p95"] = GetPercentile(sorted, 0.95);
                snapshot[$"histogram.{kvp.Key}.p99"] = GetPercentile(sorted, 0.99);
            }
        }

        return snapshot;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _logger.LogInformation("Resetting all metrics");
        
        _counters.Clear();
        _gauges.Clear();
        _histograms.Clear();
    }

    private static string BuildMetricKey(string name, Dictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return name;
        }

        var tagString = string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{name}[{tagString}]";
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        
        var index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
        
        return sortedValues[index];
    }

    private class MetricTimer : IDisposable
    {
        private readonly MetricsCollector _collector;
        private readonly string _name;
        private readonly Dictionary<string, string>? _tags;
        private readonly Stopwatch _stopwatch;

        public MetricTimer(MetricsCollector collector, string name, Dictionary<string, string>? tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _collector.RecordHistogram(_name, _stopwatch.ElapsedMilliseconds, _tags);
        }
    }
}