# SnakeWorker Usage Examples

This document provides comprehensive examples of how to use SnakeWorker for testing Python-C# integration scenarios.

## Basic Task Creation

### Simple Math Operations

```csharp
var mathTask = new WorkerTask
{
    TaskType = "MathOperation",
    PythonModule = "math_operations",
    PythonFunction = "add_numbers",
    InputData = new Dictionary<string, object>
    {
        ["a"] = 15.5,
        ["b"] = 24.3
    }
};

await taskQueue.EnqueueAsync(mathTask);
```

### Text Processing

```csharp
var textTask = new WorkerTask
{
    TaskType = "TextProcessing",
    PythonModule = "text_processor",
    PythonFunction = "analyze_text",
    InputData = new Dictionary<string, object>
    {
        ["text"] = "Sample text for analysis",
        ["options"] = new Dictionary<string, object>
        {
            ["count_words"] = true,
            ["extract_keywords"] = false
        }
    },
    TimeoutSeconds = 60
};
```

## Advanced Scenarios

### Data Analysis with Complex Objects

```csharp
var dataset = new[]
{
    new { id = 1, name = "Alice", age = 25, salary = 50000 },
    new { id = 2, name = "Bob", age = 30, salary = 60000 }
};

var analysisTask = new WorkerTask
{
    TaskType = "DataAnalysis",
    PythonModule = "advanced_operations",
    PythonFunction = "data_analysis",
    InputData = new Dictionary<string, object>
    {
        ["dataset"] = dataset
    },
    Priority = 7,
    MaxRetries = 2
};
```

### Image Processing Simulation

```csharp
var imageTask = new WorkerTask
{
    TaskType = "ImageProcessing",
    PythonModule = "advanced_operations",
    PythonFunction = "simulate_image_processing",
    InputData = new Dictionary<string, object>
    {
        ["width"] = 1920,
        ["height"] = 1080,
        ["operations"] = new[] { "resize", "blur", "contrast" }
    },
    TimeoutSeconds = 180
};
```

## Error Handling and Testing

### Simulating Different Error Types

```csharp
// Test ValueError handling
var errorTask = new WorkerTask
{
    TaskType = "ErrorTest",
    PythonModule = "development_test",
    PythonFunction = "simulate_error",
    InputData = new Dictionary<string, object>
    {
        ["error_type"] = "value_error"
    },
    MaxRetries = 0  // Don't retry error tests
};

// Test timeout scenarios
var timeoutTask = new WorkerTask
{
    TaskType = "TimeoutTest",
    PythonModule = "development_test",
    PythonFunction = "simulate_work",
    InputData = new Dictionary<string, object>
    {
        ["duration_seconds"] = 60.0  // 60 seconds
    },
    TimeoutSeconds = 30  // Timeout after 30 seconds
};
```

### Random Success/Failure Testing

```csharp
var randomTask = new WorkerTask
{
    TaskType = "RandomTest",
    PythonModule = "development_test",
    PythonFunction = "random_success_failure",
    InputData = new Dictionary<string, object>
    {
        ["success_rate"] = 0.7  // 70% success rate
    },
    MaxRetries = 3
};
```

## Scheduled and Priority Tasks

### Scheduled Execution

```csharp
var scheduledTask = new WorkerTask
{
    TaskType = "ScheduledTask",
    PythonModule = "development_test",
    PythonFunction = "simple_hello",
    InputData = new Dictionary<string, object>
    {
        ["name"] = "Future Task"
    },
    ScheduledAt = DateTime.UtcNow.AddMinutes(5)  // Run in 5 minutes
};
```

### Priority Queue Testing

```csharp
// High priority task (processed first)
var urgentTask = new WorkerTask
{
    TaskType = "UrgentTask",
    Priority = 10,
    PythonModule = "math_operations",
    PythonFunction = "add_numbers",
    InputData = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }
};

// Low priority task (processed later)
var backgroundTask = new WorkerTask
{
    TaskType = "BackgroundTask", 
    Priority = 1,
    PythonModule = "text_processor",
    PythonFunction = "count_words",
    InputData = new Dictionary<string, object> { ["text"] = "background processing" }
};
```

## Performance and Load Testing

### Memory Usage Testing

```csharp
var memoryTask = new WorkerTask
{
    TaskType = "MemoryTest",
    PythonModule = "development_test", 
    PythonFunction = "simulate_work",
    InputData = new Dictionary<string, object>
    {
        ["duration_seconds"] = 5.0,
        ["memory_mb"] = 50  // Allocate 50MB
    }
};
```

### Concurrent Operations Testing

```csharp
var concurrentTask = new WorkerTask
{
    TaskType = "ConcurrentTest",
    PythonModule = "development_test",
    PythonFunction = "concurrent_operation",
    InputData = new Dictionary<string, object>
    {
        ["thread_count"] = 5,
        ["operation_duration"] = 2.0
    }
};
```

### Algorithm Benchmarking

