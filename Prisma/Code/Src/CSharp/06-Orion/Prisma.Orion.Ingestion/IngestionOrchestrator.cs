using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using DomainEvent = ExxerCube.Prisma.Domain.Events.DomainEvent;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Orchestrates SIARA monitoring, download, and journaling (logic only, host-agnostic).
/// </summary>
public class IngestionOrchestrator
{
    private readonly IIngestionJournal _journal;
    private readonly IDocumentDownloader _downloader;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<IngestionOrchestrator> _logger;
    private readonly string _storageBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionOrchestrator"/> class.
    /// </summary>
    /// <param name="journal">The ingestion journal for idempotency tracking.</param>
    /// <param name="downloader">The document downloader.</param>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="storageBasePath">Base path for document storage (defaults to ./storage).</param>
    public IngestionOrchestrator(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IEventPublisher eventPublisher,
        ILogger<IngestionOrchestrator> logger,
        string? storageBasePath = null)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageBasePath = storageBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
    }

    /// <summary>
    /// Ingests a single document with idempotency, hashing, storage, and event emission.
    /// </summary>
    /// <param name="documentId">SIARA document ID.</param>
    /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task IngestDocumentAsync(string documentId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting document ingestion. DocumentId: {DocumentId}, CorrelationId: {CorrelationId}",
            documentId,
            correlationId);

        try
        {
            // Step 1: Download document
            var documentBytes = await _downloader.DownloadAsync(documentId, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Document downloaded. Size: {Size} bytes", documentBytes.Length);

            // Step 2: Compute SHA-256 hash
            var hash = ComputeSha256Hash(documentBytes);
            _logger.LogDebug("Document hash computed: {Hash}", hash);

            // Step 3: Check for duplicate (idempotency)
            var isDuplicate = await _journal.IsDuplicateAsync(hash, cancellationToken).ConfigureAwait(false);
            if (isDuplicate)
            {
                _logger.LogInformation(
                    "Duplicate document detected (hash: {Hash}). Skipping ingestion. DocumentId: {DocumentId}",
                    hash,
                    documentId);
                return; // DEFENSIVE - skip duplicate documents
            }

            // Step 4: Create partitioned storage path: {base}/YYYY/MM/DD/{docId}.pdf
            var now = DateTime.UtcNow;
            var partitionPath = Path.Combine(
                _storageBasePath,
                $"{now.Year:D4}",
                $"{now.Month:D2}",
                $"{now.Day:D2}");

            Directory.CreateDirectory(partitionPath);

            var filePath = Path.Combine(partitionPath, $"{documentId}.pdf");

            // Step 5: Write file to disk
            await File.WriteAllBytesAsync(filePath, documentBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Document stored at: {FilePath}", filePath);

            // Step 6: Record in journal
            await _journal.RecordAsync(hash, filePath, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Document recorded in journal");

            // Step 7: Emit DocumentDownloadedEvent (using existing Domain event)
            var fileId = Guid.NewGuid();
            var @event = new ExxerCube.Prisma.Domain.Events.DocumentDownloadedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                FileName = $"{documentId}.pdf",
                Source = "SIARA",
                FileSizeBytes = documentBytes.Length,
                Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
                DownloadUrl = $"siara://documents/{documentId}"
            };

            _eventPublisher.Publish(@event);
            _logger.LogInformation(
                "DocumentDownloadedEvent published. FileId: {FileId}, CorrelationId: {CorrelationId}",
                fileId,
                correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document ingestion cancelled. DocumentId: {DocumentId}", documentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Document ingestion failed. DocumentId: {DocumentId}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                documentId,
                correlationId,
                ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Starts the ingestion orchestrator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ingestion orchestrator starting");
        // Placeholder for watcher wiring; implement SIARA polling, download, partitioned storage, and journal writes.
        return Task.CompletedTask;
    }

    private static string ComputeSha256Hash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}