namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// TDD tests for DocxStructureAnalyzer.
/// Tests document structure analysis for strategy selection.
/// </summary>
public sealed class DocxStructureAnalyzerTests(ITestOutputHelper output)
{
    private readonly ILogger<DocxStructureAnalyzerTests> logger = XUnitLogger.CreateLogger<DocxStructureAnalyzerTests>(output);

    [Fact]
    public async Task AnalyzeStructure_StructuredCNBVDocument_ReturnsStructuredFormat()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateStructuredCNBVDocument();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);
        logger.LogInformation("Analysis Result: {@Result}", result);
        // Assert
        result.Should().NotBeNull();
        result.HasStructuredFormat.Should().BeTrue("CNBV template should be detected");
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.Structured);
    }

    [Fact]
    public async Task AnalyzeStructure_DocumentWithTables_ReturnsTableBasedStrategy()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateDocumentWithTables();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);

        // Assert
        result.HasTables.Should().BeTrue();
        result.TableStructure.Should().NotBeNull();
        result.TableStructure!.RowCount.Should().BeGreaterThan(1);
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.TableBased);
    }

    [Fact]
    public async Task AnalyzeStructure_DocumentWithBoldLabels_ReturnsContextualStrategy()
    {
        // Arrange
        var analyzer = new DocxStructureAnalyzer();
        var docxBytes = await CreateDocumentWithBoldLabels();

        // Act
        var result = analyzer.AnalyzeStructure(docxBytes);

        // Assert
        result.HasBoldLabels.Should().BeTrue();
        result.HasKeyValuePairs.Should().BeTrue();
        result.RecommendedStrategy.Should().Be(DocxExtractionStrategy.Contextual);
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
        return memoryStream.ToArray();
    }

    private async Task<byte[]> CreateDocxWithTable()
    {
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

            // Data row
            var dataRow = new TableRow();
            dataRow.Append(CreateTableCell("A/AS1-2505-088637-PHM"));
            dataRow.Append(CreateTableCell("XAXX010101000"));
            table.Append(dataRow);

            body.Append(table);
        }
        return memoryStream.ToArray();
    }

    private async Task<byte[]> CreateDocxWithBoldText(string label, string value)
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var paragraph = body.AppendChild(new Paragraph());

            // Bold label
            var boldRun = paragraph.AppendChild(new Run());
            boldRun.AppendChild(new RunProperties(new Bold()));
            boldRun.AppendChild(new Text(label));

            // Normal value
            var normalRun = paragraph.AppendChild(new Run());
            normalRun.AppendChild(new Text(" " + value));
        }
        return memoryStream.ToArray();
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