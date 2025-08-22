using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Python;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Integration tests for the end-to-end OCR processing pipeline.
/// Tests the complete flow from document input to structured output.
/// </summary>
[Collection("Integration Tests")]
public class EndToEndPipelineTests : IDisposable
{
    private readonly IOcrProcessingService _processingService;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly ILogger<OcrProcessingService> _logger;
    private readonly ProcessingMetricsService _metricsService;
    private readonly string _testDataPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndToEndPipelineTests"/> class.
    /// </summary>
    public EndToEndPipelineTests()
    {
        _imagePreprocessor = Substitute.For<IImagePreprocessor>();
        _ocrExecutor = Substitute.For<IOcrExecutor>();
        _fieldExtractor = Substitute.For<IFieldExtractor>();
        _logger = Substitute.For<ILogger<OcrProcessingService>>();
        _metricsService = new ProcessingMetricsService(Substitute.For<ILogger<ProcessingMetricsService>>(), maxConcurrency: 5);
        
        _processingService = new OcrProcessingService(
            _imagePreprocessor,
            _ocrExecutor,
            _fieldExtractor,
            _logger,
            _metricsService);

        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        EnsureTestDataDirectory();
    }

    /// <summary>
    /// Tests that the complete pipeline processes documents and extracts all required fields.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_CompletePipeline_ExtractsAllFields()
    {
        // Arrange
        var imageData = CreateTestImageData();
        var config = CreateDefaultProcessingConfig();
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        SetupMockServices(imageData, config, expectedOcrResult, expectedExtractedFields);

        // Act
        var result = await _processingService.ProcessDocumentAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.SourcePath.ShouldBe(imageData.SourcePath);
        result.Value!.PageNumber.ShouldBe(imageData.PageNumber);
        result.Value!.OCRResult.Text.ShouldBe(expectedOcrResult.Text);
        result.Value!.OCRResult.ConfidenceAvg.ShouldBe(expectedOcrResult.ConfidenceAvg, 0.01f);
        result.Value!.ExtractedFields.Expediente.ShouldBe(expectedExtractedFields.Expediente);
        result.Value!.ExtractedFields.Fechas.ShouldHaveSingleItem();
        result.Value!.ExtractedFields.Montos.ShouldHaveSingleItem();
    }

    /// <summary>
    /// Tests that the pipeline handles various document formats correctly.
    /// </summary>
    [Theory]
    [InlineData("test_document.pdf")]
    [InlineData("test_document.png")]
    [InlineData("test_document.jpg")]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_VariousFormats_ProcessesSuccessfully(string fileName)
    {
        // Arrange
        var imageData = CreateTestImageData(fileName);
        var config = CreateDefaultProcessingConfig();
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        SetupMockServices(imageData, config, expectedOcrResult, expectedExtractedFields);

        // Act
        var result = await _processingService.ProcessDocumentAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.SourcePath.ShouldBe(imageData.SourcePath);
    }

    /// <summary>
    /// Tests that batch processing works correctly with multiple documents.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocuments_BatchProcessing_HandlesMultipleDocuments()
    {
        // Arrange
        var documents = CreateTestDocuments(3);
        var config = CreateDefaultProcessingConfig();
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        foreach (var document in documents)
        {
            SetupMockServices(document, config, expectedOcrResult, expectedExtractedFields);
        }

        // Act
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 2);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(3);
        
