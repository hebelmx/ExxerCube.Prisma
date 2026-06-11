using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Implementation-specific tests for <see cref="FileTypeIdentifierService"/>.
/// </summary>
/// <remarks>
/// The universal contract behaviours (PDF magic-number identification, extension fallback, null/empty
/// failure, pre-cancelled-token Cancelled) live in <see cref="FileTypeIdentifierServiceContractTests"/>
/// (the ADR-005 §7 contract instance, Phase 6). What remains here is implementation-specific:
/// XML/DOCX/ZIP signature detection (beyond the contract's scope, per its remarks) and the exact
/// "null or empty" error-message pins. The redundant PdfContent / UnknownContentWithExtension tests were
/// removed (now run against this service via the contract instance + the mutation suite).
/// </remarks>
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
