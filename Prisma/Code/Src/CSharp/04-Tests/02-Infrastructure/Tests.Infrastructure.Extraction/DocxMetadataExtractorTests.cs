using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="DocxMetadataExtractor"/>.
/// </summary>
public class DocxMetadataExtractorTests
{
    private readonly ILogger<DocxMetadataExtractor> _logger;
    private readonly DocxMetadataExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxMetadataExtractorTests"/> class.
    /// </summary>
    public DocxMetadataExtractorTests()
    {
        _logger = Substitute.For<ILogger<DocxMetadataExtractor>>();
        _extractor = new DocxMetadataExtractor(_logger);
    }

    /// <summary>
    /// Tests that DOCX metadata extraction succeeds with valid DOCX content.
    /// </summary>
    [Fact]
    public async Task ExtractFromDocxAsync_ValidDocx_ReturnsExtractedMetadata()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "ASEGURAMIENTO", "PERJ800101ABC", "Juan Perez");

        // Act
        var result = await _extractor.ExtractFromDocxAsync(docxBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();
        result.Value.Expediente.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Expediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
    }

    /// <summary>
    /// Tests that DOCX extraction fails when document has no main part.
    /// </summary>
    [Fact]
    public async Task ExtractFromDocxAsync_InvalidDocx_ReturnsFailure()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _extractor.ExtractFromDocxAsync(invalidBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that XML extraction returns failure (not supported by DOCX extractor).
    /// </summary>
    [Fact]
    public async Task ExtractFromXmlAsync_Always_ReturnsFailure()
    {
        // Arrange
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("XmlMetadataExtractor");
    }

    /// <summary>
    /// Tests that PDF extraction returns failure (not supported by DOCX extractor).
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_Always_ReturnsFailure()
    {
        // Arrange
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("PdfMetadataExtractor");
    }

    /// <summary>
    /// Creates a sample DOCX document in memory with specified content.
    /// </summary>
    private static byte[] CreateSampleDocx(string expedienteNumber, string areaDescripcion, string rfc, string name)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            
            // Add content with expediente number, area, RFC, and name
            var paragraph1 = body.AppendChild(new Paragraph());
            paragraph1.AppendChild(new Run(new Text($"Expediente: {expedienteNumber}")));
            
            var paragraph2 = body.AppendChild(new Paragraph());
            paragraph2.AppendChild(new Run(new Text($"Area: {areaDescripcion}")));
            
            var paragraph3 = body.AppendChild(new Paragraph());
            paragraph3.AppendChild(new Run(new Text($"RFC: {rfc}")));
            
            var paragraph4 = body.AppendChild(new Paragraph());
            paragraph4.AppendChild(new Run(new Text($"Nombre: {name}")));
            
            mainPart.Document.Save();
        }
        
        return stream.ToArray();
    }
}

