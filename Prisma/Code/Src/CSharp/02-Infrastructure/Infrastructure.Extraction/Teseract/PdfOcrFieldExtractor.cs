namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// PDF field extractor implementation with OCR fallback using existing OCR pipeline.
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="PdfSource"/>.
/// </summary>
public class PdfOcrFieldExtractor : IFieldExtractor<PdfSource>
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly ILogger<PdfOcrFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOcrFieldExtractor"/> class.
    /// </summary>
    /// <param name="ocrExecutor">The OCR executor for text extraction.</param>
    /// <param name="imagePreprocessor">The image preprocessor for scanned PDF preprocessing.</param>
    /// <param name="logger">The logger instance.</param>
    public PdfOcrFieldExtractor(
        IOcrExecutor ocrExecutor,
        IImagePreprocessor imagePreprocessor,
        ILogger<PdfOcrFieldExtractor> logger)
    {
        _ocrExecutor = ocrExecutor;
        _imagePreprocessor = imagePreprocessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(PdfSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Extracting fields from PDF document: {FilePath}", source.FilePath);

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

            // Extract text from PDF (try direct extraction first, then OCR fallback)
            var textResult = await ExtractTextFromPdfAsync(fileContent);
            if (textResult.IsFailure)
            {
                return Result<ExtractedFields>.WithFailure($"Failed to extract text from PDF: {textResult.Error}");
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = source.OcrConfidence ?? 0.8f; // Default confidence, or use provided value

            // Extract fields based on definitions
            var extractedFields = new ExtractedFields();

            foreach (var fieldDef in fieldDefinitions)
            {
                var fieldResult = ExtractFieldByName(text, fieldDef.FieldName, confidence);
                if (fieldResult.IsSuccess && fieldResult.Value != null)
                {
                    ApplyFieldToExtractedFields(extractedFields, fieldDef.FieldName, fieldResult.Value.Value);
                }
            }

            _logger.LogDebug("Successfully extracted {Count} fields from PDF document", fieldDefinitions.Length);
            return Result<ExtractedFields>.Success(extractedFields);
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
        try
        {
            _logger.LogDebug("Extracting field {FieldName} from PDF document: {FilePath}", fieldName, source.FilePath);

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

            // Extract text from PDF
            var textResult = await ExtractTextFromPdfAsync(fileContent);
            if (textResult.IsFailure)
            {
                return Result<FieldValue>.WithFailure($"Failed to extract text from PDF: {textResult.Error}");
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = source.OcrConfidence ?? 0.8f; // Default confidence, or use provided value

            // Extract specific field
            var fieldResult = ExtractFieldByName(text, fieldName, confidence);
            if (fieldResult.IsSuccess && fieldResult.Value != null)
            {
                return Result<FieldValue>.Success(fieldResult.Value);
            }

            return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in PDF document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting field {FieldName} from PDF", fieldName);
            return Result<FieldValue>.WithFailure($"Error extracting field from PDF: {ex.Message}", default(FieldValue), ex);
        }
    }

    private async Task<Result<string>> ExtractTextFromPdfAsync(byte[] fileContent)
    {
        try
        {
            // Try to extract text directly from PDF first (placeholder - would use PDF library)
            // For now, assume it's a scanned PDF and use OCR
            _logger.LogDebug("Attempting OCR extraction from PDF");

            // Convert PDF first page to image (placeholder - full implementation would use PDF library)
            var imageData = new ImageData
            {
                Data = fileContent,
                SourcePath = "pdf_page_1"
            };

            // Preprocess image
            var preprocessResult = await _imagePreprocessor.PreprocessAsync(imageData, new ProcessingConfig());
            if (preprocessResult.IsFailure)
            {
                return Result<string>.WithFailure(preprocessResult.Error ?? "Preprocessing failed");
            }

            var preprocessedImage = preprocessResult.Value;
            if (preprocessedImage == null)
            {
                return Result<string>.WithFailure("Preprocessed image is null");
            }

            // Run OCR
            var ocrResult = await _ocrExecutor.ExecuteOcrAsync(preprocessedImage, new OCRConfig());
            if (ocrResult.IsFailure)
            {
                return Result<string>.WithFailure(ocrResult.Error ?? "OCR execution failed");
            }

            var ocrResultValue = ocrResult.Value;
            if (ocrResultValue == null)
            {
                return Result<string>.WithFailure("OCR result is null");
            }

            return Result<string>.Success(ocrResultValue.Text);
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"PDF text extraction failed: {ex.Message}", string.Empty, ex);
        }
    }

    private Result<FieldValue> ExtractFieldByName(string text, string fieldName, float confidence)
    {
        var value = fieldName.ToLowerInvariant() switch
        {
            "expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),
            _ => null
        };

        if (value != null)
        {
            return Result<FieldValue>.Success(new FieldValue(fieldName, value, confidence, "PDF", FieldOrigin.PdfOcr));
        }

        return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found");
    }

    private static void ApplyFieldToExtractedFields(ExtractedFields fields, string fieldName, string? value)
    {
        switch (fieldName.ToLowerInvariant())
        {
            case "expediente":
                fields.Expediente = value;
                break;

            case "causa":
                fields.Causa = value;
                break;

            case "accionsolicitada":
            case "accion_solicitada":
                fields.AccionSolicitada = value;
                break;
        }
    }

    private static string? ExtractExpediente(string text)
    {
        // Pattern: A/AS1-2505-088637-PHM or similar
        var expedientePattern = @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+";
        var match = System.Text.RegularExpressions.Regex.Match(text, expedientePattern);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractCausa(string text)
    {
        // Look for "CAUSA:" or "Causa:" followed by text
        var causaPattern = @"(?:CAUSA|Causa)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, causaPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Look for "ACCIÓN SOLICITADA:" or "Accion Solicitada:" followed by text
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, accionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}
