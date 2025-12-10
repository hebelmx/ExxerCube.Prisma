namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for processing PDF fixture files with OCR.
/// Encapsulates PDF loading, OCR processing, and field extraction logic.
/// </summary>
public sealed class PdfProcessingService
{
    private readonly IOcrProcessingService _ocrService;
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly FixtureLoaderService _fixtureLoader;
    private readonly ILogger<PdfProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfProcessingService"/> class.
    /// </summary>
    /// <param name="ocrService">OCR processing service</param>
    /// <param name="pdfFieldExtractor">PDF field extractor service</param>
    /// <param name="fixtureLoader">Fixture file loader service</param>
    /// <param name="logger">Logger instance</param>
    public PdfProcessingService(
        IOcrProcessingService ocrService,
        IFieldExtractor<PdfSource> pdfFieldExtractor,
        FixtureLoaderService fixtureLoader,
        ILogger<PdfProcessingService> logger)
    {
        _ocrService = ocrService;
        _pdfFieldExtractor = pdfFieldExtractor;
        _fixtureLoader = fixtureLoader;
        _logger = logger;
    }

    /// <summary>
    /// Loads and processes a PDF fixture file with OCR and field extraction.
    /// </summary>
    /// <param name="fixtureName">The fixture file name (e.g., "222AAA-44444444442025.pdf")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PdfProcessingResult containing OCR results and extracted Expediente</returns>
    public async Task<PdfProcessingResult> LoadFixtureAsync(
        string fixtureName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading PDF fixture: {FixtureName}", fixtureName);

            // Load fixture bytes
            var pdfBytes = await _fixtureLoader.LoadFixtureBytesAsync(fixtureName, cancellationToken);

            _logger.LogDebug("PDF fixture loaded: {Size} bytes", pdfBytes.Length);

            // Create image data for OCR processing
            var imageData = new ImageData
            {
                Data = pdfBytes,
                SourcePath = fixtureName,
                PageNumber = 1,
                TotalPages = 1
            };

            // Create processing configuration with 75% confidence threshold
            var config = new ProcessingConfig
            {
                OCRConfig = new OCRConfig
                {
                    Language = "spa",
                    ConfidenceThreshold = 75.0f
                },
                RemoveWatermark = true,
                Deskew = true,
                Binarize = true,
                ExtractSections = true,
                NormalizeText = true
            };

            // Process document with OCR
            _logger.LogDebug("Processing PDF with OCR...");
            var ocrResult = await _ocrService.ProcessDocumentAsync(imageData, config, cancellationToken);

            if (!ocrResult.IsSuccess || ocrResult.Value == null)
            {
                var error = ocrResult.Error ?? "OCR processing failed";
                _logger.LogError("Failed to process PDF {FixtureName}: {Error}", fixtureName, error);
                throw new InvalidOperationException($"OCR processing failed: {error}");
            }

            var processingResult = ocrResult.Value;

            _logger.LogInformation(
                "OCR completed: {Confidence:F1}% confidence, {TextLength} characters",
                processingResult.OCRResult.ConfidenceAvg, processingResult.OCRResult.Text.Length);

            // Extract Expediente from OCR result
            var expedienteResult = await ExtractExpedienteFromOcrAsync(
                processingResult,
                pdfBytes,
                fixtureName,
                cancellationToken);

            if (!expedienteResult.IsSuccess || expedienteResult.Value == null)
            {
                _logger.LogWarning(
                    "Field extraction failed for {FixtureName}: {Error}",
                    fixtureName, expedienteResult.Error);
                // Continue - OCR succeeded even if field extraction failed
            }

            var expediente = expedienteResult.Value;

            // Create extraction metadata
            var metadata = CreateExtractionMetadata(processingResult, fixtureName, expediente);

            _logger.LogInformation(
                "Successfully processed PDF fixture {FixtureName}: Expediente={NumeroExpediente}",
                fixtureName, expediente?.NumeroExpediente);

            return new PdfProcessingResult
            {
                OcrResult = processingResult,
                Expediente = expediente,
                FixtureName = fixtureName,
                PdfBytes = pdfBytes,
                Metadata = metadata
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "PDF fixture not found: {FixtureName}", fixtureName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF fixture: {FixtureName}", fixtureName);
            throw;
        }
    }

