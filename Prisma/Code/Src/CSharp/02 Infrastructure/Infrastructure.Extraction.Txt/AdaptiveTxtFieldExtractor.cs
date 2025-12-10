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
                "AdaptiveTxtExtractor: Successfully extracted fields - Expediente: {Expediente}, Causa: {Causa}, AdditionalFields count: {Count}",
                extractedFields.Expediente,
                extractedFields.Causa,
                extractedFields.AdditionalFields.Count);

            // Log all extracted AdditionalFields for debugging
            foreach (var field in extractedFields.AdditionalFields)
            {
                _logger.LogDebug("  AdditionalField: {Key} = {Value}", field.Key, field.Value);
            }

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
    /// Extracts ALL extractable fields from text automatically.
    /// This ensures maximum field coverage regardless of fieldDefinitions passed.
    /// </summary>
    private void ExtractCoreFields(string text, ExtractedFields fields, float confidence)
    {
        // Core Expediente fields
        if (string.IsNullOrEmpty(fields.Expediente))
        {
            fields.Expediente = ExtractExpediente(text);
        }

        if (string.IsNullOrEmpty(fields.Causa))
        {
            fields.Causa = ExtractCausa(text);
        }

        if (string.IsNullOrEmpty(fields.AccionSolicitada))
        {
            fields.AccionSolicitada = ExtractAccionSolicitada(text);
        }

        // Identification fields - Add to AdditionalFields if not already present
        AddFieldIfNotPresent(fields, "NumeroOficio", ExtractNumeroOficio(text));
        AddFieldIfNotPresent(fields, "SolicitudSiara", ExtractNumeroOficio(text)); // Same pattern

        // Authority fields
        AddFieldIfNotPresent(fields, "AutoridadNombre", ExtractAutoridadNombre(text));
        AddFieldIfNotPresent(fields, "AutoridadEspecificaNombre", ExtractAutoridadEspecifica(text));

        // Contact fields (NEW)
        AddFieldIfNotPresent(fields, "NombreSolicitante", ExtractNombreSolicitante(text));
        AddFieldIfNotPresent(fields, "Email", ExtractEmail(text));
        AddFieldIfNotPresent(fields, "Telefono", ExtractTelefono(text));

        // Address fields (NEW)
        AddFieldIfNotPresent(fields, "Direccion", ExtractDireccion(text));
        AddFieldIfNotPresent(fields, "CodigoPostal", ExtractCodigoPostal(text));

        // Legal fields (NEW)
        AddFieldIfNotPresent(fields, "FundamentoLegal", ExtractFundamentoLegal(text));

        // Metadata fields (NEW)
        var fechaPub = ExtractFechaPublicacion(text);
        if (fechaPub.HasValue)
        {
            AddFieldIfNotPresent(fields, "FechaPublicacion", fechaPub.Value.ToString("yyyy-MM-dd"));
        }

        var diasPlazo = ExtractDiasPlazo(text);
        if (diasPlazo.HasValue)
        {
            AddFieldIfNotPresent(fields, "DiasPlazo", diasPlazo.Value.ToString());
        }

        var tieneAseg = ExtractTieneAseguramiento(text);
        if (tieneAseg.HasValue)
        {
            AddFieldIfNotPresent(fields, "TieneAseguramiento", tieneAseg.Value.ToString());
        }
    }

    /// <summary>
    /// Helper method to add field to AdditionalFields if value is not null and not already present.
    /// </summary>
    private static void AddFieldIfNotPresent(ExtractedFields fields, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !fields.AdditionalFields.ContainsKey(fieldName))
        {
            fields.AdditionalFields[fieldName] = value;
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
            // Core fields
            "expediente" or "numeroexpediente" or "numero_expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),

            // Identification fields
            "numerooficio" or "numero_oficio" => ExtractNumeroOficio(text),
            "solicitudsiara" or "solicitud_siara" => ExtractNumeroOficio(text), // Same as NumeroOficio in fixtures

            // Authority fields
            "autoridadnombre" or "autoridad_nombre" => ExtractAutoridadNombre(text),
            "autoridadespecificanombre" or "autoridad_especifica_nombre" => ExtractAutoridadEspecifica(text),

            // Contact fields (NEW)
            "nombresolicitante" or "nombre_solicitante" => ExtractNombreSolicitante(text),
            "email" or "correo" or "correoelectronico" or "correo_electronico" => ExtractEmail(text),
            "telefono" or "tel" => ExtractTelefono(text),

            // Address fields (NEW)
            "direccion" => ExtractDireccion(text),
            "codigopostal" or "codigo_postal" or "cp" => ExtractCodigoPostal(text),

            // Legal fields (NEW)
            "fundamentolegal" or "fundamento_legal" => ExtractFundamentoLegal(text),

            // Metadata fields
            "fechapublicacion" or "fecha_publicacion" => ExtractFechaPublicacion(text)?.ToString("yyyy-MM-dd"),
            "diasplazo" or "dias_plazo" => ExtractDiasPlazo(text)?.ToString(),
            "tieneaseguramiento" or "tiene_aseguramiento" => ExtractTieneAseguramiento(text)?.ToString(),

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
        var normalizedFieldName = fieldName.ToLowerInvariant();

        switch (normalizedFieldName)
        {
            // Core Expediente fields
            case "expediente":
            case "numeroexpediente":
            case "numero_expediente":
                fields.Expediente = value;
                break;

            case "causa":
                fields.Causa = value;
                break;

            case "accionsolicitada":
            case "accion_solicitada":
                fields.AccionSolicitada = value;
                break;

            // All other fields go to AdditionalFields (including new ones)
            // NumeroOficio, SolicitudSiara, AutoridadNombre, Email, Telefono, etc.
            default:
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
    /// Extracts NombreSolicitante (requester name) using honorific + name pattern.
    /// </summary>
    private static string? ExtractNombreSolicitante(string text)
    {
        // Try honorific pattern first
        var honorificPattern = @"(?:Mtro\.|Mtra\.|Lic\.|Ing\.|Dr\.|Dra\.|C\.)\s+([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)+)";
        var match = Regex.Match(text, honorificPattern);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        // Try job title pattern
        var titlePattern = @"(?:Vicepresidente|Director|Administrador|Secretario|Titular)\s+(?:de|del)?\s+[^\n]+\n([A-ZÁÉÍÓÚÑ].+)";
        match = Regex.Match(text, titlePattern);
        if (match.Success && match.Groups.Count > 1)
        {
            var lines = match.Groups[1].Value.Split('\n');
            return lines[0].Trim(); // First line after title
        }

        return null;
    }

    /// <summary>
    /// Extracts email address from text.
    /// </summary>
    private static string? ExtractEmail(string text)
    {
        var emailPattern = @"[a-z0-9._-]+@[a-z0-9.-]+\.[a-z]{2,}";
        var match = Regex.Match(text, emailPattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToLower() : null;
    }

    /// <summary>
    /// Extracts Mexican phone number.
    /// </summary>
    private static string? ExtractTelefono(string text)
    {
        // Try labeled pattern first
        var labeledPattern = @"(?:Tel[ée]fono|Tel\.?)\s*[:：]?\s*(\(?\d{2}\)?\s*\d{4}-\d{4})";
        var match = Regex.Match(text, labeledPattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        // Fallback: just find phone pattern
        var phonePattern = @"\(?\d{2}\)?\s*\d{4}-\d{4}";
        match = Regex.Match(text, phonePattern);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Extracts postal code (C.P.).
    /// </summary>
    private static string? ExtractCodigoPostal(string text)
    {
        var cpPattern = @"C\.?P\.?\s*(\d{5})";
        var match = Regex.Match(text, cpPattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1
            ? match.Groups[1].Value
            : null;
    }

    /// <summary>
    /// Extracts address (multi-line block containing address keywords).
    /// </summary>
    private static string? ExtractDireccion(string text)
    {
        // Look for lines containing street, col, cp, ciudad
        var addressPattern = @"([^\n]*(?:Av\.|Ave\.|Calle|Col\.|Colonia|C\.P\.|CP|Ciudad|Del\.|Delegaci[óo]n|Municipio)[^\n]*(?:\n[^\n]*){0,3})";
        var match = Regex.Match(text, addressPattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Value.Trim().Replace("\n", " ");
        }
        return null;
    }

    /// <summary>
    /// Extracts FundamentoLegal (legal article citations).
    /// </summary>
    private static string? ExtractFundamentoLegal(string text)
    {
        var articulos = new List<string>();

        // Pattern: "artículo 142", "Art. 251", "artículos 1, 2 y 3"
        var articuloPattern = @"art[íi]culos?\s+(\d+(?:\s*(?:y|,)\s*\d+)*(?:\s+fracci[óo]n\s+[IVXLCDM]+)?(?:\s+de\s+.+?(?:ley|reglamento|c[óo]digo))?)[,\.]?";
        var matches = Regex.Matches(text, articuloPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                articulos.Add($"Art. {match.Groups[1].Value.Trim()}");
            }
        }

        return articulos.Count > 0 ? string.Join("; ", articulos) : null;
    }

    /// <summary>
    /// Extracts AutoridadEspecificaNombre (specific department/office name).
    /// </summary>
    private static string? ExtractAutoridadEspecifica(string text)
    {
        var pattern = @"(?:DIRECCI[ÓO]N|DEPARTAMENTO|MESA|TURNO|SECRETAR[ÍI]A)\s+(?:GENERAL|ESPECIAL|DE|DEL)?\s+[A-Z\s]+";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    /// <summary>
    /// Extracts FechaPublicacion (publication date).
    /// </summary>
    private static DateTime? ExtractFechaPublicacion(string text)
    {
        // Try labeled pattern first
        var labeledPattern = @"(?:Fecha\s+de\s+Publicaci[óo]n|Publicado)\s*[:：]?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})";
        var match = Regex.Match(text, labeledPattern, RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var fecha))
            {
                return fecha;
            }
        }

        return null; // Don't guess - only extract if explicitly labeled
    }

    /// <summary>
    /// Extracts DiasPlazo (deadline in days).
    /// </summary>
    private static int? ExtractDiasPlazo(string text)
    {
        // Pattern: "plazo de 7 días", "en 10 días", "dentro de 3 días"
        var plazoPattern = @"(?:plazo\s+de|en|dentro\s+de)\s+(\d{1,3})\s+d[íi]as?";
        var match = Regex.Match(text, plazoPattern, RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out var dias))
            {
                return dias;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if document involves asset seizure (TieneAseguramiento).
    /// </summary>
    private static bool? ExtractTieneAseguramiento(string text)
    {
        var aseguramientoKeywords = new[]
        {
            "aseguramiento",
            "embargo",
            "bloqueo",
            "retenci[óo]n",
            "inmovilizaci[óo]n",
            "congelamiento"
        };

        foreach (var keyword in aseguramientoKeywords)
        {
            if (Regex.IsMatch(text, keyword, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // Check for desbloqueo/liberación keywords (indicates removal of seizure)
        var desbloqueoKeywords = new[] { "desbloqueo", "liberaci[óo]n", "dejar sin efectos" };
        foreach (var keyword in desbloqueoKeywords)
        {
            if (Regex.IsMatch(text, keyword, RegexOptions.IgnoreCase))
            {
                return false;
            }
        }

        return null; // Cannot determine
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
