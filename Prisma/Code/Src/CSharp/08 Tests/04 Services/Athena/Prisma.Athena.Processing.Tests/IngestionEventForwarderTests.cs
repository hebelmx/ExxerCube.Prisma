using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing.Ingestion;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="IngestionEventForwarder"/> (MVP-PATH 1.3): a <see cref="DocumentDownloadedEvent"/>
/// received from the Orion ingestion hub must be republished onto Athena's local event stream so the
/// processing pipeline (which subscribes to that stream) runs — after resolving the event's storage-relative
/// path against the shared-storage base (ADR-011).
/// </summary>
public sealed class IngestionEventForwarderTests
{
    private const string TestBaseSegment = "prisma-fwd-test-storage";

    private static IngestionEventForwarder CreateForwarder(IEventPublisher publisher, string? basePath = null)
    {
        var resolver = new SharedStoragePathResolver(
            Options.Create(new StorageOptions
            {
                BasePath = basePath ?? Path.Combine(Path.GetTempPath(), TestBaseSegment),
            }),
            NullLogger<SharedStoragePathResolver>.Instance);

        return new IngestionEventForwarder(publisher, resolver, NullLogger<IngestionEventForwarder>.Instance);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Forward_RepublishesEventOntoLocalEventStream()
    {
        // Real EventPublisher (Rx) so we prove the event actually reaches GetEventStream subscribers.
        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var forwarder = CreateForwarder(publisher);
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
    public void Forward_WithRelativeStoragePath_ResolvesToAbsoluteFileName()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), TestBaseSegment, Guid.NewGuid().ToString("N"));
        var expected = Path.GetFullPath(Path.Combine(baseDir, "2026", "06", "12", "abc.pdf"));

        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var forwarder = CreateForwarder(publisher, baseDir);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "abc.pdf",
            Path = "2026/06/12/abc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
        };

        forwarder.Forward(downloadEvent);

        // The pipeline opens FileName: it must now be the locally-resolved absolute path, while the
        // storage-relative Path is preserved for provenance.
        received.ShouldNotBeNull();
        received!.FileName.ShouldBe(expected);
        received.Path.ShouldBe("2026/06/12/abc.pdf");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Forward_WhenStorageUnresolvable_ForwardsOriginalEventUnchanged()
    {
        // Unconfigured base (blank) models a storage misconfiguration: resolution fails closed and the
        // forwarder must still deliver the event (tolerant) so the pipeline can log-and-continue.
        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var forwarder = CreateForwarder(publisher, basePath: string.Empty);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "abc.pdf",
            Path = "2026/06/12/abc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
        };

        forwarder.Forward(downloadEvent);

        received.ShouldNotBeNull();
        received!.FileName.ShouldBe("abc.pdf"); // unchanged — no resolution applied
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Forward_NullEvent_DoesNotPublish()
    {
        var publisher = Substitute.For<IEventPublisher>();
        var forwarder = CreateForwarder(publisher);

        forwarder.Forward(null);

        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullPublisher_Throws() =>
        Should.Throw<ArgumentNullException>(() => CreateForwarder(null!));

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullResolver_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new IngestionEventForwarder(
                Substitute.For<IEventPublisher>(),
                null!,
                NullLogger<IngestionEventForwarder>.Instance));
}
