namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Strategies;

/// <summary>
/// Structured extraction strategy for CNBV-standard formatted DOCX documents.
/// Uses regex patterns to extract fields from well-formatted documents with predictable structure.
/// High confidence for documents matching CNBV template patterns.
/// </summary>
public sealed class StructuredDocxStrategy : IDocxExtractionStrategy
{
    private readonly ILogger<StructuredDocxStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public StructuredDocxStrategy(ILogger<StructuredDocxStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public DocxExtractionStrategy StrategyType => DocxExtractionStrategy.Structured;

    /// <inheritdoc />
    public float CalculateConfidence(DocxStructure structure)
    {
        // High confidence if matches CNBV structured format
        if (structure.HasStructuredFormat)
        {
            return 0.95f;
        }

        // Medium confidence if has key-value pairs and styled elements
        if (structure.HasKeyValuePairs && structure.StyledElementCount > 10)
        {
            return 0.70f;
        }

        // Low confidence for semi-structured documents
        if (structure.HasKeyValuePairs || structure.StyledElementCount > 5)
        {
            return 0.50f;
        }

        // Not suitable for unstructured documents
        return 0.0f;
    }

    /// <inheritdoc />
    public bool CanHandle(DocxStructure structure)
    {
        // Can handle if has structured format or sufficient structure indicators
        return structure.HasStructuredFormat ||
               structure.HasKeyValuePairs ||
               structure.StyledElementCount > 5;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractAsync(DocxSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Structured strategy extracting fields from DOCX: {FilePath}", source.FilePath);

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
            var confidence = 0.95f; // High confidence for structured extraction

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

            _logger.LogDebug("Structured strategy extracted {Count} fields from DOCX", fieldDefinitions.Length);
            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Structured strategy error extracting fields from DOCX");
            return Task.FromResult(Result<ExtractedFields>.WithFailure(
                $"Structured extraction error: {ex.Message}", default(ExtractedFields), ex));
        }
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
            return Result<FieldValue>.Success(new FieldValue(
                fieldName, value, confidence, "DOCX-Structured", FieldOrigin.Docx));
        }

        return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in structured document");
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
        var match = System.Text.RegularExpressions.Regex.Match(text, causaPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Look for "ACCIÓN SOLICITADA:" or "Accion Solicitada:" followed by text
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada)\s*:?\s*([^\n\r]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, accionPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}