```csharp
// Sorting algorithm benchmark
var sortingBenchmark = new WorkerTask
{
    TaskType = "SortingBenchmark",
    PythonModule = "advanced_operations",
    PythonFunction = "algorithm_benchmark", 
    InputData = new Dictionary<string, object>
    {
        ["algorithm"] = "sorting",
        ["size"] = 5000
    },
    TimeoutSeconds = 300
};

// Fibonacci calculation benchmark
var fibonacciBenchmark = new WorkerTask
{
    TaskType = "FibonacciBenchmark",
    PythonModule = "advanced_operations",
    PythonFunction = "algorithm_benchmark",
    InputData = new Dictionary<string, object>
    {
        ["algorithm"] = "fibonacci", 
        ["size"] = 30  // Calculate 30th Fibonacci number
    }
};
```

## System Integration Testing

### File Operations

```csharp
var fileTask = new WorkerTask
{
    TaskType = "FileOperations",
    PythonModule = "development_test",
    PythonFunction = "file_operations",
    InputData = new Dictionary<string, object>
    {
        ["temp_dir"] = $"test_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
    },
    TimeoutSeconds = 60
};
```

### System Information Gathering

```csharp
var systemTask = new WorkerTask
{
    TaskType = "SystemInfo", 
    PythonModule = "development_test",
    PythonFunction = "system_info",
    InputData = new Dictionary<string, object>(),
    Priority = 2
};
```

### Network Simulation

```csharp
var networkTask = new WorkerTask
{
    TaskType = "NetworkSimulation",
    PythonModule = "advanced_operations", 
    PythonFunction = "network_simulation",
    InputData = new Dictionary<string, object>
    {
        ["endpoints"] = new[]
        {
            "https://api.example.com/users",
            "https://api.example.com/posts"
        },
        ["simulate_latency"] = true
    }
};
```

## Cryptography Operations

### Hash Operations

```csharp
var hashTask = new WorkerTask
{
    TaskType = "HashOperations",
    PythonModule = "advanced_operations",
    PythonFunction = "cryptography_operations", 
    InputData = new Dictionary<string, object>
    {
        ["data"] = "Sensitive data to hash",
        ["operations"] = new[] { "md5", "sha256", "sha512" }
    }
};
```

### Encoding Operations

```csharp
var encodingTask = new WorkerTask
{
    TaskType = "EncodingOperations", 
    PythonModule = "advanced_operations",
    PythonFunction = "cryptography_operations",
    InputData = new Dictionary<string, object>
    {
        ["data"] = "Data to encode",
        ["operations"] = new[] { "base64_encode", "url_encode" }
    }
};
```

## Batch Processing Examples

### Creating Multiple Tasks

```csharp
// Create 100 similar tasks for load testing
var batchTasks = Enumerable.Range(1, 100).Select(i => new WorkerTask
{
    TaskType = "BatchTask",
    PythonModule = "math_operations",
    PythonFunction = "power",
    InputData = new Dictionary<string, object>
    {
        ["base"] = i,
        ["exponent"] = 2
    },
    Priority = Random.Shared.Next(1, 6)
});

foreach (var task in batchTasks)
{
    await taskQueue.EnqueueAsync(task);
}
```

## Monitoring and Debugging

### Getting Queue Statistics

```csharp
// Check current queue size
var queueSize = await taskQueue.GetQueueSizeAsync();
Console.WriteLine($"Current queue size: {queueSize}");

// Monitor queue in real-time
while (true)
{
    var size = await taskQueue.GetQueueSizeAsync();
    Console.WriteLine($"Queue size: {size} at {DateTime.Now}");
    await Task.Delay(5000); // Check every 5 seconds
}
```

### Health Monitoring

Check the logs for health information:

```
[2024-01-01 10:00:00] [INF] [SnakeWorker.Host.Services.HealthService] Python environment is healthy
[2024-01-01 10:00:00] [INF] [SnakeWorker.Host.Services.HealthService] Task queue size: 25
```

### Metrics Interpretation

The metrics reports will show:
- `tasks.dequeued`: Tasks taken from queue
- `tasks.completed`: Successfully finished tasks
- `tasks.failed`: Tasks that failed
- `tasks.retried`: Retry attempts
- `tasks.duration_ms`: Execution time histogram

## Best Practices

1. **Set Appropriate Timeouts**: Different operations need different timeout values
2. **Use Priorities**: Critical tasks should have higher priorities
3. **Handle Retries**: Set `MaxRetries` based on task importance
4. **Monitor Queue Size**: Prevent memory issues with large queues
5. **Test Error Scenarios**: Use error simulation tasks to test resilience
6. **Use Scheduling**: Spread load by scheduling non-urgent tasks

## Common Patterns

### Fire and Forget
```csharp
await taskQueue.EnqueueAsync(simpleTask);
// Task will be processed in background
```

### High Priority Processing
```csharp
criticalTask.Priority = 10;
await taskQueue.EnqueueAsync(criticalTask);
```

### Scheduled Batch Job
```csharp
batchTask.ScheduledAt = DateTime.Today.AddHours(2); // Run at 2 AM
await taskQueue.EnqueueAsync(batchTask);
```

### Resource-Heavy Task
```csharp
heavyTask.TimeoutSeconds = 600; // 10 minutes
heavyTask.MaxRetries = 1;       // Limited retries
await taskQueue.EnqueueAsync(heavyTask);
```