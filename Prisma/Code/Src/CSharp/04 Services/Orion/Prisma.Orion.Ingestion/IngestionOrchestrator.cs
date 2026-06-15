using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _postWriteFlushDelay;

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
    /// <param name="timeProvider">
    /// Optional time provider; defaults to <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// Injected for testability so tests may use a fake provider or pass
    /// <paramref name="postWriteFlushDelay"/> of <see cref="TimeSpan.Zero"/> to avoid real delays.
    /// </param>
    /// <param name="postWriteFlushDelay">
    /// Optional I/O-race mitigation delay inserted after all case files are written and before the
    /// broadcast event is emitted, giving the filesystem time to flush. Defaults to 250 ms when
    /// <see langword="null"/>; pass <see cref="TimeSpan.Zero"/> to disable (e.g. in tests).
    /// MVP-PATH 2.1 (owner-approved).
    /// </param>
    public IngestionOrchestrator(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IExxerHub<DocumentDownloadedEvent> eventHub,
        ILogger<IngestionOrchestrator> logger,
        string? storageBasePath = null,
        IServiceScopeFactory? scopeFactory = null,
        ISiaraActorIdentityProvider? actorIdentityProvider = null,
        ProcessClearance processClearance = ProcessClearance.Download,
        TimeProvider? timeProvider = null,
        TimeSpan? postWriteFlushDelay = null)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageBasePath = storageBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
        _scopeFactory = scopeFactory;
        _actorIdentityProvider = actorIdentityProvider;
        _processClearance = processClearance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _postWriteFlushDelay = postWriteFlushDelay ?? TimeSpan.FromMilliseconds(250);
    }

    // -------------------------------------------------------------------------
    // Case ingestion (MVP-PATH 2.1) — one event per case, all companion files
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ingests all companion files of a <see cref="SiaraCase"/> with idempotency, hashing, storage, and a
    /// single event emission covering the whole case. Uses Railway-Oriented Programming — no exceptions for
    /// control flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The case FileId is <em>deterministic</em> — derived from <paramref name="siaraCase"/>'s
    /// <see cref="SiaraCase.CaseId"/> via an MD5 byte-hash so re-ingesting the same case always produces the
    /// same <c>FileId</c> on the emitted event (stable across restarts and re-discoveries).
    /// MD5 is used solely for id derivation, not for security — see <see cref="DeterministicGuid"/>.
    /// </para>
    /// <para>
    /// A single <see cref="DocumentDownloadedEvent"/> is broadcast with all <see cref="CaseFileReference"/>
    /// entries so the downstream Extractor can load every source without a second discovery round. If
    /// <em>all</em> files are already known to the journal (all duplicates) the event is suppressed and the
    /// call returns success with <see cref="IngestionResult.WasDuplicate"/> true.
    /// </para>
    /// <para>
    /// After writing all new files and before broadcasting, a configurable flush delay
    /// (<see cref="_postWriteFlushDelay"/>) is awaited to mitigate I/O-race conditions on the shared storage
    /// volume (owner-approved, MVP-PATH 2.1).
    /// </para>
    /// </remarks>
    /// <param name="siaraCase">The SIARA case bundle to ingest.</param>
    /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing <see cref="IngestionResult"/> on success, or error messages on failure.</returns>
    public async Task<Result<IngestionResult>> IngestCaseAsync(
        SiaraCase siaraCase,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<IngestionResult>();
        }

        ArgumentNullException.ThrowIfNull(siaraCase);

        var caseFileId = DeterministicGuid(siaraCase.CaseId);

        _logger.LogInformation(
            "Starting case ingestion. CaseId: {CaseId}, FileId: {FileId}, CorrelationId: {CorrelationId}, Files: {FileCount}",
            siaraCase.CaseId,
            caseFileId,
            correlationId,
            siaraCase.Files.Count);

        // Audit: case received (start of ingestion) — fail-open.
        await EmitAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId: caseFileId.ToString(),
            correlationId: correlationId.ToString(),
            success: true,
            actionKey: "DocumentReceived",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var caseRefs = new List<CaseFileReference>(siaraCase.Files.Count);

        // Helpers to track the primary file (PDF preferred, else first)
        string? primaryRelativePath = null;
        FileFormat primaryFormat = FileFormat.Unknown;
        string? primaryUrl = null;
        string? primaryFileName = null;
        long primarySizeBytes = 0;
        bool anyNew = false;
        bool anyMissing = false;
        string? lastActorId = null;
        string? lastSessionId = null;

        foreach (var file in siaraCase.Files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ResultExtensions.Cancelled<IngestionResult>();
            }

            // Download the individual file
            var downloadResult = await DownloadFileAsync(file.Url, cancellationToken).ConfigureAwait(false);
            if (downloadResult.IsCancelled())
            {
                return ResultExtensions.Cancelled<IngestionResult>();
            }

            if (downloadResult.IsFailure || downloadResult.Value is null)
            {
                _logger.LogError(
                    "Failed to download case file {Url} for case {CaseId}: {Errors}",
                    file.Url, siaraCase.CaseId, string.Join(", ", downloadResult.Errors));

                // Audit: this INDIVIDUAL case file could not be downloaded. Use a distinct action key
                // ("CaseFileDownloadFailed") so this expected best-effort skip — which fires on the
                // owner's normal ~5-15% partial-case scenario — is NOT conflated with the terminal
                // "IngestionFailed" emitted only when the WHOLE case yields zero files. Monitors can then
                // exclude expected per-file skips from case-level failure counts.
                await EmitAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    fileId: caseFileId.ToString(),
                    correlationId: correlationId.ToString(),
                    success: false,
                    actionKey: "CaseFileDownloadFailed",
                    errorMessage: string.Join(", ", downloadResult.Errors),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Best-effort: a missing file does NOT fail the whole case.
                // Track the gap and continue to the next file (issue #4, owner ruling 2026-06-13).
                anyMissing = true;
                continue;
            }

            var downloaded = downloadResult.Value;
            lastActorId = downloaded.AcquiredBy.ActorId;
            lastSessionId = downloaded.SessionId;

            var hash = ComputeSha256Hash(downloaded.Content);
            var isDuplicate = await _journal.ExistsAsync(hash, file.Url, cancellationToken).ConfigureAwait(false);

            var relativePath = BuildRelativePath(now, siaraCase.CaseId, file.FileName);

            if (!isDuplicate)
            {
                // Store the file under YYYY/MM/DD/{caseId}/{fileName}
                var storeResult = StoreFileBytes(downloaded.Content, relativePath);
                if (storeResult.IsFailure)
                {
                    await EmitAuditAsync(
                        AuditActionType.Download,
                        ProcessingStage.Ingestion,
                        fileId: caseFileId.ToString(),
                        correlationId: correlationId.ToString(),
                        success: false,
                        actionKey: "IngestionFailed",
                        errorMessage: string.Join(", ", storeResult.Errors),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    return Result<IngestionResult>.WithFailure(storeResult.Errors);
                }

                // Record in journal
                var journalResult = await RecordFileInJournalAsync(
                    caseFileId, file.FileName, file.Url, hash,
                    downloaded.Content.LongLength, relativePath,
                    downloaded.AcquiredBy.ActorId, downloaded.SessionId,
                    cancellationToken).ConfigureAwait(false);

                if (journalResult.IsFailure)
                {
                    await EmitAuditAsync(
                        AuditActionType.Download,
                        ProcessingStage.Ingestion,
                        fileId: caseFileId.ToString(),
                        correlationId: correlationId.ToString(),
                        success: false,
                        actionKey: "IngestionFailed",
                        errorMessage: string.Join(", ", journalResult.Errors),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    return Result<IngestionResult>.WithFailure(journalResult.Errors);
                }

                anyNew = true;

                _logger.LogDebug(
                    "Stored case file {FileName} at {RelativePath} for case {CaseId}",
                    file.FileName, relativePath, siaraCase.CaseId);
            }
            else
            {
                // issue #3: a duplicate's bytes live where they were FIRST stored — possibly an earlier
                // day's partition — NOT under today's recomputed path. Reuse the journaled stored path so
                // the emitted CaseFileReference points at bytes that actually exist on disk (otherwise the
                // Extractor's companion resolution drops this source from fusion).
                var originalPath = await _journal.TryGetStoredPathAsync(hash, file.Url, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(originalPath))
                {
                    relativePath = originalPath;
                }
                else
                {
                    _logger.LogWarning(
                        "Duplicate file {FileName} for case {CaseId} has no journaled stored path; using recomputed partition {RelativePath} (may not exist on disk).",
                        file.FileName, siaraCase.CaseId, relativePath);
                }

                _logger.LogDebug(
                    "Duplicate file resolved to original stored path: {FileName} (hash: {Hash}) -> {RelativePath} for case {CaseId}",
                    file.FileName, hash, relativePath, siaraCase.CaseId);
            }

            // Always collect a CaseFileReference — duplicate or new — so the event lists all case files.
            caseRefs.Add(new CaseFileReference
            {
                RelativePath = relativePath,
                Format = file.Format,
            });

            // Primary file: prefer PDF; otherwise use the first file encountered.
            if (primaryRelativePath is null || (file.Format == FileFormat.Pdf && primaryFormat != FileFormat.Pdf))
            {
                primaryRelativePath = relativePath;
                primaryFormat = file.Format;
                primaryUrl = file.Url;
                primaryFileName = file.FileName;
                primarySizeBytes = downloaded.Content.LongLength;
            }
        }

        _logger.LogInformation(
            "Case {CaseId} processing complete: {ObtainedCount}/{ExpectedCount} file(s) obtained, anyNew={AnyNew}, anyMissing={AnyMissing}",
            siaraCase.CaseId, caseRefs.Count, siaraCase.Files.Count, anyNew, anyMissing);

        // Hard fail: zero files were obtained — nothing to process.
        if (caseRefs.Count == 0)
        {
            var noFilesMessage = $"Case {siaraCase.CaseId}: no files could be downloaded";
            _logger.LogError("Case ingestion abandoned — {Message}", noFilesMessage);

            await EmitAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                fileId: caseFileId.ToString(),
                correlationId: correlationId.ToString(),
                success: false,
                actionKey: "IngestionFailed",
                errorMessage: noFilesMessage,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result<IngestionResult>.WithFailure(noFilesMessage);
        }

        if (!anyNew)
        {
            // All files were duplicates — idempotent skip, no broadcast.
            await EmitAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                fileId: caseFileId.ToString(),
                correlationId: correlationId.ToString(),
                success: true,
                actionKey: "DuplicateSkipped",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result<IngestionResult>.Success(new IngestionResult(
                FileId: caseFileId,
                FileName: primaryFileName ?? string.Empty,
                Hash: string.Empty,
                StoredPath: primaryRelativePath ?? string.Empty,
                FileSizeBytes: primarySizeBytes,
                CorrelationId: correlationId,
                WasDuplicate: true,
                IsComplete: true)); // Duplicate: the case was fully seen before.
        }

        // Post-write flush delay: give the filesystem time to flush before the Extractor acts on the event.
        if (_postWriteFlushDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(_postWriteFlushDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ResultExtensions.Cancelled<IngestionResult>();
            }
        }

        // Broadcast ONE event covering the case (complete or best-effort partial).
        var evt = new DocumentDownloadedEvent
        {
            FileId = caseFileId,
            FileName = primaryFileName ?? string.Empty,
            Source = "SIARA",
            FileSizeBytes = primarySizeBytes,
            Format = primaryFormat,
            DownloadUrl = primaryUrl ?? string.Empty,
            Path = primaryRelativePath ?? string.Empty,
            EventType = nameof(DocumentDownloadedEvent),
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            CaseFiles = caseRefs,
            IsComplete = caseRefs.Count == siaraCase.Files.Count,
        };

        await _eventHub.SendToAllAsync(evt, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "DocumentDownloadedEvent broadcast for case {CaseId}. FileId: {FileId}, CaseFiles: {Count}, CorrelationId: {CorrelationId}",
            siaraCase.CaseId, caseFileId, caseRefs.Count, correlationId);

        // Audit: stored successfully.
        await EmitAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId: caseFileId.ToString(),
            correlationId: correlationId.ToString(),
            success: true,
            actionKey: "DocumentStored",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Result<IngestionResult>.Success(new IngestionResult(
            FileId: caseFileId,
            FileName: primaryFileName ?? string.Empty,
            Hash: string.Empty,
            StoredPath: primaryRelativePath ?? string.Empty,
            FileSizeBytes: primarySizeBytes,
            CorrelationId: correlationId,
            WasDuplicate: false,
            IsComplete: evt.IsComplete));
    }

    // -------------------------------------------------------------------------
    // Private shared helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Downloads a single file from the given URL.
    /// Pass-through to the downloader port (Railway-Oriented, fail-closed).
    /// </summary>
    private async Task<Result<DownloadedDocument>> DownloadFileAsync(
        string fileUrl,
        CancellationToken cancellationToken)
    {
        var result = await _downloader.DownloadAsync(fileUrl, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            _logger.LogDebug(
                "File downloaded. Size: {Size} bytes, Actor: {ActorId}, Session: {SessionId}",
                result.Value.Content.Length,
                result.Value.AcquiredBy.ActorId,
                result.Value.SessionId);
        }

        return result;
    }

    /// <summary>
    /// Builds the storage-relative path for a case file: <c>YYYY/MM/DD/{caseId}/{fileName}</c>.
    /// Path segments are sanitized to prevent directory traversal.
    /// </summary>
    private static string BuildRelativePath(DateTime utcNow, string caseId, string fileName)
    {
        var safeCaseId = SanitizePathSegment(caseId);
        var safeFileName = SanitizePathSegment(fileName);
        return $"{utcNow.Year:D4}/{utcNow.Month:D2}/{utcNow.Day:D2}/{safeCaseId}/{safeFileName}";
    }

    /// <summary>
    /// Removes directory-traversal sequences and invalid path characters from a path segment so it is safe
    /// to compose into a storage path without risking an escape above the storage root.
    /// </summary>
    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "_empty_";
        }

        // Strip directory-separator characters and dot-dot sequences.
        var result = segment
            .Replace("..", string.Empty, StringComparison.Ordinal)
            .Replace('/', '_')
            .Replace('\\', '_');

        // Remove any remaining characters that are invalid in a file-system path name.
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            result = result.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "_empty_" : result;
    }

    /// <summary>
    /// Writes the file bytes to the storage path derived from the relative path and the storage base.
    /// Returns a failure result if the write fails; never throws.
    /// </summary>
    private Result<bool> StoreFileBytes(byte[] content, string relativePath)
    {
        try
        {
            // Forward-slash relative path → OS-native path under the storage base.
            var nativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_storageBasePath, nativePath);
            var dir = Path.GetDirectoryName(fullPath)!;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, content);

            _logger.LogDebug("File stored at: {FullPath}", fullPath);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage failed for relative path {RelativePath}", relativePath);
            return Result<bool>.WithFailure($"Storage failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Records a file entry in the ingestion journal so re-discovery is a no-op (SHA-256 idempotency).
    /// Returns a failure result if recording fails; never throws.
    /// </summary>
    private async Task<Result<bool>> RecordFileInJournalAsync(
        Guid fileId,
        string fileName,
        string sourceUrl,
        string hash,
        long fileSizeBytes,
        string relativePath,
        string actorId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifestEntry = new IngestionManifestEntry(
                FileId: fileId,
                FileName: fileName,
                SourceUrl: sourceUrl,
                ContentHash: hash,
                FileSizeBytes: fileSizeBytes,
                StoredPath: relativePath,
                CorrelationId: Guid.NewGuid(),
                DownloadedAt: DateTimeOffset.UtcNow,
                ActorId: actorId,
                SessionId: sessionId);

            await _journal.RecordAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("File recorded in journal: {FileId} / {FileName}", fileId, fileName);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Journal recording failed for {FileName}", fileName);
            return Result<bool>.WithFailure($"Journal recording failed: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Deterministic case FileId
    // -------------------------------------------------------------------------

    /// <summary>
    /// Derives a deterministic <see cref="Guid"/> from a case id string using MD5 over the UTF-8 bytes.
    /// MD5 is used here solely for stable id derivation (not for security or collision resistance) so
    /// repeated ingestion of the same case always produces the same <see cref="Guid"/>.
    /// </summary>
    // Not a security use — suppress the CA5351 "Do not use broken cryptographic algorithms" warning.
    // MD5 is used only to deterministically map a string key → 16-byte Guid.
    private static Guid DeterministicGuid(string caseId)
    {
#pragma warning disable CA5351
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(caseId));
#pragma warning restore CA5351
        return new Guid(hash);
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
}
