using FluentAssertions;
using SnakeWorker.Core.Models;
using Xunit;

namespace SnakeWorker.Tests.Core.Models;

public class WorkerTaskTests
{
    [Fact]
    public void WorkerTask_DefaultValues_ShouldBeSetCorrectly()
    {
        var task = new WorkerTask();

        task.Id.Should().NotBeEmpty();
        task.TaskType.Should().BeEmpty();
        task.PythonModule.Should().BeEmpty();
        task.PythonFunction.Should().BeEmpty();
        task.InputData.Should().BeEmpty();
        task.Priority.Should().Be(0);
        task.CurrentRetry.Should().Be(0);
        task.MaxRetries.Should().Be(3);
        task.TimeoutSeconds.Should().Be(300);
        task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        task.ScheduledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WorkerTask_WithCustomValues_ShouldSetPropertiesCorrectly()
    {
        var customId = Guid.NewGuid().ToString();
        var customInputData = new Dictionary<string, object>
        {
            ["test_key"] = "test_value",
            ["number"] = 42
        };
        var customScheduledTime = DateTime.UtcNow.AddMinutes(5);

        var task = new WorkerTask
        {
            Id = customId,
            TaskType = "TestTask",
            PythonModule = "test_module",
            PythonFunction = "test_function",
            InputData = customInputData,
            Priority = 5,
            MaxRetries = 2,
            TimeoutSeconds = 120,
            ScheduledAt = customScheduledTime
        };

        task.Id.Should().Be(customId);
        task.TaskType.Should().Be("TestTask");
        task.PythonModule.Should().Be("test_module");
        task.PythonFunction.Should().Be("test_function");
        task.InputData.Should().BeEquivalentTo(customInputData);
        task.Priority.Should().Be(5);
        task.MaxRetries.Should().Be(2);
        task.TimeoutSeconds.Should().Be(120);
        task.ScheduledAt.Should().Be(customScheduledTime);
    }

    [Fact]
    public void WorkerTask_IsReadyToExecute_WithFutureScheduledTime_ShouldReturnFalse()
    {
        var task = new WorkerTask
        {
            ScheduledAt = DateTime.UtcNow.AddMinutes(5)
        };

        task.IsReadyToExecute().Should().BeFalse();
    }

    [Fact]
    public void WorkerTask_IsReadyToExecute_WithPastScheduledTime_ShouldReturnTrue()
    {
        var task = new WorkerTask
        {
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        };

        task.IsReadyToExecute().Should().BeTrue();
    }

    [Fact]
    public void WorkerTask_IsReadyToExecute_WithCurrentTime_ShouldReturnTrue()
    {
        var task = new WorkerTask
        {
            ScheduledAt = DateTime.UtcNow
        };

        task.IsReadyToExecute().Should().BeTrue();
    }
}