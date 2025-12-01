using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Analysis;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// TDD tests for DocxStructureAnalyzer.
/// Tests document structure analysis for strategy selection.
/// </summary>
public sealed class DocxStructureAnalyzerTests(ITestOutputHelper output)
{
    private readonly ILogger<DocxStructureAnalyzerTests> logger = XUnitLogger.CreateLogger<DocxStructureAnalyzerTests>(output);

    [Fact]
    public async Task AnalyzeStructure_DocumentWithTables_ReturnsTableBasedStrategy()
    {
        logger.LogInformation("═══ TEST: AnalyzeStructure_DocumentWithTables_ReturnsTableBasedStrategy ═══");

        // Arrange
        logger.LogInformation("Creating DocxStructureAnalyzer...");
        var analyzer = new DocxStructureAnalyzer();

        logger.LogInformation("Creating document with tables...");
        var docxBytes = await CreateDocumentWithTables();
        logger.LogInformation("DOCX created: {ByteSize} bytes", docxBytes.Length);

        // Act
        logger.LogInformation("Analyzing document structure...");
        var result = analyzer.AnalyzeStructure(docxBytes);

        logger.LogInformation("═══ ANALYSIS RESULTS ═══");
        logger.LogInformation("HasTables: {HasTables} (expected: True)", result.HasTables);
        logger.LogInformation("TableStructure: {@TableStructure}", result.TableStructure);
        if (result.TableStructure != null)
        {
            logger.LogInformation("  RowCount: {RowCount}", result.TableStructure.RowCount);
            logger.LogInformation("  HasHeaderRow: {HasHeaderRow}", result.TableStructure.HasHeaderRow);
            logger.LogInformation("  ColumnHeaders: {ColumnHeaders}", string.Join(", ", result.TableStructure.ColumnHeaders ?? Array.Empty<string>()));
        }
        logger.LogInformation("RecommendedStrategy: {RecommendedStrategy} (expected: TableBased)", result.RecommendedStrategy);

        // Assert
        result.HasTables.Should().BeTrue();
        result.TableStructure.Should().NotBeNull();
        result.TableStructure!.RowCount.Should().BeGreaterThan(1);
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.TableBased);

