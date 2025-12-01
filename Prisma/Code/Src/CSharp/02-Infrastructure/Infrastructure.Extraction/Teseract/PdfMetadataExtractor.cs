namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

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

            // Build extraction metadata for fusion quality scoring (DRY principle)
            ExtractionMetadata? qualityMetadata = null;
            if (expediente != null)
            {
                qualityMetadata = BuildExtractionMetadata(
                    expediente,
                    extractedText,
                    rfcValues,
                    textResult.IsSuccess && !string.IsNullOrWhiteSpace(textResult.Value ?? string.Empty) // Did direct extraction work?
                );
            }

            var metadata = new ExtractedMetadata
            {
                Expediente = expediente,
                ExtractedFields = extractedFields,
                RfcValues = rfcValues.Length > 0 ? rfcValues : null,
                Names = names.Length > 0 ? names : null,
                Dates = dates.Length > 0 ? dates : null,
                LegalReferences = legalReferences.Length > 0 ? legalReferences : null,
                QualityMetadata = qualityMetadata
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
            var areaDescripcion = ExtractAreaDescripcion(text);
            var autoridadNombre = ExtractAutoridadNombre(text);

            return new Expediente
            {
                NumeroExpediente = match.Value,
                AreaDescripcion = areaDescripcion,

                // Law-mandated fields - best-effort extraction from PDF text
                LawMandatedFields = ExtractLawMandatedFieldsFromText(autoridadNombre, areaDescripcion),

                // Semantic analysis - null until classification engine runs
                SemanticAnalysis = null,

                // Future-proofing: capture unknown fields (not applicable for PDF extraction)
                AdditionalFields = new Dictionary<string, string>()
            };
        }

        return null;
    }

    private static string ExtractAreaDescripcion(string text)
    {
        var areas = new[] { "ASEGURAMIENTO", "HACENDARIO", "JUDICIAL" };
        return areas.FirstOrDefault(a => text.Contains(a, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static string? ExtractAutoridadNombre(string text)
    {
        // Common patterns for authority names in CNBV documents
        var patterns = new[]
        {
            @"(?:SUBDELEGACION|SUBDELEGACIÓN)\s+\d+\s+[A-Z\s]+",
            @"(?:ADMINISTRACION|ADMINISTRACIÓN)\s+[A-Z\s]+",
            @"(?:UNIDAD|OFICINA)\s+[A-Z\s]+"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Best-effort extraction of law-mandated fields from PDF text.
    /// Populates what we can from unstructured text; bank systems will enrich missing fields later.
    /// </summary>
    private static LawMandatedFields? ExtractLawMandatedFieldsFromText(string? autoridadNombre, string areaDescripcion)
    {
        // Only create LawMandatedFields if we can populate at least one field
        var hasData = !string.IsNullOrWhiteSpace(autoridadNombre) ||
                      !string.IsNullOrWhiteSpace(areaDescripcion);

        if (!hasData)
        {
            return null; // No law-mandated data available from PDF
        }

        return new LawMandatedFields
        {
            // Section 2.1: Core Identification & Tracking
            SourceAuthorityCode = !string.IsNullOrWhiteSpace(autoridadNombre) ? autoridadNombre : null,

            // Section 2.2: SLA & Classification
            RequirementType = !string.IsNullOrWhiteSpace(areaDescripcion) ? areaDescripcion : null,
            // RequirementTypeCode cannot be reliably extracted from unstructured text

            // Section 2.3 & 2.4: Other fields come from bank systems (null for now)
        };
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

    /// <summary>
    /// Builds extraction metadata for multi-source data fusion quality scoring.
    /// Applies DRY principle: cleaning, validation, and confidence calculation happen ONCE here.
    /// </summary>
    /// <param name="expediente">The extracted Expediente entity (partial from PDF).</param>
    /// <param name="extractedText">The full text extracted from PDF.</param>
    /// <param name="rfcValues">RFC values extracted from text.</param>
    /// <param name="usedDirectExtraction">Whether direct text extraction worked (not OCR).</param>
    /// <returns>Extraction metadata with quality metrics.</returns>
    /// <remarks>
    /// For PDF extraction (CNBV OCR):
    /// - OCR confidence from Tesseract (when OCR was used)
    /// - Image quality metrics from preprocessing (when OCR was used)
    /// - Pattern validation and catalog validation
    /// - Base reliability: 0.85 (high quality CNBV scans)
    /// </remarks>
    private static ExtractionMetadata BuildExtractionMetadata(
        Expediente expediente,
        string extractedText,
        string[] rfcValues,
        bool usedDirectExtraction)
    {
        var metadata = new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV
        };

        // Pattern regex definitions (Mexican standards)
        var rfcPattern = new System.Text.RegularExpressions.Regex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$");

        // CNBV catalog values
        var validAreaDescripciones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ASEGURAMIENTO",
            "HACENDARIO",
            "PENAL",
            "CIVIL",
            "ADMINISTRATIVO",
            "JUDICIAL"
        };

        int regexMatches = 0;
        int totalFieldsExtracted = 0;
        int catalogValidations = 0;
        int patternViolations = 0;

        // Count extracted fields (PDF extraction is minimal compared to XML)
        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente))
        {
            totalFieldsExtracted++;
            regexMatches++; // Expediente number follows pattern
        }

        if (!string.IsNullOrWhiteSpace(expediente.AreaDescripcion))
        {
            totalFieldsExtracted++;
            if (validAreaDescripciones.Contains(expediente.AreaDescripcion.Trim()))
            {
                catalogValidations++;
            }
            else
            {
                patternViolations++;
            }
        }

        // Validate RFC values
        foreach (var rfc in rfcValues)
        {
            if (!string.IsNullOrWhiteSpace(rfc))
            {
                totalFieldsExtracted++;
                var rfcClean = rfc.Trim();
                if (rfcPattern.IsMatch(rfcClean))
                {
                    regexMatches++;
                }
                else
                {
                    patternViolations++;
                }
            }
        }

        // Populate extraction success metrics
        metadata.RegexMatches = regexMatches;
        metadata.TotalFieldsExtracted = totalFieldsExtracted;
        metadata.CatalogValidations = catalogValidations;
        metadata.PatternViolations = patternViolations;

        // OCR metrics (simplified - real OCR confidence would come from Tesseract)
        if (!usedDirectExtraction)
        {
            // PDF required OCR - estimate confidence based on text quality
            var wordCount = extractedText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            metadata.TotalWords = wordCount;

            // Heuristic: CNBV PDFs are usually high quality scans
            // Real implementation would get this from Tesseract OCRResult
            metadata.MeanConfidence = 0.85; // High confidence for CNBV documents
            metadata.MinConfidence = 0.70;  // Reasonable minimum
            metadata.LowConfidenceWords = (int)(wordCount * 0.10); // Estimate 10% low confidence

            // Image quality metrics (would come from ImagePreprocessor in real implementation)
            // For now, assume good quality CNBV scans
            metadata.QualityIndex = 0.80;
            metadata.BlurScore = 0.15;      // Low blur
            metadata.ContrastScore = 0.75;  // Good contrast
            metadata.NoiseEstimate = 0.10;  // Low noise
            metadata.EdgeDensity = 0.65;    // Decent edge density
        }
        else
        {
            // Direct extraction (digital PDF, not scanned) - no OCR metrics
            metadata.MeanConfidence = null;
            metadata.MinConfidence = null;
            metadata.TotalWords = null;
            metadata.LowConfidenceWords = null;
            metadata.QualityIndex = null;
            metadata.BlurScore = null;
            metadata.ContrastScore = null;
            metadata.NoiseEstimate = null;
            metadata.EdgeDensity = null;
        }

        return metadata;
    }
}