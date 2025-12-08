namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// DOCX field extractor implementation for extracting structured fields from DOCX documents.
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="DocxSource"/>.
/// </summary>
public class DocxFieldExtractor : IFieldExtractor<DocxSource>
{
    private readonly ILogger<DocxFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxFieldExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DocxFieldExtractor(ILogger<DocxFieldExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(DocxSource source, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Extracting fields from DOCX document: {FilePath}", source.FilePath);

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
                return Task.FromResult(Result<ExtractedFields>.WithFailure("DOCX source must have either FileContent or valid FilePath"));
            }

            // Extract text from DOCX
            var textResult = ExtractTextFromDocx(fileContent);
            if (textResult.IsFailure)
            {
                return Task.FromResult(Result<ExtractedFields>.WithFailure($"Failed to extract text from DOCX: {textResult.Error}"));
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = 1.0f; // DOCX text extraction has high confidence

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

            _logger.LogDebug("Successfully extracted {Count} fields from DOCX document", fieldDefinitions.Length);
            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fields from DOCX");
            return Task.FromResult(Result<ExtractedFields>.WithFailure($"Error extracting DOCX fields: {ex.Message}", default(ExtractedFields), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<FieldValue>> ExtractFieldAsync(DocxSource source, string fieldName)
    {
        try
        {
            _logger.LogDebug("Extracting field {FieldName} from DOCX document: {FilePath}", fieldName, source.FilePath);

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
                return Task.FromResult(Result<FieldValue>.WithFailure("DOCX source must have either FileContent or valid FilePath"));
            }

            // Extract text from DOCX
            var textResult = ExtractTextFromDocx(fileContent);
            if (textResult.IsFailure)
            {
                return Task.FromResult(Result<FieldValue>.WithFailure($"Failed to extract text from DOCX: {textResult.Error}"));
            }

            var text = textResult.Value ?? string.Empty;
            var confidence = 1.0f; // DOCX text extraction has high confidence

            // Extract specific field
            var fieldResult = ExtractFieldByName(text, fieldName, confidence);
            if (fieldResult.IsSuccess && fieldResult.Value != null)
            {
                return Task.FromResult(Result<FieldValue>.Success(fieldResult.Value));
            }

            return Task.FromResult(Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in DOCX document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting field {FieldName} from DOCX", fieldName);
            return Task.FromResult(Result<FieldValue>.WithFailure($"Error extracting field from DOCX: {ex.Message}", default(FieldValue), ex));
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
            return Result<FieldValue>.Success(new FieldValue(fieldName, value, confidence, "DOCX", FieldOrigin.Docx));
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

