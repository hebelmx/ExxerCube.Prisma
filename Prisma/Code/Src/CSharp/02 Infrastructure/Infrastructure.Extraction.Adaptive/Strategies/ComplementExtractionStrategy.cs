namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;

/// <summary>
/// Complement extraction strategy that combines multiple extraction techniques.
/// </summary>
/// <remarks>
/// <para>
/// Uses a hybrid approach combining structured labels, contextual patterns, and narrative extraction
/// to maximize field extraction. Best suited as a fallback or complement to other specialized strategies.
/// </para>
/// <para>
/// <strong>Confidence Scoring:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>85: High extraction success (5+ fields extracted)</description></item>
///   <item><description>75: Moderate extraction (3-4 fields)</description></item>
///   <item><description>60: Minimal extraction (1-2 fields)</description></item>
///   <item><description>0: No data extracted</description></item>
/// </list>
/// </remarks>
public sealed class ComplementExtractionStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<ComplementExtractionStrategy> _logger;

    /// <summary>
    /// Keywords used for document assessment.
    /// </summary>
    private static readonly string[] AssessmentKeywords = new[]
    {
        "expediente", "oficio", "autoridad", "causa", "solicitud", "aseguramiento",
        "precautorio", "cuenta", "clabe", "banco", "monto", "fecha", "procuraduría", "pgr"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplementExtractionStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public ComplementExtractionStrategy(ILogger<ComplementExtractionStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string StrategyName => "ComplementExtraction";

    /// <inheritdoc />
    public Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("ComplementExtraction: Empty document text, returning null");
            return Task.FromResult<ExtractedFields?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("ComplementExtraction: Extracting from document ({Length} chars)", docxText.Length);

            var fields = new ExtractedFields();
            var hasAnyData = false;

            // Extract core fields using multiple approaches
            var expediente = ExtractExpediente(docxText);
            if (expediente != null)
            {
                fields.Expediente = expediente;
                hasAnyData = true;
                _logger.LogTrace("ComplementExtraction: Extracted Expediente: {Value}", expediente);
            }

            var causa = ExtractCausa(docxText);
            if (causa != null)
            {
                fields.Causa = causa;
                hasAnyData = true;
                _logger.LogTrace("ComplementExtraction: Extracted Causa: {Value}", causa);
            }

            var accion = ExtractAccionSolicitada(docxText);
            if (accion != null)
            {
                fields.AccionSolicitada = accion;
                hasAnyData = true;
                _logger.LogTrace("ComplementExtraction: Extracted Accion: {Value}", accion);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Extract extended fields
            ExtractExtendedFields(docxText, fields);

            // Extract dates
            ExtractDates(docxText, fields);

            // Extract monetary amounts
            ExtractMonetaryAmounts(docxText, fields);

            cancellationToken.ThrowIfCancellationRequested();

            // Extract account information
            ExtractAccountInformation(docxText, fields);

            // Return null if no meaningful data extracted
            if (!hasAnyData && fields.AdditionalFields.Count == 0 && fields.Montos.Count == 0)
            {
                _logger.LogDebug("ComplementExtraction: No data extracted, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogInformation("ComplementExtraction: Extracted {FieldCount} additional fields, {MontoCount} amounts",
                fields.AdditionalFields.Count, fields.Montos.Count);

            return Task.FromResult<ExtractedFields?>(fields);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ComplementExtraction: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ComplementExtraction: Extraction error");
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

        // Can extract if document contains assessment keywords
        var keywordCount = AssessmentKeywords.Count(keyword =>
            docxText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(keywordCount >= 2);
    }

    /// <inheritdoc />
    public Task<int> GetConfidenceAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return Task.FromResult(0);
        }

        // Count assessment keywords
        var keywordCount = AssessmentKeywords.Count(keyword =>
            docxText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // Quick extraction attempt to gauge confidence
        var hasExpediente = ExtractExpediente(docxText) != null;
        var hasCausa = ExtractCausa(docxText) != null;
        var hasAccion = ExtractAccionSolicitada(docxText) != null;

        var extractionScore = (hasExpediente ? 1 : 0) + (hasCausa ? 1 : 0) + (hasAccion ? 1 : 0);

        // Confidence based on keyword density and extraction success
        var confidence = (keywordCount, extractionScore) switch
        {
            (>= 6, >= 2) => 85,  // High keyword density + good extraction
            (>= 4, >= 2) => 75,  // Moderate keywords + good extraction
            (>= 2, >= 1) => 60,  // Minimal presence
            _ => 0               // No viable data
        };

        _logger.LogTrace("ComplementExtraction: Confidence={Confidence} (keywords={KeywordCount}, extracted={ExtractionScore})",
            confidence, keywordCount, extractionScore);

        return Task.FromResult(confidence);
    }

    //
    // Extraction Methods (Hybrid Approach)
    //

    private static string? ExtractExpediente(string text)
    {
        // Try multiple patterns (label-based and contextual)
        var patterns = new[]
        {
            // Label-based patterns
            @"Expediente\s*:?\s*([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",
            @"No\.?\s*Expediente\s*:?\s*([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",

            // Contextual patterns
            @"(?:en\s+el\s+)?expediente\s+([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",
            @"expediente\s+(?:no\.?|número)?\s*:?\s*([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",

            // Standalone pattern
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
        // Try multiple patterns
        var patterns = new[]
        {
            // Label-based
            @"Causa\s*(?:Legal)?\s*:?\s*([a-záéíóúñ\s]{5,50})",

            // Contextual
            @"causa\s+(?:de|legal|penal)?\s*:?\s*([a-záéíóúñ\s]{5,50})",
            @"en\s+la\s+causa\s+(?:de|penal|por)\s+([a-záéíóúñ\s]{5,50})",
            @"(?:causa|delito)\s+(?:de|por)\s+([a-záéíóúñ\s]{5,50})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var causa = match.Groups[1].Value.Trim();
                // Clean up: remove trailing punctuation and common stop words
                causa = Regex.Replace(causa, @",.*$", "");
                causa = Regex.Replace(causa, @"(?:conforme|según|artículo).*$", "", RegexOptions.IgnoreCase);
                return causa.Trim();
            }
        }

        return null;
    }

    private static string? ExtractAccionSolicitada(string text)
    {
        // Try multiple patterns
        var patterns = new[]
        {
            // Label-based
            @"Acci[oó]n\s+Solicitada\s*:?\s*([a-záéíóúñ\s]{10,80})",
            @"Acci[oó]n\s*:?\s*([a-záéíóúñ\s]{10,80})",

            // Contextual
            @"(?:solicita|solicitada?)\s+(?:el|la)?\s*([a-záéíóúñ\s]{10,80})",
            @"se\s+solicita\s+(?:el|la)?\s*([a-záéíóúñ\s]{10,80})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var accion = match.Groups[1].Value.Trim();
                // Clean up: stop at common boundaries
                accion = Regex.Replace(accion, @"de\s+la\s+cuenta.*$", "", RegexOptions.IgnoreCase);
                accion = Regex.Replace(accion, @"(?:identificada|con|en).*$", "", RegexOptions.IgnoreCase);
                return accion.Trim();
            }
        }

        return null;
    }

    private void ExtractExtendedFields(string text, ExtractedFields fields)
    {
        // Extract oficio number
        var oficioPatterns = new[]
        {
            @"Oficio\s*:?\s*(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})",
            @"oficio\s+(?:no\.?|número)?\s*:?\s*(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})"
        };

        foreach (var pattern in oficioPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                fields.AdditionalFields["NumeroOficio"] = match.Groups[1].Value.Trim();
                _logger.LogTrace("ComplementExtraction: Extracted NumeroOficio: {Value}", match.Groups[1].Value);
                break;
            }
        }

        // Extract autoridad (authority)
        var autoridadPatterns = new[]
        {
            @"Autoridad\s*(?:Emisora)?\s*:?\s*([A-Z]{2,10})",
            @"(?:PGR|FGR|Procuraduría|Fiscalía)",
            @"autoridad\s+(?:emisora)?\s*:?\s*([A-Z]{2,10})"
        };

        foreach (var pattern in autoridadPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var autoridad = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                fields.AdditionalFields["AutoridadNombre"] = autoridad.Trim().ToUpperInvariant();
                _logger.LogTrace("ComplementExtraction: Extracted AutoridadNombre: {Value}", autoridad);
                break;
            }
        }

        // Extract RFC
        var rfcPattern = @"RFC\s*:?\s*([A-Z]{4}\d{6}[A-Z0-9]{3})";
        var rfcMatch = Regex.Match(text, rfcPattern, RegexOptions.IgnoreCase);
        if (rfcMatch.Success && rfcMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["RFC"] = rfcMatch.Groups[1].Value.Trim().ToUpperInvariant();
            _logger.LogTrace("ComplementExtraction: Extracted RFC: {Value}", rfcMatch.Groups[1].Value);
        }
    }

    private void ExtractDates(string text, ExtractedFields fields)
    {
        // Mexican date formats
        var patterns = new[]
        {
            @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{4})\b",
            @"\b(\d{1,2}\s+de\s+\w+\s+de\s+\d{4})\b",
            @"Fecha\s*:?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{4})"
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
            _logger.LogTrace("ComplementExtraction: Extracted {Count} dates", fields.Fechas.Count);
        }
    }

    private void ExtractMonetaryAmounts(string text, ExtractedFields fields)
    {
        // Pattern: $100,000.00 MXN or variations
        var pattern = @"\$\s*([\d,]+(?:\.\d{2})?)\s*(MXN|USD|EUR|M\.N\.|pesos)?";
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 2)
            {
                var amountStr = match.Groups[1].Value.Replace(",", "");
                var currencyRaw = match.Groups.Count > 2 ? match.Groups[2].Value : "";

                // Normalize currency
                var currency = currencyRaw.ToUpperInvariant() switch
                {
                    "M.N." => "MXN",
                    "PESOS" => "MXN",
                    var c when c.StartsWith("USD") => "USD",
                    var c when c.StartsWith("EUR") => "EUR",
                    var c when !string.IsNullOrWhiteSpace(c) => c,
                    _ => "MXN" // Default
                };

                if (decimal.TryParse(amountStr, out var amount))
                {
                    fields.Montos.Add(new AmountData(currency, amount, match.Value.Trim()));
                    _logger.LogTrace("ComplementExtraction: Extracted amount: {Amount} {Currency}", amount, currency);
                }
            }
        }
    }

    private void ExtractAccountInformation(string text, ExtractedFields fields)
    {
        // CLABE: 18-digit Mexican bank account
        var clabePatterns = new[]
        {
            @"CLABE\s*:?\s*(\d{18})",
            @"\b(\d{18})\b"
        };

        foreach (var pattern in clabePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                fields.AdditionalFields["CLABE"] = match.Groups[1].Value;
                _logger.LogTrace("ComplementExtraction: Extracted CLABE: {Value}", match.Groups[1].Value);
                break;
            }
        }

        // Bank name
        var bankPatterns = new[]
        {
            @"Banco\s*:?\s*([A-Z]{4,15})",
            @"en\s+([A-Z]{4,15})(?:\s*[.\r\n]|$)",
            @"BANAMEX|BANCOMER|SANTANDER|HSBC"
        };

        foreach (var pattern in bankPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var banco = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                fields.AdditionalFields["Banco"] = banco.Trim().ToUpperInvariant();
                _logger.LogTrace("ComplementExtraction: Extracted Banco: {Value}", banco);
                break;
            }
        }
    }
}
