using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing.Ingestion;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="IngestionEventForwarder"/> (MVP-PATH 1.3/1.5): a
/// <see cref="DocumentDownloadedEvent"/> received from the Orion ingestion hub must be republished onto
/// Athena's local event stream — but ONLY after its process clearance token is validated as
/// <see cref="ProcessClearance.Download"/> with a matching <c>file_id</c> (A5 DoD, MVP-PATH 1.5).
/// </summary>
public sealed class IngestionEventForwarderTests
{
    private const string TestBaseSegment = "prisma-fwd-test-storage";

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IProcessClearanceTokenService BuildAcceptingTokenService(
        Guid fileId,
        ProcessClearance clearance = ProcessClearance.Download)
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();
        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "orion-downloader",
                ActorType = SiaraActorType.ServiceAccount,
                Clearance = clearance,
                FileId = fileId,
            }));
        return mock;
    }

    private static IProcessClearanceTokenService BuildFailingTokenService()
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();
        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClearanceTokenClaims>.WithFailure("Token invalid."));
        return mock;
    }

    private static IngestionEventForwarder CreateForwarder(
        IEventPublisher publisher,
        IProcessClearanceTokenService? tokenService = null,
        string? basePath = null)
    {
        var resolver = new SharedStoragePathResolver(
            Options.Create(new StorageOptions
            {
                BasePath = basePath ?? Path.Combine(Path.GetTempPath(), TestBaseSegment),
            }),
            NullLogger<SharedStoragePathResolver>.Instance);

        return new IngestionEventForwarder(
            publisher,
            resolver,
            tokenService ?? Substitute.For<IProcessClearanceTokenService>(),
            NullLogger<IngestionEventForwarder>.Instance);
    }

    // ---------------------------------------------------------------------------
    // Existing behavioural tests (updated for ForwardAsync + clearance token)
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RepublishesEventOntoLocalEventStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();

        // Real EventPublisher (Rx) so we prove the event actually reaches GetEventStream subscribers.
        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var tokenService = BuildAcceptingTokenService(fileId);
        var forwarder = CreateForwarder(publisher, tokenService);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "doc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
            ClearanceToken = "valid-token",
        };

        await forwarder.ForwardAsync(downloadEvent, ct);

        received.ShouldNotBeNull();
        received!.FileId.ShouldBe(downloadEvent.FileId);
        received.CorrelationId.ShouldBe(downloadEvent.CorrelationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_WithRelativeStoragePath_ResolvesToAbsoluteFileName()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var baseDir = Path.Combine(Path.GetTempPath(), TestBaseSegment, Guid.NewGuid().ToString("N"));
        var expected = Path.GetFullPath(Path.Combine(baseDir, "2026", "06", "12", "abc.pdf"));

        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var tokenService = BuildAcceptingTokenService(fileId);
        var forwarder = CreateForwarder(publisher, tokenService, baseDir);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "abc.pdf",
            Path = "2026/06/12/abc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
            ClearanceToken = "valid-token",
        };

        await forwarder.ForwardAsync(downloadEvent, ct);

        // The pipeline opens FileName: it must now be the locally-resolved absolute path, while the
        // storage-relative Path is preserved for provenance.
        received.ShouldNotBeNull();
        received!.FileName.ShouldBe(expected);
        received.Path.ShouldBe("2026/06/12/abc.pdf");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_WhenStorageUnresolvable_ForwardsOriginalEventUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();

        // Unconfigured base (blank) models a storage misconfiguration: resolution fails closed and the
        // forwarder must still deliver the event (tolerant) so the pipeline can log-and-continue.
        using var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
        DocumentDownloadedEvent? received = null;
        using var subscription = publisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(evt => received = evt);

        var tokenService = BuildAcceptingTokenService(fileId);
        var forwarder = CreateForwarder(publisher, tokenService, basePath: string.Empty);
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "abc.pdf",
            Path = "2026/06/12/abc.pdf",
            Source = "SIARA",
            CorrelationId = Guid.NewGuid(),
            ClearanceToken = "valid-token",
        };

        await forwarder.ForwardAsync(downloadEvent, ct);

        received.ShouldNotBeNull();
        received!.FileName.ShouldBe("abc.pdf"); // unchanged — no resolution applied
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_NullEvent_DoesNotPublish()
    {
        var ct = TestContext.Current.CancellationToken;
        var publisher = Substitute.For<IEventPublisher>();
        var forwarder = CreateForwarder(publisher);

        await forwarder.ForwardAsync(null, ct);

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
                Substitute.For<IProcessClearanceTokenService>(),
                NullLogger<IngestionEventForwarder>.Instance));

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullClearanceTokenService_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new IngestionEventForwarder(
                Substitute.For<IEventPublisher>(),
                Substitute.For<IStoragePathResolver>(),
                null!,
                NullLogger<IngestionEventForwarder>.Instance));

    // ---------------------------------------------------------------------------
    // A5 DoD tests: clearance enforcement on the ingestion edge
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenTokenIsMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = Substitute.For<IProcessClearanceTokenService>();
        var forwarder = CreateForwarder(publisher, tokenService);
        var evt = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "doc.pdf",
            Source = "SIARA",
            ClearanceToken = string.Empty, // missing
        };

        // Act
        await forwarder.ForwardAsync(evt, ct);

        // Assert: ValidateAsync never called and nothing published
        await tokenService.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenTokenValidationFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildFailingTokenService();
        var forwarder = CreateForwarder(publisher, tokenService);
        var evt = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "doc.pdf",
            Source = "SIARA",
            ClearanceToken = "bad-token",
        };

        // Act
        await forwarder.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenClearanceIsNotDownload()
    {
        // Arrange — token is valid but carries Extract clearance (wrong stage for this edge)
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(fileId, clearance: ProcessClearance.Extract);
        var forwarder = CreateForwarder(publisher, tokenService);
        var evt = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "doc.pdf",
            Source = "SIARA",
            ClearanceToken = "token-with-wrong-clearance",
        };

        // Act
        await forwarder.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenFileIdMismatches()
    {
        // Arrange — token carries a different file_id than the event (replay / tamper attempt)
        var ct = TestContext.Current.CancellationToken;
        var eventFileId = Guid.NewGuid();
        var tokenFileId = Guid.NewGuid(); // different
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(tokenFileId, clearance: ProcessClearance.Download);
        var forwarder = CreateForwarder(publisher, tokenService);
        var evt = new DocumentDownloadedEvent
        {
            FileId = eventFileId,
            FileName = "doc.pdf",
            Source = "SIARA",
            ClearanceToken = "token-for-other-file",
        };

        // Act
        await forwarder.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_Accepts_WhenClearanceIsDownloadAndFileIdMatches()
    {
        // Arrange — correct clearance + file_id: event must be published
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(fileId, clearance: ProcessClearance.Download);
        var forwarder = CreateForwarder(publisher, tokenService);
        var evt = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "doc.pdf",
            Source = "SIARA",
            ClearanceToken = "valid-download-token",
        };

        // Act
        await forwarder.ForwardAsync(evt, ct);

        // Assert: event published (file_id unchanged; the forwarded event is the same record or a
        // storage-resolved copy — match on FileId which is always preserved)
        publisher.Received(1).Publish(Arg.Is<DocumentDownloadedEvent>(e => e.FileId == fileId));
    }
}