        logger.LogInformation("✓ TEST PASSED");
    }

    [Fact]
    public async Task AnalyzeStructure_DocumentWithCrossReferences_ReturnsHybridStrategy()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateDocumentWithCrossReferences();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);

        // Assert
        result.HasCrossReferences.Should().BeTrue();
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.Hybrid);
    }

    [Fact]
    public async Task AnalyzeStructure_UnstructuredDocument_ReturnsFuzzyStrategy()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateUnstructuredDocument();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);

        // Assert
        result.HasStructuredFormat.Should().BeFalse();
        result.HasTables.Should().BeFalse();
        result.HasBoldLabels.Should().BeFalse();
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.Fuzzy);
    }

    [Fact]
    public async Task AnalyzeStructure_TableWithHeaders_DetectsHeaderRow()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateTableWithHeaders();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);

        // Assert
        result.TableStructure.Should().NotBeNull();
        result.TableStructure!.HasHeaderRow.Should().BeTrue();
        result.TableStructure.ColumnHeaders.Should().NotBeNull();
        result.TableStructure.ColumnHeaders.Should().Contain("Expediente");
        result.TableStructure.ColumnHeaders.Should().Contain("RFC");
    }

    [Fact]
    public void AnalyzeStructure_EmptyDocument_ThrowsArgumentException()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var emptyBytes = Array.Empty<byte>();

        // Act
        var act = () => analyzer.AnalyzeStructure(emptyBytes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*"); // Actual: "DOCX bytes cannot be null or empty. (Parameter 'docxBytes')"
    }

    // Helper methods to create test DOCX documents
    private async Task<byte[]> CreateStructuredCNBVDocument()
    {
        // Create a DOCX with CNBV standard format
        // For now, return minimal valid DOCX (will implement actual creation)
        return await CreateMinimalDocx("CNBV Formato Estándar\n\nExpediente: A/AS1-2505-088637-PHM\nRFC: XAXX010101000");
    }

    private async Task<byte[]> CreateDocumentWithTables()
    {
        // Create DOCX with table structure
        return await CreateDocxWithTable();
    }

    private async Task<byte[]> CreateDocumentWithBoldLabels()
    {
        // Create DOCX with bold labels
        return await CreateDocxWithBoldText("Expediente:", "A/AS1-2505-088637-PHM");
    }

    private async Task<byte[]> CreateDocumentWithCrossReferences()
    {
        // Create DOCX with "arriba mencionada" references
        return await CreateMinimalDocx("Monto: $100,000.00\n\nTransferir por la cantidad arriba mencionada");
    }

    private async Task<byte[]> CreateUnstructuredDocument()
    {
        // Create unstructured DOCX
        return await CreateMinimalDocx("Este es un documento sin estructura clara con información mezclada");
    }

    private async Task<byte[]> CreateTableWithHeaders()
    {
        // Create table with "Expediente" and "RFC" headers
        return await CreateDocxWithTable();
    }

    private async Task<byte[]> CreateMinimalDocx(string text)
    {
        logger.LogInformation("→ CreateMinimalDocx: Creating DOCX with text: '{Text}'", text);

        // Minimal DOCX creation using DocumentFormat.OpenXml
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var paragraph = body.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text(text));
        }

        var bytes = memoryStream.ToArray();
        logger.LogInformation("→ CreateMinimalDocx: Created {Size} bytes", bytes.Length);
        return bytes;
    }

    private async Task<byte[]> CreateDocxWithTable()
    {
        logger.LogInformation("→ CreateDocxWithTable: Creating DOCX with 2x2 table");

        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var table = new Table();

            // Header row
            var headerRow = new TableRow();
            headerRow.Append(CreateTableCell("Expediente", isBold: true));
            headerRow.Append(CreateTableCell("RFC", isBold: true));
            table.Append(headerRow);
            logger.LogInformation("→   Header row: ['Expediente' (bold), 'RFC' (bold)]");

            // Data row
            var dataRow = new TableRow();
            dataRow.Append(CreateTableCell("A/AS1-2505-088637-PHM"));
            dataRow.Append(CreateTableCell("XAXX010101000"));
            table.Append(dataRow);
            logger.LogInformation("→   Data row: ['A/AS1-2505-088637-PHM', 'XAXX010101000']");

            body.Append(table);
        }

        var bytes = memoryStream.ToArray();
        logger.LogInformation("→ CreateDocxWithTable: Created {Size} bytes", bytes.Length);
        return bytes;
    }

    private async Task<byte[]> CreateDocxWithBoldText(string label, string value)
    {
        logger.LogInformation("→ CreateDocxWithBoldText: Creating DOCX with bold '{Label}' and value '{Value}'", label, value);

        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var paragraph = body.AppendChild(new Paragraph());

            // Bold label
            var boldRun = paragraph.AppendChild(new Run());
            var boldProps = new RunProperties(new Bold());
            boldRun.AppendChild(boldProps);
            boldRun.AppendChild(new Text(label));
            logger.LogInformation("→   Added BOLD run: '{Label}'", label);

            // Normal value
            var normalRun = paragraph.AppendChild(new Run());
            normalRun.AppendChild(new Text(" " + value));
            logger.LogInformation("→   Added normal run: '{Value}'", " " + value);
        }

        var bytes = memoryStream.ToArray();
        logger.LogInformation("→ CreateDocxWithBoldText: Created {Size} bytes", bytes.Length);
        return bytes;
    }

    private TableCell CreateTableCell(string text, bool isBold = false)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        var run = new Run();

        if (isBold)
        {
            run.AppendChild(new RunProperties(new Bold()));
        }

        run.AppendChild(new Text(text));
        paragraph.Append(run);
        cell.Append(paragraph);

        return cell;
    }
}