namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// DOCX metadata extractor implementation for extracting metadata from DOCX documents using DocumentFormat.OpenXml.
/// </summary>
public class DocxMetadataExtractor : IMetadataExtractor
{
    private readonly ILogger<DocxMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxMetadataExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DocxMetadataExtractor(ILogger<DocxMetadataExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting metadata from DOCX document");

            using var stream = new System.IO.MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Task.FromResult(Result<ExtractedMetadata>.WithFailure("DOCX document has no main document part"));
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Task.FromResult(Result<ExtractedMetadata>.WithFailure("DOCX document has no body"));
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

            // Extract structured fields using pattern matching
            var expediente = ExtractExpediente(textContent);
            var rfcValues = ExtractRfcValues(textContent);
            var names = ExtractNames(textContent);
            var dates = ExtractDates(textContent);
            var legalReferences = ExtractLegalReferences(textContent);

            // Build extraction metadata for fusion quality scoring (DRY principle)
            ExtractionMetadata? qualityMetadata = null;
            if (expediente != null)
            {
                qualityMetadata = BuildExtractionMetadata(expediente, textContent, rfcValues);
            }

            var metadata = new ExtractedMetadata
            {
                Expediente = expediente,
                RfcValues = rfcValues.Length > 0 ? rfcValues : null,
                Names = names.Length > 0 ? names : null,
                Dates = dates.Length > 0 ? dates : null,
                LegalReferences = legalReferences.Length > 0 ? legalReferences : null,
                QualityMetadata = qualityMetadata
            };

            _logger.LogDebug("Successfully extracted metadata from DOCX document");
            return Task.FromResult(Result<ExtractedMetadata>.Success(metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from DOCX");
            return Task.FromResult(Result<ExtractedMetadata>.WithFailure($"Error extracting DOCX metadata: {ex.Message}", default(ExtractedMetadata), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("XML extraction not supported by DocxMetadataExtractor. Use XmlMetadataExtractor instead."));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("PDF extraction not supported by DocxMetadataExtractor. Use PdfMetadataExtractor instead."));
    }

    private static Expediente? ExtractExpediente(string text)
    {
        // Pattern: A/AS1-2505-088637-PHM or similar
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

                // Law-mandated fields - best-effort extraction from DOCX text
                LawMandatedFields = ExtractLawMandatedFieldsFromText(autoridadNombre, areaDescripcion),

                // Semantic analysis - null until classification engine runs
                SemanticAnalysis = null,

                // Future-proofing: capture unknown fields (not applicable for DOCX extraction)
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
    /// Best-effort extraction of law-mandated fields from DOCX text.
    /// Populates what we can from unstructured text; bank systems will enrich missing fields later.
    /// </summary>
    private static LawMandatedFields? ExtractLawMandatedFieldsFromText(string? autoridadNombre, string areaDescripcion)
    {
        // Only create LawMandatedFields if we can populate at least one field
        var hasData = !string.IsNullOrWhiteSpace(autoridadNombre) ||
                      !string.IsNullOrWhiteSpace(areaDescripcion);

        if (!hasData)
        {
            return null; // No law-mandated data available from DOCX
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
        // RFC pattern: 4 letters, 6 digits, 3 alphanumeric
        var rfcPattern = @"[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, rfcPattern);
        return matches.Select(m => m.Value).Distinct().ToArray();
    }

    private static string[] ExtractNames(string text)
    {
        // Simple name extraction - look for capitalized words sequences
        var namePattern = @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, namePattern);
        return matches.Select(m => m.Value.Trim()).Distinct().Take(10).ToArray();
    }

    private static DateTime[] ExtractDates(string text)
    {
        // Date patterns: DD/MM/YYYY, DD-MM-YYYY, etc.
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
        // Look for legal reference patterns
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

    /// <inheritdoc />
    public Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("DOCX text extraction cancelled before starting");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        try
        {
            _logger.LogDebug("Extracting text from DOCX document");

            using var stream = new System.IO.MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Task.FromResult(Result<string>.WithFailure("DOCX document has no main document part"));
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Task.FromResult(Result<string>.WithFailure("DOCX document has no body"));
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

            _logger.LogDebug("Successfully extracted text from DOCX document (length: {Length})", textContent.Length);
            return Task.FromResult(Result<string>.Success(textContent));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("DOCX text extraction cancelled");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from DOCX");
            return Task.FromResult(Result<string>.WithFailure($"Error extracting DOCX text: {ex.Message}", default(string), ex));
        }
    }

    /// <summary>
    /// Builds extraction metadata for multi-source data fusion quality scoring.
    /// Applies DRY principle: cleaning, validation, and confidence calculation happen ONCE here.
    /// </summary>
    /// <param name="expediente">The extracted Expediente entity (partial from DOCX).</param>
    /// <param name="extractedText">The full text extracted from DOCX.</param>
    /// <param name="rfcValues">RFC values extracted from text.</param>
    /// <returns>Extraction metadata with quality metrics.</returns>
    /// <remarks>
    /// For DOCX extraction (authority OCR):
    /// - OCR confidence estimates (DOCX from authorities may have varying quality)
    /// - Image quality metrics (estimated based on text quality)
    /// - Pattern validation and catalog validation
    /// - Base reliability: 0.70 (authority scans may have inconsistent quality)
    /// </remarks>
    private static ExtractionMetadata BuildExtractionMetadata(
        Expediente expediente,
        string extractedText,
        string[] rfcValues)
    {
        var metadata = new ExtractionMetadata
        {
            Source = SourceType.DOCX_OCR_Authority
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

        // Count extracted fields (DOCX extraction is minimal compared to XML)
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

        // OCR metrics (estimated - DOCX from authorities may have varying quality)
        var wordCount = extractedText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        metadata.TotalWords = wordCount;

        // Heuristic: Authority DOCX files have moderate quality (not as good as CNBV PDFs)
        // Real implementation would get this from actual OCR if DOCX was generated from scanned documents
        metadata.MeanConfidence = 0.70; // Moderate confidence for authority documents
        metadata.MinConfidence = 0.55;  // Lower minimum than CNBV
        metadata.LowConfidenceWords = (int)(wordCount * 0.15); // Estimate 15% low confidence

        // Image quality metrics (estimated - authority scans may vary)
        metadata.QualityIndex = 0.70;
        metadata.BlurScore = 0.25;      // Moderate blur
        metadata.ContrastScore = 0.65;  // Moderate contrast
        metadata.NoiseEstimate = 0.15;  // Moderate noise
        metadata.EdgeDensity = 0.55;    // Moderate edge density

        return metadata;
    }
}

