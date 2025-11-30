// <copyright file="TableBasedDocxStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Table-based extraction from DOCX tables.
/// Uses column headers for field mapping.
/// </summary>
/// <remarks>
/// Works for documents with tabular data:
/// | Expediente | Nombre | Cuenta | Monto |
/// | XXX        | YYY    | ZZZ    | AAA   |.
/// </remarks>
public class TableBasedDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<TableBasedDocxStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableBasedDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TableBasedDocxStrategy(ILogger<TableBasedDocxStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DocxExtractionStrategyType StrategyType => DocxExtractionStrategyType.TableBased;

    /// <inheritdoc/>
    public int CanHandle(string text)
    {
        // Detect table structure: multiple tab characters on consecutive lines
        var lines = text.Split('\n');
        var tabLines = lines.Count(line => line.Count(c => c == '\t') >= 2);

        // Check for table headers
        var hasTableHeaders = text.ToUpperInvariant().Contains("EXPEDIENTE") &&
                             text.ToUpperInvariant().Contains("NOMBRE") &&
                             (text.Contains("\t") || text.Contains("|"));

        if (hasTableHeaders && tabLines >= 2)
            return 85; // High confidence for table-based
        if (tabLines >= 3)
            return 70; // Medium-high confidence
        if (tabLines >= 1)
            return 50; // Medium confidence

        return 20; // Low confidence
    }

    /// <inheritdoc/>
    public ExtractedFields? Extract(string text)
    {
        try
        {
            // Parse table structure
            var table = ParseTable(text);
            if (table == null || table.Rows.Count == 0)
            {
                _logger.LogDebug("No table structure found");
                return null;
            }

            // Map headers to column indices
            var headerMap = MapHeaders(table.Headers);

            // Extract first data row (assume single expediente per table)
            var dataRow = table.Rows.FirstOrDefault();
            if (dataRow == null)
            {
                return null;
            }

            var expediente = new Expediente();

            if (headerMap.TryGetValue("Expediente", out var expIndex) && expIndex < dataRow.Count)
                expediente.NumeroExpediente = dataRow[expIndex];

            if (headerMap.TryGetValue("Oficio", out var ofIndex) && ofIndex < dataRow.Count)
                expediente.NumeroOficio = dataRow[ofIndex];

            if (headerMap.TryGetValue("Nombre", out var nameIndex) && nameIndex < dataRow.Count)
                expediente.NombreCompleto = dataRow[nameIndex];

            if (headerMap.TryGetValue("Cuenta", out var ctaIndex) && ctaIndex < dataRow.Count)
                expediente.Cuenta = dataRow[ctaIndex];

            if (headerMap.TryGetValue("CLABE", out var clabeIndex) && clabeIndex < dataRow.Count)
                expediente.CLABE = dataRow[clabeIndex];

            if (headerMap.TryGetValue("RFC", out var rfcIndex) && rfcIndex < dataRow.Count)
                expediente.RFC = dataRow[rfcIndex];

            if (headerMap.TryGetValue("Monto", out var montoIndex) && montoIndex < dataRow.Count)
            {
                var montoStr = dataRow[montoIndex].Replace(",", string.Empty).Replace("$", string.Empty).Trim();
                if (decimal.TryParse(montoStr, out var monto))
                {
                    expediente.Monto = monto;
                }
            }

            if (headerMap.TryGetValue("Banco", out var bancoIndex) && bancoIndex < dataRow.Count)
                expediente.Banco = dataRow[bancoIndex];

            _logger.LogDebug("Table strategy extracted: Expediente={Expediente}, Nombre={Nombre}",
                expediente.NumeroExpediente, expediente.NombreCompleto);

            return expediente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TableBasedDocxStrategy");
            return null;
        }
    }

    /// <summary>
    /// Parses table structure from text.
    /// </summary>
    private static TableData? ParseTable(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find table start (header row)
        var headerLineIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].ToUpperInvariant();
            if ((line.Contains("EXPEDIENTE") || line.Contains("NOMBRE")) &&
                (line.Contains("\t") || line.Contains("|")))
            {
                headerLineIndex = i;
                break;
            }
        }

        if (headerLineIndex == -1)
            return null;

        // Parse header row
        var headerLine = lines[headerLineIndex];
        var headers = SplitTableRow(headerLine);

        // Parse data rows
        var rows = new List<List<string>>();
        for (var i = headerLineIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                break; // End of table

            var cells = SplitTableRow(line);
            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        return new TableData { Headers = headers, Rows = rows };
    }

    /// <summary>
    /// Splits a table row into cells.
    /// Handles both tab-delimited and pipe-delimited tables.
    /// </summary>
    private static List<string> SplitTableRow(string row)
    {
        char delimiter = '\t';
        if (row.Contains('|') && row.Count(c => c == '|') > row.Count(c => c == '\t'))
        {
            delimiter = '|';
        }

        return row.Split(delimiter, StringSplitOptions.TrimEntries)
                  .Where(cell => !string.IsNullOrWhiteSpace(cell))
                  .ToList();
    }

    /// <summary>
    /// Maps column headers to field names.
    /// </summary>
    private static Dictionary<string, int> MapHeaders(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i].ToUpperInvariant();

            if (header.Contains("EXPEDIENTE"))
                map["Expediente"] = i;
            else if (header.Contains("OFICIO"))
                map["Oficio"] = i;
            else if (header.Contains("NOMBRE"))
                map["Nombre"] = i;
            else if (header.Contains("CUENTA") && !header.Contains("CLABE"))
                map["Cuenta"] = i;
            else if (header.Contains("CLABE"))
                map["CLABE"] = i;
            else if (header.Contains("RFC"))
                map["RFC"] = i;
            else if (header.Contains("MONTO") || header.Contains("CANTIDAD"))
                map["Monto"] = i;
            else if (header.Contains("BANCO") || header.Contains("INSTITUCIÓN"))
                map["Banco"] = i;
        }

        return map;
    }

    /// <summary>
    /// Represents parsed table data.
    /// </summary>
    private class TableData
    {
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }
}
