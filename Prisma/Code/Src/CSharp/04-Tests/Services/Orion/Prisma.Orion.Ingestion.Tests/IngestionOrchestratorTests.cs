using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Stage 2.5 REFACTORED tests for IngestionOrchestrator using IExxerHub&lt;T&gt; and Result&lt;T&gt;.
/// Validates Railway-Oriented Programming, transport-agnostic event broadcasting, and idempotency.
/// </summary>
/// <remarks>
/// Stage 2.5 Exit Criteria:
/// - Uses IExxerHub&lt;DocumentDownloadedEvent&gt; instead of IEventPublisher
/// - Returns Result&lt;IngestionResult&gt; instead of Task (void)
/// - No exceptions for control flow (uses Result.Failure(), ResultExtensions.Cancelled())
/// - Events broadcast via SendToAllAsync() (transport-agnostic)
/// - All tests green (Railway-Oriented Programming validated)
/// </remarks>
public sealed class IngestionOrchestratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);
        var documentId = "DOC123";
        var correlationId = Guid.NewGuid();

        // Act
        var result = await orchestrator.IngestDocumentAsync(documentId, correlationId, TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.FileId.ShouldNotBe(Guid.Empty);
        result.Value.CorrelationId.ShouldBe(correlationId);
        result.Value.WasDuplicate.ShouldBeFalse();

        await journal.Received(1).RecordAsync(
            Arg.Is<IngestionManifestEntry>(e =>
                !string.IsNullOrEmpty(e.ContentHash) &&
                !string.IsNullOrEmpty(e.SourceUrl) &&
                e.FileId != Guid.Empty),
            Arg.Any<CancellationToken>());

        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentDownloadedEvent>(e =>
                e.FileId != Guid.Empty &&
                e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        // Return test data that will hash to a known value
        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // Duplicate detected after hashing

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);
        var documentId = "DOC123";
        var correlationId = Guid.NewGuid();

        // Act
        var result = await orchestrator.IngestDocumentAsync(documentId, correlationId, TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented: duplicate is SUCCESS (idempotent skip)
        result.IsSuccess.ShouldBeTrue();
        result.Value!.WasDuplicate.ShouldBeTrue();
        result.Value!.CorrelationId.ShouldBe(correlationId);

        // MUST download to compute hash, but should NOT store or broadcast after detecting duplicate
        await downloader.Received(1).DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
        await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_ComputesCorrectSHA256Hash()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var expectedHash = "185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969"; // SHA-256 of "Hello"

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(testData);

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);

        // Act
        var result = await orchestrator.IngestDocumentAsync("DOC123", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - verify hash was computed correctly
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Hash.ShouldBe(expectedHash);

        await journal.Received(1).RecordAsync(
            Arg.Is<IngestionManifestEntry>(e => e.ContentHash == expectedHash),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_CreatesPartitionedStoragePath()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);
        var documentId = "DOC123";
        var now = DateTime.UtcNow;

        // Act
        var result = await orchestrator.IngestDocumentAsync(documentId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - path should be: {base}/YYYY/MM/DD/{docId}.pdf
        result.IsSuccess.ShouldBeTrue();
        result.Value!.StoredPath.ShouldContain($"{now.Year:D4}");
        result.Value!.StoredPath.ShouldContain($"{now.Month:D2}");
        result.Value!.StoredPath.ShouldContain($"{now.Day:D2}");
        result.Value!.StoredPath.ShouldEndWith($"{documentId}.pdf");

        await journal.Received(1).RecordAsync(
            Arg.Is<IngestionManifestEntry>(e =>
                e.StoredPath.Contains($"{now.Year:D4}") &&
                e.StoredPath.Contains($"{now.Month:D2}") &&
                e.StoredPath.Contains($"{now.Day:D2}") &&
                e.StoredPath.EndsWith($"{documentId}.pdf")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_CorrelationId_PreservedInResult()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);
        var correlationId = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var result = await orchestrator.IngestDocumentAsync("DOC123", correlationId, TestContext.Current.CancellationToken);

        // Assert - CRITICAL: correlation ID must be preserved exactly
        result.IsSuccess.ShouldBeTrue();
        result.Value!.CorrelationId.ShouldBe(correlationId);

        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentDownloadedEvent>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await orchestrator.IngestDocumentAsync("DOC123", Guid.NewGuid(), cts.Token);

        // Assert - Railway-Oriented: cancellation is Result, not exception
        result.IsCancelled().ShouldBeTrue();

        // No operations should have been called
        await downloader.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
        await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_WhenDownloadFails_ReturnsFailureWithoutBroadcast()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new InvalidOperationException("Network error"));

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);

        // Act
        var result = await orchestrator.IngestDocumentAsync("DOC123", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented: failure is Result, not exception
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("Download failed"));

        // No downstream operations should have been called
        await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
        await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestDocument_BroadcastsViaIExxerHub_NotIEventPublisher()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new IngestionOrchestrator(journal, downloader, eventHub, logger);

        // Act
        var result = await orchestrator.IngestDocumentAsync("DOC123", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - CRITICAL: uses IExxerHub<T>.SendToAllAsync() (transport-agnostic)
        result.IsSuccess.ShouldBeTrue();

        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentDownloadedEvent>(e =>
                e.Source == "SIARA" &&
                e.FileSizeBytes > 0),
            Arg.Any<CancellationToken>());
    }
}
