namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for processing PDF fixture files with OCR.
/// Encapsulates PDF loading, OCR processing, and field extraction logic.
/// </summary>
public sealed class PdfProcessingService
{
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly FixtureLoaderService _fixtureLoader;
    private readonly ILogger<PdfProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfProcessingService"/> class.
    /// </summary>
    /// <param name="pdfFieldExtractor">PDF field extractor service (handles multi-page PDF → OCR → field extraction)</param>
    /// <param name="fixtureLoader">Fixture file loader service</param>
    /// <param name="logger">Logger instance</param>
    public PdfProcessingService(
        IFieldExtractor<PdfSource> pdfFieldExtractor,
        FixtureLoaderService fixtureLoader,
        ILogger<PdfProcessingService> logger)
    {
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
            _logger.LogWarning("📄 PDF PROCESSING: START - Requested fixture: {FixtureName}", fixtureName);

            // Load fixture bytes
            var pdfBytes = await _fixtureLoader.LoadFixtureBytesAsync(fixtureName, cancellationToken);

            _logger.LogWarning("📄 PDF PROCESSING: Loaded {Size} bytes for fixture: {FixtureName}", pdfBytes.Length, fixtureName);

            // Extract Expediente directly using PdfOcrFieldExtractor (handles multi-page PDF → OCR → field extraction)
            _logger.LogWarning("📄 PDF PROCESSING: Starting PDF field extraction for fixture: {FixtureName}", fixtureName);
            var expedienteResult = await ExtractExpedienteFromPdfAsync(
                pdfBytes,
                fixtureName,
                cancellationToken);

            if (!expedienteResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Field extraction failed for {FixtureName}: {Error}",
                    fixtureName, expedienteResult.Error);
                throw new InvalidOperationException($"Field extraction failed: {expedienteResult.Error}");
            }

            var (expediente, ocrText, confidence) = expedienteResult.Value;

            // Create extraction metadata
            var metadata = CreateExtractionMetadata(fixtureName, expediente, confidence);

            _logger.LogWarning(
                "✅ PDF PROCESSING: COMPLETE for {FixtureName}: Expediente={NumeroExpediente}, Fields extracted: {FieldCount}, OCR confidence: {Confidence:F1}%",
                fixtureName, expediente?.NumeroExpediente, metadata.TotalFieldsExtracted, confidence * 100);

            // Create a mock ProcessingResult for backward compatibility
            var processingResult = new ProcessingResult
            {
                OCRResult = new OCRResult
                {
                    Text = ocrText,
                    ConfidenceAvg = confidence * 100,
                    LanguageUsed = "spa"
                },
                PageNumber = 1,
                SourcePath = fixtureName,
                ProcessingErrors = new List<string>()
            };

            var result = new PdfProcessingResult
            {
                OcrResult = processingResult,
                Expediente = expediente,
                FixtureName = fixtureName,
                PdfBytes = pdfBytes,
                Metadata = metadata
            };

            _logger.LogWarning("📄 PDF PROCESSING: Returning PdfProcessingResult with FixtureName: {FixtureName}", result.FixtureName);

            return result;
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
    /// Extracts Expediente entity from PDF using PdfOcrFieldExtractor (handles multi-page OCR).
    /// </summary>
    private async Task<Result<(Expediente Expediente, string OcrText, float Confidence)>> ExtractExpedienteFromPdfAsync(
        byte[] pdfBytes,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("🔬 FIELD EXTRACTION: Starting PDF OCR + field extraction for source: {SourceName}", sourceName);

            // Create PdfSource
            var pdfSource = new PdfSource
            {
                FileContent = pdfBytes,
                FilePath = sourceName,
            };

            _logger.LogWarning("🔬 FIELD EXTRACTION: PdfSource created with FilePath: {FilePath}", pdfSource.FilePath);

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

            // Extract fields using PdfOcrFieldExtractor (handles multi-page PDF → images → OCR → field extraction)
            _logger.LogWarning("🔬 FIELD EXTRACTION: Calling PdfOcrFieldExtractor for source: {SourceName}", sourceName);
            var extractionResult = await _pdfFieldExtractor.ExtractFieldsAsync(
                pdfSource,
                fieldDefinitions);

            if (extractionResult.IsFailure)
            {
                _logger.LogError("🔬 FIELD EXTRACTION: Failed for {SourceName}: {Error}", sourceName, extractionResult.Error);
                return Result<(Expediente, string, float)>.WithFailure(
                    $"Failed to extract fields from PDF: {extractionResult.Error}");
            }

            var extractedFields = extractionResult.Value;
            if (extractedFields == null)
            {
                _logger.LogError("🔬 FIELD EXTRACTION: Extracted fields are NULL for {SourceName}", sourceName);
                return Result<(Expediente, string, float)>.WithFailure("Extracted fields are null");
            }

            _logger.LogWarning("🔬 FIELD EXTRACTION: ExtractedFields received - Expediente: {Expediente}, Causa: {Causa}, AdditionalFields: {Count}",
                extractedFields.Expediente, extractedFields.Causa, extractedFields.AdditionalFields?.Count ?? 0);

            // Map ExtractedFields to Expediente entity
            var expediente = MapExtractedFieldsToExpediente(extractedFields);

            // Get OCR text and confidence from extracted fields metadata
            var ocrText = extractedFields.AdditionalFields?.GetValueOrDefault("_OcrText") ?? "";
            var confidence = extractedFields.AdditionalFields?.TryGetValue("_OcrConfidence", out var confStr) == true
                && float.TryParse(confStr, out var conf) ? conf : 0.8f;

            _logger.LogWarning(
                "✅ FIELD EXTRACTION: Successfully mapped to Expediente for {SourceName}: NumeroExpediente={NumeroExpediente}, OCR text length={TextLength}",
                sourceName, expediente.NumeroExpediente, ocrText.Length);

            return Result<(Expediente, string, float)>.Success((expediente, ocrText, confidence));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting Expediente from PDF");
            return Result<(Expediente, string, float)>.WithFailure(
                $"Error extracting Expediente: {ex.Message}",
                default,
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
        string sourceName,
        Expediente? expediente,
        float confidence)
    {
        return new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV,
            MeanConfidence = confidence,
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