    /// <summary>
    /// Extracts Expediente entity from OCR processing result.
    /// </summary>
    private async Task<Result<Expediente>> ExtractExpedienteFromOcrAsync(
        ProcessingResult ocrResult,
        byte[] pdfBytes,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting Expediente from OCR result using PdfOcrFieldExtractor");

            // Create PdfSource from OCR result
            var pdfSource = new PdfSource
            {
                FileContent = pdfBytes,
                FilePath = sourceName,
            };

            // Define fields to extract
            var fieldDefinitions = new[]
            {
                new FieldDefinition { FieldName = "Expediente", IsRequired = true },
                new FieldDefinition { FieldName = "Causa", IsRequired = false },
                new FieldDefinition { FieldName = "AccionSolicitada", IsRequired = false },
                new FieldDefinition { FieldName = "NumeroOficio", IsRequired = true },
                new FieldDefinition { FieldName = "AutoridadNombre", IsRequired = true },
                new FieldDefinition { FieldName = "FechaPublicacion", IsRequired = false },
                new FieldDefinition { FieldName = "DiasPlazo", IsRequired = false }
            };

            // Extract fields using PdfOcrFieldExtractor
            var extractionResult = await _pdfFieldExtractor.ExtractFieldsAsync(
                pdfSource,
                fieldDefinitions);

            if (extractionResult.IsFailure)
            {
                return Result<Expediente>.WithFailure(
                    $"Failed to extract fields from PDF: {extractionResult.Error}");
            }

            var extractedFields = extractionResult.Value;
            if (extractedFields == null)
            {
                return Result<Expediente>.WithFailure("Extracted fields are null");
            }

            // Map ExtractedFields to Expediente entity
            var expediente = MapExtractedFieldsToExpediente(extractedFields);

            _logger.LogDebug(
                "Successfully extracted Expediente from PDF: {NumeroExpediente}",
                expediente.NumeroExpediente);

            return Result<Expediente>.Success(expediente);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting Expediente from OCR result");
            return Result<Expediente>.WithFailure(
                $"Error extracting Expediente: {ex.Message}",
                default(Expediente),
                ex);
        }
    }

    /// <summary>
    /// Maps ExtractedFields (from field extraction service) to Expediente entity.
    /// </summary>
    private Expediente MapExtractedFieldsToExpediente(ExtractedFields fields)
    {
        var expediente = new Expediente
        {
            NumeroExpediente = fields.Expediente ?? "",
            NumeroOficio = fields.AdditionalFields?.GetValueOrDefault("NumeroOficio") ?? "",
            SolicitudSiara = fields.AdditionalFields?.GetValueOrDefault("SolicitudSiara") ?? "",
            Folio = 0, // TODO: Extract from additional fields
            OficioYear = DateTime.Now.Year, // TODO: Parse from NumeroOficio
            AreaClave = 0,
            AreaDescripcion = fields.AdditionalFields?.GetValueOrDefault("AreaDescripcion") ?? "",
            AutoridadNombre = fields.AdditionalFields?.GetValueOrDefault("AutoridadNombre") ?? "",
            NombreSolicitante = null,
            Referencia = "",
            Referencia1 = fields.Causa ?? "",
            Referencia2 = fields.AccionSolicitada ?? "",
            TieneAseguramiento = false
        };

        // Parse dates if available
        if (fields.AdditionalFields?.TryGetValue("FechaPublicacion", out var fechaPubStr) == true &&
            DateTime.TryParse(fechaPubStr, out var fechaPub))
        {
            expediente.FechaPublicacion = fechaPub;
        }

        if (fields.AdditionalFields?.TryGetValue("DiasPlazo", out var diasStr) == true &&
            int.TryParse(diasStr, out var dias))
        {
            expediente.DiasPlazo = dias;
        }

        return expediente;
    }

    /// <summary>
    /// Creates extraction metadata for fusion service.
    /// </summary>
    private ExtractionMetadata CreateExtractionMetadata(
        ProcessingResult ocrResult,
        string sourceName,
        Expediente? expediente)
    {
        return new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV,
            MeanConfidence = ocrResult.OCRResult.ConfidenceAvg / 100.0,
            QualityIndex = 0.8, // Default for demo
            RegexMatches = 0,
            TotalFieldsExtracted = expediente != null ? CountExtractedFields(expediente) : 0,
            PatternViolations = 0,
            CatalogValidations = 0
        };
    }

    /// <summary>
    /// Counts the number of extracted fields in an Expediente (simplified version).
    /// </summary>
    private int CountExtractedFields(Expediente expediente)
    {
        int count = 0;

        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroOficio)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.SolicitudSiara)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante)) count++;
        if (expediente.FechaPublicacion != DateTime.MinValue) count++;
        if (expediente.DiasPlazo != 0) count++;

        return count;
    }
}

/// <summary>
/// Result of processing a PDF fixture file.
/// </summary>
public sealed record PdfProcessingResult
{
    /// <summary>
    /// OCR processing result with extracted text and confidence scores.
    /// </summary>
    public required ProcessingResult OcrResult { get; init; }

    /// <summary>
    /// Expediente entity extracted from PDF (may be null if extraction failed).
    /// </summary>
    public Expediente? Expediente { get; init; }

    /// <summary>
    /// Name of the fixture file.
    /// </summary>
    public required string FixtureName { get; init; }

    /// <summary>
    /// PDF file bytes for viewer rendering.
    /// </summary>
    public required byte[] PdfBytes { get; init; }

    /// <summary>
    /// Extraction metadata for fusion service.
    /// </summary>
    public required ExtractionMetadata Metadata { get; init; }
}
