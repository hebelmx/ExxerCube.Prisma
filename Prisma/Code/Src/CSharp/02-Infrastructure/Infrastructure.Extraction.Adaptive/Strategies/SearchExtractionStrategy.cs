namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;

/// <summary>
/// Search-based DOCX extraction strategy optimized for keyword proximity extraction.
/// </summary>
/// <remarks>
/// <para>
/// Uses broad keyword matching and proximity-based extraction for documents with minimal structure.
/// Best suited as a fallback strategy when structured approaches fail, or for highly variable document formats.
/// </para>
/// <para>
/// <strong>Confidence Scoring:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>75: Strong keyword presence and successful extraction</description></item>
///   <item><description>65: Moderate keyword presence</description></item>
///   <item><description>50: Minimal keyword presence</description></item>
///   <item><description>0: No relevant keywords found</description></item>
/// </list>
/// </remarks>
public sealed class SearchExtractionStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<SearchExtractionStrategy> _logger;

    /// <summary>
    /// Primary search keywords for document assessment.
    /// </summary>
    private static readonly string[] PrimaryKeywords = new[]
    {
        "expediente", "oficio", "aseguramiento", "precautorio", "pgr", "fgr",
        "procuraduría", "fiscalía", "autoridad", "causa", "solicitud", "cuenta",
        "clabe", "banco", "monto"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchExtractionStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public SearchExtractionStrategy(ILogger<SearchExtractionStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string StrategyName => "SearchExtraction";

    /// <inheritdoc />
    public Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("SearchExtraction: Empty document text, returning null");
            return Task.FromResult<ExtractedFields?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("SearchExtraction: Extracting from document ({Length} chars)", docxText.Length);

            var fields = new ExtractedFields();
            var hasAnyData = false;

            // Extract core fields using broad search patterns
            var expediente = SearchExpediente(docxText);
            if (expediente != null)
            {
                fields.Expediente = expediente;
                hasAnyData = true;
                _logger.LogTrace("SearchExtraction: Extracted Expediente: {Value}", expediente);
            }

            var causa = SearchCausa(docxText);
            if (causa != null)
            {
                fields.Causa = causa;
                hasAnyData = true;
                _logger.LogTrace("SearchExtraction: Extracted Causa: {Value}", causa);
            }

            var accion = SearchAccionSolicitada(docxText);
            if (accion != null)
            {
                fields.AccionSolicitada = accion;
                hasAnyData = true;
                _logger.LogTrace("SearchExtraction: Extracted Accion: {Value}", accion);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Extract extended fields using search
            SearchExtendedFields(docxText, fields);

            // Extract dates
            SearchDates(docxText, fields);

            // Extract monetary amounts
            SearchMonetaryAmounts(docxText, fields);

            cancellationToken.ThrowIfCancellationRequested();

            // Extract account information
            SearchAccountInformation(docxText, fields);

            // Return null if no meaningful data extracted
            if (!hasAnyData && fields.AdditionalFields.Count == 0 && fields.Montos.Count == 0)
            {
                _logger.LogDebug("SearchExtraction: No data extracted, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogInformation("SearchExtraction: Extracted {FieldCount} additional fields, {MontoCount} amounts",
                fields.AdditionalFields.Count, fields.Montos.Count);

            return Task.FromResult<ExtractedFields?>(fields);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SearchExtraction: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchExtraction: Extraction error");
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

        // Can extract if document contains primary keywords
        var keywordCount = PrimaryKeywords.Count(keyword =>
            docxText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // More lenient threshold for search strategy
        return Task.FromResult(keywordCount >= 2);
    }

    /// <inheritdoc />
    public Task<int> GetConfidenceAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return Task.FromResult(0);
        }

        // Count keyword occurrences
        var keywordCount = PrimaryKeywords.Count(keyword =>
            docxText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // Quick extraction attempt
        var hasExpediente = SearchExpediente(docxText) != null;
        var hasCausa = SearchCausa(docxText) != null;
        var hasAccion = SearchAccionSolicitada(docxText) != null;

        var extractionSuccess = (hasExpediente ? 1 : 0) + (hasCausa ? 1 : 0) + (hasAccion ? 1 : 0);

        // Confidence based on keyword density and extraction
        var confidence = (keywordCount, extractionSuccess) switch
        {
            (>= 5, >= 2) => 75,  // Strong keyword presence + good extraction
            (>= 3, >= 1) => 65,  // Moderate presence
            (>= 2, >= 1) => 50,  // Minimal presence
            _ => 0               // No viable data
        };

        _logger.LogTrace("SearchExtraction: Confidence={Confidence} (keywords={KeywordCount}, extracted={ExtractionSuccess})",
            confidence, keywordCount, extractionSuccess);

        return Task.FromResult(confidence);
    }

    //
    // Search-Based Extraction Methods
    //

    private static string? SearchExpediente(string text)
    {
        // Broad patterns optimized for search
        var patterns = new[]
        {
            // Standard expediente formats
            @"([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",

            // With keywords nearby (within 50 chars)
            @"(?i)expediente.{0,50}?([A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+)",

            // Uppercase standalone
            @"\b([A-Z]/[A-Z]{2,4}\d{1,4}-\d{4,6}-\d{6,9}-[A-Z]{2,4})\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var expediente = match.Groups[1].Value.Trim().ToUpperInvariant();
                // Validate format (basic sanity check)
                if (expediente.Contains('/') && expediente.Contains('-'))
                {
                    return expediente;
                }
            }
        }

        return null;
    }

    private static string? SearchCausa(string text)
    {
        // Search for causa with broad proximity
        var patterns = new[]
        {
            // Keyword proximity (higher priority - captures full phrases)
            @"(?i)investigación.{0,30}?(?:por|de)\s+([a-záéíóúñ\s]{5,60})",
            @"(?i)(?:por|de)\s+(lavado\s+de\s+dinero|fraude|robo|extorsión|secuestro|narcotráfico)",

            // Direct matches
            @"(?i)causa[^:]{0,20}?:?\s*([a-záéíóúñ\s]{5,60})",
            @"(?i)delito.{0,20}?(?:de|por)\s+([a-záéíóúñ\s]{5,60})",

            // Fallback: single keywords (last resort)
            @"(?i)\b(lavado\s+de\s+dinero|fraude|robo|extorsión|secuestro|narcotráfico)\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var causa = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();

                // Clean up common noise
                causa = Regex.Replace(causa, @"[,.].*$", "");
                causa = Regex.Replace(causa, @"(?:conforme|según|artículo|solicita|fundamento).*$", "", RegexOptions.IgnoreCase);

                if (causa.Length >= 5 && causa.Length <= 60)
                {
                    return causa.Trim();
                }
            }
        }

        return null;
    }

    private static string? SearchAccionSolicitada(string text)
    {
        // Search for acción solicitada with keyword proximity
        var patterns = new[]
        {
            // Common actions
            @"(?i)aseguramiento\s+(?:precautorio|provisional|cautelar)?",
            @"(?i)bloqueo\s+(?:de\s+)?(?:cuenta|recursos)?",
            @"(?i)congelamiento",
            @"(?i)inmovilización",

            // With "solicita" keyword
            @"(?i)solicita.{0,20}?(aseguramiento|bloqueo|congelamiento|inmovilización)[a-záéíóúñ\s]{0,40}"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var accion = match.Value.Trim();

                // Clean up
                accion = Regex.Replace(accion, @"^solicita\s+", "", RegexOptions.IgnoreCase);
                accion = Regex.Replace(accion, @"de\s+la\s+cuenta.*$", "", RegexOptions.IgnoreCase);

                if (accion.Length >= 5)
                {
                    return accion.Trim();
                }
            }
        }

        return null;
    }

    private void SearchExtendedFields(string text, ExtractedFields fields)
    {
        // Search for oficio number
        var oficioPattern = @"(?i)oficio.{0,30}?(\d{1,4}-\d{1,2}-\d{5,10}/\d{4})";
        var oficioMatch = Regex.Match(text, oficioPattern);
        if (oficioMatch.Success && oficioMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["NumeroOficio"] = oficioMatch.Groups[1].Value.Trim();
            _logger.LogTrace("SearchExtraction: Extracted NumeroOficio: {Value}", oficioMatch.Groups[1].Value);
        }

        // Search for autoridad (broad match)
        var autoridadPatterns = new[]
        {
            @"(?i)(pgr|fgr|procuraduría|fiscalía)",
            @"(?i)autoridad.{0,20}?(pgr|fgr|[A-Z]{3,10})"
        };

        foreach (var pattern in autoridadPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var autoridad = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                fields.AdditionalFields["AutoridadNombre"] = autoridad.Trim().ToUpperInvariant();
                _logger.LogTrace("SearchExtraction: Extracted AutoridadNombre: {Value}", autoridad);
                break;
            }
        }

        // Search for RFC
        var rfcPattern = @"(?i)rfc.{0,10}?([A-Z]{4}\d{6}[A-Z0-9]{3})";
        var rfcMatch = Regex.Match(text, rfcPattern);
        if (rfcMatch.Success && rfcMatch.Groups.Count > 1)
        {
            fields.AdditionalFields["RFC"] = rfcMatch.Groups[1].Value.Trim().ToUpperInvariant();
            _logger.LogTrace("SearchExtraction: Extracted RFC: {Value}", rfcMatch.Groups[1].Value);
        }
    }

    private void SearchDates(string text, ExtractedFields fields)
    {
        // Broad date search patterns
        var patterns = new[]
        {
            @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{4})\b",
            @"\b(\d{1,2}\s+de\s+(?:enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+de\s+\d{4})\b",
            @"(?i)fecha.{0,20}?(\d{1,2}[/-]\d{1,2}[/-]\d{4})"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1 && !fields.Fechas.Contains(match.Groups[1].Value))
                {
                    fields.Fechas.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        if (fields.Fechas.Count > 0)
        {
            _logger.LogTrace("SearchExtraction: Extracted {Count} dates", fields.Fechas.Count);
        }
    }

    private void SearchMonetaryAmounts(string text, ExtractedFields fields)
    {
        // Broad monetary search
        var patterns = new[]
        {
            @"\$\s*([\d,]+(?:\.\d{2})?)\s*(MXN|USD|EUR|M\.N\.|pesos)?",
            @"(?i)monto.{0,30}?\$\s*([\d,]+(?:\.\d{2})?)\s*(MXN|USD|EUR|M\.N\.|pesos)?"
        };

        foreach (var pattern in patterns)
        {
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
                        _ => "MXN"
                    };

                    if (decimal.TryParse(amountStr, out var amount) && amount > 0)
                    {
                        fields.Montos.Add(new AmountData(currency, amount, match.Value.Trim()));
                        _logger.LogTrace("SearchExtraction: Extracted amount: {Amount} {Currency}", amount, currency);
                    }
                }
            }
        }
    }

    private void SearchAccountInformation(string text, ExtractedFields fields)
    {
        // CLABE search (18 digits)
        var clabePatterns = new[]
        {
            @"(?i)clabe.{0,20}?(\d{18})",
            @"\b(\d{18})\b"
        };

        foreach (var pattern in clabePatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                var clabe = match.Groups[1].Value;
                // Validate it's actually 18 digits
                if (clabe.Length == 18 && clabe.All(char.IsDigit))
                {
                    fields.AdditionalFields["CLABE"] = clabe;
                    _logger.LogTrace("SearchExtraction: Extracted CLABE: {Value}", clabe);
                    break;
                }
            }
        }

        // Bank name search
        var bankPatterns = new[]
        {
            @"(?i)(?:banco|institución).{0,20}?(BANAMEX|BANCOMER|SANTANDER|HSBC|BANORTE|SCOTIABANK)",
            @"\b(BANAMEX|BANCOMER|SANTANDER|HSBC|BANORTE|SCOTIABANK)\b"
        };

        foreach (var pattern in bankPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var banco = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                fields.AdditionalFields["Banco"] = banco.Trim().ToUpperInvariant();
                _logger.LogTrace("SearchExtraction: Extracted Banco: {Value}", banco);
                break;
            }
        }
    }
}
