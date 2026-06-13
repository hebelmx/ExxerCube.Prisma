using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing.Reconciliation;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="ReconciliationEventForwarder"/> (MVP-PATH 1.4 Reconciliator edge): it republishes a
/// received <see cref="ExtractionCompletedEvent"/> onto the local stream, and ignores a null frame defensively.
/// </summary>
public sealed class ReconciliationEventForwarderTests
{
    [Fact]
    public void Forward_NullEvent_DoesNotPublish()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var sut = new ReconciliationEventForwarder(eventPublisher, NullLogger<ReconciliationEventForwarder>.Instance);

        sut.Forward(null);

        eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    [Fact]
    public void Forward_Event_PublishesSameEventOntoLocalStream()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var sut = new ReconciliationEventForwarder(eventPublisher, NullLogger<ReconciliationEventForwarder>.Instance);

        var evt = new ExtractionCompletedEvent
        {
            FileId = Guid.NewGuid(),
            Path = "2026/06/12/doc.fusion.json",
            CorrelationId = Guid.NewGuid(),
        };

        sut.Forward(evt);

        eventPublisher.Received(1).Publish(Arg.Is<ExtractionCompletedEvent>(e => ReferenceEquals(e, evt)));
    }
}
