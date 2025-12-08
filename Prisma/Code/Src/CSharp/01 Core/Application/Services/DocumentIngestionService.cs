using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Orchestrates Stage 1 workflow: browser automation, file download, duplicate detection, storage, and metadata logging.
/// </summary>
public class DocumentIngestionService
{
    private readonly IBrowserAutomationAgent _browserAutomationAgent;
    private readonly IDownloadTracker _downloadTracker;
    private readonly IDownloadStorage _downloadStorage;
    private readonly IFileMetadataLogger _fileMetadataLogger;
    private readonly IAuditLogger _auditLogger;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DocumentIngestionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionService"/> class.
    /// </summary>
    /// <param name="browserAutomationAgent">The browser automation agent.</param>
    /// <param name="downloadTracker">The download tracker for duplicate detection.</param>
    /// <param name="downloadStorage">The download storage adapter.</param>
    /// <param name="fileMetadataLogger">The file metadata logger.</param>
    /// <param name="auditLogger">The audit logger service.</param>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentIngestionService(
        IBrowserAutomationAgent browserAutomationAgent,
        IDownloadTracker downloadTracker,
        IDownloadStorage downloadStorage,
        IFileMetadataLogger fileMetadataLogger,
        IAuditLogger auditLogger,
        IEventPublisher eventPublisher,
        ILogger<DocumentIngestionService> logger)
    {
        _browserAutomationAgent = browserAutomationAgent;
        _downloadTracker = downloadTracker;
        _downloadStorage = downloadStorage;
        _fileMetadataLogger = fileMetadataLogger;
        _auditLogger = auditLogger;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Ingests documents from a regulatory website by downloading new files and logging metadata.
    /// </summary>
    /// <param name="websiteUrl">The URL of the regulatory website.</param>
    /// <param name="filePatterns">Array of file patterns to match (e.g., "*.pdf", "*.xml", "*.docx").</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of ingested file metadata or an error.</returns>
    public async Task<Result<List<FileMetadata>>> IngestDocumentsAsync(
        string websiteUrl,
        string[] filePatterns,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(websiteUrl))
        {
            return Result<List<FileMetadata>>.WithFailure("Website URL cannot be null or empty");
        }

        if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result<List<FileMetadata>>.WithFailure($"Invalid URL format: {websiteUrl}. Must be a valid HTTP or HTTPS URL.");
        }

        if (filePatterns == null || filePatterns.Length == 0)
        {
            return Result<List<FileMetadata>>.WithFailure("File patterns cannot be null or empty");
        }

        if (filePatterns.Any(string.IsNullOrWhiteSpace))
        {
            return Result<List<FileMetadata>>.WithFailure("File patterns cannot contain null or empty values");
        }

        // Generate correlation ID for this ingestion operation
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting document ingestion from {WebsiteUrl} (CorrelationId: {CorrelationId})", websiteUrl, correlationId);

        try
        {
            // Step 1: Launch browser
            var launchResult = await _browserAutomationAgent.LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation FIRST
            if (launchResult.IsCancelled())
            {
                _logger.LogWarning("Browser launch cancelled");
                return ResultExtensions.Cancelled<List<FileMetadata>>();
            }

            if (launchResult.IsFailure)
            {
                // Log audit for failed browser launch
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    null,
                    correlationId,
                    null,
                    $"{{\"WebsiteUrl\":\"{websiteUrl}\",\"Action\":\"BrowserLaunch\"}}",
                    false,
                    launchResult.Error,
                    cancellationToken).ConfigureAwait(false);
                
                return Result<List<FileMetadata>>.WithFailure($"Failed to launch browser: {launchResult.Error}");
            }

            // Log audit for successful browser launch
            await _auditLogger.LogAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                null,
                correlationId,
                null,
                $"{{\"WebsiteUrl\":\"{websiteUrl}\",\"Action\":\"BrowserLaunch\"}}",
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            // Step 2: Navigate to website
            var navigateResult = await _browserAutomationAgent.NavigateToAsync(websiteUrl, cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation FIRST
            if (navigateResult.IsCancelled())
            {
                var _ = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Navigation cancelled");
                return ResultExtensions.Cancelled<List<FileMetadata>>();
            }

            if (navigateResult.IsFailure)
            {
                // Log audit for failed navigation
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    null,
                    correlationId,
                    null,
                    $"{{\"WebsiteUrl\":\"{websiteUrl}\",\"Action\":\"Navigate\"}}",
                    false,
                    navigateResult.Error,
                    cancellationToken).ConfigureAwait(false);
                
                var _ = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                return Result<List<FileMetadata>>.WithFailure($"Failed to navigate to website: {navigateResult.Error}");
            }

            // Log audit for successful navigation
            await _auditLogger.LogAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                null,
                correlationId,
                null,
                $"{{\"WebsiteUrl\":\"{websiteUrl}\",\"Action\":\"Navigate\"}}",
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            // Step 3: Identify downloadable files
            var identifyResult = await _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, cancellationToken).ConfigureAwait(false);
            if (identifyResult.IsSuccess)
            {
                var downloadableFiles = identifyResult.Value;
                if (downloadableFiles == null)
                {
                    var _ = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                    return Result<List<FileMetadata>>.WithFailure("Failed to identify downloadable files: No files found");
                }

                _logger.LogInformation("Found {Count} downloadable files", downloadableFiles.Count);

                var ingestedFiles = new List<FileMetadata>();

                // Step 4: Process each file
                foreach (var downloadableFile in downloadableFiles)
                {
                    var processResult = await ProcessFileAsync(downloadableFile, correlationId, cancellationToken).ConfigureAwait(false);
                    if (processResult.IsSuccess)
                    {
                        var fileMetadata = processResult.Value;
                        if (fileMetadata != null)
                        {
                            ingestedFiles.Add(fileMetadata);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to process file {FileName}: {Error}", downloadableFile.FileName, processResult.Error);
                    }
                }

                // Step 5: Close browser
                var closeResult = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                if (closeResult.IsFailure)
                {
                    _logger.LogWarning("Failed to close browser: {Error}", closeResult.Error);
                }

                _logger.LogInformation("Document ingestion completed. Ingested {Count} files", ingestedFiles.Count);
                return Result<List<FileMetadata>>.Success(ingestedFiles);
            }
            else
            {
                var _ = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                return Result<List<FileMetadata>>.WithFailure($"Failed to identify downloadable files: {identifyResult.Error ?? "Unknown error"}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document ingestion cancelled for {WebsiteUrl}", websiteUrl);
            
            // Ensure browser is closed on cancellation
            try
            {
                var closeBrowserResult = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                if (closeBrowserResult.IsFailure)
                {
                    _logger.LogWarning("Failed to close browser after cancellation: {Error}", closeBrowserResult.Error);
                }
            }
            catch (Exception closeEx)
            {
                _logger.LogError(closeEx, "Failed to close browser after cancellation");
            }

            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document ingestion from {WebsiteUrl}", websiteUrl);
            
            // Ensure browser is closed even on exception
            try
            {
                var closeBrowserResult = await _browserAutomationAgent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
                if (closeBrowserResult.IsFailure)
                {
                    _logger.LogError("Failed to close browser after exception: {Error}", closeBrowserResult.Error);
                }
            }
            catch (Exception closeEx)
            {
                _logger.LogError(closeEx, "Failed to close browser after exception");
            }

            return Result<List<FileMetadata>>.WithFailure($"Unexpected error during document ingestion: {ex.Message}", default, ex);
        }
    }

    private async Task<Result<FileMetadata?>> ProcessFileAsync(
        DownloadableFile downloadableFile,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Download file
            var downloadResult = await _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, cancellationToken).ConfigureAwait(false);
            
            // Log download audit
            await _auditLogger.LogAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                null,
                correlationId,
                null,
                $"{{\"FileName\":\"{downloadableFile.FileName}\",\"Url\":\"{downloadableFile.Url}\",\"Format\":\"{downloadableFile.Format}\"}}",
                downloadResult.IsSuccess,
                downloadResult.IsFailure ? downloadResult.Error : null,
                cancellationToken).ConfigureAwait(false);

            if (downloadResult.IsSuccess)
            {
                var downloadedFile = downloadResult.Value;
                if (downloadedFile == null)
                {
                    return Result<FileMetadata?>.WithFailure("Failed to download file: No file downloaded");
                }

                // Step 2: Compute checksum
                var checksum = ComputeChecksum(downloadedFile.Content);

                // Step 3: Check for duplicates
                var duplicateResult = await _downloadTracker.IsDuplicateAsync(checksum, cancellationToken).ConfigureAwait(false);
                
                // Log duplicate detection audit
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Download,
                    ProcessingStage.Ingestion,
                    null,
                    correlationId,
                    null,
                    $"{{\"FileName\":\"{downloadableFile.FileName}\",\"Checksum\":\"{checksum}\"}}",
                    duplicateResult.IsSuccess,
                    duplicateResult.IsFailure ? duplicateResult.Error : null,
                    cancellationToken).ConfigureAwait(false);
                if (duplicateResult.IsSuccess)
                {
                    var isDuplicate = duplicateResult.Value;
                    if (isDuplicate)
                    {
                        _logger.LogInformation("Skipping duplicate file: {FileName} (checksum: {Checksum})", downloadableFile.FileName, checksum);
                        return Result<FileMetadata?>.Success(null); // Not an error, just skip
                    }

                    // Step 4: Save file to storage
                    var saveResult = await _downloadStorage.SaveFileAsync(
                        downloadedFile.Content,
                        downloadedFile.FileName,
                        downloadedFile.Format,
                        cancellationToken).ConfigureAwait(false);

                    if (saveResult.IsSuccess)
                    {
                        var storagePath = saveResult.Value;
                        if (storagePath == null)
                        {
                            return Result<FileMetadata?>.WithFailure("Failed to save file: No storage path returned");
                        }

                        // Step 5: Create file metadata
                        var fileMetadata = new FileMetadata
                        {
                            FileId = Guid.NewGuid().ToString(),
                            FileName = downloadedFile.FileName,
                            FilePath = storagePath,
                            Url = downloadableFile.Url,
                            DownloadTimestamp = DateTime.UtcNow,
                            Checksum = checksum,
                            FileSize = downloadedFile.FileSize,
                            Format = downloadedFile.Format
                        };

                        // Log storage audit
                        await _auditLogger.LogAuditAsync(
                            AuditActionType.Download,
                            ProcessingStage.Ingestion,
                            fileMetadata.FileId,
                            correlationId,
                            null,
                            $"{{\"FileName\":\"{downloadedFile.FileName}\",\"FilePath\":\"{storagePath}\",\"FileSize\":{downloadedFile.FileSize}}}",
                            true,
                            null,
                            cancellationToken).ConfigureAwait(false);

                        // Step 6: Log metadata to database
                        var logResult = await _fileMetadataLogger.LogFileMetadataAsync(fileMetadata, cancellationToken).ConfigureAwait(false);
                        
                        // Log metadata logging audit
                        await _auditLogger.LogAuditAsync(
                            AuditActionType.Download,
                            ProcessingStage.Ingestion,
                            fileMetadata.FileId,
                            correlationId,
                            null,
                            $"{{\"FileName\":\"{downloadedFile.FileName}\",\"FileId\":\"{fileMetadata.FileId}\"}}",
                            logResult.IsSuccess,
                            logResult.IsFailure ? logResult.Error : null,
                            cancellationToken).ConfigureAwait(false);

                        if (logResult.IsFailure)
                        {
                            _logger.LogWarning("Failed to log file metadata for {FileName}: {Error}", downloadableFile.FileName, logResult.Error);
                            // Continue even if logging fails - file is saved
                        }

                        // Publish DocumentDownloadedEvent for real-time monitoring
                        var correlationGuid = Guid.TryParse(correlationId, out var corrId) ? corrId : (Guid?)null;
                        _eventPublisher.Publish(new DocumentDownloadedEvent
                        {
                            FileId = Guid.Parse(fileMetadata.FileId),
                            FileName = downloadedFile.FileName,
                            Source = "SIARA", // TODO: Make this configurable based on source
                            FileSizeBytes = downloadedFile.FileSize,
                            Format = fileMetadata.Format,
                            DownloadUrl = downloadableFile.Url,
                            CorrelationId = correlationGuid
                        });

                        _logger.LogInformation("Successfully processed file: {FileName} (FileId: {FileId})", downloadableFile.FileName, fileMetadata.FileId);
                        return Result<FileMetadata?>.Success(fileMetadata);
                    }
                    else
                    {
                        return Result<FileMetadata?>.WithFailure($"Failed to save file: {saveResult.Error ?? "Unknown error"}");
                    }
                }
                else
                {
                    return Result<FileMetadata?>.WithFailure($"Failed to check for duplicates: {duplicateResult.Error ?? "Unknown error"}");
                }
            }
            else
            {
                return Result<FileMetadata?>.WithFailure($"Failed to download file: {downloadResult.Error ?? "Unknown error"}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("File processing cancelled for {FileName}", downloadableFile.FileName);
            return ResultExtensions.Cancelled<FileMetadata?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FileName}", downloadableFile.FileName);
            return Result<FileMetadata?>.WithFailure($"Error processing file: {ex.Message}", default, ex);
        }
    }

    private static string ComputeChecksum(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(content);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}

