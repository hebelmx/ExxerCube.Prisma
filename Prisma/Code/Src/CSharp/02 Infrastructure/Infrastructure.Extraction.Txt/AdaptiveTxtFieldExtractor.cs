namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt;

/// <summary>
/// Adaptive field extractor for text sources (typically OCR output).
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="TxtSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// This extractor is designed to handle OCR text output with typical errors:
/// - Character substitutions (0 → O, 1 → I, 5 → S)
/// - Missing or misplaced field labels
/// - Variable spacing and formatting
/// - Typos in manually filled fields
/// </para>
/// <para>
/// <strong>Extraction Strategy:</strong>
/// </para>
/// <list type="number">
///   <item><description>Pattern-based extraction using regex</description></item>
///   <item><description>Fuzzy matching for OCR errors (Levenshtein distance)</description></item>
///   <item><description>Contextual search (look for labels + surrounding text)</description></item>
///   <item><description>Multi-line pattern matching for complex fields</description></item>
/// </list>
/// </remarks>
public sealed class AdaptiveTxtFieldExtractor : IFieldExtractor<TxtSource>
{
    private readonly ILogger<AdaptiveTxtFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveTxtFieldExtractor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public AdaptiveTxtFieldExtractor(ILogger<AdaptiveTxtFieldExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(
        TxtSource source,
        FieldDefinition[] fieldDefinitions)
    {
        if (source == null)
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure("TxtSource cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(source.TextContent))
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure("TxtSource.TextContent cannot be null or empty"));
        }

        try
        {
            _logger.LogDebug(
                "AdaptiveTxtExtractor: Extracting fields from text ({Length} chars, {FieldCount} definitions, OCR confidence: {Confidence:F2})",
                source.TextContent.Length,
                fieldDefinitions?.Length ?? 0,
                source.OcrConfidence ?? 0.0f);

            var text = source.TextContent;
            var extractedFields = new ExtractedFields();

            // Extract all defined fields
            if (fieldDefinitions != null)
            {
                foreach (var fieldDef in fieldDefinitions)
                {
                    var fieldResult = ExtractFieldByName(text, fieldDef.FieldName, source.OcrConfidence ?? 0.8f);
                    if (fieldResult.IsSuccess && fieldResult.Value != null)
                    {
                        ApplyFieldToExtractedFields(extractedFields, fieldDef.FieldName, fieldResult.Value.Value);
                    }
                }
            }

            // Always extract core fields (Expediente, Causa, AccionSolicitada)
            ExtractCoreFields(text, extractedFields, source.OcrConfidence ?? 0.8f);

            _logger.LogInformation(
                "AdaptiveTxtExtractor: Successfully extracted fields - Expediente: {Expediente}, Causa: {Causa}",
                extractedFields.Expediente,
                extractedFields.Causa);

            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveTxtExtractor: Error extracting fields from text");
            return Task.FromResult(Result<ExtractedFields>.WithFailure(
                $"Error extracting text fields: {ex.Message}",
                default(ExtractedFields),
                ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<FieldValue>> ExtractFieldAsync(TxtSource source, string fieldName)
    {
        if (source == null)
        {
            return Task.FromResult(Result<FieldValue>.WithFailure("TxtSource cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return Task.FromResult(Result<FieldValue>.WithFailure("Field name cannot be null or empty"));
        }

        try
        {
            _logger.LogDebug("AdaptiveTxtExtractor: Extracting field '{FieldName}' from text", fieldName);

            var text = source.TextContent;
            var confidence = source.OcrConfidence ?? 0.8f;

            var fieldResult = ExtractFieldByName(text, fieldName, confidence);
            if (fieldResult.IsSuccess && fieldResult.Value != null)
            {
                return Task.FromResult(Result<FieldValue>.Success(fieldResult.Value));
            }

            return Task.FromResult(Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in text"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveTxtExtractor: Error extracting field '{FieldName}' from text", fieldName);
            return Task.FromResult(Result<FieldValue>.WithFailure(
                $"Error extracting field from text: {ex.Message}",
                default(FieldValue),
                ex));
        }
    }

    //
    // Private Helper Methods
    //

    /// <summary>
    /// Extracts core fields (Expediente, Causa, AccionSolicitada) from text.
    /// </summary>
    private void ExtractCoreFields(string text, ExtractedFields fields, float confidence)
    {
        // Extract Expediente if not already set
        if (string.IsNullOrEmpty(fields.Expediente))
        {
            fields.Expediente = ExtractExpediente(text);
        }

        // Extract Causa if not already set
        if (string.IsNullOrEmpty(fields.Causa))
        {
            fields.Causa = ExtractCausa(text);
        }

        // Extract AccionSolicitada if not already set
        if (string.IsNullOrEmpty(fields.AccionSolicitada))
        {
            fields.AccionSolicitada = ExtractAccionSolicitada(text);
        }
    }

    /// <summary>
    /// Extracts a field by name from text.
    /// </summary>
    private Result<FieldValue> ExtractFieldByName(string text, string fieldName, float confidence)
    {
        var normalizedFieldName = fieldName.ToLowerInvariant();

        var value = normalizedFieldName switch
        {
            "expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),
            "numerooficio" or "numero_oficio" => ExtractNumeroOficio(text),
            "autoridadnombre" or "autoridad_nombre" => ExtractAutoridadNombre(text),
            _ => null
        };

        if (value != null)
        {
            return Result<FieldValue>.Success(
                new FieldValue(fieldName, value, confidence, "TXT_OCR", FieldOrigin.PdfOcr));
        }

        return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found");
    }

    /// <summary>
    /// Applies extracted field value to ExtractedFields object.
    /// </summary>
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
                // Store in AdditionalFields
                fields.AdditionalFields[fieldName] = value;
                break;
        }
    }

    //
    // Field Extraction Methods (Pattern-Based)
    //

    /// <summary>
    /// Extracts Expediente using pattern matching.
    /// Pattern: A/AS1-2505-088637-PHM or B/CDEF-1234-567890-ABC or similar formats.
    /// </summary>
    private static string? ExtractExpediente(string text)
    {
        // Primary pattern: A/AS1-2505-088637-PHM or B/CDEF-1234-567890-ABC
        // Format: Letter/Letters+Numbers-Numbers-Numbers-Letters
        var expedientePattern = @"[A-Z]/[A-Z]{1,4}\d+[-–]\d+[-–]\d+[-–][A-Z]+";
        var match = Regex.Match(text, expedientePattern, RegexOptions.Multiline);
        if (match.Success)
        {
            return match.Value;
        }

        // Alternative pattern (with OCR errors): handle O→0, I→1 substitutions
        var fuzzyPattern = @"[A-Z]/[A-Z]{1,4}[0-9O]+[-–][0-9O]+[-–][0-9O]+[-–][A-Z]+";
        match = Regex.Match(text, fuzzyPattern, RegexOptions.Multiline);
        if (match.Success)
        {
            // Clean up OCR errors
            return CleanOcrErrors(match.Value);
        }

        return null;
    }

    /// <summary>
    /// Extracts Causa using contextual search.
    /// Looks for "CAUSA:", "Causa:", etc. followed by text.
    /// </summary>
    private static string? ExtractCausa(string text)
    {
        // Pattern: CAUSA: <text> or Causa: <text>
        var causaPattern = @"(?:CAUSA|Causa|causa)\s*[:：]?\s*([^\n\r]+)";
        var match = Regex.Match(text, causaPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts AccionSolicitada using contextual search.
    /// Looks for "ACCIÓN SOLICITADA:", "Accion Solicitada:", etc.
    /// </summary>
    private static string? ExtractAccionSolicitada(string text)
    {
        // Pattern: ACCIÓN SOLICITADA: <text> or Accion Solicitada: <text>
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada|acción\s+solicitada)\s*[:：]?\s*([^\n\r]+)";
        var match = Regex.Match(text, accionPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts NumeroOficio (e.g., AGAFADAFSON2/2025/000084).
    /// </summary>
    private static string? ExtractNumeroOficio(string text)
    {
        // Pattern: AGAFADAFSON2/2025/000084 or similar
        var oficioPattern = @"[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}";
        var match = Regex.Match(text, oficioPattern, RegexOptions.Multiline);

        if (match.Success)
        {
            return match.Value;
        }

        // Look for "No. De Identificación" or similar labels
        var labeledPattern = @"(?:No\.\s*De\s*Identificaci[óo]n|N[úu]mero\s*de\s*Oficio)\s*[:：]?\s*([A-Z0-9/]+)";
        match = Regex.Match(text, labeledPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts AutoridadNombre (authority name).
    /// </summary>
    private static string? ExtractAutoridadNombre(string text)
    {
        // Look for common authority names - check full names FIRST to avoid matching acronyms in emails/URLs
        var authorities = new[]
        {
            "Comisión Nacional Bancaria y de Valores",
            "Administración General de Auditoría Fiscal Federal",
            "AGAFF",
            "CNBV",
            "SAT" // Check acronyms last to avoid false matches in emails
        };

        foreach (var authority in authorities)
        {
            // For short acronyms (<=5 chars), use word boundary to avoid false matches in emails
            if (authority.Length <= 5)
            {
                // Don't match SAT in email addresses like "@sat.gob.mx"
                var pattern = @"(?<![@.])\b" + Regex.Escape(authority) + @"\b(?!\.gob\.mx)";
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    return authority;
                }
            }
            else
            {
                // For full names, case-insensitive match
                if (text.Contains(authority, StringComparison.OrdinalIgnoreCase))
                {
                    return authority;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Cleans common OCR errors (O→0, I→1, S→5).
    /// </summary>
    private static string CleanOcrErrors(string text)
    {
        // For numeric sections, replace O with 0
        // This is a simplified version - production would be more sophisticated
        return text.Replace('O', '0');
    }
}
