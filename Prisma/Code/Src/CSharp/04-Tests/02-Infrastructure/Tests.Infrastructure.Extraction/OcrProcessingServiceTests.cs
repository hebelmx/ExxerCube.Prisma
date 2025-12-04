using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Execution;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="OcrProcessingService"/> covering preprocessing, OCR execution, extraction, and failure paths.
/// </summary>
public class OcrProcessingServiceTests
{
    private readonly OcrProcessingService _service;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<IOcrProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessingServiceTests"/> class with mocked pipeline dependencies.
    /// </summary>
    public OcrProcessingServiceTests()
    {
        _imagePreprocessor = Substitute.For<IImagePreprocessor>();
        _ocrExecutor = Substitute.For<IOcrExecutor>();
        _fieldExtractor = Substitute.For<IFieldExtractor>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _logger = Substitute.For<ILogger<IOcrProcessingService>>();
        var metricsService = Substitute.For<IProcessingMetricsService>();
        _service = new OcrProcessingService(_imagePreprocessor, _ocrExecutor, _fieldExtractor, _eventPublisher, _logger, metricsService);
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns a successful result for valid input.
    /// </summary>
    /// <returns>A task that completes after asserting successful OCR and extraction.</returns>
    [Fact]
    public async Task ProcessDocument_ValidDocument_ReturnsSuccessResult()
    {
        // Arrange
        var imageData = CreateValidImageData();
        var config = CreateValidProcessingConfig();
        var preprocessedImage = CreatePreprocessedImageData();
        var ocrResult = CreateExpectedOcrResult();
        var extractedFields = CreateExpectedExtractedFields();

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));
        _fieldExtractor.ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _service.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.SourcePath.ShouldBe(imageData.SourcePath);
        result.Value.PageNumber.ShouldBe(imageData.PageNumber);
        result.Value.OCRResult.ShouldNotBeNull();
        result.Value.ExtractedFields.ShouldNotBeNull();

        await _imagePreprocessor.Received(1).PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received(1).ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.Received(1).ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns a failure result for invalid image data.
    /// </summary>
    /// <returns>A task that completes after asserting invalid input is rejected.</returns>
    [Fact]
    public async Task ProcessDocument_InvalidImageData_ReturnsFailureResult()
    {
        // Arrange
        var invalidImageData = CreateInvalidImageData();
        var config = CreateValidProcessingConfig();

        // Act
        var result = await _service.ProcessDocumentAsync(invalidImageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
        result.Error.ShouldContain("Image data is empty");

        await _imagePreprocessor.DidNotReceive().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.DidNotReceive().ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns a failure result when preprocessing fails.
    /// </summary>
    /// <returns>A task that completes after asserting preprocessing failures are surfaced.</returns>
    [Fact]
    public async Task ProcessDocument_PreprocessingFails_ReturnsFailureResult()
    {
        // Arrange
        var imageData = CreateValidImageData();
        var config = CreateValidProcessingConfig();

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Failure("Preprocessing failed"));

        // Act
        var result = await _service.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Preprocessing failed");

        await _imagePreprocessor.Received(1).PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.DidNotReceive().ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns a failure result when OCR fails.
    /// </summary>
    /// <returns>A task that completes after asserting OCR failures are surfaced.</returns>
    [Fact]
    public async Task ProcessDocument_OcrFails_ReturnsFailureResult()
    {
        // Arrange
        var imageData = CreateValidImageData();
        var config = CreateValidProcessingConfig();
        var preprocessedImage = CreatePreprocessedImageData();

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Failure("OCR failed"));

        // Act
        var result = await _service.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("OCR failed");

        await _imagePreprocessor.Received(1).PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received(1).ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.DidNotReceive().ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns a failure result when field extraction fails.
    /// </summary>
    /// <returns>A task that completes after asserting extraction failures are surfaced.</returns>
    [Fact]
    public async Task ProcessDocument_FieldExtractionFails_ReturnsFailureResult()
    {
        // Arrange
        var imageData = CreateValidImageData();
        var config = CreateValidProcessingConfig();
        var preprocessedImage = CreatePreprocessedImageData();
        var ocrResult = CreateExpectedOcrResult();

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));
        _fieldExtractor.ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>())
            .Returns(Result<ExtractedFields>.Failure("Field extraction failed"));

        // Act
        var result = await _service.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Field extraction failed");

