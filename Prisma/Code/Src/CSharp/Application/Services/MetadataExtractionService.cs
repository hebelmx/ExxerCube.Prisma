namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Orchestrates Stage 2 workflow: file type identification, metadata extraction (XML/DOCX/PDF), classification, file naming, and organization.
/// Integrates with existing OCR pipeline via IMetadataExtractor wrapper.
/// </summary>
public class MetadataExtractionService
{
    private readonly IFileTypeIdentifier _fileTypeIdentifier;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IFileClassifier _fileClassifier;
    private readonly ISafeFileNamer _safeFileNamer;
    private readonly IFileMover _fileMover;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<MetadataExtractionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataExtractionService"/> class.
    /// </summary>
    /// <param name="fileTypeIdentifier">The file type identifier service.</param>
    /// <param name="metadataExtractor">The composite metadata extractor.</param>
    /// <param name="fileClassifier">The file classifier service.</param>
    /// <param name="safeFileNamer">The safe file namer service.</param>
    /// <param name="fileMover">The file mover service.</param>
    /// <param name="auditLogger">The audit logger service.</param>
    /// <param name="logger">The logger instance.</param>
    public MetadataExtractionService(
        IFileTypeIdentifier fileTypeIdentifier,
        IMetadataExtractor metadataExtractor,
        IFileClassifier fileClassifier,
        ISafeFileNamer safeFileNamer,
        IFileMover fileMover,
        IAuditLogger auditLogger,
        ILogger<MetadataExtractionService> logger)
    {
        _fileTypeIdentifier = fileTypeIdentifier;
        _metadataExtractor = metadataExtractor;
        _fileClassifier = fileClassifier;
        _safeFileNamer = safeFileNamer;
        _fileMover = fileMover;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Processes a file through the complete Stage 2 workflow: identification → extraction → classification → naming → organization.
    /// </summary>
    /// <param name="filePath">The path to the file to process.</param>
    /// <param name="originalFileName">The original filename.</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the processing result with classification and new file path, or an error.</returns>
    public async Task<Result<MetadataExtractionResult>> ProcessFileAsync(
        string filePath,
        string originalFileName,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Metadata extraction cancelled before starting");
            return ResultExtensions.Cancelled<MetadataExtractionResult>();
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<MetadataExtractionResult>.WithFailure("File path cannot be null or empty.");
        }
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return Result<MetadataExtractionResult>.WithFailure("Original file name cannot be null or empty.");
        }
        if (!File.Exists(filePath))
        {
            return Result<MetadataExtractionResult>.WithFailure($"File not found: {filePath}");
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting metadata extraction for file: {FilePath} (CorrelationId: {CorrelationId})", filePath, actualCorrelationId);

            // Step 1: Identify file type based on content
            var fileContent = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var fileTypeResult = await _fileTypeIdentifier.IdentifyFileTypeAsync(fileContent, originalFileName, cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation from dependencies FIRST
            if (fileTypeResult.IsCancelled())
            {
                _logger.LogWarning("Metadata extraction cancelled by file type identifier");
                
                // Log audit for cancelled file type identification
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.Extraction,
                    fileId,
                    actualCorrelationId,
                    null,
                    $"{{\"FileName\":\"{originalFileName}\",\"FilePath\":\"{filePath}\"}}",
                    false,
                    "Operation cancelled",
                    cancellationToken).ConfigureAwait(false);
                
                return ResultExtensions.Cancelled<MetadataExtractionResult>();
            }
            
            if (fileTypeResult.IsFailure)
            {
                // Log audit for failed file type identification
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.Extraction,
                    fileId,
                    actualCorrelationId,
                    null,
                    $"{{\"FileName\":\"{originalFileName}\",\"FilePath\":\"{filePath}\"}}",
                    false,
                    fileTypeResult.Error ?? "File type identification failed",
                    cancellationToken).ConfigureAwait(false);
                
                return Result<MetadataExtractionResult>.WithFailure(fileTypeResult.Error!);
            }

            // Log audit for successful file type identification
            await _auditLogger.LogAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId,
                actualCorrelationId,
                null,
                $"{{\"FileName\":\"{originalFileName}\",\"FilePath\":\"{filePath}\"}}",
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            FileFormat fileFormat = fileTypeResult.Value ?? FileFormat.Unknown;
            _logger.LogDebug("Identified file type as: {FileFormat}", fileFormat);

            // Step 2: Extract metadata based on file type
            var metadataResult = await ExtractMetadataByTypeAsync(fileContent, fileFormat, cancellationToken).ConfigureAwait(false);
            
            // Log metadata extraction audit
            await _auditLogger.LogAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId,
                actualCorrelationId,
                null,
                $"{{\"FileName\":\"{originalFileName}\",\"FileFormat\":\"{fileFormat}\"}}",
                metadataResult.IsSuccess,
                metadataResult.IsFailure ? metadataResult.Error : null,
                cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation from dependencies
            if (metadataResult.IsCancelled())
            {
                _logger.LogWarning("Metadata extraction cancelled by metadata extractor");
                return ResultExtensions.Cancelled<MetadataExtractionResult>();
            }
            
            if (metadataResult.IsFailure)
            {
                return Result<MetadataExtractionResult>.WithFailure(metadataResult.Error!);
            }

            var metadata = metadataResult.Value;
            if (metadata == null)
            {
                return Result<MetadataExtractionResult>.WithFailure("Extracted metadata is null");
            }

            _logger.LogDebug("Extracted metadata successfully");

