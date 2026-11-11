namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Analysis;

/// <summary>
/// Analyzes DOCX documents to infer structure and recommend an extraction strategy.
/// </summary>
public sealed class DocxStructureAnalyzer
{
    private static readonly string[] CrossReferencePatterns = new[]
    {
        "arriba mencionada", "arriba mencionado",
        "anteriormente indicado", "anteriormente indicada",
        "previamente indicado", "previamente indicada",
        "según anexo", "segun anexo",
        "ver anexo", "adjunto"
    };

    /// <summary>
    /// Analyzes the structure of a DOCX document.
    /// </summary>
    /// <param name="docxBytes">The DOCX document bytes.</param>
    /// <returns>Structural analysis of the document.</returns>
    /// <exception cref="ArgumentException">Thrown when docxBytes is null or empty.</exception>
    public DocxStructure AnalyzeStructure(byte[] docxBytes)
    {
        if (docxBytes == null || docxBytes.Length == 0)
        {
            throw new ArgumentException("DOCX bytes cannot be null or empty.", nameof(docxBytes));
        }

        using var memoryStream = new MemoryStream(docxBytes);
        using var doc = WordprocessingDocument.Open(memoryStream, false);

        if (doc.MainDocumentPart?.Document?.Body == null)
        {
            throw new InvalidOperationException("Invalid DOCX document: missing body.");
        }

        var body = doc.MainDocumentPart.Document.Body;

        var structure = new DocxStructure
        {
            HasTables = HasTables(body),
            ParagraphCount = CountParagraphs(body),
            HasBoldLabels = HasBoldLabels(body),
            HasKeyValuePairs = HasKeyValuePairs(body),
            HasStructuredFormat = MatchesCNBVTemplate(body),
            TableStructure = AnalyzeTables(body),
            StyledElementCount = CountStyledElements(body),
            HasCrossReferences = HasCrossReferences(body)
        };

        return structure;
    }

    private static bool HasTables(Body body) => body.Descendants<Table>().Any();

    private static int CountParagraphs(Body body) => body.Descendants<Paragraph>().Count();

    private static bool HasBoldLabels(Body body)
    {
        foreach (var para in body.Descendants<Paragraph>())
        {
            foreach (var run in para.Descendants<Run>())
            {
                var isBold = run.RunProperties?.Bold != null;
                var text = run.InnerText;

                if (isBold && text.Contains(':'))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasKeyValuePairs(Body body)
    {
        var text = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));
        var colonCount = text.Count(c => c == ':');
        return colonCount >= 3;
    }

    private static bool MatchesCNBVTemplate(Body body)
    {
        var text = string.Join(" ", body.Descendants<Text>().Select(t => t.Text)).ToUpperInvariant();
        var indicators = new[] { "CNBV", "COMISION NACIONAL", "REQUERIMIENTO", "EXPEDIENTE", "ASUNTO" };
        var matchCount = indicators.Count(indicator => text.Contains(indicator));
        return matchCount >= 3;
    }

    private static DocxTableStructure? AnalyzeTables(Body body)
    {
        var tables = body.Descendants<Table>().ToList();
        if (!tables.Any())
        {
            return null;
        }

        var firstTable = tables.First();
        var rows = firstTable.Descendants<TableRow>().ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var firstRow = rows.First();
        var cells = firstRow.Descendants<TableCell>().ToList();

        var hasHeaderRow = DetectHeaderRow(firstRow);
        var columnHeaders = hasHeaderRow
            ? cells.Select(c => c.InnerText.Trim()).Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : null;

        return new DocxTableStructure
        {
            TableCount = tables.Count,
            RowCount = rows.Count,
            ColumnCount = cells.Count,
            HasHeaderRow = hasHeaderRow,
            ColumnHeaders = columnHeaders
        };
    }

    private static bool DetectHeaderRow(TableRow row)
    {
        var cells = row.Descendants<TableCell>();
        foreach (var cell in cells)
        {
            foreach (var run in cell.Descendants<Run>())
            {
                if (run.RunProperties?.Bold != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountStyledElements(Body body)
    {
        var count = 0;
        count += body.Descendants<Paragraph>().Count(p => p.ParagraphProperties?.ParagraphStyleId != null);
        count += body.Descendants<Run>().Count(r => r.RunProperties?.Bold != null);
        count += body.Descendants<Run>().Count(r => r.RunProperties?.Italic != null);
        return count;
    }

    private static bool HasCrossReferences(Body body)
    {
        var text = string.Join(" ", body.Descendants<Text>().Select(t => t.Text)).ToLowerInvariant();
        return CrossReferencePatterns.Any(pattern => text.Contains(pattern));
    }
}
