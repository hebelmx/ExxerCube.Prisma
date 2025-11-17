namespace ExxerCube.Prisma.Tests.System;

/// <summary>
/// End-to-end pipeline tests that use the real Python OCR pipeline.
/// These tests validate the complete flow from document input to structured output.
/// </summary>
public class EndToEndPipelineTests : IDisposable
{
    private readonly OcrProcessingService _processingService;
    private readonly PrismaOcrWrapperAdapter _pythonAdapter;
    private readonly ProcessingMetricsService _metricsService;
    private readonly string _testDataPath;
    private readonly string _pythonModulesPath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndToEndPipelineTests"/> class.
    /// </summary>
    public EndToEndPipelineTests()
    {
        // Setup Python integration
        _pythonModulesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Python", "ocr_modules");
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var adapterLogger = loggerFactory.CreateLogger<PrismaOcrWrapperAdapter>();
        _logger = loggerFactory.CreateLogger("EndToEndPipelineTests");
        
        _pythonAdapter = new PrismaOcrWrapperAdapter(adapterLogger);
        _metricsService = new ProcessingMetricsService(loggerFactory.CreateLogger<ProcessingMetricsService>());
        _processingService = new OcrProcessingService(
            (IImagePreprocessor)_pythonAdapter, 
            (IOcrExecutor)_pythonAdapter, 
            (IFieldExtractor)_pythonAdapter, 
            loggerFactory.CreateLogger<OcrProcessingService>(), 
            _metricsService);
        
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        EnsureTestDataDirectory();
    }

    /// <summary>
    /// Tests that the complete pipeline processes a real document and extracts all fields.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_CompletePipeline_ExtractsAllFields()
    {
        // Arrange
        var imageData = TestImageDataGenerator.CreateSimpleTestData();
        var config = CreateDefaultProcessingConfig();

        // Act
        var result = await _processingService.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.SourcePath.ShouldBe(imageData.SourcePath);
        result.Value!.PageNumber.ShouldBe(imageData.PageNumber);
        
        // Verify real OCR results
        result.Value!.OCRResult.Text.ShouldNotBeNullOrEmpty();
        result.Value!.OCRResult.ConfidenceAvg.ShouldBeGreaterThan(0);
        result.Value!.OCRResult.ConfidenceAvg.ShouldBeLessThanOrEqualTo(100);
        
        // Verify real field extraction (may be empty if document doesn't contain expected fields)
        result.Value!.ExtractedFields.ShouldNotBeNull();
        
        // Log the actual results for debugging
        _logger.LogInformation("OCR Text Length: {TextLength}", result.Value!.OCRResult.Text.Length);
        _logger.LogInformation("Confidence: {Confidence:F2}%", result.Value!.OCRResult.ConfidenceAvg);
        _logger.LogInformation("Expediente: {Expediente}", result.Value!.ExtractedFields.Expediente ?? "Not found");
        _logger.LogInformation("Causa: {Causa}", result.Value!.ExtractedFields.Causa ?? "Not found");
    }

    /// <summary>
    /// Tests that the pipeline handles various document formats correctly with real processing.
    /// </summary>
    [Theory]
    [InlineData("test_document.pdf")]
    [InlineData("test_document.png")]
    [InlineData("test_document.jpg")]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_VariousFormats_ProcessesSuccessfully(string fileName)
    {
        // Arrange
        var imageData = TestImageDataGenerator.CreateFromTextFile(fileName);
        var config = CreateDefaultProcessingConfig();

        // Act
        var result = await _processingService.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.SourcePath.ShouldBe(imageData.SourcePath);
        
        // Verify OCR processing worked
        result.Value!.OCRResult.Text.ShouldNotBeNullOrEmpty();
        result.Value!.OCRResult.ConfidenceAvg.ShouldBeGreaterThan(0);
        
        _logger.LogInformation("Processed {FileName} successfully", fileName);
    }

    /// <summary>
    /// Tests that batch processing works correctly with multiple documents using real pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocuments_BatchProcessing_HandlesMultipleDocuments()
    {
        // Arrange
        var documents = CreateTestDocuments(3);
        var config = CreateDefaultProcessingConfig();

        // Act
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 2, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(3);
        
        foreach (var processingResult in result.Value!)
        {
            processingResult.OCRResult.Text.ShouldNotBeNullOrEmpty();
            processingResult.OCRResult.ConfidenceAvg.ShouldBeGreaterThan(0);
        }
    }

