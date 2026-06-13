using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISiaraActorIdentityProvider? _actorIdentityProvider;
    private readonly ProcessClearance _processClearance;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionOrchestrator"/> class.
    /// </summary>
    /// <param name="journal">The ingestion journal for idempotency tracking.</param>
    /// <param name="downloader">The document downloader.</param>
    /// <param name="eventHub">The event hub for broadcasting DocumentDownloadedEvent.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="storageBasePath">Base path for document storage (defaults to ./storage).</param>
    /// <param name="scopeFactory">
    /// Optional scope factory used to resolve <see cref="IAuditLogger"/> per audit call (avoids captive
    /// dependency: IAuditLogger is scoped, IngestionOrchestrator may be scoped in the worker host).
    /// When <see langword="null"/>, audit calls are silently skipped (no-op). MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="actorIdentityProvider">
    /// Optional provider of the current process actor identity. When <see langword="null"/>, audit records
    /// use a degraded identity ("system") — fail-open so the pipeline is never blocked by an audit failure.
    /// MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="processClearance">
    /// The clearance level of this process, stamped on every audit record's <c>ActionDetails</c> JSON.
    /// Defaults to <see cref="ProcessClearance.Download"/> (Orion Downloader). MVP-PATH 1.6 A6.
    /// </param>
    public IngestionOrchestrator(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IExxerHub<DocumentDownloadedEvent> eventHub,
        ILogger<IngestionOrchestrator> logger,
        string? storageBasePath = null,
        IServiceScopeFactory? scopeFactory = null,
        ISiaraActorIdentityProvider? actorIdentityProvider = null,
        ProcessClearance processClearance = ProcessClearance.Download)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageBasePath = storageBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
        _scopeFactory = scopeFactory;
        _actorIdentityProvider = actorIdentityProvider;
        _processClearance = processClearance;
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

        // Audit: document received (start of ingestion) — fail-open: pipeline continues on audit failure.
        await EmitAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId: null,
            correlationId: correlationId.ToString(),
            success: true,
            actionKey: "DocumentReceived",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // ✅ Railway-Oriented Programming: each step returns Result<T>
        var result = await DownloadDocumentAsync(documentId, cancellationToken)
            .ThenAsync(async document => await CheckDuplicateAsync(document, documentId, cancellationToken))
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

            if (context.WasDuplicate)
            {
                // Audit: duplicate-skipped.
                await EmitAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    fileId: context.FileId.ToString(),
                    correlationId: correlationId.ToString(),
                    success: true,
                    actionKey: "DuplicateSkipped",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Audit: stored successfully.
                await EmitAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    fileId: context.FileId.ToString(),
                    correlationId: correlationId.ToString(),
                    success: true,
                    actionKey: "DocumentStored",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

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

        // Audit: ingestion failed.
        await EmitAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId: null,
            correlationId: correlationId.ToString(),
            success: false,
            actionKey: "IngestionFailed",
            errorMessage: string.Join(", ", result.Errors),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Result<IngestionResult>.WithFailure(result.Errors);
    }

    private async Task<Result<DownloadedDocument>> DownloadDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        // The downloader port is Railway-Oriented: it returns Result<DownloadedDocument> and fails closed
        // rather than throwing, so this is a pass-through (the provenance rides along on the document).
        var result = await _downloader.DownloadAsync(documentId, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            _logger.LogDebug(
                "Document downloaded. Size: {Size} bytes, Actor: {ActorId}, Session: {SessionId}",
                result.Value.Content.Length,
                result.Value.AcquiredBy.ActorId,
                result.Value.SessionId);
        }

        return result;
    }

    private async Task<Result<IngestionContext>> CheckDuplicateAsync(
        DownloadedDocument document,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        var documentBytes = document.Content;
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
                DocumentBytes: documentBytes,
                ActorId: document.AcquiredBy.ActorId,
                SessionId: document.SessionId,
                SourceUrl: document.SourceUrl));
        }

        return Result<IngestionContext>.Success(new IngestionContext(
            FileId: Guid.NewGuid(),
            FileName: string.Empty,
            Hash: hash,
            StoredPath: string.Empty,
            FileSizeBytes: documentBytes.Length,
            WasDuplicate: false,
            DocumentBytes: documentBytes,
            ActorId: document.AcquiredBy.ActorId,
            SessionId: document.SessionId,
            SourceUrl: document.SourceUrl));
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

            // The storage-relative path (forward-slash, mount-path independent) is what crosses to the
            // Extractor; it resolves it against its own shared-storage base (ADR-011).
            var relativeStoragePath = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}/{fileName}";

            var updatedContext = context with
            {
                FileName = fileName,
                StoredPath = filePath,
                RelativeStoragePath = relativeStoragePath
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
                DownloadedAt: DateTimeOffset.UtcNow,
                ActorId: context.ActorId,
                SessionId: context.SessionId);

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
            DownloadUrl = context.SourceUrl,
            // Storage-relative path so the Extractor resolves the file against its own shared-storage base
            // (ADR-011) — the two processes may mount the shared volume at different absolute paths.
            Path = context.RelativeStoragePath,
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

    // -------------------------------------------------------------------------
    // Per-process audit helpers (MVP-PATH 1.6 A6)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits a single audit record stamped with the current process identity.
    /// Fail-open: any audit failure is logged at Warning and swallowed so the pipeline is never blocked.
    /// </summary>
    private async Task EmitAuditAsync(
        AuditActionType actionType,
        ProcessingStage stage,
        string? fileId,
        string correlationId,
        bool success,
        string actionKey,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory is null)
        {
            return; // Audit is not wired — silent no-op.
        }

        try
        {
            var (actorId, details) = await ResolveProcessIdentityAsync(actionKey, cancellationToken)
                .ConfigureAwait(false);

            using var scope = _scopeFactory.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAuditAsync(
                actionType,
                stage,
                fileId,
                correlationId,
                userId: actorId,
                actionDetails: details,
                success: success,
                errorMessage: errorMessage,
                cancellationToken: cancellationToken,
                processId: actorId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fail-open: audit failure must never crash the pipeline.
            _logger.LogWarning(ex,
                "Audit emission failed for action {Action} (fail-open — pipeline continues). CorrelationId: {CorrelationId}",
                actionKey, correlationId);
        }
    }

    /// <summary>
    /// Resolves the current actor identity and builds the process-identity audit-details JSON.
    /// Fail-open: if the provider is null or resolution fails, returns a degraded ("unknown-process") identity.
    /// </summary>
    private async Task<(string actorId, string auditDetails)> ResolveProcessIdentityAsync(
        string action,
        CancellationToken ct)
    {
        if (_actorIdentityProvider is null)
        {
            return ("system", JsonSerializer.Serialize(new { action }));
        }

        var actorResult = await _actorIdentityProvider.GetCurrentActorAsync(ct).ConfigureAwait(false);
        if (actorResult.IsFailure)
        {
            _logger.LogWarning(
                "Process identity resolution failed for Orion audit (fail-open). Error: {Error}",
                string.Join(", ", actorResult.Errors));
            return ("unknown-process", JsonSerializer.Serialize(new
            {
                action,
                error = string.Join(", ", actorResult.Errors)
            }));
        }

        var actor = actorResult.Value!;
        return (actor.ActorId, BuildProcessAuditDetails(actor, _processClearance, action));
    }

    /// <summary>
    /// Serializes the process-identity payload that rides in <c>ActionDetails</c> on every worker audit record.
    /// </summary>
    private static string BuildProcessAuditDetails(SiaraActor actor, ProcessClearance clearance, string action)
        => JsonSerializer.Serialize(new
        {
            processId = actor.ActorId,
            processType = actor.ActorType.ToString(),
            processClearance = clearance.ToString(),
            displayName = actor.DisplayName,
            action
        });

    private static string ComputeSha256Hash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Internal context for passing data through the Railway-Oriented Programming pipeline.
    /// Carries the provenance (actor + session + source URL) resolved by the downloader so it can be
    /// stamped onto the journal manifest for per-document non-repudiation (ADR-010 P2).
    /// </summary>
    private sealed record IngestionContext(
        Guid FileId,
        string FileName,
        string Hash,
        string StoredPath,
        long FileSizeBytes,
        bool WasDuplicate,
        byte[] DocumentBytes,
        string ActorId,
        string SessionId,
        string SourceUrl)
    {
        /// <summary>
        /// The storage-relative path (forward-slash, <c>YYYY/MM/DD/{documentId}.pdf</c>) the document was
        /// stored under, carried onto the cross-process event so the Extractor can resolve it against its own
        /// shared-storage base (ADR-011). Empty for duplicates (which are not re-stored or broadcast).
        /// </summary>
        public string RelativeStoragePath { get; init; } = string.Empty;
    }
}