            // Step 3: Classify document
            var classificationResult = await _fileClassifier.ClassifyAsync(metadata, cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation from dependencies
            if (classificationResult.IsCancelled())
            {
                _logger.LogWarning("Metadata extraction cancelled by file classifier");
                return ResultExtensions.Cancelled<MetadataExtractionResult>();
            }
            
            if (classificationResult.IsFailure)
            {
                return Result<MetadataExtractionResult>.WithFailure(classificationResult.Error ?? "Classification failed");
            }

            var classification = classificationResult.Value;
            if (classification == null)
            {
                return Result<MetadataExtractionResult>.WithFailure("Classification returned null result");
            }

            // AC9: Log all classification decisions with confidence scores to audit trail
            var classificationDetails = $"{{\"Level1\":\"{classification.Level1}\",\"Level2\":\"{classification.Level2}\",\"Confidence\":{classification.Confidence},\"Scores\":{{\"Aseguramiento\":{classification.Scores.AseguramientoScore},\"Desembargo\":{classification.Scores.DesembargoScore},\"Documentacion\":{classification.Scores.DocumentacionScore},\"Informacion\":{classification.Scores.InformacionScore},\"Transferencia\":{classification.Scores.TransferenciaScore},\"OperacionesIlicitas\":{classification.Scores.OperacionesIlicitasScore}}}}}";
            
            // Log classification audit
            await _auditLogger.LogAuditAsync(
                AuditActionType.Classification,
                ProcessingStage.Extraction,
                fileId,
                actualCorrelationId,
                null,
                classificationDetails,
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Document classified as {Level1}/{Level2} with confidence {Confidence}%. " +
                "Detailed scores - Aseguramiento: {AseguramientoScore}, Desembargo: {DesembargoScore}, " +
                "Documentacion: {DocumentacionScore}, Informacion: {InformacionScore}, " +
                "Transferencia: {TransferenciaScore}, OperacionesIlicitas: {OperacionesIlicitasScore}",
                classification.Level1,
                classification.Level2,
                classification.Confidence,
                classification.Scores.AseguramientoScore,
                classification.Scores.DesembargoScore,
                classification.Scores.DocumentacionScore,
                classification.Scores.InformacionScore,
                classification.Scores.TransferenciaScore,
                classification.Scores.OperacionesIlicitasScore);

            // Step 4: Generate safe file name
            var fileNameResult = await _safeFileNamer.GenerateSafeFileNameAsync(originalFileName, classification, metadata, cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation from dependencies
            if (fileNameResult.IsCancelled())
            {
                _logger.LogWarning("Metadata extraction cancelled by safe file namer");
                return ResultExtensions.Cancelled<MetadataExtractionResult>();
            }
            
            if (fileNameResult.IsFailure)
            {
                return Result<MetadataExtractionResult>.WithFailure(fileNameResult.Error ?? "Failed to generate safe file name");
            }

            var safeFileName = fileNameResult.Value;
            if (string.IsNullOrEmpty(safeFileName))
            {
                return Result<MetadataExtractionResult>.WithFailure("Generated safe file name is null or empty");
            }

            _logger.LogDebug("Generated safe file name: {SafeFileName}", safeFileName);

            // Step 5: Move file to organized location
            var moveResult = await _fileMover.MoveFileAsync(filePath, classification, safeFileName, cancellationToken).ConfigureAwait(false);
            
            // Log file move audit
            await _auditLogger.LogAuditAsync(
                AuditActionType.Move,
                ProcessingStage.Extraction,
                fileId,
                actualCorrelationId,
                null,
                $"{{\"OriginalPath\":\"{filePath}\",\"SafeFileName\":\"{safeFileName}\",\"Classification\":\"{classification.Level1}/{classification.Level2}\"}}",
                moveResult.IsSuccess,
                moveResult.IsFailure ? moveResult.Error : null,
                cancellationToken).ConfigureAwait(false);
            
            // Propagate cancellation from dependencies
            if (moveResult.IsCancelled())
            {
                _logger.LogWarning("Metadata extraction cancelled by file mover");
                return ResultExtensions.Cancelled<MetadataExtractionResult>();
            }
            
            if (moveResult.IsFailure)
            {
                return Result<MetadataExtractionResult>.WithFailure(moveResult.Error ?? "Failed to move file");
            }

            var newFilePath = moveResult.Value;
            if (string.IsNullOrEmpty(newFilePath))
            {
                return Result<MetadataExtractionResult>.WithFailure("New file path is null or empty");
            }

            _logger.LogInformation("File organized to: {NewFilePath}", newFilePath);

            var result = new MetadataExtractionResult
            {
                OriginalFilePath = filePath,
                NewFilePath = newFilePath,
                Classification = classification,
                Metadata = metadata,
                FileFormat = fileFormat
            };

            return Result<MetadataExtractionResult>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Metadata extraction cancelled for {FilePath}", filePath);
            return ResultExtensions.Cancelled<MetadataExtractionResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            return Result<MetadataExtractionResult>.WithFailure($"Error processing file: {ex.Message}", default(MetadataExtractionResult), ex);
        }
    }

    private async Task<Result<ExtractedMetadata>> ExtractMetadataByTypeAsync(
        byte[] fileContent,
        FileFormat fileFormat,
        CancellationToken cancellationToken)
    {
        fileFormat ??= FileFormat.Unknown;
        return fileFormat.Name switch
        {
            nameof(FileFormat.Xml) => await _metadataExtractor.ExtractFromXmlAsync(fileContent, cancellationToken).ConfigureAwait(false),
            nameof(FileFormat.Docx) => await _metadataExtractor.ExtractFromDocxAsync(fileContent, cancellationToken).ConfigureAwait(false),
            nameof(FileFormat.Pdf) => await _metadataExtractor.ExtractFromPdfAsync(fileContent, cancellationToken).ConfigureAwait(false),
            _ => Result<ExtractedMetadata>.WithFailure($"Unsupported file format: {fileFormat}")
        };
    }
}
