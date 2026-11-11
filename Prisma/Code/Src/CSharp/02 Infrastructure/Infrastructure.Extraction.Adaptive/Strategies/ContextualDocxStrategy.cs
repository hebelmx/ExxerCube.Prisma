namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;

/// <summary>
/// Contextual DOCX extraction strategy for documents with narrative/prose format.
/// </summary>
/// <remarks>
/// <para>
/// Uses contextual clues and keyword proximity to extract fields from documents
/// that don't follow rigid label:value patterns. Best suited for narrative documents,
/// official letters, and prose-formatted legal texts.
/// </para>
/// <para>
/// <strong>Confidence Scoring:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>80: High keyword density with contextual patterns</description></item>
///   <item><description>70: Moderate keyword presence with some context</description></item>
///   <item><description>50: Minimal keywords found</description></item>
///   <item><description>0: No contextual indicators found</description></item>
/// </list>
/// </remarks>
public sealed class ContextualDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<ContextualDocxStrategy> _logger;

    /// <summary>
    /// Contextual keywords used for document assessment and extraction.
    /// </summary>
    private static readonly string[] ContextualKeywords = new[]
    {
        "expediente", "oficio", "autoridad", "causa", "solicitud", "aseguramiento",
        "precautorio", "cuenta", "clabe", "banco", "monto", "fecha"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public ContextualDocxStrategy(ILogger<ContextualDocxStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string StrategyName => "ContextualDocx";

    /// <inheritdoc />
    public Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("ContextualDocx: Empty document text, returning null");
            return Task.FromResult<ExtractedFields?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("ContextualDocx: Extracting from document ({Length} chars)", docxText.Length);

            var fields = new ExtractedFields();
            var hasAnyData = false;

            // Extract core fields using contextual patterns
            var expediente = ExtractExpedienteContextual(docxText);
            if (expediente != null)
            {
                fields.Expediente = expediente;
                hasAnyData = true;
                _logger.LogTrace("ContextualDocx: Extracted Expediente: {Value}", expediente);
            }

            var causa = ExtractCausaContextual(docxText);
            if (causa != null)
            {
                fields.Causa = causa;
                hasAnyData = true;
                _logger.LogTrace("ContextualDocx: Extracted Causa: {Value}", causa);
            }

            var accion = ExtractAccionSolicitadaContextual(docxText);
            if (accion != null)
            {
                fields.AccionSolicitada = accion;
                hasAnyData = true;
                _logger.LogTrace("ContextualDocx: Extracted Accion: {Value}", accion);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Extract extended fields
            ExtractExtendedFieldsContextual(docxText, fields);

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
                _logger.LogDebug("ContextualDocx: No data extracted, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogInformation("ContextualDocx: Extracted {FieldCount} additional fields, {MontoCount} amounts",
                fields.AdditionalFields.Count, fields.Montos.Count);

            return Task.FromResult<ExtractedFields?>(fields);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ContextualDocx: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextualDocx: Extraction error");
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

        // Can extract if document contains contextual keywords
        var keywordCount = ContextualKeywords.Count(keyword =>
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

        // Count contextual keywords
        var keywordCount = ContextualKeywords.Count(keyword =>
            docxText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // Confidence based on keyword density
        var confidence = keywordCount switch
        {
            >= 6 => 80,  // High contextual density
            >= 4 => 70,  // Moderate density
            >= 2 => 50,  // Minimal presence
            _ => 0       // No contextual indicators
        };

        _logger.LogTrace("ContextualDocx: Confidence={Confidence} (found {KeywordCount} keywords)",
            confidence, keywordCount);

        return Task.FromResult(confidence);
    }

    //
    // Contextual Extraction Methods
    //

    private static string? ExtractExpedienteContextual(string text)
    {
        // Pattern: Look for expediente number in context (proximity-based)
        var patterns = new[]
        {
            @"(?:en\s+el\s+)?expediente\s+([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",
            @"expediente\s+(?:no\.?|número)?\s*:?\s*([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",
            @"([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)" // Fallback: standalone pattern
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

    private static string? ExtractCausaContextual(string text)
    {
        // Pattern: Extract causa from contextual sentences
        var patterns = new[]
        {
            @"causa\s+(?:de|legal)?\s*:?\s*([a-záéíóúñ\s]{5,50})",
            @"en\s+la\s+causa\s+de\s+([a-záéíóúñ\s]{5,50})",
            @"fundamenta.*causa\s+de\s+([a-záéíóúñ\s]{5,50})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var causa = match.Groups[1].Value.Trim();
                // Clean up: remove trailing punctuation and extra words
                causa = Regex.Replace(causa, @",.*$", ""); // Stop at comma
                causa = Regex.Replace(causa, @"conforme.*$", "", RegexOptions.IgnoreCase); // Stop at "conforme"
                return causa.Trim();
            }
        }

        return null;
    }

    private static string? ExtractAccionSolicitadaContextual(string text)
    {
        // Pattern: Extract action from contextual sentences
        var patterns = new[]
        {
            @"(?:solicita|solicitada?)\s+(?:el|la)?\s*([a-záéíóúñ\s]{10,80})",
            @"acción\s+solicitada\s*:?\s*([a-záéíóúñ\s]{10,80})",
            @"se\s+solicita\s+(?:el|la)?\s*([a-záéíóúñ\s]{10,80})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var accion = match.Groups[1].Value.Trim();
                // Clean up: stop at "de la cuenta" or similar
                accion = Regex.Replace(accion, @"de\s+la\s+cuenta.*$", "", RegexOptions.IgnoreCase);
                accion = Regex.Replace(accion, @"identificada.*$", "", RegexOptions.IgnoreCase);
                return accion.Trim();
            }
        }

        return null;
    }

    private void ExtractExtendedFieldsContextual(string text, ExtractedFields fields)
    {
        // Extract oficio number
        var oficioPattern = @"oficio\s+(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})";
        var oficioMatch = Regex.Match(text, oficioPattern, RegexOptions.IgnoreCase);
        if (oficioMatch.Success && oficioMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["NumeroOficio"] = oficioMatch.Groups[1].Value.Trim();
            _logger.LogTrace("ContextualDocx: Extracted NumeroOficio: {Value}", oficioMatch.Groups[1].Value);
        }

        // Extract autoridad (authority) - look for known authorities
        var autoridadPattern = @"autoridad\s+(?:emisora)?\s*:?\s*([A-Z]{2,10})";
        var autoridadMatch = Regex.Match(text, autoridadPattern, RegexOptions.IgnoreCase);
        if (autoridadMatch.Success && autoridadMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["AutoridadNombre"] = autoridadMatch.Groups[1].Value.Trim().ToUpperInvariant();
            _logger.LogTrace("ContextualDocx: Extracted AutoridadNombre: {Value}", autoridadMatch.Groups[1].Value);
        }
    }

    private void ExtractDates(string text, ExtractedFields fields)
    {
        // Mexican date formats
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
            _logger.LogTrace("ContextualDocx: Extracted {Count} dates", fields.Fechas.Count);
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
                    _logger.LogTrace("ContextualDocx: Extracted amount: {Amount} {Currency}", amount, currency);
                }
            }
        }
    }

    private void ExtractAccountInformation(string text, ExtractedFields fields)
    {
        // CLABE: 18-digit Mexican bank account
        var clabePattern = @"\b(\d{18})\b";
        var clabeMatch = Regex.Match(text, clabePattern);
        if (clabeMatch.Success)
        {
            fields.AdditionalFields["CLABE"] = clabeMatch.Groups[1].Value;
            _logger.LogTrace("ContextualDocx: Extracted CLABE: {Value}", clabeMatch.Groups[1].Value);
        }

        // Bank name
        var bankPattern = @"(?:en|banco)\s+([A-Z]{4,15})(?=\s*[.\r\n]|$)";
        var bankMatch = Regex.Match(text, bankPattern, RegexOptions.IgnoreCase);
        if (bankMatch.Success && bankMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["Banco"] = bankMatch.Groups[1].Value.Trim().ToUpperInvariant();
            _logger.LogTrace("ContextualDocx: Extracted Banco: {Value}", bankMatch.Groups[1].Value);
        }
    }
}
