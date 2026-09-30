using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Strategies;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// TDD tests for ComplementExtractionStrategy.
/// This strategy fills gaps when XML/OCR are missing data (EXPECTED behavior, not failure mode).
/// Example: "transferir fondos de la cuenta xyz a la cuenta xysx por la cantidad arriba mencionada"
/// - XML has: account numbers ✅
/// - PDF has: account numbers ✅
/// - Neither has: cantidad (amount) ❌
/// - DOCX has: amount somewhere in document ✅
/// </summary>
public sealed class ComplementExtractionStrategyTests
{
    [Fact]
    public async Task ExtractComplementAsync_XmlMissingRFC_DocxFillsGap()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithRFC("XAXX010101000");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // RFC is missing - EXPECTED scenario
        };

        var ocrFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // RFC is missing - EXPECTED scenario
        };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", "string"),
            new FieldDefinition("RFC", "string")
        };

        // Act
        var result = await strategy.ExtractComplementAsync(docxSource, fieldDefinitions, xmlFields, ocrFields);

        // Assert
        result.IsSuccess.ShouldBeTrue("complement extraction should succeed");
        result.Value.ShouldNotBeNull();
        result.Value!.AdditionalFields["RFC"].ShouldBe("XAXX010101000", "DOCX should fill RFC gap");
        result.Value.Expediente.ShouldBeNull("should not duplicate data from XML/OCR");
    }

    [Fact]
    public async Task ExtractComplementAsync_XmlMissingCausa_DocxFillsGap()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithCausa("Transferencia no autorizada");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // Causa is missing
        };

        var ocrFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // Causa is missing
        };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", "string"),
            new FieldDefinition("Causa", "string")
        };

        // Act
        var result = await strategy.ExtractComplementAsync(docxSource, fieldDefinitions, xmlFields, ocrFields);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Causa.ShouldBe("Transferencia no autorizada", "DOCX should fill Causa gap");
        result.Value.Expediente.ShouldBeNull("should not duplicate existing field");
    }

    [Fact]
    public async Task ExtractComplementAsync_AllFieldsInXml_ReturnsEmptyComplement()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithRFC("XAXX010101000");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
        };
        xmlFields.AdditionalFields["RFC"] = "XAXX010101000"; // Already present

        var ocrFields = new ExtractedFields();

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", "string"),
            new FieldDefinition("RFC", "string")
        };

        // Act
        var result = await strategy.ExtractComplementAsync(docxSource, fieldDefinitions, xmlFields, ocrFields);

        // Assert
        result.IsSuccess.ShouldBeTrue("should succeed even when no complement needed");
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBeNull("should not return fields that already exist");
        result.Value.AdditionalFields.ShouldNotContainKey("RFC", "should not return fields that already exist");
    }

    [Fact]
    public async Task ExtractComplementAsync_OcrHasFieldXmlDoesNot_ReturnsEmptyForThatField()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var docxBytes = await CreateDocxWithCausa("Transferencia");
        var docxSource = new DocxSource { FileContent = docxBytes };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // Causa missing
        };

        var ocrFields = new ExtractedFields
        {
            Causa = "Transferencia" // OCR has it
        };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", "string"),
            new FieldDefinition("Causa", "string")
        };

        // Act
        var result = await strategy.ExtractComplementAsync(docxSource, fieldDefinitions, xmlFields, ocrFields);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Causa.ShouldBeNull("OCR already has this field");
        result.Value.Expediente.ShouldBeNull("XML already has this field");
    }

    [Fact]
    public void CanHandle_Always_ReturnsTrue()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasStructuredFormat = false,
            HasTables = false,
            HasKeyValuePairs = false
        };

        // Act
        var canHandle = strategy.CanHandle(structure);

        // Assert
        canHandle.ShouldBeTrue("complement strategy can always attempt to fill gaps");
    }

    [Fact]
    public void CalculateConfidence_Always_ReturnsHighConfidence()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);
        var structure = new DocxStructure
        {
            HasStructuredFormat = false,
            HasTables = false,
            HasKeyValuePairs = false
        };

        // Act
        var confidence = strategy.CalculateConfidence(structure);

        // Assert
        confidence.ShouldBe(0.80f, "complement strategy has high confidence for filling gaps");
    }

    [Fact]
    public void StrategyType_Returns_Complement()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(NullLogger<ComplementExtractionStrategy>.Instance);

        // Act
        var type = strategy.StrategyType;

        // Assert
        type.ShouldBe(DocxExtractionStrategy.Complement);
    }

    // Helper methods to create test DOCX documents
    private async Task<byte[]> CreateDocxWithRFC(string rfc)
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var paragraph = body.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text($"RFC: {rfc}"));
        }
        return memoryStream.ToArray();
    }

    private async Task<byte[]> CreateDocxWithCausa(string causa)
    {
        using var memoryStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var paragraph = body.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text($"Causa: {causa}"));
        }
        return memoryStream.ToArray();
    }
}