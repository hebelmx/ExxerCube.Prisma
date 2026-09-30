using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Analysis;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using Italic = DocumentFormat.OpenXml.Wordprocessing.Italic;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using ParagraphStyleId = DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="DocxStructureAnalyzer"/>. The analyzer was previously untested
/// in the deterministic project (only reached via the flaky Teseract suite). Every structural flag, count,
/// and boundary is pinned with an exact-value assertion, and each LINQ <c>Any()</c> is killed with both an
/// empty and a non-empty fixture (Stryker mutates <c>Any()</c> → <c>All()</c>, which is vacuously true on
/// an empty sequence).
/// </summary>
public class DocxStructureAnalyzerMutationTests
{
    private readonly DocxStructureAnalyzer _analyzer = new();

    // ---------------------------------------------------------------- guards

    [Fact]
    public void AnalyzeStructure_NullBytes_Throws()
    {
        Should.Throw<ArgumentException>(() => _analyzer.AnalyzeStructure(null!));
    }

    [Fact]
    public void AnalyzeStructure_EmptyBytes_Throws()
    {
        Should.Throw<ArgumentException>(() => _analyzer.AnalyzeStructure(Array.Empty<byte>()));
    }

    [Fact]
    public void AnalyzeStructure_ValidPackageMissingBody_ThrowsInvalidOperation()
    {
        // Reaches the "missing body" guard (a valid package whose MainDocumentPart.Document has no Body).
        var bytes = BuildDocxNoBody();

        Should.Throw<InvalidOperationException>(() => _analyzer.AnalyzeStructure(bytes));
    }

    // ---------------------------------------------------------------- HasTables / ParagraphCount

