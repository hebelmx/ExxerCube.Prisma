using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Strategies;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// TDD tests for SearchExtractionStrategy.
/// This strategy resolves cross-references in DOCX documents.
/// Examples:
/// - "transferir por la cantidad arriba mencionada" → searches backward for amount
/// - "el RFC anteriormente indicado" → searches backward for RFC
/// - "según anexo" → searches for referenced data
/// This is EXPECTED behavior when documents reference data instead of repeating it.
/// </summary>
public sealed class SearchExtractionStrategyTests
{
    [Fact]
    public async Task ExtractAsync_BackwardReference_FindsReferencedAmount()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithBackwardReference(
            "Monto: $100,000.00",
            "Transferir por la cantidad arriba mencionada");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Monto", "amount")
        };

        // Act
        var result = await strategy.ExtractAsync(docxSource, fieldDefinitions);

        // Assert
        result.IsSuccess.Should().BeTrue("search strategy should resolve backward reference");
        result.Value.Should().NotBeNull();
        result.Value!.AdditionalFields["Monto"].Should().Be("$100,000.00", "should find referenced amount");
    }

    [Fact]
    public async Task ExtractAsync_AnteriorsReference_FindsReferencedRFC()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithBackwardReference(
            "RFC: XAXX010101000",
            "El RFC anteriormente indicado corresponde al beneficiario");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("RFC", "string")
        };

        // Act
        var result = await strategy.ExtractAsync(docxSource, fieldDefinitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AdditionalFields["RFC"].Should().Be("XAXX010101000", "should find referenced RFC");
    }

    [Fact]
    public async Task ExtractAsync_PreviamenteReference_FindsReferencedExpediente()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithBackwardReference(
            "Expediente: A/AS1-2505-088637-PHM",
            "Según el expediente previamente indicado se procede...");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", "string")
        };

        // Act
        var result = await strategy.ExtractAsync(docxSource, fieldDefinitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Expediente.Should().Be("A/AS1-2505-088637-PHM", "should find referenced expediente");
    }

    [Fact]
    public async Task ExtractAsync_NoReferences_ExtractsDirectly()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var docxBytes = await CreateSimpleDocx("RFC: XAXX010101000\nCausa: Transferencia");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("RFC", "string"),
            new FieldDefinition("Causa", "string")
        };

        // Act
        var result = await strategy.ExtractAsync(docxSource, fieldDefinitions);

        // Assert
        result.IsSuccess.Should().BeTrue("should extract directly when no references");
        result.Value.Should().NotBeNull();
        result.Value!.AdditionalFields["RFC"].Should().Be("XAXX010101000");
        result.Value!.Causa.Should().Be("Transferencia");
    }

    [Fact]
    public async Task ExtractAsync_MultipleReferences_ResolvesAll()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithMultipleReferences();
        var docxSource = new DocxSource { FileContent = docxBytes };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("RFC", "string"),
            new FieldDefinition("Monto", "amount")
        };

        // Act
        var result = await strategy.ExtractAsync(docxSource, fieldDefinitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AdditionalFields["RFC"].Should().Be("XAXX010101000", "should resolve RFC reference");
        result.Value!.AdditionalFields["Monto"].Should().Be("$50,000.00", "should resolve amount reference");
    }

    [Fact]
    public void CanHandle_DocumentWithCrossReferences_ReturnsTrue()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasCrossReferences = true,
            HasStructuredFormat = false
        };

        // Act
        var canHandle = strategy.CanHandle(structure);

        // Assert
        canHandle.Should().BeTrue("search strategy should handle documents with cross-references");
    }

    [Fact]
    public void CanHandle_DocumentWithoutCrossReferences_ReturnsTrue()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasCrossReferences = false
        };

        // Act
        var canHandle = strategy.CanHandle(structure);

        // Assert
        canHandle.Should().BeTrue("search strategy can still extract even without cross-references");
    }

    [Fact]
    public void CalculateConfidence_WithCrossReferences_ReturnsHighConfidence()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasCrossReferences = true
        };

        // Act
        var confidence = strategy.CalculateConfidence(structure);

        // Assert
        confidence.Should().Be(0.90f, "high confidence when cross-references detected");
    }

    [Fact]
    public void CalculateConfidence_WithoutCrossReferences_ReturnsMediumConfidence()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasCrossReferences = false
        };

        // Act
        var confidence = strategy.CalculateConfidence(structure);

        // Assert
        confidence.Should().Be(0.60f, "medium confidence when no cross-references");
    }

    [Fact]
    public void StrategyType_Returns_Search()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(NullLogger<SearchExtractionStrategy>.Instance);

        // Act
        var type = strategy.StrategyType;

        // Assert
        type.Should().Be(DocxExtractionStrategy.Search);
    }

    // Helper methods to create test DOCX documents
    private async Task<byte[]> CreateDocxWithBackwardReference(string firstPart, string referencePart)
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // First paragraph with actual data
            var p1 = body.AppendChild(new Paragraph());
            var r1 = p1.AppendChild(new Run());
            r1.AppendChild(new Text(firstPart));

            // Second paragraph with reference
            var p2 = body.AppendChild(new Paragraph());
            var r2 = p2.AppendChild(new Run());
            r2.AppendChild(new Text(referencePart));
        }
        return memoryStream.ToArray();
    }

    private async Task<byte[]> CreateSimpleDocx(string text)
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
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

    private async Task<byte[]> CreateDocxWithMultipleReferences()
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // RFC data
            var p1 = body.AppendChild(new Paragraph());
            var r1 = p1.AppendChild(new Run());
            r1.AppendChild(new Text("RFC del beneficiario: XAXX010101000"));

            // Amount data
            var p2 = body.AppendChild(new Paragraph());
            var r2 = p2.AppendChild(new Run());
            r2.AppendChild(new Text("Monto: $50,000.00"));

            // RFC reference
            var p3 = body.AppendChild(new Paragraph());
            var r3 = p3.AppendChild(new Run());
            r3.AppendChild(new Text("Según el RFC arriba mencionado"));

            // Amount reference
            var p4 = body.AppendChild(new Paragraph());
            var r4 = p4.AppendChild(new Run());
            r4.AppendChild(new Text("Transferir por la cantidad anteriormente indicada"));
        }
        return memoryStream.ToArray();
    }
}
