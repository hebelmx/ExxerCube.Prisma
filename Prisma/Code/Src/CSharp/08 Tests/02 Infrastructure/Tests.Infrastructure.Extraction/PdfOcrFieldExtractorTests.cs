using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="PdfOcrFieldExtractor"/>.
/// </summary>
/// <remarks>
/// The PDF→image conversion and the OCR/preprocess boundaries are mocked; the real
/// <see cref="AdaptiveTxtFieldExtractor"/> performs field parsing so these tests verify the
/// orchestration end-to-end with controlled OCR text (no real PDF rendering required).
/// </remarks>
public class PdfOcrFieldExtractorTests
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IPdfToImageConverter _pdfToImageConverter;
    private readonly IFieldExtractor<TxtSource> _txtFieldExtractor;
    private readonly ILogger<PdfOcrFieldExtractor> _logger;
    private readonly PdfOcrFieldExtractor _extractor;

    public PdfOcrFieldExtractorTests(ITestOutputHelper output)
    {
        _ocrExecutor = Substitute.For<IOcrExecutor>();
        _imagePreprocessor = Substitute.For<IImagePreprocessor>();
        _pdfToImageConverter = Substitute.For<IPdfToImageConverter>();
        // Real field parser (the SUT delegates parsing to it).
        _txtFieldExtractor = new AdaptiveTxtFieldExtractor(XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output));
        _logger = XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output);
        _extractor = new PdfOcrFieldExtractor(_ocrExecutor, _imagePreprocessor, _pdfToImageConverter, _txtFieldExtractor, _logger);

        // Default: non-empty PDF bytes render to a single page; empty/null bytes fail conversion.
        _pdfToImageConverter
            .ConvertToImagesAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<byte[]>>.Success(new List<byte[]> { new byte[] { 1, 2, 3 } }));
        _pdfToImageConverter
            .ConvertToImagesAsync(Arg.Is<byte[]>(b => b == null || b.Length == 0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<byte[]>>.WithFailure("PDF content cannot be null or empty"));
    }

    [Fact]
    public async Task ExtractFieldsAsync_ValidPdfWithOCR_ReturnsExtractedFields()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM CAUSA: Test Causa";
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        await _imagePreprocessor.Received().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

    /// <summary>
    /// Tests that ExtractFieldsAsync extracts fields from PDF file path correctly.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_WithFilePath_ExtractsFields()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await File.WriteAllBytesAsync(tempFile, pdfContent, TestContext.Current.CancellationToken);

        try
        {
            var source = new PdfSource(tempFile);
            var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

            var imageData = new ImageData { Data = pdfContent, SourcePath = tempFile };
            var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = tempFile };
            var ocrText = "Expediente: A/AS1-2505-088637-PHM";
            var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

            _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
                .Returns(Result<ImageData>.Success(preprocessedImage));
            _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
                .Returns(Result<OCRResult>.Success(ocrResult));

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

    /// <summary>
    /// Tests that ExtractFieldsAsync returns failure when no file content or path is provided.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_NoFileContentOrPath_ReturnsFailure()
    {
        // Arrange
        var source = new PdfSource();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    /// <summary>
    /// Tests that ExtractFieldsAsync returns failure when OCR execution fails.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_OCRFails_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("OCR execution failed"));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("OCR");
    }

    /// <summary>
    /// Tests that ExtractFieldsAsync returns failure when image preprocessing fails.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_PreprocessingFails_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.WithFailure("Preprocessing failed"));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert — the extractor tolerates a per-page preprocessing failure by skipping that
        // page; with the only page skipped, no text is produced and extraction fails overall.
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that ExtractFieldAsync extracts a single field from valid PDF correctly.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_ValidPdf_ReturnsFieldValue()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent) { OcrConfidence = 0.85f };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM";
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FieldName.ShouldBe("Expediente");
        result.Value.Value.ShouldBe("A/AS1-2505-088637-PHM");
        // The PDF extractor delegates parsing to the TXT field extractor, which stamps the
        // field origin as PdfOcr with SourceType "TXT_OCR" and an OCR-derived confidence.
        result.Value.SourceType.ShouldBe("TXT_OCR");
        result.Value.Origin.ShouldBe(Domain.Enums.FieldOrigin.PdfOcr);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(0f);
    }

    /// <summary>
    /// Tests that ExtractFieldAsync returns failure when field is not found in PDF.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FieldNotFound_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);

        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrText = "Some text without expediente";
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }
}