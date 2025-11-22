namespace ExxerCube.Prisma.Infrastructure.Extraction.Teseract;

/// <summary>
/// PDF metadata extractor implementation with OCR fallback using existing OCR pipeline.
/// Detects scanned PDFs and applies image preprocessing before OCR.
/// </summary>
public class PdfMetadataExtractor : IMetadataExtractor
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly ILogger<PdfMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMetadataExtractor"/> class.
    /// </summary>
    /// <param name="ocrExecutor">The OCR executor for text extraction.</param>
    /// <param name="imagePreprocessor">The image preprocessor for scanned PDF preprocessing.</param>
    /// <param name="logger">The logger instance.</param>
    public PdfMetadataExtractor(
        IOcrExecutor ocrExecutor,
        IImagePreprocessor imagePreprocessor,
        ILogger<PdfMetadataExtractor> logger)
    {
        _ocrExecutor = ocrExecutor;
        _imagePreprocessor = imagePreprocessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting metadata from PDF document");

            // Try to extract text directly from PDF first
            var textResult = await TryExtractTextFromPdfAsync(fileContent, cancellationToken);
            if (textResult.IsFailure)
            {
                _logger.LogWarning("Failed to extract text directly from PDF, attempting OCR fallback");
            }

            string extractedText = textResult.IsSuccess && textResult.Value != null ? textResult.Value : string.Empty;

            // If no text extracted or very little text, it's likely a scanned PDF - use OCR
            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
            {
                _logger.LogDebug("PDF appears to be scanned, using OCR with preprocessing");
                var ocrResult = await ExtractWithOcrAsync(fileContent, cancellationToken);
                if (ocrResult.IsSuccess && ocrResult.Value != null)
                {
                    extractedText = ocrResult.Value;
                }
                else
                {
                    return Result<ExtractedMetadata>.WithFailure($"Failed to extract text from PDF: {ocrResult.Error ?? "OCR failed"}");
                }
            }

            // Extract structured fields from text
            if (string.IsNullOrEmpty(extractedText))
            {
                return Result<ExtractedMetadata>.WithFailure("No text extracted from PDF");
            }

            var expediente = ExtractExpediente(extractedText);
            var rfcValues = ExtractRfcValues(extractedText);
            var names = ExtractNames(extractedText);
            var dates = ExtractDates(extractedText);
            var legalReferences = ExtractLegalReferences(extractedText);

            // Create ExtractedFields for text reconstruction
            var extractedFields = new ExtractedFields
            {
                Expediente = expediente?.NumeroExpediente,
                Causa = ExtractCausa(extractedText),
                AccionSolicitada = ExtractAccionSolicitada(extractedText),
                Fechas = dates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
                Montos = ExtractMontos(extractedText)
            };

            var metadata = new ExtractedMetadata
            {
                Expediente = expediente,
                ExtractedFields = extractedFields,
                RfcValues = rfcValues.Length > 0 ? rfcValues : null,
                Names = names.Length > 0 ? names : null,
                Dates = dates.Length > 0 ? dates : null,
                LegalReferences = legalReferences.Length > 0 ? legalReferences : null
            };

            _logger.LogDebug("Successfully extracted metadata from PDF document");
            return Result<ExtractedMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from PDF");
            return Result<ExtractedMetadata>.WithFailure($"Error extracting PDF metadata: {ex.Message}", default(ExtractedMetadata), ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("XML extraction not supported by PdfMetadataExtractor. Use XmlMetadataExtractor instead."));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("DOCX extraction not supported by PdfMetadataExtractor. Use DocxMetadataExtractor instead."));
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDF text extraction cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        try
        {
            _logger.LogDebug("Extracting text from PDF document");

            // Try to extract text directly from PDF first
            var textResult = await TryExtractTextFromPdfAsync(fileContent, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation
            if (textResult.IsCancelled())
            {
                _logger.LogWarning("PDF text extraction cancelled");
                return ResultExtensions.Cancelled<string>();
            }

            string extractedText = textResult.IsSuccess && textResult.Value != null ? textResult.Value : string.Empty;

            // If no text extracted or very little text, it's likely a scanned PDF - use OCR
            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
            {
                _logger.LogDebug("PDF appears to be scanned, using OCR with preprocessing");
                var ocrResult = await ExtractWithOcrAsync(fileContent, cancellationToken).ConfigureAwait(false);

                if (ocrResult.IsCancelled())
                {
                    return ResultExtensions.Cancelled<string>();
                }

                if (ocrResult.IsFailure)
                {
                    return Result<string>.WithFailure($"Failed to extract text from PDF: {ocrResult.Error ?? "OCR failed"}");
                }

                if (ocrResult.Value != null)
                {
                    extractedText = ocrResult.Value;
                }
            }

            if (string.IsNullOrEmpty(extractedText))
            {
                return Result<string>.WithFailure("No text extracted from PDF");
            }

            _logger.LogDebug("Successfully extracted text from PDF document (length: {Length})", extractedText.Length);
            return Result<string>.Success(extractedText);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PDF text extraction cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            return Result<string>.WithFailure($"Error extracting PDF text: {ex.Message}", default(string), ex);
        }
    }

    private static async Task<Result<string>> TryExtractTextFromPdfAsync(byte[] fileContent, CancellationToken cancellationToken)
    {
        try
        {
            // Basic PDF text extraction using iTextSharp or similar
            // For now, return empty string - full implementation would use a PDF library
            // This is a placeholder - in production, use iTextSharp or PdfSharp
            await Task.CompletedTask;
            return Result<string>.Success(string.Empty);
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Failed to extract text from PDF: {ex.Message}", default(string), ex);
        }
    }

    private async Task<Result<string>> ExtractWithOcrAsync(byte[] fileContent, CancellationToken cancellationToken)
    {
        try
        {
            // Convert PDF first page to image
            // For now, this is a placeholder - full implementation would:
            // 1. Convert PDF page to image
            // 2. Preprocess image using IImagePreprocessor
            // 3. Run OCR using IOcrExecutor

            // Placeholder implementation
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
            return Result<string>.WithFailure($"OCR extraction failed: {ex.Message}", default(string), ex);
        }
    }

    private static Expediente? ExtractExpediente(string text)
    {
        var expedientePattern = @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+";
        var match = System.Text.RegularExpressions.Regex.Match(text, expedientePattern);
        if (match.Success)
        {
            return new Expediente
            {
                NumeroExpediente = match.Value,
                AreaDescripcion = ExtractAreaDescripcion(text)
            };
        }

        return null;
    }

    private static string ExtractAreaDescripcion(string text)
    {
        var areas = new[] { "ASEGURAMIENTO", "HACENDARIO", "JUDICIAL" };
        return areas.FirstOrDefault(a => text.Contains(a, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static string[] ExtractRfcValues(string text)
    {
        var rfcPattern = @"[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, rfcPattern);
        return matches.Select(m => m.Value).Distinct().ToArray();
    }

    private static string[] ExtractNames(string text)
    {
        var namePattern = @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, namePattern);
        return matches.Select(m => m.Value.Trim()).Distinct().Take(10).ToArray();
    }

    private static DateTime[] ExtractDates(string text)
    {
        var datePatterns = new[]
        {
            @"\d{2}/\d{2}/\d{4}",
            @"\d{2}-\d{2}-\d{4}",
            @"\d{4}-\d{2}-\d{2}"
        };

        var dates = new System.Collections.Generic.List<DateTime>();
        foreach (var pattern in datePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (DateTime.TryParse(match.Value, out var date))
                {
                    dates.Add(date);
                }
            }
        }

        return dates.Distinct().ToArray();
    }

    private static string[] ExtractLegalReferences(string text)
    {
        var referencePatterns = new[]
        {
            @"(?:Referencia|REF|Ref\.?)\s*:?\s*([A-Z0-9/-]+)",
            @"(?:Artículo|Art\.?)\s+\d+",
            @"(?:Ley|LEY)\s+[A-Z0-9]+"
        };

        var references = new System.Collections.Generic.List<string>();
        foreach (var pattern in referencePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            references.AddRange(matches.Select(m => m.Value.Trim()));
        }

        return references.Distinct().ToArray();
    }

    private static string? ExtractCausa(string text)
    {
        var causaPatterns = new[]
        {
            @"(?:CAUSA|Causa|causa)\s*:?\s*([^\n]+)",
            @"(?:MOTIVO|Motivo|motivo)\s*:?\s*([^\n]+)"
        };

        foreach (var pattern in causaPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Extract action from common patterns
        var actionPatterns = new[]
        {
            @"(?:ACCIÓN|Acción|accion|ACCIÓN SOLICITADA)\s*:?\s*([^\n]+)",
            @"(?:SOLICITA|Solicita|solicita)\s+([^\n]+?)(?:\n|\.|$)",
            @"(?:REQUERIMIENTO|Requerimiento|requerimiento)\s*:?\s*([^\n]+)"
        };

        foreach (var pattern in actionPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // Fallback: use first 200 characters if no pattern matches
        if (text.Length > 0)
        {
            return text.Substring(0, Math.Min(200, text.Length)).Trim();
        }

        return null;
    }

    private static List<AmountData> ExtractMontos(string text)
    {
        var montos = new List<AmountData>();
        var amountPatterns = new[]
        {
            @"\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
            @"(?:MONTO|Monto|monto)\s*:?\s*\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)"
        };

        foreach (var pattern in amountPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var amountStr = match.Groups[1].Value.Replace(",", string.Empty);
                    if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                    {
                        montos.Add(new AmountData
                        {
                            Value = amount,
                            Currency = "MXN"
                        });
                    }
                }
            }
        }

        return montos.DistinctBy(m => m.Value).ToList();
    }
}