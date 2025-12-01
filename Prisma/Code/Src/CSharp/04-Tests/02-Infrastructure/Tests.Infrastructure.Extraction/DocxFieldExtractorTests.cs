using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="DocxFieldExtractor"/>.
/// </summary>
public class DocxFieldExtractorTests
{
    private readonly ILogger<DocxFieldExtractor> _logger;
    private readonly DocxFieldExtractor _extractor;

    public DocxFieldExtractorTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<DocxFieldExtractor>(output);
        _extractor = new DocxFieldExtractor(_logger);
    }

    [Fact]
    public async Task ExtractFieldsAsync_ValidDocx_ReturnsExtractedFields()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", "Test Action");
        var source = new DocxSource(docxBytes);
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_WithFilePath_ExtractsFields()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.docx");
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", null);
        await File.WriteAllBytesAsync(tempFile, docxBytes, TestContext.Current.CancellationToken);

        try
        {
            var source = new DocxSource(tempFile);
            var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

            // Act
            var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExtractFieldsAsync_InvalidDocx_ReturnsFailure()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };
        var source = new DocxSource(invalidBytes);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoFileContentOrPath_ReturnsFailure()
    {
        // Arrange
        var source = new DocxSource();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    [Fact]
    public async Task ExtractFieldAsync_ValidDocx_ReturnsFieldValue()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", null);
        var source = new DocxSource(docxBytes);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FieldName.ShouldBe("Expediente");
        result.Value.Value.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.SourceType.ShouldBe("DOCX");
        result.Value.Confidence.ShouldBe(1.0f);
    }

    [Fact]
    public async Task ExtractFieldAsync_FieldNotFound_ReturnsFailure()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", null, null);
        var source = new DocxSource(docxBytes);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "NonExistentField");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyFieldDefinitions_ReturnsEmptyExtractedFields()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", null, null);
        var source = new DocxSource(docxBytes);
        var fieldDefinitions = Array.Empty<FieldDefinition>();

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBeNull(); // No fields extracted
    }

    private static byte[] CreateSampleDocx(string? expediente, string? causa, string? accionSolicitada)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var paragraph = body.AppendChild(new Paragraph());

            var text = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(expediente))
            {
                text.AppendLine($"Expediente: {expediente}");
            }
            if (!string.IsNullOrEmpty(causa))
            {
                text.AppendLine($"CAUSA: {causa}");
            }
            if (!string.IsNullOrEmpty(accionSolicitada))
            {
                text.AppendLine($"ACCIÓN SOLICITADA: {accionSolicitada}");
            }

            paragraph.AppendChild(new Run(new Text(text.ToString())));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}

