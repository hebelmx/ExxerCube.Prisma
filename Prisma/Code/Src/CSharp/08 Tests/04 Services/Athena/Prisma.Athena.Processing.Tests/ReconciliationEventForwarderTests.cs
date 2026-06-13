using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing.Reconciliation;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="ReconciliationEventForwarder"/> (MVP-PATH 1.4/1.5): it republishes a received
/// <see cref="ExtractionCompletedEvent"/> onto the local stream — but ONLY after its process clearance token
/// is validated as <see cref="ProcessClearance.Extract"/> with a matching <c>file_id</c> (A5 DoD, MVP-PATH 1.5).
/// </summary>
public sealed class ReconciliationEventForwarderTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IProcessClearanceTokenService BuildAcceptingTokenService(
        Guid fileId,
        ProcessClearance clearance = ProcessClearance.Extract)
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();
        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "athena-extractor",
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

    private static ReconciliationEventForwarder CreateForwarder(
        IEventPublisher publisher,
        IProcessClearanceTokenService? tokenService = null)
        => new ReconciliationEventForwarder(
            publisher,
            tokenService ?? Substitute.For<IProcessClearanceTokenService>(),
            NullLogger<ReconciliationEventForwarder>.Instance);

    // ---------------------------------------------------------------------------
    // Existing behavioural tests (updated for ForwardAsync + clearance token)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ForwardAsync_NullEvent_DoesNotPublish()
    {
        var ct = TestContext.Current.CancellationToken;
        var eventPublisher = Substitute.For<IEventPublisher>();
        var sut = CreateForwarder(eventPublisher);

        await sut.ForwardAsync(null, ct);

        eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    [Fact]
    public async Task ForwardAsync_Event_PublishesSameEventOntoLocalStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(fileId);
        var sut = CreateForwarder(eventPublisher, tokenService);

        var evt = new ExtractionCompletedEvent
        {
            FileId = fileId,
            Path = "2026/06/12/doc.fusion.json",
            CorrelationId = Guid.NewGuid(),
            ClearanceToken = "valid-extract-token",
        };

        await sut.ForwardAsync(evt, ct);

        eventPublisher.Received(1).Publish(Arg.Is<ExtractionCompletedEvent>(e => ReferenceEquals(e, evt)));
    }

    [Fact]
    public void Constructor_NullClearanceTokenService_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new ReconciliationEventForwarder(
                Substitute.For<IEventPublisher>(),
                null!,
                NullLogger<ReconciliationEventForwarder>.Instance));

    // ---------------------------------------------------------------------------
    // A5 DoD tests: clearance enforcement on the reconciliation edge
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenTokenIsMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = Substitute.For<IProcessClearanceTokenService>();
        var sut = CreateForwarder(publisher, tokenService);
        var evt = new ExtractionCompletedEvent
        {
            FileId = Guid.NewGuid(),
            Path = "2026/06/12/doc.fusion.json",
            ClearanceToken = string.Empty, // missing
        };

        // Act
        await sut.ForwardAsync(evt, ct);

        // Assert: ValidateAsync never called and nothing published
        await tokenService.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        publisher.DidNotReceive().Publish(Arg.Any<ExtractionCompletedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenTokenValidationFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildFailingTokenService();
        var sut = CreateForwarder(publisher, tokenService);
        var evt = new ExtractionCompletedEvent
        {
            FileId = Guid.NewGuid(),
            Path = "2026/06/12/doc.fusion.json",
            ClearanceToken = "bad-token",
        };

        // Act
        await sut.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<ExtractionCompletedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_RejectsEvent_WhenClearanceIsNotExtract()
    {
        // Arrange — token carries Download clearance (wrong stage for this edge)
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(fileId, clearance: ProcessClearance.Download);
        var sut = CreateForwarder(publisher, tokenService);
        var evt = new ExtractionCompletedEvent
        {
            FileId = fileId,
            Path = "2026/06/12/doc.fusion.json",
            ClearanceToken = "token-with-wrong-clearance",
        };

        // Act
        await sut.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<ExtractionCompletedEvent>());
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
        var tokenService = BuildAcceptingTokenService(tokenFileId, clearance: ProcessClearance.Extract);
        var sut = CreateForwarder(publisher, tokenService);
        var evt = new ExtractionCompletedEvent
        {
            FileId = eventFileId,
            Path = "2026/06/12/doc.fusion.json",
            ClearanceToken = "token-for-other-file",
        };

        // Act
        await sut.ForwardAsync(evt, ct);

        // Assert: nothing published
        publisher.DidNotReceive().Publish(Arg.Any<ExtractionCompletedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForwardAsync_Accepts_WhenClearanceIsExtractAndFileIdMatches()
    {
        // Arrange — correct clearance + file_id: event must be published
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var publisher = Substitute.For<IEventPublisher>();
        var tokenService = BuildAcceptingTokenService(fileId, clearance: ProcessClearance.Extract);
        var sut = CreateForwarder(publisher, tokenService);
        var evt = new ExtractionCompletedEvent
        {
            FileId = fileId,
            Path = "2026/06/12/doc.fusion.json",
            ClearanceToken = "valid-extract-token",
        };

        // Act
        await sut.ForwardAsync(evt, ct);

        // Assert: the exact event instance is published onto the local stream
        publisher.Received(1).Publish(Arg.Is<ExtractionCompletedEvent>(e => ReferenceEquals(e, evt)));
    }
}
