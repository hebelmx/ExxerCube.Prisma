namespace SnakeWorker.Core.Models;

/// <summary>
/// Represents a task to be processed by the background worker
/// </summary>
public class WorkerTask
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of task to execute
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Input data for the task (JSON serialized)
    /// </summary>
    public string InputData { get; set; } = string.Empty;

    /// <summary>
    /// Python module name to execute
    /// </summary>
    public string PythonModule { get; set; } = string.Empty;

    /// <summary>
    /// Python function name to call
    /// </summary>
    public string PythonFunction { get; set; } = string.Empty;

    /// <summary>
    /// Task priority (higher number = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Current retry attempt
    /// </summary>
    public int CurrentRetry { get; set; } = 0;

    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the task should be executed (for delayed tasks)
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Task timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Additional metadata for the task
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}