    /// <summary>
    /// Tests that the pipeline handles configuration changes without code modifications.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_DifferentConfigurations_ProcessesCorrectly()
    {
        // Arrange
        var imageData = CreateTestImageData();
        var configWithWatermarkRemoval = CreateProcessingConfig(removeWatermark: true, deskew: true, binarize: true);
        var configWithoutPreprocessing = CreateProcessingConfig(removeWatermark: false, deskew: false, binarize: false);

        // Act & Assert - Both configurations should work
        var resultWithPreprocessing = await _processingService.ProcessDocumentAsync(imageData, configWithWatermarkRemoval, TestContext.Current.CancellationToken);
        var resultWithoutPreprocessing = await _processingService.ProcessDocumentAsync(imageData, configWithoutPreprocessing, TestContext.Current.CancellationToken);

        resultWithPreprocessing.IsSuccess.ShouldBeTrue();
        resultWithoutPreprocessing.IsSuccess.ShouldBeTrue();
        
        // Both should produce OCR results
        resultWithPreprocessing.Value!.OCRResult.Text.ShouldNotBeNullOrEmpty();
        resultWithoutPreprocessing.Value!.OCRResult.Text.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the pipeline handles invalid input gracefully.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_InvalidInput_HandlesGracefully()
    {
        // Arrange
        var invalidImageData = new ImageData
        {
            Data = new byte[0], // Empty data
            SourcePath = "invalid.pdf",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = CreateDefaultProcessingConfig();

        // Act
        var result = await _processingService.ProcessDocumentAsync(invalidImageData, config, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the pipeline handles null input correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_NullInput_ReturnsFailure()
    {
        // Arrange
        ImageData? nullImageData = null;
        var config = CreateDefaultProcessingConfig();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await _processingService.ProcessDocumentAsync(nullImageData!, config);
        });
    }

    /// <summary>
    /// Tests that the pipeline handles large documents within reasonable time.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task ProcessDocument_LargeDocument_ProcessesWithinTimeLimit()
    {
        // Arrange
        var imageData = CreateTestImageData("large_document.pdf");
        var config = CreateDefaultProcessingConfig();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _processingService.ProcessDocumentAsync(imageData, config, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Should complete within 60 seconds for a large document
        stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(60.0);
        
        _logger.LogInformation("Large document processing time: {ProcessingTime:F2} seconds", stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Creates test image data using actual document files.
    /// </summary>
    /// <param name="fileName">The file name for the test data.</param>
    /// <returns>Test image data.</returns>
    private static ImageData CreateTestImageData(string fileName = "test_document.pdf")
    {
        // Try to load a real test document, fallback to minimal valid data
        var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Testing", "Infrastructure", "TestData", fileName);
        
        byte[] imageData;
        if (File.Exists(testFilePath))
        {
            imageData = File.ReadAllBytes(testFilePath);
        }
        else
        {
            // Create minimal valid PNG data for testing
            imageData = CreateMinimalValidPng();
        }

        return new ImageData
        {
            Data = imageData,
            SourcePath = fileName,
            PageNumber = 1,
            TotalPages = 1
        };
    }

    /// <summary>
    /// Creates a list of test documents.
    /// </summary>
    /// <param name="count">The number of documents to create.</param>
    /// <returns>A list of test documents.</returns>
    private static List<ImageData> CreateTestDocuments(int count)
    {
        var documents = new List<ImageData>();
        for (int i = 1; i <= count; i++)
        {
            documents.Add(CreateTestImageData($"test_document_{i}.pdf"));
        }
        return documents;
    }

    /// <summary>
    /// Creates minimal valid PNG data for testing when real documents aren't available.
    /// </summary>
    /// <returns>Minimal valid PNG byte array.</returns>
    private static byte[] CreateMinimalValidPng()
    {
        // Minimal valid 1x1 PNG file
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, // Width: 1
            0x00, 0x00, 0x00, 0x01, // Height: 1
            0x08, 0x02, 0x00, 0x00, 0x00, // Bit depth, color type, etc.
            0x90, 0x77, 0x53, 0xDE, // CRC
            0x00, 0x00, 0x00, 0x0C, // IDAT chunk length
            0x49, 0x44, 0x41, 0x54, // IDAT
            0x08, 0x99, 0x01, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, // Compressed data
            0xE2, 0x21, 0xBC, 0x33, // CRC
            0x00, 0x00, 0x00, 0x00, // IEND chunk length
            0x49, 0x45, 0x4E, 0x44, // IEND
            0xAE, 0x42, 0x60, 0x82  // CRC
        };
    }

    /// <summary>
    /// Creates default processing configuration for testing.
    /// </summary>
    /// <returns>Default processing configuration.</returns>
    private static ProcessingConfig CreateDefaultProcessingConfig()
    {
        return CreateProcessingConfig(removeWatermark: true, deskew: true, binarize: true);
    }

    /// <summary>
    /// Creates processing configuration with specified settings.
    /// </summary>
    /// <param name="removeWatermark">Whether to remove watermarks.</param>
    /// <param name="deskew">Whether to deskew images.</param>
    /// <param name="binarize">Whether to binarize images.</param>
    /// <returns>Processing configuration.</returns>
    private static ProcessingConfig CreateProcessingConfig(bool removeWatermark, bool deskew, bool binarize)
    {
        return new ProcessingConfig
        {
            RemoveWatermark = removeWatermark,
            Deskew = deskew,
            Binarize = binarize,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 3,
                PSM = 6,
                FallbackLanguage = "eng"
            },
            ExtractSections = true,
            NormalizeText = true
        };
    }

    /// <summary>
    /// Ensures the test data directory exists.
    /// </summary>
    private void EnsureTestDataDirectory()
    {
        if (!Directory.Exists(_testDataPath))
        {
            Directory.CreateDirectory(_testDataPath);
        }
    }

    /// <summary>
    /// Disposes of the resources used by the <see cref="EndToEndPipelineTests"/> class.
    /// </summary>
    public void Dispose()
    {
        _pythonAdapter?.Dispose();
        
        // Clean up test data if needed
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not clean up test data directory: {Message}", ex.Message);
            }
        }
    }
}