        await _imagePreprocessor.Received(1).PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received(1).ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.Received(1).ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentsAsync"/> returns successful results for multiple documents.
    /// </summary>
    /// <returns>A task that completes after asserting multiple documents process successfully.</returns>
    [Fact]
    public async Task ProcessDocuments_MultipleDocuments_ReturnsSuccessfulResults()
    {
        // Arrange
        var imageDataList = new List<ImageData>
        {
            CreateValidImageData("file1.png"),
            CreateValidImageData("file2.png"),
            CreateValidImageData("file3.png")
        };
        var config = CreateValidProcessingConfig();
        var preprocessedImage = CreatePreprocessedImageData();
        var ocrResult = CreateExpectedOcrResult();
        var extractedFields = CreateExpectedExtractedFields();

        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));
        _fieldExtractor.ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _service.ProcessDocumentsAsync(imageDataList, config, maxConcurrency: 2, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(3);

        await _imagePreprocessor.Received(3).PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await _ocrExecutor.Received(3).ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _fieldExtractor.Received(3).ExtractFieldsAsync(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Tests that <see cref="OcrProcessingService.ProcessDocumentAsync"/> returns appropriate errors for invalid inputs.
    /// </summary>
    /// <param name="sourcePath">The source path to test.</param>
    /// <param name="expectedError">The expected error message.</param>
    /// <returns>A task that completes after asserting invalid input errors are returned.</returns>
    [Theory]
    [InlineData(null, "Image source path is required")]
    [InlineData("", "Image source path is required")]
    [InlineData("test.png", "Image data is empty")]
    public async Task ProcessDocument_InvalidInputs_ReturnsAppropriateErrors(string? sourcePath, string expectedError)
    {
        // Arrange
        var imageData = new ImageData
        {
            Data = sourcePath == "test.png" ? Array.Empty<byte>() : new byte[] { 1, 2, 3, 4 },
            SourcePath = sourcePath ?? "",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = CreateValidProcessingConfig();

        // Act
        var result = await _service.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain(expectedError);
    }

    /// <summary>
    /// Creates a valid <see cref="ImageData"/> instance for testing.
    /// </summary>
    /// <param name="sourcePath">The source path of the image.</param>
    /// <returns>A valid <see cref="ImageData"/> object.</returns>
    private static ImageData CreateValidImageData(string sourcePath = "test-document.png")
    {
        return new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            SourcePath = sourcePath,
            PageNumber = 1,
            TotalPages = 1
        };
    }

    /// <summary>
    /// Creates an invalid <see cref="ImageData"/> instance for testing.
    /// </summary>
    /// <returns>An invalid <see cref="ImageData"/> object.</returns>
    private static ImageData CreateInvalidImageData()
    {
        return new ImageData
        {
            Data = Array.Empty<byte>(),
            SourcePath = "test-document.png",
            PageNumber = 1,
            TotalPages = 1
        };
    }

    /// <summary>
    /// Creates a preprocessed <see cref="ImageData"/> instance for testing.
    /// </summary>
    /// <returns>A preprocessed <see cref="ImageData"/> object.</returns>
    private static ImageData CreatePreprocessedImageData()
    {
        return new ImageData
        {
            Data = new byte[] { 9, 10, 11, 12 },
            SourcePath = "test-document.png",
            PageNumber = 1,
            TotalPages = 1
        };
    }

    /// <summary>
    /// Creates a valid <see cref="ProcessingConfig"/> instance for testing.
    /// </summary>
    /// <returns>A valid <see cref="ProcessingConfig"/> object.</returns>
    private static ProcessingConfig CreateValidProcessingConfig()
    {
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 1,
                PSM = 6,
                FallbackLanguage = "eng"
            },
            ExtractSections = true,
            NormalizeText = true
        };
    }

    /// <summary>
    /// Creates an expected <see cref="OCRResult"/> instance for testing.
    /// </summary>
    /// <returns>An expected <see cref="OCRResult"/> object.</returns>
    private static OCRResult CreateExpectedOcrResult()
    {
        return new OCRResult
        {
            Text = "Sample OCR text",
            ConfidenceAvg = 95.5f,
            ConfidenceMedian = 97.0f,
            Confidences = new List<float> { 95.0f, 97.0f, 94.5f },
            LanguageUsed = "spa"
        };
    }

    /// <summary>
    /// Creates an expected <see cref="ExtractedFields"/> instance for testing.
    /// </summary>
    /// <returns>An expected <see cref="ExtractedFields"/> object.</returns>
    private static ExtractedFields CreateExpectedExtractedFields()
    {
        return new ExtractedFields
        {
            Expediente = "EXP-2024-001",
            Causa = "Test cause",
            AccionSolicitada = "Test action",
            Fechas = new List<string> { "2024-01-15", "2024-02-20" },
            Montos = new List<AmountData>
            {
                new AmountData("MXN", 1500.00m, "MXN 1,500.00"),
                new AmountData("MXN", 2500.00m, "MXN 2,500.00")
            }
        };
    }
}
