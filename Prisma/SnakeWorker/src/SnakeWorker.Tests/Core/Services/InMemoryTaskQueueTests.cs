using FluentAssertions;
using SnakeWorker.Core.Models;
using SnakeWorker.Core.Services;
using Xunit;

namespace SnakeWorker.Tests.Core.Services;

public class InMemoryTaskQueueTests
{
    [Fact]
    public async Task EnqueueAsync_WithValidTask_ShouldAddToQueue()
    {
        var queue = new InMemoryTaskQueue();
        var task = CreateTestTask();

        await queue.EnqueueAsync(task);
        var queueSize = await queue.GetQueueSizeAsync();

        queueSize.Should().Be(1);
    }

    [Fact]
    public async Task DequeueAsync_WithEmptyQueue_ShouldReturnNull()
    {
        var queue = new InMemoryTaskQueue();

        var result = await queue.DequeueAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DequeueAsync_WithSingleTask_ShouldReturnTask()
    {
        var queue = new InMemoryTaskQueue();
        var task = CreateTestTask();
        await queue.EnqueueAsync(task);

        var result = await queue.DequeueAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(task.Id);
        result.TaskType.Should().Be(task.TaskType);
    }

    [Fact]
    public async Task DequeueAsync_WithMultipleTasks_ShouldReturnHighestPriority()
    {
        var queue = new InMemoryTaskQueue();
        
        var lowPriorityTask = CreateTestTask("low", priority: 1);
        var highPriorityTask = CreateTestTask("high", priority: 10);
        var mediumPriorityTask = CreateTestTask("medium", priority: 5);

        await queue.EnqueueAsync(lowPriorityTask);
        await queue.EnqueueAsync(highPriorityTask);
        await queue.EnqueueAsync(mediumPriorityTask);

        var result = await queue.DequeueAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.TaskType.Should().Be("high");
    }

    [Fact]
    public async Task DequeueAsync_WithFutureScheduledTasks_ShouldSkipNotReadyTasks()
    {
        var queue = new InMemoryTaskQueue();
        
        var futureTask = CreateTestTask("future");
        futureTask.ScheduledAt = DateTime.UtcNow.AddMinutes(5);
        
        var readyTask = CreateTestTask("ready");
        readyTask.ScheduledAt = DateTime.UtcNow.AddMinutes(-1);

        await queue.EnqueueAsync(futureTask);
        await queue.EnqueueAsync(readyTask);

        var result = await queue.DequeueAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.TaskType.Should().Be("ready");
    }

    [Fact]
    public async Task DequeueAsync_WithCancellation_ShouldThrowOperationCancelledException()
    {
        var queue = new InMemoryTaskQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await queue.DequeueAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetQueueSizeAsync_WithMultipleTasks_ShouldReturnCorrectCount()
    {
        var queue = new InMemoryTaskQueue();
        
        await queue.EnqueueAsync(CreateTestTask("task1"));
        await queue.EnqueueAsync(CreateTestTask("task2"));
        await queue.EnqueueAsync(CreateTestTask("task3"));

        var size = await queue.GetQueueSizeAsync();

        size.Should().Be(3);
    }

    [Fact]
    public async Task GetQueueSizeAsync_AfterDequeue_ShouldDecrease()
    {
        var queue = new InMemoryTaskQueue();
        
        await queue.EnqueueAsync(CreateTestTask("task1"));
        await queue.EnqueueAsync(CreateTestTask("task2"));
        
        var initialSize = await queue.GetQueueSizeAsync();
        await queue.DequeueAsync(CancellationToken.None);
        var finalSize = await queue.GetQueueSizeAsync();

        initialSize.Should().Be(2);
        finalSize.Should().Be(1);
    }

    [Theory]
    [InlineData(10, 5, 3, 1)]  // priority: 10 > 5 > 3 > 1
    [InlineData(1, 10, 5, 3)]  // should reorder to: 10 > 5 > 3 > 1
    public async Task DequeueAsync_WithMultiplePriorities_ShouldMaintainPriorityOrder(
        int priority1, int priority2, int priority3, int priority4)
    {
        var queue = new InMemoryTaskQueue();
        var expectedOrder = new[] { priority1, priority2, priority3, priority4 }
            .OrderByDescending(x => x)
            .ToArray();

        await queue.EnqueueAsync(CreateTestTask($"task{priority1}", priority: priority1));
        await queue.EnqueueAsync(CreateTestTask($"task{priority2}", priority: priority2));
        await queue.EnqueueAsync(CreateTestTask($"task{priority3}", priority: priority3));
        await queue.EnqueueAsync(CreateTestTask($"task{priority4}", priority: priority4));

        var results = new List<WorkerTask>();
        for (int i = 0; i < 4; i++)
        {
            var task = await queue.DequeueAsync(CancellationToken.None);
            task.Should().NotBeNull();
            results.Add(task!);
        }

        for (int i = 0; i < expectedOrder.Length; i++)
        {
            results[i].Priority.Should().Be(expectedOrder[i]);
        }
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        var queue = new InMemoryTaskQueue();
        const int taskCount = 100;
        const int workerCount = 5;

        // Enqueue tasks concurrently
        var enqueueTasks = Enumerable.Range(0, taskCount)
            .Select(i => queue.EnqueueAsync(CreateTestTask($"task{i}")))
            .ToArray();

        await Task.WhenAll(enqueueTasks);

        // Dequeue tasks concurrently
        var dequeuedTasks = new List<WorkerTask>();
        var dequeueTasks = Enumerable.Range(0, workerCount)
            .Select(async _ =>
            {
                var tasks = new List<WorkerTask>();
                WorkerTask? task;
                while ((task = await queue.DequeueAsync(CancellationToken.None)) != null)
                {
                    tasks.Add(task);
                }
                return tasks;
            })
            .ToArray();

        var results = await Task.WhenAll(dequeueTasks);
        dequeuedTasks = results.SelectMany(x => x).ToList();

        dequeuedTasks.Should().HaveCount(taskCount);
        dequeuedTasks.Select(t => t.TaskType).Should().OnlyHaveUniqueItems();
    }

    private static WorkerTask CreateTestTask(string taskType = "test", int priority = 0)
    {
        return new WorkerTask
        {
            Id = Guid.NewGuid().ToString(),
            TaskType = taskType,
            PythonModule = "test_module",
            PythonFunction = "test_function",
            Priority = priority,
            InputData = new Dictionary<string, object>
            {
                ["test_param"] = "test_value"
            }
        };
    }
}