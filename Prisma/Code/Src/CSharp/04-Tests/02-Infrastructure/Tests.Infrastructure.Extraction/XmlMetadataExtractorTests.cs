using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="XmlMetadataExtractor"/>.
/// </summary>
public class XmlMetadataExtractorTests
{
    private readonly IXmlNullableParser<Expediente> _xmlParser;
    private readonly ILogger<XmlMetadataExtractor> _logger;
    private readonly XmlMetadataExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlMetadataExtractorTests"/> class.
    /// </summary>
    public XmlMetadataExtractorTests()
    {
        _xmlParser = Substitute.For<IXmlNullableParser<Expediente>>();
        _logger = Substitute.For<ILogger<XmlMetadataExtractor>>();
        _extractor = new XmlMetadataExtractor(_xmlParser, _logger);
    }

    /// <summary>
    /// Tests that XML metadata extraction succeeds when parser succeeds.
    /// </summary>
    [Fact]
    public async Task ExtractFromXmlAsync_ValidXml_ReturnsExtractedMetadata()
    {
        // Arrange
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            NumeroOficio = "214-1-18714972/2025",
            AreaDescripcion = "ASEGURAMIENTO",
            SolicitudPartes =
            {
                new SolicitudParte
                {
                    Nombre = "Juan",
                    Paterno = "Perez",
                    Rfc = "PERJ800101ABC"
                }
            }
        };
        var xmlBytes = new byte[] { 1, 2, 3 };

        _xmlParser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.Success(expediente));

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();
        result.Value.Expediente.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.RfcValues.ShouldNotBeNull();
        result.Value.RfcValues.ShouldContain("PERJ800101ABC");
    }

    /// <summary>
    /// Tests that XML metadata extraction fails when parser fails.
    /// </summary>
    [Fact]
    public async Task ExtractFromXmlAsync_ParserFails_ReturnsFailure()
    {
        // Arrange
        var xmlBytes = new byte[] { 1, 2, 3 };

        _xmlParser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.WithFailure("Parse error"));

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Parse error");
    }

    /// <summary>
    /// Tests that DOCX extraction returns failure (not supported by XML extractor).
    /// </summary>
    [Fact]
    public async Task ExtractFromDocxAsync_Always_ReturnsFailure()
    {
        // Arrange
        var docxBytes = new byte[] { 1, 2, 3 };

        // Act
        var result = await _extractor.ExtractFromDocxAsync(docxBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("DocxMetadataExtractor");
    }

    /// <summary>
    /// Tests that PDF extraction returns failure (not supported by XML extractor).
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_Always_ReturnsFailure()
    {
        // Arrange
        var pdfBytes = new byte[] { 1, 2, 3 };

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("PdfMetadataExtractor");
    }
}

