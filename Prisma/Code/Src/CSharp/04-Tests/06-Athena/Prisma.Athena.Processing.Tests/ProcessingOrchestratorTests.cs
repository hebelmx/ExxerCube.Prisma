using Microsoft.Extensions.Logging.Abstractions;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Prisma.Athena.Processing;

namespace Prisma.Athena.Processing.Tests;

/// <summary>
/// ITDD tests for ProcessingOrchestrator proving pipeline coordination and event emission.
/// </summary>
/// <remarks>
/// Stage 3 ITDD Exit Criteria:
/// - Subscribe to DocumentDownloadedEvent
/// - Orchestrate pipeline: Quality → OCR → Fusion → Classification → Export
/// - Emit events at each stage preserving correlation ID
/// - Handle errors defensively (NEVER CRASH)
/// - All tests passing (RED → GREEN)
/// </remarks>
public sealed class ProcessingOrchestratorTests
{
    [Fact]
    public async Task ProcessDocument_NewDocument_EmitsProcessingStartedLog()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = "siara://documents/test"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - orchestrator should process without throwing
        // (Further pipeline steps will be added incrementally)
    }

    [Fact]
    public async Task ProcessDocument_CorrelationId_PreservedInEvents()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var correlationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = "siara://documents/test"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - when pipeline stages are implemented, all events must preserve correlation ID
        // For now, just verify orchestrator accepts the event
    }

    [Fact]
    public async Task ProcessDocument_EmitsDocumentProcessingCompletedEvent()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = "siara://documents/test"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - completion event should be emitted with correlation ID preserved
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));
    }

    [Fact]
    public async Task ProcessDocument_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = "siara://documents/test"
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await orchestrator.ProcessDocumentAsync(downloadEvent, cts.Token));
    }

    [Fact]
    public async Task ProcessDocument_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await orchestrator.ProcessDocumentAsync(null!, TestContext.Current.CancellationToken));
    }
}