    [Fact]
    public void AnalyzeStructure_NoTables_HasTablesFalseAndTableStructureNull()
    {
        // Empty/non-empty pair for the Descendants<Table>().Any() in both HasTables and AnalyzeTables:
        // All() over an empty sequence is true, so asserting false here kills the Any()->All() mutant.
        var bytes = BuildDocx(body => body.Append(Para("Just a plain paragraph")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasTables.ShouldBeFalse();
        structure.TableStructure.ShouldBeNull();
    }

    [Fact]
    public void AnalyzeStructure_WithTable_HasTablesTrue()
    {
        var bytes = BuildDocx(body => body.Append(BuildTable(boldFirstRow: false, ("a", "b"), ("c", "d"))));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasTables.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeStructure_ThreeStandaloneParagraphs_ParagraphCountIsThree()
    {
        var bytes = BuildDocx(body =>
        {
            body.Append(Para("one"));
            body.Append(Para("two"));
            body.Append(Para("three"));
        });

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.ParagraphCount.ShouldBe(3);
    }

    // ---------------------------------------------------------------- HasBoldLabels

    [Fact]
    public void AnalyzeStructure_BoldRunWithColon_HasBoldLabelsTrue()
    {
        var bytes = BuildDocx(body => body.Append(Para("Expediente:", bold: true)));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasBoldLabels.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeStructure_BoldRunWithoutColon_HasBoldLabelsFalse()
    {
        // isBold == true but Contains(':') == false. Kills the '&&' -> '||' mutant (|| would be true here).
        var bytes = BuildDocx(body => body.Append(Para("Expediente", bold: true)));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasBoldLabels.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeStructure_ColonButNotBold_HasBoldLabelsFalse()
    {
        // Contains(':') == true but isBold == false. Kills removal of the 'isBold' conjunct.
        var bytes = BuildDocx(body => body.Append(Para("Expediente:", bold: false)));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasBoldLabels.ShouldBeFalse();
    }

    // ---------------------------------------------------------------- HasKeyValuePairs (colonCount >= 3)

    [Fact]
    public void AnalyzeStructure_TwoColons_HasKeyValuePairsFalse()
    {
        var bytes = BuildDocx(body => body.Append(Para("A: 1 B: 2")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasKeyValuePairs.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeStructure_ThreeColons_HasKeyValuePairsTrue()
    {
        // Exactly the >= 3 boundary: kills '>=' -> '>' and the colon-equality mutant.
        var bytes = BuildDocx(body => body.Append(Para("A: 1 B: 2 C: 3")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasKeyValuePairs.ShouldBeTrue();
    }

    // ---------------------------------------------------------------- MatchesCNBVTemplate (matchCount >= 3)

    [Fact]
    public void AnalyzeStructure_TwoCnbvIndicators_HasStructuredFormatFalse()
    {
        var bytes = BuildDocx(body => body.Append(Para("CNBV ASUNTO general")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasStructuredFormat.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeStructure_ThreeCnbvIndicators_HasStructuredFormatTrue()
    {
        // Exactly 3 distinct indicators -> matchCount == 3 boundary. Lower-case input proves ToUpperInvariant.
        var bytes = BuildDocx(body => body.Append(Para("cnbv expediente asunto del oficio")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasStructuredFormat.ShouldBeTrue();
    }

    // ---------------------------------------------------------------- AnalyzeTables

    [Fact]
    public void AnalyzeStructure_TableWithBoldHeaderRow_PinsTableStructure()
    {
        var bytes = BuildDocx(body => body.Append(BuildTable(
            boldFirstRow: true,
            ("Nombre", "RFC", "Monto"),
            ("Juan", "PERJ800101ABC", "100"))));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.TableStructure.ShouldNotBeNull();
        structure.TableStructure!.TableCount.ShouldBe(1);
        structure.TableStructure.RowCount.ShouldBe(2);
        structure.TableStructure.ColumnCount.ShouldBe(3);
        structure.TableStructure.HasHeaderRow.ShouldBeTrue();
        structure.TableStructure.ColumnHeaders.ShouldBe(new[] { "Nombre", "RFC", "Monto" });
    }

    [Fact]
    public void AnalyzeStructure_TableWithoutBoldFirstRow_NoHeaderAndNullHeaders()
    {
        var bytes = BuildDocx(body => body.Append(BuildTable(
            boldFirstRow: false,
            ("a", "b"),
            ("c", "d"))));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.TableStructure.ShouldNotBeNull();
        structure.TableStructure!.HasHeaderRow.ShouldBeFalse();
        structure.TableStructure.ColumnHeaders.ShouldBeNull();
    }

    [Fact]
    public void AnalyzeStructure_TableWithNoRows_TableStructureNull()
    {
        // Has a table but zero rows => AnalyzeTables hits the rows.Count == 0 -> return null guard.
        var bytes = BuildDocx(body => body.Append(new Table()));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasTables.ShouldBeTrue();
        structure.TableStructure.ShouldBeNull();
    }

    [Fact]
    public void AnalyzeStructure_TwoTables_TableCountIsTwoButDimensionsFromFirst()
    {
        var bytes = BuildDocx(body =>
        {
            body.Append(BuildTable(boldFirstRow: false, ("a", "b")));               // first: 1 row, 2 cols
            body.Append(BuildTable(boldFirstRow: false, ("x", "y", "z"), ("p", "q", "r"))); // second: 2 rows, 3 cols
        });

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.TableStructure.ShouldNotBeNull();
        structure.TableStructure!.TableCount.ShouldBe(2);
        structure.TableStructure.RowCount.ShouldBe(1);     // from the FIRST table
        structure.TableStructure.ColumnCount.ShouldBe(2);  // from the FIRST table
    }

    // ---------------------------------------------------------------- CountStyledElements

    [Fact]
    public void AnalyzeStructure_StyledElements_CountsStyleIdPlusBoldPlusItalic()
    {
        // 1 paragraph with a style id, 2 bold runs, 1 italic run => 1 + 2 + 1 = 4.
        var bytes = BuildDocx(body =>
        {
            body.Append(Para("titulo", styleId: "Heading1")); // +1 styled paragraph (no bold/italic run)
            body.Append(Para("uno", bold: true));             // +1 bold run
            body.Append(Para("dos", bold: true));             // +1 bold run
            body.Append(Para("tres", italic: true));          // +1 italic run
        });

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.StyledElementCount.ShouldBe(4);
    }

    [Fact]
    public void AnalyzeStructure_NoStyling_StyledElementCountIsZero()
    {
        var bytes = BuildDocx(body => body.Append(Para("plain text")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.StyledElementCount.ShouldBe(0);
    }

    // ---------------------------------------------------------------- HasCrossReferences

    [Fact]
    public void AnalyzeStructure_CrossReferencePhrase_HasCrossReferencesTrue()
    {
        // Upper-case input proves ToLowerInvariant; "adjunto" is one of the configured patterns.
        var bytes = BuildDocx(body => body.Append(Para("Ver documento ADJUNTO para detalle")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasCrossReferences.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeStructure_NoCrossReferencePhrase_HasCrossReferencesFalse()
    {
        var bytes = BuildDocx(body => body.Append(Para("Texto sin referencias cruzadas")));

        var structure = _analyzer.AnalyzeStructure(bytes);

        structure.HasCrossReferences.ShouldBeFalse();
    }

    // ================================================================ builders

    private static byte[] BuildDocx(Action<Body> configure)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            configure(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] BuildDocxNoBody()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(); // intentionally no Body appended
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph Para(string text, bool bold = false, bool italic = false, string? styleId = null)
    {
        var run = new Run();
        if (bold || italic)
        {
            var rp = new RunProperties();
            if (bold)
            {
                rp.Bold = new Bold();
            }

            if (italic)
            {
                rp.Italic = new Italic();
            }

            run.RunProperties = rp;
        }

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var paragraph = new Paragraph();
        if (styleId != null)
        {
            paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = styleId });
        }

        paragraph.AppendChild(run);
        return paragraph;
    }

    private static Table BuildTable(bool boldFirstRow, params string[][] rows)
    {
        var table = new Table();
        for (var r = 0; r < rows.Length; r++)
        {
            var row = new TableRow();
            var bold = boldFirstRow && r == 0;
            foreach (var cellText in rows[r])
            {
                var run = new Run();
                if (bold)
                {
                    run.RunProperties = new RunProperties { Bold = new Bold() };
                }

                run.AppendChild(new Text(cellText) { Space = SpaceProcessingModeValues.Preserve });
                var cell = new TableCell(new Paragraph(run));
                row.Append(cell);
            }

            table.Append(row);
        }

        return table;
    }

    // params overloads for tuple-ish readable rows
    private static Table BuildTable(bool boldFirstRow, params (string, string)[] rows)
        => BuildTable(boldFirstRow, rows.Select(t => new[] { t.Item1, t.Item2 }).ToArray());

    private static Table BuildTable(bool boldFirstRow, params (string, string, string)[] rows)
        => BuildTable(boldFirstRow, rows.Select(t => new[] { t.Item1, t.Item2, t.Item3 }).ToArray());
}
