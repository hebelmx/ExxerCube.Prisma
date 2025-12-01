using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="FileTypeIdentifierService"/>.
/// </summary>
public class FileTypeIdentifierServiceTests
{
    private readonly ILogger<FileTypeIdentifierService> _logger;
    private readonly FileTypeIdentifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTypeIdentifierServiceTests"/> class.
    /// </summary>
    public FileTypeIdentifierServiceTests()
    {
        _logger = Substitute.For<ILogger<FileTypeIdentifierService>>();
        _service = new FileTypeIdentifierService(_logger);
    }

    /// <summary>
    /// Tests that PDF files are identified correctly by content signature.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_PdfContent_ReturnsPdf()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4

        // Act
        var result = await _service.IdentifyFileTypeAsync(pdfContent, "test.pdf", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    /// <summary>
    /// Tests that XML files are identified correctly by content signature.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_XmlContent_ReturnsXml()
    {
        // Arrange
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");

        // Act
        var result = await _service.IdentifyFileTypeAsync(xmlContent, "test.xml", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Xml);
    }

    /// <summary>
    /// Tests that DOCX files are identified correctly by ZIP signature and internal structure.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_DocxContent_ReturnsDocx()
    {
        // Arrange
        // DOCX files start with ZIP signature (PK) and contain "word/" marker
        var docxHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP signature
        var docxContent = new byte[2000];
        Array.Copy(docxHeader, docxContent, docxHeader.Length);
        var wordMarker = System.Text.Encoding.UTF8.GetBytes("word/");
        Array.Copy(wordMarker, 0, docxContent, 100, wordMarker.Length);

        // Act
        var result = await _service.IdentifyFileTypeAsync(docxContent, "test.docx", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Docx);
    }

    /// <summary>
    /// Tests that file type identification falls back to extension when content identification fails.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_UnknownContentWithExtension_ReturnsFormatFromExtension()
    {
        // Arrange
        var unknownContent = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await _service.IdentifyFileTypeAsync(unknownContent, "test.pdf", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    /// <summary>
    /// Tests that empty content returns failure.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_EmptyContent_ReturnsFailure()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();

        // Act
        var result = await _service.IdentifyFileTypeAsync(emptyContent, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("null or empty");
    }

    /// <summary>
    /// Tests that null content returns failure.
    /// </summary>
    [Fact]
    public async Task IdentifyFileTypeAsync_NullContent_ReturnsFailure()
    {
        // Arrange
        byte[]? nullContent = null;

        // Act
        var result = await _service.IdentifyFileTypeAsync(nullContent!, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("null or empty");
    }
}
