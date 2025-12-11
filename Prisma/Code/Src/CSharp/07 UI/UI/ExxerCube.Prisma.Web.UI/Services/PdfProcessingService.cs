namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
            _logger.LogWarning("📄 PDF PROCESSING: START - Requested fixture: {FixtureName}", fixtureName);

            // Load fixture bytes
            var pdfBytes = await _fixtureLoader.LoadFixtureBytesAsync(fixtureName, cancellationToken);

            _logger.LogWarning("📄 PDF PROCESSING: Loaded {Size} bytes for fixture: {FixtureName}", pdfBytes.Length, fixtureName);

            // Convert PDF to individual page images
            _logger.LogWarning("📄 PDF PROCESSING: Converting PDF to individual page images for: {FixtureName}", fixtureName);
            var imagePages = ConvertPdfPagesToImages(pdfBytes);
            _logger.LogWarning("📄 PDF PROCESSING: Converted to {PageCount} pages for: {FixtureName}", imagePages.Count, fixtureName);

            if (imagePages.Count == 0)
            {
                _logger.LogError("PDF contains no pages: {FixtureName}", fixtureName);
                throw new InvalidOperationException($"PDF contains no pages: {fixtureName}");
            }

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

            // Process each page with OCR
            _logger.LogWarning("📄 PDF PROCESSING: Starting OCR for {PageCount} pages of {FixtureName}", imagePages.Count, fixtureName);
            var allPageTexts = new List<string>();
            var confidences = new List<float>();
            var totalPages = imagePages.Count;

            for (int pageIndex = 0; pageIndex < imagePages.Count; pageIndex++)
            {
                var imageBytes = imagePages[pageIndex];
                var pageNumber = pageIndex + 1;

                _logger.LogWarning("📄 PDF PROCESSING: Processing page {PageNumber}/{TotalPages} ({Size} bytes) for {FixtureName}",
                    pageNumber, totalPages, imageBytes.Length, fixtureName);

                // Create ImageData for this page
                var imageData = new ImageData
                {
                    Data = imageBytes,
                    SourcePath = fixtureName,
                    PageNumber = pageNumber,
                    TotalPages = totalPages
                };

                // Process this page with OCR
                var ocrResult = await _ocrService.ProcessDocumentAsync(imageData, config, cancellationToken);

                if (!ocrResult.IsSuccess || ocrResult.Value == null)
                {
                    var error = ocrResult.Error ?? "OCR processing failed";
                    _logger.LogWarning("OCR failed for page {PageNumber}/{TotalPages} of {FixtureName}: {Error}",
                        pageNumber, totalPages, fixtureName, error);
                    continue; // Skip this page but continue with others
                }

                var pageResult = ocrResult.Value;
                allPageTexts.Add(pageResult.OCRResult.Text);
                confidences.Add(pageResult.OCRResult.ConfidenceAvg);

                _logger.LogWarning(
                    "📄 PDF PROCESSING: Page {PageNumber}/{TotalPages} OCR complete for {FixtureName}: {Confidence:F1}% confidence, {TextLength} characters",
                    pageNumber, totalPages, fixtureName, pageResult.OCRResult.ConfidenceAvg, pageResult.OCRResult.Text.Length);
            }

            if (allPageTexts.Count == 0)
            {
                _logger.LogError("OCR failed for all pages of {FixtureName}", fixtureName);
                throw new InvalidOperationException($"OCR failed for all pages of {fixtureName}");
            }

            // Combine all page texts with page separators
            var combinedText = string.Join("\n\n", allPageTexts);
            var averageConfidence = confidences.Average();

            // Create combined processing result
            var processingResult = new ProcessingResult
            {
                OCRResult = new OCRResult
                {
                    Text = combinedText,
                    ConfidenceAvg = averageConfidence,
                    Confidences = new List<float>(confidences),
                    LanguageUsed = "spa"
                },
                PageNumber = 1, // Combined result represents all pages
                SourcePath = fixtureName,
                ProcessingErrors = new List<string>()
            };

            _logger.LogWarning(
                "📄 PDF PROCESSING: OCR completed for ALL {TotalPages} pages of {FixtureName}: {Confidence:F1}% avg confidence, {TextLength} total characters",
                totalPages, fixtureName, averageConfidence, combinedText.Length);

            // Extract Expediente from OCR result
            _logger.LogWarning("📄 PDF PROCESSING: Starting field extraction for fixture: {FixtureName}", fixtureName);
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

            _logger.LogWarning(
                "✅ PDF PROCESSING: COMPLETE for {FixtureName}: Expediente={NumeroExpediente}, Fields extracted: {FieldCount}",
                fixtureName, expediente?.NumeroExpediente, metadata.TotalFieldsExtracted);

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
            _logger.LogWarning("🔬 FIELD EXTRACTION: Starting for source: {SourceName}", sourceName);

            // Create PdfSource from OCR result
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

            // Extract fields using PdfOcrFieldExtractor
            _logger.LogWarning("🔬 FIELD EXTRACTION: Calling PdfFieldExtractor for source: {SourceName}", sourceName);
            var extractionResult = await _pdfFieldExtractor.ExtractFieldsAsync(
                pdfSource,
                fieldDefinitions);

            if (extractionResult.IsFailure)
            {
                _logger.LogError("🔬 FIELD EXTRACTION: Failed for {SourceName}: {Error}", sourceName, extractionResult.Error);
                return Result<Expediente>.WithFailure(
                    $"Failed to extract fields from PDF: {extractionResult.Error}");
            }

            var extractedFields = extractionResult.Value;
            if (extractedFields == null)
            {
                _logger.LogError("🔬 FIELD EXTRACTION: Extracted fields are NULL for {SourceName}", sourceName);
                return Result<Expediente>.WithFailure("Extracted fields are null");
            }

            _logger.LogWarning("🔬 FIELD EXTRACTION: ExtractedFields received - Expediente: {Expediente}, Causa: {Causa}, AdditionalFields: {Count}",
                extractedFields.Expediente, extractedFields.Causa, extractedFields.AdditionalFields?.Count ?? 0);

            // Map ExtractedFields to Expediente entity
            var expediente = MapExtractedFieldsToExpediente(extractedFields);

            _logger.LogWarning(
                "✅ FIELD EXTRACTION: Successfully mapped to Expediente for {SourceName}: NumeroExpediente={NumeroExpediente}",
                sourceName, expediente.NumeroExpediente);

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

    /// <summary>
    /// Converts all pages of a PDF to individual image bytes using PDFtoImage library.
    /// </summary>
    /// <param name="pdfBytes">The PDF file bytes.</param>
    /// <returns>List of image bytes (PNG format), one per page.</returns>
    private List<byte[]> ConvertPdfPagesToImages(byte[] pdfBytes)
    {
        const int dpi = 300; // Standard DPI for high-quality OCR
        var imagePages = new List<byte[]>();

        try
        {
            _logger.LogDebug("Converting PDF pages to images at {DPI} DPI using PDFtoImage", dpi);

            using var pdfStream = new MemoryStream(pdfBytes);
            var options = new RenderOptions(Dpi: dpi);

            // Get page count (PDFtoImage doesn't provide direct page count, so we'll iterate)
            int pageIndex = 0;
            while (true)
            {
                try
                {
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
                    using var skBitmap = Conversion.ToImage(pdfStream, pageIndex, options: options);
#pragma warning restore CA1416

                    if (skBitmap == null)
                    {
                        break; // No more pages
                    }

                    // Convert SKBitmap to ImageSharp Image<Rgba32>
                    using var image = Image.LoadPixelData<Rgba32>(
                        skBitmap.GetPixelSpan(),
                        skBitmap.Width,
                        skBitmap.Height);

                    // Save as PNG bytes
                    using var outputMs = new MemoryStream();
                    image.SaveAsPng(outputMs);

                    imagePages.Add(outputMs.ToArray());

                    _logger.LogDebug("Page {PageNumber} converted: {Width}x{Height} pixels",
                        pageIndex + 1, skBitmap.Width, skBitmap.Height);

                    pageIndex++;

                    // Reset stream position for next page
                    pdfStream.Position = 0;
                }
                catch (Exception ex)
                {
                    // Break on error (likely no more pages)
                    _logger.LogDebug("Page iteration stopped at page {PageNumber}: {Error}",
                        pageIndex + 1, ex.Message);
                    break;
                }
            }

            _logger.LogInformation("PDF conversion complete: {PageCount} pages extracted", imagePages.Count);
            return imagePages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert PDF to images");
            throw new InvalidOperationException($"Failed to convert PDF to images: {ex.Message}", ex);
        }
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
