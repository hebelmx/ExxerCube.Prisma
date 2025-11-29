using System.Reactive.Linq;
using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="EventPublisher"/> covering event publishing and streaming with Rx.NET.
/// </summary>
public class EventPublisherTests
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly EventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPublisherTests"/> class.
    /// </summary>
    public EventPublisherTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<EventPublisher>(output);
        _publisher = new EventPublisher(_logger);
    }

    /// <summary>
    /// Tests that <see cref="EventPublisher.Publish"/> successfully publishes an event to subscribers.
    /// </summary>
    [Fact]
    public void Publish_ValidEvent_PublishesToSubscribers()
    {
        // Arrange
        DocumentDownloadedEvent? receivedEvent = null;
        var subscription = _publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => receivedEvent = e);

        var testEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://test.com/file.pdf"
        };

        // Act
        _publisher.Publish(testEvent);

        // Assert
        receivedEvent.ShouldNotBeNull();
        receivedEvent.FileId.ShouldBe(testEvent.FileId);
        receivedEvent.FileName.ShouldBe(testEvent.FileName);
        receivedEvent.Source.ShouldBe(testEvent.Source);
        receivedEvent.EventType.ShouldBe(nameof(DocumentDownloadedEvent));

        subscription.Dispose();
    }

    /// <summary>
    /// Tests that <see cref="EventPublisher.GetEventStream{TEvent}"/> filters events by type.
    /// </summary>
    [Fact]
    public void GetEventStream_FiltersByEventType_OnlyReceivesMatchingEvents()
    {
        // Arrange
        var receivedDocEvents = new List<DocumentDownloadedEvent>();
        var receivedOcrEvents = new List<OcrCompletedEvent>();

        var docSubscription = _publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => receivedDocEvents.Add(e));
        var ocrSubscription = _publisher.GetEventStream<OcrCompletedEvent>()
            .Subscribe(e => receivedOcrEvents.Add(e));

        var docEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA"
        };

        var ocrEvent = new OcrCompletedEvent
        {
            FileId = Guid.NewGuid(),
            OcrEngine = "Tesseract",
            Confidence = 95.5m
        };

        // Act
        _publisher.Publish(docEvent);
        _publisher.Publish(ocrEvent);

        // Assert
        receivedDocEvents.Count.ShouldBe(1);
        receivedDocEvents[0].FileId.ShouldBe(docEvent.FileId);

        receivedOcrEvents.Count.ShouldBe(1);
        receivedOcrEvents[0].FileId.ShouldBe(ocrEvent.FileId);

        docSubscription.Dispose();
        ocrSubscription.Dispose();
    }

    /// <summary>
    /// Tests that <see cref="EventPublisher.GetAllEventsStream"/> receives all event types.
    /// </summary>
    [Fact]
    public void GetAllEventsStream_ReceivesAllEventTypes()
    {
        // Arrange
        var receivedEvents = new List<DomainEvent>();
        var subscription = _publisher.GetAllEventsStream()
            .Subscribe(e => receivedEvents.Add(e));

        var docEvent = new DocumentDownloadedEvent { FileId = Guid.NewGuid() };
        var ocrEvent = new OcrCompletedEvent { FileId = Guid.NewGuid() };
        var classEvent = new ClassificationCompletedEvent { FileId = Guid.NewGuid() };

        // Act
        _publisher.Publish(docEvent);
        _publisher.Publish(ocrEvent);
        _publisher.Publish(classEvent);

        // Assert
        receivedEvents.Count.ShouldBe(3);
        receivedEvents[0].ShouldBeOfType<DocumentDownloadedEvent>();
        receivedEvents[1].ShouldBeOfType<OcrCompletedEvent>();
        receivedEvents[2].ShouldBeOfType<ClassificationCompletedEvent>();

        subscription.Dispose();
    }

    /// <summary>
    /// Tests that <see cref="EventPublisher.Publish"/> does not throw when publishing after disposal.
    /// Defensive Intelligence: Publishing after disposal should not break the system.
    /// </summary>
    [Fact]
    public void Publish_AfterDisposal_DoesNotThrow()
    {
        // Arrange
        var publisher = new EventPublisher(_logger);
        var testEvent = new DocumentDownloadedEvent { FileId = Guid.NewGuid() };

        // Act
        publisher.Dispose();
        var act = () => publisher.Publish(testEvent);

        // Assert - should not throw (defensive intelligence)
        act.ShouldNotThrow();
    }

    /// <summary>
    /// Tests that multiple subscribers receive the same published event.
    /// </summary>
    [Fact]
    public void Publish_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange
        DocumentDownloadedEvent? received1 = null;
        DocumentDownloadedEvent? received2 = null;
        DocumentDownloadedEvent? received3 = null;

        var sub1 = _publisher.GetEventStream<DocumentDownloadedEvent>().Subscribe(e => received1 = e);
        var sub2 = _publisher.GetEventStream<DocumentDownloadedEvent>().Subscribe(e => received2 = e);
        var sub3 = _publisher.GetEventStream<DocumentDownloadedEvent>().Subscribe(e => received3 = e);

        var testEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "broadcast-test.pdf"
        };

        // Act
        _publisher.Publish(testEvent);

        // Assert
        received1.ShouldNotBeNull();
        received2.ShouldNotBeNull();
        received3.ShouldNotBeNull();

        received1.FileId.ShouldBe(testEvent.FileId);
        received2.FileId.ShouldBe(testEvent.FileId);
        received3.FileId.ShouldBe(testEvent.FileId);

        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    /// <summary>
    /// Tests that events have correlation IDs for distributed tracing.
    /// </summary>
    [Fact]
    public void Publish_EventWithCorrelationId_PreservesCorrelationId()
    {
        // Arrange
        DocumentDownloadedEvent? receivedEvent = null;
        var subscription = _publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => receivedEvent = e);

        var correlationId = Guid.NewGuid();
        var testEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "correlated.pdf",
            CorrelationId = correlationId
        };

        // Act
        _publisher.Publish(testEvent);

        // Assert
        receivedEvent.ShouldNotBeNull();
        receivedEvent.CorrelationId.ShouldBe(correlationId);
        receivedEvent.EventId.ShouldNotBe(Guid.Empty);
        receivedEvent.Timestamp.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);

        subscription.Dispose();
    }

    /// <summary>
    /// Tests that unsubscribing stops receiving events.
    /// </summary>
    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        // Arrange
        var receivedEvents = new List<DocumentDownloadedEvent>();
        var subscription = _publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => receivedEvents.Add(e));

        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid() });

        // Act - unsubscribe
        subscription.Dispose();
        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid() });

        // Assert - should only have received first event
        receivedEvents.Count.ShouldBe(1);
    }

    /// <summary>
    /// Tests reactive operators on event stream (demonstrates Rx.NET power).
    /// </summary>
    [Fact]
    public async Task GetEventStream_WithReactiveOperators_FiltersAndTransforms()
    {
        // Arrange
        var largeFilesReceived = new List<DocumentDownloadedEvent>();
        var subscription = _publisher.GetEventStream<DocumentDownloadedEvent>()
            .Where(e => e.FileSizeBytes > 10000) // Filter large files
            .Take(2) // Take only first 2
            .Subscribe(e => largeFilesReceived.Add(e));

        // Act
        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid(), FileSizeBytes = 500 });  // Too small
        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid(), FileSizeBytes = 15000 }); // Match
        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid(), FileSizeBytes = 20000 }); // Match
        _publisher.Publish(new DocumentDownloadedEvent { FileId = Guid.NewGuid(), FileSizeBytes = 25000 }); // Ignored (Take(2))

        await Task.Delay(100, TestContext.Current.CancellationToken); // Allow async processing

        // Assert
        largeFilesReceived.Count.ShouldBe(2);
        largeFilesReceived.All(e => e.FileSizeBytes > 10000).ShouldBeTrue();

        subscription.Dispose();
    }
}