using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Enhanced unit tests for <see cref="PdfOcrFieldExtractor"/> covering edge cases, error recovery, and performance.
/// </summary>
public class PdfOcrFieldExtractorEnhancedTests
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly ILogger<PdfOcrFieldExtractor> _logger;
    private readonly PdfOcrFieldExtractor _extractor;

    public PdfOcrFieldExtractorEnhancedTests(ITestOutputHelper output)
    {
        _ocrExecutor = Substitute.For<IOcrExecutor>();
        _imagePreprocessor = Substitute.For<IImagePreprocessor>();
        _logger = XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output);
        _extractor = new PdfOcrFieldExtractor(_ocrExecutor, _imagePreprocessor, _logger);
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyPdfFile_ReturnsFailure()
    {
        // Arrange
        var emptyPdf = Array.Empty<byte>();
        var source = new PdfSource(emptyPdf);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFieldsAsync_CorruptedPdfFile_ReturnsFailure()
    {
        // Arrange
        var corruptedPdf = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }; // Not a valid PDF
        var source = new PdfSource(corruptedPdf);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = corruptedPdf, SourcePath = "corrupted.pdf" };
        var preprocessedImage = new ImageData { Data = corruptedPdf, SourcePath = "corrupted.pdf" };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("Invalid PDF format"));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("PDF");
    }

    [Fact]
    public async Task ExtractFieldsAsync_MultiPagePdf_ProcessesFirstPage()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "multipage.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "multipage.pdf" };
        var ocrText = "Page 1: Expediente: A/AS1-2505-088637-PHM";
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

    [Fact]
    public async Task ExtractFieldsAsync_PdfWithImagesOnly_ReturnsEmptyFields()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "images_only.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "images_only.pdf" };
        var ocrText = ""; // No text extracted
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.1f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBeNull(); // No fields extracted
    }

    [Fact]
    public async Task ExtractFieldsAsync_OCRTimesOut_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "timeout.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "timeout.pdf" };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("OCR operation timed out"));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("OCR");
    }

    [Fact]
    public async Task ExtractFieldsAsync_OCRServiceUnavailable_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "unavailable.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "unavailable.pdf" };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("OCR service unavailable"));

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("OCR");
    }

    [Fact]
    public async Task ExtractFieldsAsync_PartialOCRResults_ExtractsAvailableFields()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var source = new PdfSource(pdfContent);
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "partial.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "partial.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM"; // Missing Causa
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.7f };

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
        result.Value.Causa.ShouldBeNull(); // Not found in partial OCR
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ExtractFieldsAsync_LargePdf_CompletesWithin30Seconds()
    {
        // Arrange
        var largePdf = new byte[10 * 1024 * 1024]; // 10MB PDF (simulated)
        Array.Fill(largePdf, (byte)0x25);
        Array.Copy(new byte[] { 0x25, 0x50, 0x44, 0x46 }, 0, largePdf, 0, 4); // PDF header

        var source = new PdfSource(largePdf);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = largePdf, SourcePath = "large.pdf" };
        var preprocessedImage = new ImageData { Data = largePdf, SourcePath = "large.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM";
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000,
            $"Large PDF processing took {stopwatch.ElapsedMilliseconds}ms, exceeding 30 second target (NFR4)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ExtractFieldsAsync_ConcurrentPdfProcessing_HandlesMultipleRequests()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var imageData = new ImageData { Data = pdfContent, SourcePath = "concurrent.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "concurrent.pdf" };
        var ocrText = "Expediente: A/AS1-2505-088637-PHM";
        var ocrResult = new OCRResult { Text = ocrText, ConfidenceAvg = 0.9f };

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Act - Process multiple PDFs concurrently
        var tasks = new List<Task<Result<ExtractedFields>>>();
        for (int i = 0; i < 5; i++)
        {
            var source = new PdfSource(pdfContent);
            tasks.Add(_extractor.ExtractFieldsAsync(source, fieldDefinitions));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Length.ShouldBe(5);
        foreach (var result in results)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
        }
    }
}