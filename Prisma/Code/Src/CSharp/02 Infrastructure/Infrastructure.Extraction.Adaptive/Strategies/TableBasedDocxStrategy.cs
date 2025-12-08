namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;

/// <summary>
/// Table-based DOCX extraction strategy for structured tabular documents.
/// </summary>
/// <remarks>
/// <para>
/// Extracts fields from documents formatted as tables with key-value pairs.
/// Best suited for documents with clear table structures (| Key | Value |).
/// </para>
/// <para>
/// <strong>Confidence Scoring:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>95: Strong table structure with pipe delimiters</description></item>
///   <item><description>85: Moderate table indicators</description></item>
///   <item><description>60: Minimal table-like patterns</description></item>
///   <item><description>0: No table structure found</description></item>
/// </list>
/// </remarks>
public sealed class TableBasedDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<TableBasedDocxStrategy> _logger;

    /// <summary>
    /// Table field names that map to our domain fields.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Expediente"] = "Expediente",
        ["No. Expediente"] = "Expediente",
        ["Número de Expediente"] = "Expediente",
        ["Causa"] = "Causa",
        ["Causa Legal"] = "Causa",
        ["Acción Solicitada"] = "AccionSolicitada",
        ["Accion Solicitada"] = "AccionSolicitada",
        ["Oficio"] = "NumeroOficio",
        ["No. Oficio"] = "NumeroOficio",
        ["Autoridad"] = "AutoridadNombre",
        ["RFC"] = "RFC",
        ["CLABE"] = "CLABE",
        ["Banco"] = "Banco",
        ["Nombre"] = "NombreCompleto",
        ["Monto"] = "Monto",
        ["Fecha"] = "Fecha"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TableBasedDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public TableBasedDocxStrategy(ILogger<TableBasedDocxStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string StrategyName => "TableBased";

    /// <inheritdoc />
    public Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("TableBased: Empty document text, returning null");
            return Task.FromResult<ExtractedFields?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("TableBased: Extracting from document ({Length} chars)", docxText.Length);

            // Parse table rows
            var tableRows = ParseTableRows(docxText);
            if (tableRows.Count == 0)
            {
                _logger.LogDebug("TableBased: No table rows found, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogTrace("TableBased: Found {RowCount} table rows", tableRows.Count);

            var fields = new ExtractedFields();
            var hasAnyData = false;

            // Extract core fields from table
            foreach (var (key, value) in tableRows)
            {
                if (FieldMappings.TryGetValue(key, out var fieldName))
                {
                    switch (fieldName)
                    {
                        case "Expediente":
                            fields.Expediente = value;
                            hasAnyData = true;
                            _logger.LogTrace("TableBased: Extracted Expediente: {Value}", value);
                            break;

                        case "Causa":
                            fields.Causa = value;
                            hasAnyData = true;
                            _logger.LogTrace("TableBased: Extracted Causa: {Value}", value);
                            break;

                        case "AccionSolicitada":
                            fields.AccionSolicitada = value;
                            hasAnyData = true;
                            _logger.LogTrace("TableBased: Extracted AccionSolicitada: {Value}", value);
                            break;

                        case "Monto":
                            ExtractMonetaryAmount(value, fields);
                            break;

                        case "Fecha":
                            if (!fields.Fechas.Contains(value))
                            {
                                fields.Fechas.Add(value);
                                _logger.LogTrace("TableBased: Extracted Fecha: {Value}", value);
                            }
                            break;

                        default:
                            // Extended field
                            fields.AdditionalFields[fieldName] = value;
                            _logger.LogTrace("TableBased: Extracted {FieldName}: {Value}", fieldName, value);
                            break;
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Return null if no meaningful data extracted
            if (!hasAnyData && fields.AdditionalFields.Count == 0 && fields.Montos.Count == 0)
            {
                _logger.LogDebug("TableBased: No data extracted, returning null");
                return Task.FromResult<ExtractedFields?>(null);
            }

            _logger.LogInformation("TableBased: Extracted {FieldCount} additional fields, {MontoCount} amounts",
                fields.AdditionalFields.Count, fields.Montos.Count);

            return Task.FromResult<ExtractedFields?>(fields);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("TableBased: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TableBased: Extraction error");
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

        // Can extract if document has table-like structure (pipe delimiters or dashes)
        var hasTableStructure = docxText.Contains('|') ||
                               Regex.IsMatch(docxText, @"[-]+\s*\|", RegexOptions.Multiline);

        return Task.FromResult(hasTableStructure);
    }

    /// <inheritdoc />
    public Task<int> GetConfidenceAsync(string docxText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return Task.FromResult(0);
        }

        // Count table indicators
        var pipeCount = docxText.Count(c => c == '|');
        var hasTableHeader = Regex.IsMatch(docxText, @"\|.*\|.*\|", RegexOptions.Multiline);
        var hasSeparatorLine = Regex.IsMatch(docxText, @"[-]+\|[-]+", RegexOptions.Multiline);

        // Calculate confidence based on table structure strength
        int confidence;
        if (pipeCount >= 20 && hasTableHeader && hasSeparatorLine)
        {
            confidence = 95; // Strong table structure
        }
        else if (pipeCount >= 10 && hasTableHeader)
        {
            confidence = 85; // Moderate table structure
        }
        else if (pipeCount >= 4)
        {
            confidence = 60; // Minimal table-like structure
        }
        else
        {
            confidence = 0; // No table structure
        }

        _logger.LogTrace("TableBased: Confidence={Confidence} (pipes={PipeCount}, header={HasHeader}, separator={HasSeparator})",
            confidence, pipeCount, hasTableHeader, hasSeparatorLine);

        return Task.FromResult(confidence);
    }

    //
    // Table Parsing Methods
    //

    private List<(string Key, string Value)> ParseTableRows(string text)
    {
        var rows = new List<(string Key, string Value)>();

        // Split by newlines
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Skip separator lines (e.g., |---|---|)
            if (Regex.IsMatch(line, @"^\s*\|[\s-|]+\|\s*$"))
            {
                continue;
            }

            // Parse table row: | Key | Value |
            var match = Regex.Match(line, @"\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|");
            if (match.Success && match.Groups.Count >= 3)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();

                // Skip header rows (e.g., | Campo | Valor |)
                if (key.Equals("Campo", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Field", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Key", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    rows.Add((key, value));
                    _logger.LogTrace("TableBased: Parsed row - {Key}: {Value}", key, value);
                }
            }
        }

        return rows;
    }

    private void ExtractMonetaryAmount(string value, ExtractedFields fields)
    {
        // Pattern: $100,000.00 MXN
        var pattern = @"\$\s*([\d,]+(?:\.\d{2})?)\s*(MXN|USD|EUR|M\.N\.|pesos)?";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);

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

            if (decimal.TryParse(amountStr, out var amount))
            {
                fields.Montos.Add(new AmountData(currency, amount, match.Value.Trim()));
                _logger.LogTrace("TableBased: Extracted amount: {Amount} {Currency}", amount, currency);
            }
        }
    }
}