        foreach (var processingResult in result.Value!)
        {
            processingResult.OCRResult.Text.ShouldBe(expectedOcrResult.Text);
            processingResult.ExtractedFields.Expediente.ShouldBe(expectedExtractedFields.Expediente);
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
        
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        // Test with preprocessing enabled
        SetupMockServices(imageData, configWithWatermarkRemoval, expectedOcrResult, expectedExtractedFields);
        var resultWithPreprocessing = await _processingService.ProcessDocumentAsync(imageData, configWithWatermarkRemoval);
        resultWithPreprocessing.IsSuccess.ShouldBeTrue();

        // Test without preprocessing
        SetupMockServices(imageData, configWithoutPreprocessing, expectedOcrResult, expectedExtractedFields);
        var resultWithoutPreprocessing = await _processingService.ProcessDocumentAsync(imageData, configWithoutPreprocessing);
        resultWithoutPreprocessing.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that the pipeline handles errors gracefully and returns appropriate error messages.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_ErrorScenarios_HandlesGracefully()
    {
        // Arrange
        var imageData = CreateTestImageData();
        var config = CreateDefaultProcessingConfig();

        // Test null image data
        var nullResult = await _processingService.ProcessDocumentAsync(null!, config);
        nullResult.IsSuccess.ShouldBeFalse();
        nullResult.Error!.ShouldContain("cannot be null");

        // Test invalid image data
        var invalidImageData = new ImageData
        {
            SourcePath = "",
            Data = Array.Empty<byte>(),
            PageNumber = 0,
            TotalPages = 0
        };
        var invalidResult = await _processingService.ProcessDocumentAsync(invalidImageData, config);
        invalidResult.IsSuccess.ShouldBeFalse();
        invalidResult.Error!.ShouldContain("required");
    }

    /// <summary>
    /// Tests that the pipeline meets performance requirements.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocument_Performance_MeetsRequirements()
    {
        // Arrange
        var imageData = CreateTestImageData();
        var config = CreateDefaultProcessingConfig();
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        SetupMockServices(imageData, config, expectedOcrResult, expectedExtractedFields);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _processingService.ProcessDocumentAsync(imageData, config);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(30.0); // <30 seconds requirement
    }

    /// <summary>
    /// Tests that batch processing meets throughput requirements.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocuments_Throughput_MeetsRequirements()
    {
        // Arrange
        var documents = CreateTestDocuments(10);
        var config = CreateDefaultProcessingConfig();
        var expectedOcrResult = CreateExpectedOcrResult();
        var expectedExtractedFields = CreateExpectedExtractedFields();

        foreach (var document in documents)
        {
            SetupMockServices(document, config, expectedOcrResult, expectedExtractedFields);
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 5);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(10);
        
        // Calculate throughput: 10 documents should be processed in less than 6 minutes (100 docs/hour baseline)
        var throughput = 10.0 / (stopwatch.Elapsed.TotalMinutes / 60.0);
        throughput.ShouldBeGreaterThan(100.0); // 100+ documents per hour
    }

    /// <summary>
    /// Sets up mock services for testing.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="expectedOcrResult">The expected OCR result.</param>
    /// <param name="expectedExtractedFields">The expected extracted fields.</param>
    private void SetupMockServices(
        ImageData imageData, 
        ProcessingConfig config, 
        OCRResult expectedOcrResult, 
        ExtractedFields expectedExtractedFields)
    {
        _imagePreprocessor.PreprocessAsync(Arg.Is<ImageData>(id => id.SourcePath == imageData.SourcePath), config)
            .Returns(Result<ImageData>.Success(imageData));

        _ocrExecutor.ExecuteOcrAsync(Arg.Is<ImageData>(id => id.SourcePath == imageData.SourcePath), config.OCRConfig)
            .Returns(Result<OCRResult>.Success(expectedOcrResult));

        _fieldExtractor.ExtractFieldsAsync(expectedOcrResult.Text, expectedOcrResult.ConfidenceAvg)
            .Returns(Result<ExtractedFields>.Success(expectedExtractedFields));
    }

    /// <summary>
    /// Creates test image data for testing.
    /// </summary>
    /// <param name="fileName">The file name for the test data.</param>
    /// <returns>Test image data.</returns>
    private static ImageData CreateTestImageData(string fileName = "test_document.pdf")
    {
        return new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
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
            documents.Add(new ImageData
            {
                Data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                SourcePath = $"test_document_{i}.pdf",
                PageNumber = i,
                TotalPages = count
            });
        }
        return documents;
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
    /// Creates expected OCR result for testing.
    /// </summary>
    /// <returns>Expected OCR result.</returns>
    private static OCRResult CreateExpectedOcrResult()
    {
        return new OCRResult
        {
            Text = "Sample legal document text with expediente 123/2024 and amount $50,000.00",
            ConfidenceAvg = 0.95f,
            ConfidenceMedian = 0.97f,
            Confidences = new List<float> { 0.95f, 0.97f, 0.93f },
            LanguageUsed = "spa"
        };
    }

    /// <summary>
    /// Creates expected extracted fields for testing.
    /// </summary>
    /// <returns>Expected extracted fields.</returns>
    private static ExtractedFields CreateExpectedExtractedFields()
    {
        return new ExtractedFields
        {
            Expediente = "123/2024",
            Causa = "Sample cause",
            AccionSolicitada = "Sample action",
            Fechas = new List<string> { "2024-01-15" },
            Montos = new List<AmountData> 
            { 
                new AmountData { Value = 50000.00m, Currency = "MXN", OriginalText = "Principal" } 
            }
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
    /// Disposes test resources.
    /// </summary>
    public void Dispose()
    {
        // Clean up test data if needed
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
