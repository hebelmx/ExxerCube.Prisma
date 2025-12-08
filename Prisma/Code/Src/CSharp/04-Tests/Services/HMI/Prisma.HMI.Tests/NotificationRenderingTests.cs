namespace Prisma.HMI.Tests;

/// <summary>
/// ITDD Stage 7: Tests for notification rendering and display logic.
/// Verifies that events are properly formatted and displayed to users.
/// </summary>
public sealed class NotificationRenderingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void RenderClassificationNotification_FormatsCorrectly()
    {
        // Arrange
        var classificationEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "invoice-2024-001.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.95,
            CorrelationId: Guid.NewGuid(),
            Timestamp: new DateTimeOffset(2024, 12, 2, 14, 30, 0, TimeSpan.Zero)
        );

        // Act
        var notification = RenderNotification(classificationEvent);

        // Assert - Verify notification contains key information
        notification.Title.ShouldBe("Classification Complete");
        notification.Message.ShouldContain("invoice-2024-001.pdf");
        notification.Message.ShouldContain("Invoice");
        notification.Message.ShouldContain("95%"); // Confidence as percentage
        notification.Severity.ShouldBe(NotificationSeverity.Success);
        notification.Timestamp.ShouldBe(classificationEvent.Timestamp);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RenderProcessingNotification_FormatsCorrectly()
    {
        // Arrange
        var processingEvent = new ProcessingCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "contract-2024-015.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(18.5),
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var notification = RenderNotification(processingEvent);

        // Assert - Verify notification format
        notification.Title.ShouldBe("Processing Complete");
        notification.Message.ShouldContain("contract-2024-015.pdf");
        notification.Message.ShouldContain("Success");
        notification.Message.ShouldContain("18.5"); // Duration
        notification.Severity.ShouldBe(NotificationSeverity.Success);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RenderProcessingFailure_ShowsErrorSeverity()
    {
        // Arrange - Failed processing should show as error
        var processingEvent = new ProcessingCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "corrupted-file.pdf",
            Status: "Failed",
            ProcessingDuration: TimeSpan.FromSeconds(5),
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var notification = RenderNotification(processingEvent);

        // Assert - Failed processing shows as error
        notification.Title.ShouldBe("Processing Failed");
        notification.Message.ShouldContain("corrupted-file.pdf");
        notification.Severity.ShouldBe(NotificationSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RenderLowConfidenceClassification_ShowsWarningSeverity()
    {
        // Arrange - Low confidence should trigger warning
        var classificationEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "ambiguous-doc.pdf",
            ClassificationType: "Unknown",
            ConfidenceScore: 0.45, // Low confidence
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var notification = RenderNotification(classificationEvent);

        // Assert - Low confidence triggers warning
        notification.Severity.ShouldBe(NotificationSeverity.Warning);
        notification.Message.ShouldContain("45%");
        notification.Message.ShouldContain("review"); // Should suggest manual review
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NotificationQueue_MaintainsOrder()
    {
        // Arrange - Test notification ordering (FIFO)
        var queue = CreateNotificationQueue();
        var notifications = new[]
        {
            CreateTestNotification("First", DateTimeOffset.UtcNow),
            CreateTestNotification("Second", DateTimeOffset.UtcNow.AddSeconds(1)),
            CreateTestNotification("Third", DateTimeOffset.UtcNow.AddSeconds(2))
        };

        // Act - Add notifications
        foreach (var notification in notifications)
        {
            queue.Enqueue(notification);
        }

        // Assert - FIFO order preserved
        queue.Count.ShouldBe(3);
        queue.Dequeue().Message.ShouldContain("First");
        queue.Dequeue().Message.ShouldContain("Second");
        queue.Dequeue().Message.ShouldContain("Third");
    }

    // Helper methods and types (GREEN phase - implemented)
    private Notification RenderNotification(ClassificationCompletedEvent evt)
    {
        // Determine severity based on confidence score
        var severity = evt.ConfidenceScore switch
        {
            < 0.6 => NotificationSeverity.Warning,
            _ => NotificationSeverity.Success
        };

        // Format confidence as percentage
        var confidencePercent = (evt.ConfidenceScore * 100).ToString("F0");

        // Build message with suggestion for low confidence
        var message = evt.ConfidenceScore < 0.6
            ? $"Document '{evt.FileName}' classified as {evt.ClassificationType} with {confidencePercent}% confidence. Manual review recommended."
            : $"Document '{evt.FileName}' classified as {evt.ClassificationType} with {confidencePercent}% confidence.";

        return new Notification(
            Title: "Classification Complete",
            Message: message,
            Severity: severity,
            Timestamp: evt.Timestamp
        );
    }

    private Notification RenderNotification(ProcessingCompletedEvent evt)
    {
        // Determine severity and title based on status
        var (title, severity) = evt.Status.ToLowerInvariant() switch
        {
            "success" => ("Processing Complete", NotificationSeverity.Success),
            "failed" => ("Processing Failed", NotificationSeverity.Error),
            "partialsuccess" => ("Processing Partially Complete", NotificationSeverity.Warning),
            _ => ("Processing Status", NotificationSeverity.Info)
        };

        // Format duration
        var durationSeconds = evt.ProcessingDuration.TotalSeconds.ToString("F1");
        var message = $"Document '{evt.FileName}' processing {evt.Status.ToLowerInvariant()} in {durationSeconds}s.";

        return new Notification(
            Title: title,
            Message: message,
            Severity: severity,
            Timestamp: evt.Timestamp
        );
    }

    private INotificationQueue CreateNotificationQueue()
    {
        return new NotificationQueue();
    }

    private Notification CreateTestNotification(string message, DateTimeOffset timestamp)
    {
        return new Notification(
            Title: "Test Notification",
            Message: message,
            Severity: NotificationSeverity.Info,
            Timestamp: timestamp
        );
    }
}

/// <summary>Notification severity levels.</summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>Notification display model.</summary>
public sealed record Notification(
    string Title,
    string Message,
    NotificationSeverity Severity,
    DateTimeOffset Timestamp);

/// <summary>Notification queue interface.</summary>
public interface INotificationQueue
{
    int Count { get; }
    void Enqueue(Notification notification);
    Notification Dequeue();
}

/// <summary>Simple FIFO notification queue implementation.</summary>
public sealed class NotificationQueue : INotificationQueue
{
    private readonly Queue<Notification> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(Notification notification)
    {
        _queue.Enqueue(notification);
    }

    public Notification Dequeue()
    {
        return _queue.Dequeue();
    }
}
