namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// PDF field extractor implementation with OCR pipeline for image-only PDFs.
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="PdfSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// This extractor handles image-only (scanned) PDFs by:
/// </para>
/// <list type="number">
///   <item><description>Converting PDF pages to images using PDFtoImage</description></item>
///   <item><description>Preprocessing images for OCR quality</description></item>
///   <item><description>Running OCR on each page using Tesseract</description></item>
///   <item><description>Combining text from all pages</description></item>
///   <item><description>Delegating to AdaptiveTxtFieldExtractor for field extraction</description></item>
/// </list>
/// </remarks>
public class PdfOcrFieldExtractor : IFieldExtractor<PdfSource>
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IPdfToImageConverter _pdfToImageConverter;
    private readonly IFieldExtractor<TxtSource> _txtFieldExtractor;
    private readonly ILogger<PdfOcrFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOcrFieldExtractor"/> class.
    /// </summary>
    /// <param name="ocrExecutor">The OCR executor for text extraction.</param>
    /// <param name="imagePreprocessor">The image preprocessor for scanned PDF preprocessing.</param>
    /// <param name="pdfToImageConverter">The PDF-to-image converter (rasterizes PDF pages for OCR).</param>
    /// <param name="txtFieldExtractor">The text field extractor for extracting fields from OCR output.</param>
    /// <param name="logger">The logger instance.</param>
    public PdfOcrFieldExtractor(
        IOcrExecutor ocrExecutor,
        IImagePreprocessor imagePreprocessor,
        IPdfToImageConverter pdfToImageConverter,
        IFieldExtractor<TxtSource> txtFieldExtractor,
        ILogger<PdfOcrFieldExtractor> logger)
    {
        _ocrExecutor = ocrExecutor ?? throw new ArgumentNullException(nameof(ocrExecutor));
        _imagePreprocessor = imagePreprocessor ?? throw new ArgumentNullException(nameof(imagePreprocessor));
        _pdfToImageConverter = pdfToImageConverter ?? throw new ArgumentNullException(nameof(pdfToImageConverter));
        _txtFieldExtractor = txtFieldExtractor ?? throw new ArgumentNullException(nameof(txtFieldExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(PdfSource source, FieldDefinition[] fieldDefinitions)
    {
        // Validate inputs up front and return a Result (never throw) per the project's
        // Railway-Oriented Programming rule. Without this guard a null source produced an
        // NRE that surfaced as an opaque "Object reference not set..." failure.
        if (source is null)
        {
            return Result<ExtractedFields>.WithFailure("PDF source cannot be null");
        }

        try
        {
            _logger.LogInformation("Extracting fields from PDF document: {FilePath}", source.FilePath);

            // Get file content
            byte[] fileContent;
            if (source.FileContent != null)
            {
                fileContent = source.FileContent;
            }
            else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
            {
                fileContent = File.ReadAllBytes(source.FilePath);
            }
            else
            {
                return Result<ExtractedFields>.WithFailure("PDF source must have either FileContent or valid FilePath");
            }

            // Convert PDF to images and run OCR to extract text
            var ocrResult = await ExtractTextFromPdfAsync(fileContent);
            if (ocrResult.IsFailure)
            {
                return Result<ExtractedFields>.WithFailure($"Failed to extract text from PDF via OCR: {ocrResult.Error}");
            }

            var ocrText = ocrResult.Value.OcrText;
            var confidence = ocrResult.Value.Confidence;

            _logger.LogInformation(
                "OCR extraction completed for PDF: {FilePath} - Text length: {Length} chars, Confidence: {Confidence:F2}",
                source.FilePath ?? "in-memory PDF",
                ocrText?.Length ?? 0,
                confidence);

            // Create TxtSource from OCR output
            var txtSource = new TxtSource(
                textContent: ocrText ?? string.Empty,
                ocrConfidence: confidence,
                sourceFilePath: source.FilePath);

            // Delegate to AdaptiveTxtFieldExtractor for field extraction
            _logger.LogInformation("Delegating field extraction to AdaptiveTxtFieldExtractor");
            var extractionResult = await _txtFieldExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions);

            if (extractionResult.IsSuccess && extractionResult.Value is not null)
            {
                // Surface OCR provenance the UI expects. PdfProcessingService reads AdditionalFields
                // ["_OcrText"]/["_OcrConfidence"] to show the REAL characters-extracted + OCR confidence;
                // without these it falls back to 0 chars and a hardcoded 80%. The txt extractor consumes
                // the OCR text but does not echo it back, so we attach it here.
                extractionResult.Value.AdditionalFields["_OcrText"] = ocrText ?? string.Empty;
                extractionResult.Value.AdditionalFields["_OcrConfidence"] =
                    confidence.ToString(System.Globalization.CultureInfo.InvariantCulture);

                _logger.LogInformation("Successfully extracted {Count} fields from PDF document", fieldDefinitions.Length);
            }

            return extractionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fields from PDF");
            return Result<ExtractedFields>.WithFailure($"Error extracting PDF fields: {ex.Message}", default(ExtractedFields), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldValue>> ExtractFieldAsync(PdfSource source, string fieldName)
    {
        // Validate inputs up front and return a Result (never throw) per the project's
        // Railway-Oriented Programming rule.
        if (source is null)
        {
            return Result<FieldValue>.WithFailure("PDF source cannot be null");
        }

        try
        {
            _logger.LogInformation("Extracting field {FieldName} from PDF document: {FilePath}", fieldName, source.FilePath);

            // Get file content
            byte[] fileContent;
            if (source.FileContent != null)
            {
                fileContent = source.FileContent;
            }
            else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
            {
                fileContent = File.ReadAllBytes(source.FilePath);
            }
            else
            {
                return Result<FieldValue>.WithFailure("PDF source must have either FileContent or valid FilePath");
            }

            // Convert PDF to images and run OCR to extract text
            var ocrResult = await ExtractTextFromPdfAsync(fileContent);
            if (ocrResult.IsFailure)
            {
                return Result<FieldValue>.WithFailure($"Failed to extract text from PDF via OCR: {ocrResult.Error}");
            }

            var ocrText = ocrResult.Value.OcrText;
            var confidence = ocrResult.Value.Confidence;

            // Create TxtSource from OCR output
            var txtSource = new TxtSource(
                textContent: ocrText ?? string.Empty,
                ocrConfidence: confidence,
                sourceFilePath: source.FilePath);

            // Delegate to AdaptiveTxtFieldExtractor for field extraction
            _logger.LogInformation("Delegating field '{FieldName}' extraction to AdaptiveTxtFieldExtractor", fieldName);
            var extractionResult = await _txtFieldExtractor.ExtractFieldAsync(txtSource, fieldName);

            if (extractionResult.IsSuccess)
            {
                _logger.LogInformation("Successfully extracted field '{FieldName}' from PDF document", fieldName);
            }

            return extractionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting field {FieldName} from PDF", fieldName);
            return Result<FieldValue>.WithFailure($"Error extracting field from PDF: {ex.Message}", default(FieldValue), ex);
        }
    }

    /// <summary>
    /// Extracts text from PDF by converting pages to images and running OCR on each page.
    /// </summary>
    /// <param name="pdfBytes">The PDF file bytes.</param>
    /// <returns>Result containing OCR text and confidence score.</returns>
    private async Task<Result<(string OcrText, float Confidence)>> ExtractTextFromPdfAsync(byte[] pdfBytes)
    {
        try
        {
            _logger.LogInformation("Starting PDF → Image → OCR pipeline");

            // Convert PDF to images (one per page) via the injected converter port.
            var conversionResult = await _pdfToImageConverter.ConvertToImagesAsync(pdfBytes);
            if (conversionResult.IsFailure)
            {
                return Result<(string, float)>.WithFailure($"PDF to image conversion failed: {conversionResult.Error}");
            }

            var imagePages = conversionResult.Value!;
            _logger.LogInformation("Converted PDF to {PageCount} image pages", imagePages.Count);

            if (imagePages.Count == 0)
            {
                return Result<(string, float)>.WithFailure("PDF contains no pages");
            }

            // Run OCR on each page
            var allPageTexts = new List<string>();
            var confidences = new List<float>();

            for (int pageIndex = 0; pageIndex < imagePages.Count; pageIndex++)
            {
                var imageBytes = imagePages[pageIndex];
                _logger.LogInformation("Processing page {PageNumber}/{TotalPages} ({Size} bytes)",
                    pageIndex + 1, imagePages.Count, imageBytes.Length);

                // Create ImageData for preprocessing
                var imageData = new ImageData
                {
                    Data = imageBytes,
                    SourcePath = $"pdf_page_{pageIndex + 1}"
                };

                // Preprocess image
                var preprocessResult = await _imagePreprocessor.PreprocessAsync(imageData, new ProcessingConfig());
                if (preprocessResult.IsFailure)
                {
                    _logger.LogWarning("Preprocessing failed for page {PageNumber}: {Error}",
                        pageIndex + 1, preprocessResult.Error);
                    continue; // Skip this page but continue with others
                }

                var preprocessedImage = preprocessResult.Value;
                if (preprocessedImage == null)
                {
                    _logger.LogWarning("Preprocessed image is null for page {PageNumber}", pageIndex + 1);
                    continue;
                }

                // Run OCR
                var ocrResult = await _ocrExecutor.ExecuteOcrAsync(preprocessedImage, new OCRConfig());
                if (ocrResult.IsFailure || ocrResult.Value == null)
                {
                    _logger.LogWarning("OCR failed for page {PageNumber}: {Error}",
                        pageIndex + 1, ocrResult.Error ?? "null result");
                    continue;
                }

                var pageText = ocrResult.Value.Text;
                var pageConfidence = (float)ocrResult.Value.Confidence.Value; // Already on 0-1 scale

                allPageTexts.Add(pageText);
                confidences.Add(pageConfidence);

                _logger.LogInformation("Page {PageNumber}: Extracted {TextLength} chars, Confidence: {Confidence:F2}",
                    pageIndex + 1, pageText?.Length ?? 0, pageConfidence);
            }

            if (allPageTexts.Count == 0)
            {
                return Result<(string, float)>.WithFailure("OCR failed for all pages in PDF");
            }

            // Combine all page texts
            var combinedText = string.Join("\n\n", allPageTexts);
            var averageConfidence = confidences.Average();

            _logger.LogInformation(
                "OCR extraction complete - Total text: {Length} chars, Average confidence: {Confidence:F2}",
                combinedText.Length, averageConfidence);

            return Result<(string, float)>.Success((combinedText, averageConfidence));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF text extraction failed");
            return Result<(string, float)>.WithFailure($"PDF text extraction failed: {ex.Message}", default, ex);
        }
    }
}
