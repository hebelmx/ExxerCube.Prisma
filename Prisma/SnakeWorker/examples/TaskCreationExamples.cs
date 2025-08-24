using SnakeWorker.Core.Interfaces;
using SnakeWorker.Core.Models;

namespace SnakeWorker.Examples;

/// <summary>
/// Examples of how to create and enqueue different types of tasks
/// </summary>
public static class TaskCreationExamples
{
    /// <summary>
    /// Create a simple math operation task
    /// </summary>
    public static async Task CreateMathTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "MathOperation",
            PythonModule = "math_operations",
            PythonFunction = "add_numbers",
            InputData = new Dictionary<string, object>
            {
                ["a"] = 15.5,
                ["b"] = 24.3
            },
            Priority = 5
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a text processing task
    /// </summary>
    public static async Task CreateTextProcessingTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "TextProcessing",
            PythonModule = "text_processor",
            PythonFunction = "analyze_text",
            InputData = new Dictionary<string, object>
            {
                ["text"] = "This is a sample text for analysis",
                ["options"] = new Dictionary<string, object>
                {
                    ["count_words"] = true,
                    ["count_chars"] = true,
                    ["extract_keywords"] = false
                }
            },
            Priority = 3,
            TimeoutSeconds = 60
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a data analysis task using sample dataset
    /// </summary>
    public static async Task CreateDataAnalysisTask(ITaskQueue taskQueue)
    {
        var sampleDataset = new[]
        {
            new { id = 1, name = "Alice", age = 25, salary = 50000, department = "Engineering" },
            new { id = 2, name = "Bob", age = 30, salary = 60000, department = "Marketing" },
            new { id = 3, name = "Charlie", age = 35, salary = 70000, department = "Engineering" },
            new { id = 4, name = "Diana", age = 28, salary = 55000, department = "Sales" }
        };

        var task = new WorkerTask
        {
            TaskType = "DataAnalysis",
            PythonModule = "advanced_operations",
            PythonFunction = "data_analysis",
            InputData = new Dictionary<string, object>
            {
                ["dataset"] = sampleDataset
            },
            Priority = 7,
            TimeoutSeconds = 120,
            MaxRetries = 2
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create an image processing simulation task
    /// </summary>
    public static async Task CreateImageProcessingTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "ImageProcessing",
            PythonModule = "advanced_operations",
            PythonFunction = "simulate_image_processing",
            InputData = new Dictionary<string, object>
            {
                ["width"] = 1920,
                ["height"] = 1080,
                ["operations"] = new[] { "resize", "blur", "contrast", "brightness", "sharpen" }
            },
            Priority = 4,
            TimeoutSeconds = 180
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a cryptography operations task
    /// </summary>
    public static async Task CreateCryptographyTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "Cryptography",
            PythonModule = "advanced_operations",
            PythonFunction = "cryptography_operations",
            InputData = new Dictionary<string, object>
            {
                ["data"] = "Sensitive data to be hashed",
                ["operations"] = new[] { "md5", "sha256", "sha512", "base64_encode" }
            },
            Priority = 8,
            TimeoutSeconds = 30
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create an algorithm benchmark task
    /// </summary>
    public static async Task CreateAlgorithmBenchmarkTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "AlgorithmBenchmark",
            PythonModule = "advanced_operations",
            PythonFunction = "algorithm_benchmark",
            InputData = new Dictionary<string, object>
            {
                ["algorithm"] = "sorting",
                ["size"] = 1000
            },
            Priority = 2,
            TimeoutSeconds = 300,
            MaxRetries = 1
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a development testing task for error scenarios
    /// </summary>
    public static async Task CreateErrorTestingTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "ErrorTesting",
            PythonModule = "development_test",
            PythonFunction = "simulate_error",
            InputData = new Dictionary<string, object>
            {
                ["error_type"] = "value_error"
            },
            Priority = 1,
            TimeoutSeconds = 15,
            MaxRetries = 0  // Don't retry error simulation tasks
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a scheduled task that runs in the future
    /// </summary>
    public static async Task CreateScheduledTask(ITaskQueue taskQueue, TimeSpan delay)
    {
        var task = new WorkerTask
        {
            TaskType = "ScheduledTask",
            PythonModule = "development_test",
            PythonFunction = "simple_hello",
            InputData = new Dictionary<string, object>
            {
                ["name"] = "Scheduled Task"
            },
            Priority = 6,
            ScheduledAt = DateTime.UtcNow.Add(delay)
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create multiple tasks with different priorities for testing queue ordering
    /// </summary>
    public static async Task CreatePriorityTestTasks(ITaskQueue taskQueue)
    {
        var tasks = new[]
        {
            new { Name = "Low Priority", Priority = 1 },
            new { Name = "High Priority", Priority = 10 },
            new { Name = "Medium Priority", Priority = 5 },
            new { Name = "Urgent", Priority = 15 },
            new { Name = "Normal", Priority = 3 }
        };

        foreach (var taskInfo in tasks)
        {
            var task = new WorkerTask
            {
                TaskType = "PriorityTest",
                PythonModule = "development_test",
                PythonFunction = "simple_hello",
                InputData = new Dictionary<string, object>
                {
                    ["name"] = taskInfo.Name
                },
                Priority = taskInfo.Priority
            };

            await taskQueue.EnqueueAsync(task);
        }
    }

    /// <summary>
    /// Create a long-running task for timeout testing
    /// </summary>
    public static async Task CreateLongRunningTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "LongRunning",
            PythonModule = "development_test",
            PythonFunction = "simulate_work",
            InputData = new Dictionary<string, object>
            {
                ["duration_seconds"] = 30.0,  // 30 second task
                ["memory_mb"] = 10
            },
            Priority = 3,
            TimeoutSeconds = 45,  // Allow 45 seconds
            MaxRetries = 1
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a batch of similar tasks for load testing
    /// </summary>
    public static async Task CreateBatchTasks(ITaskQueue taskQueue, int count = 10)
    {
        var tasks = Enumerable.Range(1, count).Select(i => new WorkerTask
        {
            TaskType = "BatchTask",
            PythonModule = "math_operations",
            PythonFunction = "multiply_numbers",
            InputData = new Dictionary<string, object>
            {
                ["a"] = i,
                ["b"] = i * 2
            },
            Priority = Random.Shared.Next(1, 6)
        });

        foreach (var task in tasks)
        {
            await taskQueue.EnqueueAsync(task);
        }
    }

    /// <summary>
    /// Create a file operations task
    /// </summary>
    public static async Task CreateFileOperationsTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "FileOperations",
            PythonModule = "development_test",
            PythonFunction = "file_operations",
            InputData = new Dictionary<string, object>
            {
                ["temp_dir"] = $"test_run_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
            },
            Priority = 4,
            TimeoutSeconds = 60
        };

        await taskQueue.EnqueueAsync(task);
    }

    /// <summary>
    /// Create a system information gathering task
    /// </summary>
    public static async Task CreateSystemInfoTask(ITaskQueue taskQueue)
    {
        var task = new WorkerTask
        {
            TaskType = "SystemInfo",
            PythonModule = "development_test",
            PythonFunction = "system_info",
            InputData = new Dictionary<string, object>(),
            Priority = 2,
            TimeoutSeconds = 30
        };

        await taskQueue.EnqueueAsync(task);
    }
}