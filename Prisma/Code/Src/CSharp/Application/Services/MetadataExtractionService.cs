using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<MetadataExtractionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataExtractionService"/> class.
    /// </summary>
    /// <param name="fileTypeIdentifier">The file type identifier service.</param>
    /// <param name="metadataExtractor">The composite metadata extractor.</param>
    /// <param name="fileClassifier">The file classifier service.</param>
    /// <param name="safeFileNamer">The safe file namer service.</param>
    /// <param name="fileMover">The file mover service.</param>
    /// <param name="logger">The logger instance.</param>
    public MetadataExtractionService(
        IFileTypeIdentifier fileTypeIdentifier,
        IMetadataExtractor metadataExtractor,
        IFileClassifier fileClassifier,
        ISafeFileNamer safeFileNamer,
        IFileMover fileMover,
        ILogger<MetadataExtractionService> logger)
    {
        _fileTypeIdentifier = fileTypeIdentifier;
        _metadataExtractor = metadataExtractor;
        _fileClassifier = fileClassifier;
        _safeFileNamer = safeFileNamer;
        _fileMover = fileMover;
        _logger = logger;
    }

    /// <summary>
    /// Processes a file through the complete Stage 2 workflow: identification → extraction → classification → naming → organization.
    /// </summary>
    /// <param name="filePath">The path to the file to process.</param>
    /// <param name="originalFileName">The original filename.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the processing result with classification and new file path, or an error.</returns>
    public async Task<Result<MetadataExtractionResult>> ProcessFileAsync(
        string filePath,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
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

        try
        {
            _logger.LogInformation("Starting metadata extraction for file: {FilePath}", filePath);

            // Step 1: Identify file type based on content
            var fileContent = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileTypeResult = await _fileTypeIdentifier.IdentifyFileTypeAsync(fileContent, originalFileName, cancellationToken);
            if (fileTypeResult.IsFailure)
            {
                return Result<MetadataExtractionResult>.WithFailure(fileTypeResult.Error!);
            }

            var fileFormat = fileTypeResult.Value;
            _logger.LogDebug("Identified file type as: {FileFormat}", fileFormat);

            // Step 2: Extract metadata based on file type
            var metadataResult = await ExtractMetadataByTypeAsync(fileContent, fileFormat, cancellationToken);
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
            var classificationResult = await _fileClassifier.ClassifyAsync(metadata, cancellationToken);
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
            var fileNameResult = await _safeFileNamer.GenerateSafeFileNameAsync(originalFileName, classification, metadata, cancellationToken);
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
            var moveResult = await _fileMover.MoveFileAsync(filePath, classification, safeFileName, cancellationToken);
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
        return fileFormat switch
        {
            FileFormat.Xml => await _metadataExtractor.ExtractFromXmlAsync(fileContent, cancellationToken),
            FileFormat.Docx => await _metadataExtractor.ExtractFromDocxAsync(fileContent, cancellationToken),
            FileFormat.Pdf => await _metadataExtractor.ExtractFromPdfAsync(fileContent, cancellationToken),
            _ => Result<ExtractedMetadata>.WithFailure($"Unsupported file format: {fileFormat}")
        };
    }
}

/// <summary>
/// Represents the result of metadata extraction processing.
/// </summary>
public class MetadataExtractionResult
{
    /// <summary>
    /// Gets or sets the original file path.
    /// </summary>
    public string OriginalFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new organized file path.
    /// </summary>
    public string NewFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the classification result.
    /// </summary>
    public ClassificationResult Classification { get; set; } = new();

    /// <summary>
    /// Gets or sets the extracted metadata.
    /// </summary>
    public ExtractedMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the identified file format.
    /// </summary>
    public FileFormat FileFormat { get; set; }
}

