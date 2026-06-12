using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing.Ingestion;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="IngestionEventForwarder"/> (MVP-PATH 1.3): a <see cref="DocumentDownloadedEvent"/>
/// received from the Orion ingestion hub must be republished onto Athena's local event stream so the
/// processing pipeline (which subscribes to that stream) runs.
/// </summary>
public sealed class IngestionEventForwarderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Forward_RepublishesEventOntoLocalEventStream()
    {
        // Real EventPublisher (Rx) so we prove the event actually reaches GetEventStream subscribers.
        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var forwarder = new IngestionEventForwarder(publisher, NullLogger<IngestionEventForwarder>.Instance);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "doc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
        };

        forwarder.Forward(downloadEvent);

        received.ShouldNotBeNull();
        received!.FileId.ShouldBe(downloadEvent.FileId);
        received.CorrelationId.ShouldBe(downloadEvent.CorrelationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Forward_NullEvent_DoesNotPublish()
    {
        var publisher = Substitute.For<IEventPublisher>();
        var forwarder = new IngestionEventForwarder(publisher, NullLogger<IngestionEventForwarder>.Instance);

        forwarder.Forward(null);

        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullPublisher_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new IngestionEventForwarder(null!, NullLogger<IngestionEventForwarder>.Instance));
}
