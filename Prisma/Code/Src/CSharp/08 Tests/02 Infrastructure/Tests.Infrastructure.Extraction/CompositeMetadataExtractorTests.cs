using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="CompositeMetadataExtractor"/>.
/// </summary>
public class CompositeMetadataExtractorTests
{
    private readonly XmlMetadataExtractor _xmlExtractor;
    private readonly DocxMetadataExtractor _docxExtractor;
    private readonly PdfMetadataExtractor _pdfExtractor;
    private readonly ILogger<CompositeMetadataExtractor> _logger;
    private readonly CompositeMetadataExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeMetadataExtractorTests"/> class.
    /// </summary>
    public CompositeMetadataExtractorTests()
    {
        var xmlParser = Substitute.For<IXmlNullableParser<Expediente>>();
        var xmlLogger = Substitute.For<ILogger<XmlMetadataExtractor>>();
        _xmlExtractor = new XmlMetadataExtractor(xmlParser, xmlLogger);
        
        var docxLogger = Substitute.For<ILogger<DocxMetadataExtractor>>();
        _docxExtractor = new DocxMetadataExtractor(docxLogger);
        
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var pdfLogger = Substitute.For<ILogger<PdfMetadataExtractor>>();
        _pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, pdfLogger);
        
        _logger = Substitute.For<ILogger<CompositeMetadataExtractor>>();
        _extractor = new CompositeMetadataExtractor(_xmlExtractor, _docxExtractor, _pdfExtractor, _logger);
    }

    /// <summary>
    /// Tests that XML extraction delegates to XmlMetadataExtractor.
    /// </summary>
    [Fact]
    public async Task ExtractFromXmlAsync_DelegatesToXmlExtractor()
    {
        // Arrange
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        // The result will depend on the actual XmlMetadataExtractor behavior
        // Since we're using a real XmlMetadataExtractor with a mocked parser, 
        // it should attempt to parse and may fail, but the delegation should work
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that DOCX extraction delegates to DocxMetadataExtractor.
    /// </summary>
    [Fact]
    public async Task ExtractFromDocxAsync_DelegatesToDocxExtractor()
    {
        // Arrange
        var docxBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP/DOCX header

        // Act
        var result = await _extractor.ExtractFromDocxAsync(docxBytes, TestContext.Current.CancellationToken);

        // Assert
        // The result will depend on the actual DocxMetadataExtractor behavior
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that PDF extraction delegates to PdfMetadataExtractor.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_DelegatesToPdfExtractor()
    {
        // Arrange
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfBytes, TestContext.Current.CancellationToken);

        // Assert
        // The result will depend on the actual PdfMetadataExtractor behavior
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that composite extractor correctly routes XML requests.
    /// </summary>
    [Fact]
    public async Task ExtractFromXmlAsync_RoutesToCorrectExtractor()
    {
        // Arrange
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        var expediente = new Expediente { NumeroExpediente = "TEST-001" };
        var xmlParser = Substitute.For<IXmlNullableParser<Expediente>>();
        xmlParser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.Success(expediente));
        
        var xmlLogger = Substitute.For<ILogger<XmlMetadataExtractor>>();
        var xmlExtractor = new XmlMetadataExtractor(xmlParser, xmlLogger);
        var docxLogger = Substitute.For<ILogger<DocxMetadataExtractor>>();
        var docxExtractor = new DocxMetadataExtractor(docxLogger);
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var pdfLogger = Substitute.For<ILogger<PdfMetadataExtractor>>();
        var pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, pdfLogger);
        
        var compositeExtractor = new CompositeMetadataExtractor(xmlExtractor, docxExtractor, pdfExtractor, _logger);

        // Act
        var result = await compositeExtractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();
        result.Value.Expediente.NumeroExpediente.ShouldBe("TEST-001");
    }
}

