using Microsoft.Extensions.Logging.Abstractions;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Events;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Ingestion.Tests;

/// <summary>
/// TDD tests for IngestionOrchestrator proving download, hashing, idempotency, and event emission.
/// </summary>
/// <remarks>
/// Stage 2 TDD Exit Criteria:
/// - Download PDF from SIARA (simulated)
/// - Compute SHA-256 hash
/// - Check journal for duplicate (idempotency)
/// - Store in partitioned structure: {base}/YYYY/MM/DD/{docId}.pdf
/// - Emit DocumentDownloaded event with correlation ID
/// - All tests passing (RED → GREEN)
/// </remarks>
public sealed class IngestionOrchestratorTests
{
    [Fact]
    public async Task IngestDocument_NewDocument_StoresAndEmitsEvent()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var publisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header

        var orchestrator = new IngestionOrchestrator(journal, downloader, publisher, logger);
        var documentId = "DOC123";
        var correlationId = Guid.NewGuid();

        // Act
        await orchestrator.IngestDocumentAsync(documentId, correlationId, TestContext.Current.CancellationToken);

        // Assert
        await journal.Received(1).RecordAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        publisher.Received(1).Publish(
            Arg.Is<DocumentDownloadedEvent>(e =>
                e.FileId != Guid.Empty &&
                e.CorrelationId == correlationId));
    }

    [Fact]
    public async Task IngestDocument_DuplicateHash_SkipsStorageAndEvent()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var publisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        // Return test data that will hash to a known value
        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

        journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // Duplicate detected after hashing

        var orchestrator = new IngestionOrchestrator(journal, downloader, publisher, logger);
        var documentId = "DOC123";
        var correlationId = Guid.NewGuid();

        // Act
        await orchestrator.IngestDocumentAsync(documentId, correlationId, TestContext.Current.CancellationToken);

        // Assert - MUST download to compute hash, but should NOT store or emit event after detecting duplicate
        await downloader.Received(1).DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await journal.DidNotReceive().RecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        publisher.DidNotReceive().Publish(Arg.Any<DocumentDownloadedEvent>());
    }

    [Fact]
    public async Task IngestDocument_ComputesCorrectSHA256Hash()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var publisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var expectedHash = "185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969"; // SHA-256 of "Hello"

        journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(testData);

        var orchestrator = new IngestionOrchestrator(journal, downloader, publisher, logger);

        // Act
        await orchestrator.IngestDocumentAsync("DOC123", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - verify hash was computed correctly
        await journal.Received(1).RecordAsync(
            expectedHash,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestDocument_CreatesPartitionedStoragePath()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var publisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var orchestrator = new IngestionOrchestrator(journal, downloader, publisher, logger);
        var documentId = "DOC123";
        var now = DateTime.UtcNow;

        // Act
        await orchestrator.IngestDocumentAsync(documentId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert - path should be: {base}/YYYY/MM/DD/{docId}.pdf
        await journal.Received(1).RecordAsync(
            Arg.Any<string>(),
            Arg.Is<string>(path =>
                path.Contains($"{now.Year:D4}") &&
                path.Contains($"{now.Month:D2}") &&
                path.Contains($"{now.Day:D2}") &&
                path.EndsWith($"{documentId}.pdf")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestDocument_CorrelationId_PreservedInEvent()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var publisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<IngestionOrchestrator>.Instance;

        journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var orchestrator = new IngestionOrchestrator(journal, downloader, publisher, logger);
        var correlationId = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        await orchestrator.IngestDocumentAsync("DOC123", correlationId, TestContext.Current.CancellationToken);

        // Assert - CRITICAL: correlation ID must be preserved exactly
        publisher.Received(1).Publish(
            Arg.Is<DocumentDownloadedEvent>(e => e.CorrelationId == correlationId));
    }
}
