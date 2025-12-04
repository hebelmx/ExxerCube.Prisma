using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Orchestrates SIARA monitoring, download, and journaling (logic only, host-agnostic).
/// Uses Railway-Oriented Programming with Result&lt;T&gt; and event broadcasting via IExxerHub&lt;T&gt;.
/// </summary>
public class IngestionOrchestrator
{
    private readonly IIngestionJournal _journal;
    private readonly IDocumentDownloader _downloader;
    private readonly IExxerHub<DocumentDownloadedEvent> _eventHub;
    private readonly ILogger<IngestionOrchestrator> _logger;
    private readonly string _storageBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionOrchestrator"/> class.
    /// </summary>
    /// <param name="journal">The ingestion journal for idempotency tracking.</param>
    /// <param name="downloader">The document downloader.</param>
    /// <param name="eventHub">The event hub for broadcasting DocumentDownloadedEvent.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="storageBasePath">Base path for document storage (defaults to ./storage).</param>
    public IngestionOrchestrator(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IExxerHub<DocumentDownloadedEvent> eventHub,
        ILogger<IngestionOrchestrator> logger,
        string? storageBasePath = null)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageBasePath = storageBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
    }

    /// <summary>
    /// Ingests a single document with idempotency, hashing, storage, and event emission.
    /// Uses Railway-Oriented Programming - no exceptions for control flow.
    /// </summary>
    /// <param name="documentId">SIARA document ID.</param>
    /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing IngestionResult on success, or error messages on failure.</returns>
    public async Task<Result<IngestionResult>> IngestDocumentAsync(
        string documentId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IngestionResult>();
        }

        _logger.LogInformation(
            "Starting document ingestion. DocumentId: {DocumentId}, CorrelationId: {CorrelationId}",
            documentId,
            correlationId);

        // ✅ Railway-Oriented Programming: each step returns Result<T>
        var result = await DownloadDocumentAsync(documentId, cancellationToken)
            .ThenAsync(async bytes => await CheckDuplicateAsync(bytes, documentId, cancellationToken))
            .ThenAsync(async context => await StoreDocumentAsync(context, documentId, cancellationToken))
            .ThenAsync(async context => await RecordInJournalAsync(context, documentId, cancellationToken))
            .ThenTap(async context => await BroadcastEventAsync(context, correlationId, cancellationToken));

        if (result.IsSuccess && result.Value is not null)
        {
            var context = result.Value;
            _logger.LogInformation(
                "Document ingestion completed. FileId: {FileId}, WasDuplicate: {WasDuplicate}",
                context.FileId,
                context.WasDuplicate);

            return Result<IngestionResult>.Success(new IngestionResult(
                FileId: context.FileId,
                FileName: context.FileName,
                Hash: context.Hash,
                StoredPath: context.StoredPath,
                FileSizeBytes: context.FileSizeBytes,
                CorrelationId: correlationId,
                WasDuplicate: context.WasDuplicate));
        }

        _logger.LogError("Document ingestion failed: {Errors}", string.Join(", ", result.Errors));
        return Result<IngestionResult>.WithFailure(result.Errors);
    }

    private async Task<Result<byte[]>> DownloadDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _downloader.DownloadAsync(documentId, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Document downloaded. Size: {Size} bytes", bytes.Length);
            return Result<byte[]>.Success(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document download cancelled");
            return ResultExtensions.Cancelled<byte[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document download failed");
            return Result<byte[]>.WithFailure($"Download failed: {ex.Message}");
        }
    }

    private async Task<Result<IngestionContext>> CheckDuplicateAsync(
        byte[] documentBytes,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        var hash = ComputeSha256Hash(documentBytes);
        _logger.LogDebug("Document hash computed: {Hash}", hash);

        var isDuplicate = await _journal.ExistsAsync(hash, sourceUrl, cancellationToken).ConfigureAwait(false);

        if (isDuplicate)
        {
            _logger.LogInformation("Duplicate document detected (hash: {Hash}, URL: {URL}). Skipping ingestion", hash, sourceUrl);

            // ✅ Return success with WasDuplicate=true (idempotent skip)
            return Result<IngestionContext>.Success(new IngestionContext(
                FileId: Guid.NewGuid(),
                FileName: string.Empty,
                Hash: hash,
                StoredPath: string.Empty,
                FileSizeBytes: documentBytes.Length,
                WasDuplicate: true,
                DocumentBytes: documentBytes));
        }

        return Result<IngestionContext>.Success(new IngestionContext(
            FileId: Guid.NewGuid(),
            FileName: string.Empty,
            Hash: hash,
            StoredPath: string.Empty,
            FileSizeBytes: documentBytes.Length,
            WasDuplicate: false,
            DocumentBytes: documentBytes));
    }

    private Task<Result<IngestionContext>> StoreDocumentAsync(
        IngestionContext context,
        string documentId,
        CancellationToken cancellationToken)
    {
        // Skip storage for duplicates
        if (context.WasDuplicate)
        {
            return Task.FromResult(Result<IngestionContext>.Success(context));
        }

        try
        {
            var now = DateTime.UtcNow;
            var partitionPath = Path.Combine(
                _storageBasePath,
                $"{now.Year:D4}",
                $"{now.Month:D2}",
                $"{now.Day:D2}");

            Directory.CreateDirectory(partitionPath);

            var fileName = $"{documentId}.pdf";
            var filePath = Path.Combine(partitionPath, fileName);

            File.WriteAllBytes(filePath, context.DocumentBytes);
            _logger.LogInformation("Document stored at: {FilePath}", filePath);

            var updatedContext = context with
            {
                FileName = fileName,
                StoredPath = filePath
            };

            return Task.FromResult(Result<IngestionContext>.Success(updatedContext));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document storage failed");
            return Task.FromResult(Result<IngestionContext>.WithFailure($"Storage failed: {ex.Message}"));
        }
    }

    private async Task<Result<IngestionContext>> RecordInJournalAsync(
        IngestionContext context,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        // Skip journal recording for duplicates (already exists)
        if (context.WasDuplicate)
        {
            return Result<IngestionContext>.Success(context);
        }

        try
        {
            var manifestEntry = new IngestionManifestEntry(
                FileId: context.FileId,
                FileName: context.FileName,
                SourceUrl: sourceUrl,
                ContentHash: context.Hash,
                FileSizeBytes: context.FileSizeBytes,
                StoredPath: context.StoredPath,
                CorrelationId: Guid.NewGuid(),
                DownloadedAt: DateTimeOffset.UtcNow);

            await _journal.RecordAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Document recorded in journal: {FileId}", context.FileId);
            return Result<IngestionContext>.Success(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Journal recording failed");
            return Result<IngestionContext>.WithFailure($"Journal recording failed: {ex.Message}");
        }
    }

    private async Task BroadcastEventAsync(
        IngestionContext context,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        // Skip event broadcast for duplicates
        if (context.WasDuplicate)
        {
            _logger.LogDebug("Skipping event broadcast for duplicate document");
            return;
        }

        var evt = new DocumentDownloadedEvent
        {
            FileId = context.FileId,
            FileName = context.FileName,
            Source = "SIARA",
            FileSizeBytes = context.FileSizeBytes,
            DownloadUrl = string.Empty,  // Set if available
            EventType = nameof(DocumentDownloadedEvent),
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        // ✅ Broadcast via IExxerHub<T> (transport-agnostic)
        await _eventHub.SendToAllAsync(evt, cancellationToken);

        _logger.LogInformation(
            "DocumentDownloadedEvent broadcast. FileId: {FileId}, CorrelationId: {CorrelationId}",
            context.FileId,
            correlationId);
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

    /// <summary>
    /// Internal context for passing data through the Railway-Oriented Programming pipeline.
    /// </summary>
    private sealed record IngestionContext(
        Guid FileId,
        string FileName,
        string Hash,
        string StoredPath,
        long FileSizeBytes,
        bool WasDuplicate,
        byte[] DocumentBytes);
}
