namespace SnakeWorker.Core.Models;

/// <summary>
/// Represents the result of a task execution
/// </summary>
public class TaskResult
{
    /// <summary>
    /// Task ID that this result belongs to
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the task was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Output data from the task (JSON serialized)
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Error message if task failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed error information
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// When the task started execution
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the task completed execution
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Task execution duration
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Memory usage during execution (bytes)
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// CPU time used (milliseconds)
    /// </summary>
    public long CpuTimeMs { get; set; }

    /// <summary>
    /// Additional execution metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}