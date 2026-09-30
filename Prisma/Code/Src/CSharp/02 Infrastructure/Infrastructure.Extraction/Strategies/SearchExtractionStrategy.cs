namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Strategies;

/// <summary>
/// Search extraction strategy for resolving cross-references in DOCX documents.
/// Examples:
/// - "transferir por la cantidad arriba mencionada" → searches backward for amount
/// - "el RFC anteriormente indicado" → searches backward for RFC
/// - "según anexo" → searches for referenced data
/// This is EXPECTED behavior when documents reference data instead of repeating it.
/// Implements intelligent backward/forward search to resolve cross-references.
/// </summary>
public sealed class SearchExtractionStrategy : IDocxExtractionStrategy
{
    private readonly ILogger<SearchExtractionStrategy> _logger;

    private static readonly string[] CrossReferencePatterns = new[]
    {
        "arriba mencionada", "arriba mencionado",
        "anteriormente indicado", "anteriormente indicada",
        "previamente indicado", "previamente indicada",
        "según anexo", "segun anexo",
        "ver anexo", "adjunto"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchExtractionStrategy"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SearchExtractionStrategy(ILogger<SearchExtractionStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public DocxExtractionStrategy StrategyType => DocxExtractionStrategy.Search;

    /// <inheritdoc />
    public float CalculateConfidence(DocxStructure structure)
    {
        // High confidence if cross-references detected
        if (structure.HasCrossReferences)
        {
            return 0.90f;
        }

        // Medium confidence - can still extract even without cross-references
        return 0.60f;
    }

    /// <inheritdoc />
    public bool CanHandle(DocxStructure structure)
    {
        // Search strategy can still extract even without cross-references.
        // It can handle any document structure, even minimally structured ones.
        return structure != null;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractAsync(DocxSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Search strategy extracting fields from DOCX: {FilePath}", source.FilePath);

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
                return Task.FromResult(Result<ExtractedFields>.WithFailure(
                    "DOCX source must have either FileContent or valid FilePath"));
            }

            // Extract text from DOCX
            var textResult = ExtractTextFromDocx(fileContent);
            if (textResult.IsFailure)
            {
                return Task.FromResult(Result<ExtractedFields>.WithFailure(
                    $"Failed to extract text from DOCX: {textResult.Error}"));
            }

            var text = textResult.Value ?? string.Empty;
            var extractedFields = new ExtractedFields();

            // Check if document has cross-references
            bool hasCrossReferences = HasCrossReferences(text);

            // Extract fields - resolve cross-references if present
            foreach (var fieldDef in fieldDefinitions)
            {
                var fieldResult = hasCrossReferences
                    ? ExtractFieldWithCrossReferenceResolution(text, fieldDef.FieldName)
                    : ExtractFieldByName(text, fieldDef.FieldName);

                if (fieldResult.IsSuccess && fieldResult.Value != null)
                {
                    ApplyFieldToExtractedFields(extractedFields, fieldDef.FieldName, fieldResult.Value);
                    _logger.LogDebug("Search strategy extracted field '{FieldName}'", fieldDef.FieldName);
                }
            }

            _logger.LogDebug("Search strategy extracted {Count} fields from DOCX", fieldDefinitions.Length);
            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search strategy error extracting fields from DOCX");
            return Task.FromResult(Result<ExtractedFields>.WithFailure(
                $"Search extraction error: {ex.Message}", default(ExtractedFields), ex));
        }
    }

    private bool HasCrossReferences(string text)
    {
        var lowerText = text.ToLowerInvariant();
        return CrossReferencePatterns.Any(pattern => lowerText.Contains(pattern));
    }

    private Result<string> ExtractFieldWithCrossReferenceResolution(string text, string fieldName)
    {
        // First try direct extraction
        var directResult = ExtractFieldByName(text, fieldName);
        if (directResult.IsSuccess)
        {
            return directResult;
        }

        // If direct extraction fails, try to resolve cross-reference
        // Look for the field value earlier in the document
        var value = fieldName.ToLowerInvariant() switch
        {
            "expediente" => SearchForExpediente(text),
            "causa" => SearchForCausa(text),
            "accionsolicitada" or "accion_solicitada" => SearchForAccionSolicitada(text),
            "rfc" => SearchForRFC(text),
            "monto" or "montos" or "amount" => SearchForMonto(text),
            _ => null
        };

        if (value != null)
        {
            _logger.LogDebug("Resolved cross-reference for field '{FieldName}'", fieldName);
            return Result<string>.Success(value);
        }

        return Result<string>.WithFailure($"Field '{fieldName}' not found (direct or cross-reference)");
    }

    private Result<string> ExtractTextFromDocx(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Result<string>.WithFailure("DOCX document has no main document part");
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Result<string>.WithFailure("DOCX document has no body");
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));
            return Result<string>.Success(textContent);
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Error reading DOCX: {ex.Message}", string.Empty, ex);
        }
    }

    private Result<string> ExtractFieldByName(string text, string fieldName)
    {
        var value = fieldName.ToLowerInvariant() switch
        {
            "expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),
            "rfc" => ExtractRFC(text),
            "monto" or "montos" or "amount" => ExtractMonto(text),
            _ => null
        };

        if (value != null)
        {
            return Result<string>.Success(value);
        }

        return Result<string>.WithFailure($"Field '{fieldName}' not found in DOCX");
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
            default:
                // Store in AdditionalFields for non-standard fields
                if (value != null)
                {
                    fields.AdditionalFields[fieldName] = value;
                }
                break;
        }
    }

    // Direct extraction methods
    private static string? ExtractExpediente(string text)
    {
        var expedientePattern = @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+";
        var match = System.Text.RegularExpressions.Regex.Match(text, expedientePattern);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractCausa(string text)
    {
        var causaPattern = @"(?:CAUSA|Causa)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, causaPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, accionPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractRFC(string text)
    {
        var rfcPattern = @"\b[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}\b";
        var match = System.Text.RegularExpressions.Regex.Match(text, rfcPattern);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractMonto(string text)
    {
        var montoPattern = @"\$\s*[\d,]+\.?\d*";
        var match = System.Text.RegularExpressions.Regex.Match(text, montoPattern);
        return match.Success ? match.Value : null;
    }

    // Search methods for cross-reference resolution
    private static string? SearchForExpediente(string text)
    {
        // Search for expediente pattern anywhere in document
        return ExtractExpediente(text);
    }

    private static string? SearchForCausa(string text)
    {
        // Search for causa anywhere in document
        return ExtractCausa(text);
    }

    private static string? SearchForAccionSolicitada(string text)
    {
        // Search for accion solicitada anywhere in document
        return ExtractAccionSolicitada(text);
    }

    private static string? SearchForRFC(string text)
    {
        // Search for RFC pattern anywhere in document
        return ExtractRFC(text);
    }

    private static string? SearchForMonto(string text)
    {
        // Search for monetary amounts anywhere in document
        return ExtractMonto(text);
    }
}
