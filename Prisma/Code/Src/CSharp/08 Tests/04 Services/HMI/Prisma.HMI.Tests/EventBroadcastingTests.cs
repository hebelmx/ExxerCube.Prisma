namespace Prisma.HMI.Tests;

/// <summary>
/// ITDD Stage 7: Tests for event broadcasting using IndFusion.Ember abstractions.
/// Verifies that events are properly broadcast to clients via IExxerHub transport-agnostic layer.
/// </summary>
public sealed class EventBroadcastingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastClassificationEvent_ViaSendToAllAsync_ReturnsSuccess()
    {
        // Arrange - Create mock hub for ClassificationCompletedEvent
        var mockHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        mockHub.SendToAllAsync(Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test-document.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.95,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act - Broadcast event to all clients
        var result = await mockHub.SendToAllAsync(testEvent, TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming: Result indicates success
        result.IsSuccess.ShouldBeTrue();
        await mockHub.Received(1).SendToAllAsync(
            Arg.Is<ClassificationCompletedEvent>(e => e.FileName == "test-document.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastProcessingEvent_ViaSendToAllAsync_ReturnsSuccess()
    {
        // Arrange
        var mockHub = Substitute.For<IExxerHub<ProcessingCompletedEvent>>();
        mockHub.SendToAllAsync(Arg.Any<ProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var testEvent = new ProcessingCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test-document.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(15),
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var result = await mockHub.SendToAllAsync(testEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await mockHub.Received(1).SendToAllAsync(
            Arg.Is<ProcessingCompletedEvent>(e => e.Status == "Success"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastEvent_WhenCancelled_ReturnsCancelled()
    {
        // Arrange
        var mockHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        mockHub.SendToAllAsync(Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());

        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "cancelled-document.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.90,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await mockHub.SendToAllAsync(testEvent, cts.Token);

        // Assert - Railway-Oriented: Cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastEvent_WhenFails_ReturnsFailure()
    {
        // Arrange - Simulate broadcast failure (network issue, etc.)
        var mockHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        mockHub.SendToAllAsync(Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Network connection lost"));

        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "failed-broadcast.pdf",
            ClassificationType: "Contract",
            ConfidenceScore: 0.88,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var result = await mockHub.SendToAllAsync(testEvent, TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented: Failure is Result, not exception
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Network connection lost");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastMultipleEvents_MaintainsCorrelationId()
    {
        // Arrange - Verify end-to-end tracing through event chain
        var classificationHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        var processingHub = Substitute.For<IExxerHub<ProcessingCompletedEvent>>();

        classificationHub.SendToAllAsync(Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        processingHub.SendToAllAsync(Arg.Any<ProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var classificationEvent = new ClassificationCompletedEvent(
            FileId: fileId,
            FileName: "traced-document.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.92,
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow
        );

        var processingEvent = new ProcessingCompletedEvent(
            FileId: fileId,
            FileName: "traced-document.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(20),
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow.AddSeconds(20)
        );

        // Act - Broadcast event chain with same correlation ID
        var result1 = await classificationHub.SendToAllAsync(classificationEvent, TestContext.Current.CancellationToken);
        var result2 = await processingHub.SendToAllAsync(processingEvent, TestContext.Current.CancellationToken);

        // Assert - Correlation ID preserved across entire pipeline
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();

        await classificationHub.Received(1).SendToAllAsync(
            Arg.Is<ClassificationCompletedEvent>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
        await processingHub.Received(1).SendToAllAsync(
            Arg.Is<ProcessingCompletedEvent>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastToGroup_WithValidGroupName_ReturnsSuccess()
    {
        // Arrange - Test group-specific broadcasting
        var mockHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        mockHub.SendToGroupAsync(Arg.Any<string>(), Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "group-document.pdf",
            ClassificationType: "Report",
            ConfidenceScore: 0.87,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act - Broadcast to specific group (e.g., "AdminUsers")
        var result = await mockHub.SendToGroupAsync("AdminUsers", testEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await mockHub.Received(1).SendToGroupAsync("AdminUsers", Arg.Any<ClassificationCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BroadcastToClient_WithValidConnectionId_ReturnsSuccess()
    {
        // Arrange - Test client-specific broadcasting
        var mockHub = Substitute.For<IExxerHub<ProcessingCompletedEvent>>();
        mockHub.SendToClientAsync(Arg.Any<string>(), Arg.Any<ProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var testEvent = new ProcessingCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "user-document.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(12),
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        var connectionId = "user-connection-123";

        // Act - Broadcast to specific client
        var result = await mockHub.SendToClientAsync(connectionId, testEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await mockHub.Received(1).SendToClientAsync(connectionId, Arg.Any<ProcessingCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetConnectionCount_ReturnsCount()
    {
        // Arrange
        var mockHub = Substitute.For<IExxerHub<ClassificationCompletedEvent>>();
        mockHub.GetConnectionCountAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(42));

        // Act
        var result = await mockHub.GetConnectionCountAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }
}
