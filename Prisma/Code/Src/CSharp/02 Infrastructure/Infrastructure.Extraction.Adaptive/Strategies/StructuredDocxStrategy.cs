namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;

/// <summary>
/// Adaptive DOCX extraction strategy for well-formatted CNBV documents.
/// </summary>
/// <remarks>
/// <para>
/// Uses regex patterns to extract fields from structured documents with predictable formatting.
/// Best suited for standard CNBV templates with labeled fields.
/// </para>
/// <para>
/// <strong>Confidence Scoring:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>90: 3+ standard CNBV labels found (Expediente, Oficio, Autoridad, etc.)</description></item>
///   <item><description>75: 2 standard labels found</description></item>
///   <item><description>50: 1 standard label found</description></item>
///   <item><description>0: No standard labels found</description></item>
/// </list>
/// </remarks>
public sealed class StructuredDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<StructuredDocxStrategy> _logger;

    /// <summary>
    /// Standard CNBV field labels used for confidence scoring.
    /// </summary>
    private static readonly string[] StandardLabels = new[]
    {
        "Expediente No",
        "Expediente:",
        "Oficio:",
        "Oficio No",
        "Autoridad:",
        "Causa:",
        "Acción Solicitada:",
        "Accion Solicitada:"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public StructuredDocxStrategy(ILogger<StructuredDocxStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string StrategyName => "StructuredDocx";

    /// <inheritdoc />
    public Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("StructuredDocx: Empty document text, returning null");
            return Task.FromResult<ExtractedFields?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("StructuredDocx: Extracting from document ({Length} chars)", docxText.Length);

            var fields = new ExtractedFields();
            var hasAnyData = false;

            // Extract core fields
            var expediente = ExtractExpediente(docxText);
            if (expediente != null)
            {
                fields.Expediente = expediente;
                hasAnyData = true;
                _logger.LogTrace("StructuredDocx: Extracted Expediente: {Value}", expediente);
            }

            var causa = ExtractCausa(docxText);
            if (causa != null)
            {
                fields.Causa = causa;
                hasAnyData = true;
                _logger.LogTrace("StructuredDocx: Extracted Causa: {Value}", causa);
            }

            var accion = ExtractAccionSolicitada(docxText);
            if (accion != null)
            {
                fields.AccionSolicitada = accion;
                hasAnyData = true;
                _logger.LogTrace("StructuredDocx: Extracted Accion: {Value}", accion);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Extract extended fields
            ExtractExtendedFields(docxText, fields);

            // Extract dates
            ExtractDates(docxText, fields);

            // Extract monetary amounts
            ExtractMonetaryAmounts(docxText, fields);

            cancellationToken.ThrowIfCancellationRequested();

            // Extract Mexican names
            ExtractMexicanNames(docxText, fields);

            // Extract account information
            ExtractAccountInformation(docxText, fields);

            // Return null if no meaningful data extracted (contract requirement)
            if (!hasAnyData && fields.AdditionalFields.Count == 0 && fields.Montos.Count == 0)
            {
                _logger.LogDebug("StructuredDocx: No data extracted, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogInformation("StructuredDocx: Extracted {FieldCount} additional fields, {MontoCount} amounts",
                fields.AdditionalFields.Count, fields.Montos.Count);

            return Task.FromResult<ExtractedFields?>(fields);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("StructuredDocx: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StructuredDocx: Extraction error");
            return Task.FromResult<ExtractedFields?>(null);
        }
    }

    /// <inheritdoc />
    public Task<bool> CanExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return Task.FromResult(false);
        }

        // Quick check: can extract if at least one standard label is present
        var hasStandardLabels = StandardLabels.Any(label =>
            docxText.Contains(label, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(hasStandardLabels);
    }

    /// <inheritdoc />
    public Task<int> GetConfidenceAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return Task.FromResult(0);
        }

        // Count how many standard labels are present
        var labelCount = StandardLabels.Count(label =>
            docxText.Contains(label, StringComparison.OrdinalIgnoreCase));

        // Confidence based on label count
        var confidence = labelCount switch
        {
            >= 3 => 90,  // High confidence: 3+ standard labels
            2 => 75,     // Medium-high confidence: 2 labels
            1 => 50,     // Medium confidence: 1 label
            _ => 0       // No confidence: no standard labels
        };

        _logger.LogTrace("StructuredDocx: Confidence={Confidence} (found {LabelCount} standard labels)",
            confidence, labelCount);

        return Task.FromResult(confidence);
    }

    //
    // Core Field Extraction
    //

    private static string? ExtractExpediente(string text)
    {
        // Pattern: A/AS1-2505-088637-PHM or similar
        // Format: Letter/Letters+Numbers-Numbers-Numbers-Letters
        var patterns = new[]
        {
            @"(?:Expediente\s*(?:No\.?|Número)?:?\s*)?([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",
            @"([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim().ToUpperInvariant();
            }
        }

        return null;
    }

    private static string? ExtractCausa(string text)
    {
        // Look for "Causa:" followed by text until newline or period
        var patterns = new[]
        {
            @"(?:CAUSA|Causa)\s*:?\s*([^\n\r.]{5,100})",
            @"(?:Causa\s+Legal|CAUSA\s+LEGAL)\s*:?\s*([^\n\r.]{5,100})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Look for "Acción Solicitada:" followed by text
        var patterns = new[]
        {
            @"(?:ACCI[ÓO]N\s+SOLICITADA|Acci[óo]n\s+Solicitada)\s*:?\s*([^\n\r.]{5,100})",
            @"(?:Accion\s+Solicitada|ACCION\s+SOLICITADA)\s*:?\s*([^\n\r.]{5,100})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    //
    // Extended Field Extraction
    //

    private void ExtractExtendedFields(string text, ExtractedFields fields)
    {
        // Oficio number
        var oficio = ExtractOficio(text);
        if (oficio != null)
        {
            fields.AdditionalFields["NumeroOficio"] = oficio;
            _logger.LogTrace("StructuredDocx: Extracted NumeroOficio: {Value}", oficio);
        }

        // Autoridad (Authority)
        var autoridad = ExtractAutoridad(text);
        if (autoridad != null)
        {
            fields.AdditionalFields["AutoridadNombre"] = autoridad;
            _logger.LogTrace("StructuredDocx: Extracted AutoridadNombre: {Value}", autoridad);
        }
    }

    private static string? ExtractOficio(string text)
    {
        // Pattern: 214-1-18714972/2025 or similar
        var patterns = new[]
        {
            @"(?:Oficio\s*(?:No\.?|Número)?:?\s*)?(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})",
            @"(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private static string? ExtractAutoridad(string text)
    {
        // Look for "Autoridad:" followed by authority name (stop at newline)
        var patterns = new[]
        {
            @"(?:AUTORIDAD|Autoridad)\s*:?\s*([A-Z]{2,10}(?:\s+[A-Z]{2,10}){0,2})(?=\s*[\r\n]|$)",
            @"(?:Autoridad\s+Emisora|AUTORIDAD\s+EMISORA)\s*:?\s*([A-Z]{2,10}(?:\s+[A-Z]{2,10}){0,2})(?=\s*[\r\n]|$)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim().ToUpperInvariant();
            }
        }

        return null;
    }

    //
    // Date Extraction
    //

    private void ExtractDates(string text, ExtractedFields fields)
    {
        // Mexican date formats: DD/MM/YYYY, DD-MM-YYYY, DD de MMMM de YYYY
        var patterns = new[]
        {
            @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{4})\b",
            @"\b(\d{1,2}\s+de\s+\w+\s+de\s+\d{4})\b"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Success && !fields.Fechas.Contains(match.Groups[1].Value))
                {
                    fields.Fechas.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        if (fields.Fechas.Count > 0)
        {
            _logger.LogTrace("StructuredDocx: Extracted {Count} dates", fields.Fechas.Count);
        }
    }

    //
    // Monetary Amount Extraction
    //

    private void ExtractMonetaryAmounts(string text, ExtractedFields fields)
    {
        // Pattern: $100,000.00 MXN or $5,000 USD
        var pattern = @"\$\s*([\d,]+(?:\.\d{2})?)\s*(MXN|USD|EUR|CAD)?";
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 2)
            {
                var amountStr = match.Groups[1].Value.Replace(",", "");
                var currency = match.Groups.Count > 2 && !string.IsNullOrWhiteSpace(match.Groups[2].Value)
                    ? match.Groups[2].Value.ToUpperInvariant()
                    : "MXN"; // Default to MXN

                if (decimal.TryParse(amountStr, out var amount))
                {
                    fields.Montos.Add(new AmountData(
                        currency,
                        amount,
                        match.Value.Trim()));

                    _logger.LogTrace("StructuredDocx: Extracted amount: {Amount} {Currency}",
                        amount, currency);
                }
            }
        }
    }

    //
    // Mexican Name Extraction
    //

    private void ExtractMexicanNames(string text, ExtractedFields fields)
    {
        // Pattern: Look for "Nombre:" followed by name parts
        // Mexican names: PATERNO MATERNO NOMBRE(S)
        var patterns = new[]
        {
            // Pattern 1: "Nombre: Juan Carlos GARCÍA LÓPEZ"
            @"(?:Nombre|NOMBRE)\s*:?\s*([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)?)\s+([A-ZÁÉÍÓÚÑ]+)\s+([A-ZÁÉÍÓÚÑ]+)",
            // Pattern 2: "GARCÍA LÓPEZ JUAN CARLOS" (Paterno Materno Nombre)
            @"\b([A-ZÁÉÍÓÚÑ]{3,})\s+([A-ZÁÉÍÓÚÑ]{3,})\s+([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)?)\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && match.Groups.Count >= 4)
            {
                // Pattern 1: Nombre Paterno Materno
                var nombre = match.Groups[1].Value.Trim();
                var paterno = match.Groups[2].Value.Trim().ToUpperInvariant();
                var materno = match.Groups[3].Value.Trim().ToUpperInvariant();

                // Verify it looks like a Mexican name (paterno and materno should be all caps, nombre mixed case)
                if (paterno.Length >= 3 && materno.Length >= 3)
                {
                    fields.AdditionalFields["Nombre"] = nombre;
                    fields.AdditionalFields["Paterno"] = paterno;
                    fields.AdditionalFields["Materno"] = materno;
                    fields.AdditionalFields["NombreCompleto"] = $"{nombre} {paterno} {materno}";

                    _logger.LogTrace("StructuredDocx: Extracted Mexican name: {Nombre} {Paterno} {Materno}",
                        nombre, paterno, materno);

                    break; // Only extract first name found
                }
            }
        }
    }

    //
    // Account Information Extraction
    //

    private void ExtractAccountInformation(string text, ExtractedFields fields)
    {
        // CLABE: 18-digit Mexican bank account number
        var clabePattern = @"\b(\d{18})\b";
        var clabeMatch = Regex.Match(text, clabePattern);
        if (clabeMatch.Success)
        {
            fields.AdditionalFields["CLABE"] = clabeMatch.Groups[1].Value;
            _logger.LogTrace("StructuredDocx: Extracted CLABE: {Value}", clabeMatch.Groups[1].Value);
        }

        // Account number (shorter, may be 10-16 digits)
        var accountPattern = @"(?:Cuenta|CUENTA|Account)\s*(?:No\.?|Número)?:?\s*(\d{10,16})";
        var accountMatch = Regex.Match(text, accountPattern, RegexOptions.IgnoreCase);
        if (accountMatch.Success && accountMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["NumeroCuenta"] = accountMatch.Groups[1].Value;
            _logger.LogTrace("StructuredDocx: Extracted NumeroCuenta: {Value}", accountMatch.Groups[1].Value);
        }

        // Bank name (stop at newline or end of line)
        var bankPattern = @"(?:Banco|BANCO|Bank)\s*:?\s*([A-Z]{3,}(?:\s+[A-Z]{3,}){0,2})(?=\s*[\r\n]|$)";
        var bankMatch = Regex.Match(text, bankPattern, RegexOptions.IgnoreCase);
        if (bankMatch.Success && bankMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["Banco"] = bankMatch.Groups[1].Value.Trim().ToUpperInvariant();
            _logger.LogTrace("StructuredDocx: Extracted Banco: {Value}", bankMatch.Groups[1].Value);
        }
    }
}
