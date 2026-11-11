using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="PdfMetadataExtractor"/>.
/// </summary>
public class PdfMetadataExtractorTests
{
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly ILogger<PdfMetadataExtractor> _logger;
    private readonly PdfMetadataExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMetadataExtractorTests"/> class.
    /// </summary>
    public PdfMetadataExtractorTests()
    {
        _imagePreprocessor = Substitute.For<IImagePreprocessor>();
        _ocrExecutor = Substitute.For<IOcrExecutor>();
        _logger = Substitute.For<ILogger<PdfMetadataExtractor>>();
        _extractor = new PdfMetadataExtractor(_ocrExecutor, _imagePreprocessor, _logger);
    }

    /// <summary>
    /// Tests that PDF metadata extraction succeeds when OCR pipeline succeeds.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_OCRSuccess_ReturnsExtractedMetadata()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // PDF header
        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM Area: ASEGURAMIENTO RFC: PERJ800101ABC";
        var ocrResult = new OCRResult { Text = ocrText };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();
        result.Value.Expediente.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        await _imagePreprocessor.Received().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

    /// <summary>
    /// Tests that PDF extraction fails when preprocessing fails.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_PreprocessingFails_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.WithFailure("Preprocessing failed"));

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Contains("Preprocessing", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF extraction fails when OCR execution fails.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdfAsync_OCRFails_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("OCR execution failed"));

        // Act
        var result = await _extractor.ExtractFromPdfAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Contains("OCR", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that XML extraction returns failure (not supported by PDF extractor).
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
    /// Tests that DOCX extraction returns failure (not supported by PDF extractor).
    /// </summary>
    [Fact]
    public async Task ExtractFromDocxAsync_Always_ReturnsFailure()
    {
        // Arrange
        var docxBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP/DOCX header

        // Act
        var result = await _extractor.ExtractFromDocxAsync(docxBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("DocxMetadataExtractor");
    }
}