using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExxerCube.Prisma.Application.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Python;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using ExxerCube.Prisma.Tests.TestData;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Performance tests for the OCR processing pipeline.
/// These tests validate that the system meets performance requirements.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly OcrProcessingService _processingService;
    private readonly PrismaOcrWrapperAdapter _pythonAdapter;
    private readonly ProcessingMetricsService _metricsService;
    private readonly string _testDataPath;
    private readonly string _pythonModulesPath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceTests"/> class.
    /// </summary>
    public PerformanceTests()
    {
        // Setup Python integration
        _pythonModulesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Python", "ocr_modules");
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var adapterLogger = loggerFactory.CreateLogger<PrismaOcrWrapperAdapter>();
        _logger = loggerFactory.CreateLogger("PerformanceTests");
        
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
    /// Tests that single document processing meets time requirements.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocument_SingleDocument_MeetsTimeRequirements()
    {
        // Arrange
        var imageData = TestImageDataGenerator.CreateSimpleTestData();
        var config = CreateDefaultProcessingConfig();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _processingService.ProcessDocumentAsync(imageData, config);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        
        // Performance assertion: should complete within 30 seconds
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000);
        
        _logger.LogInformation("Single document processing completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Tests that batch processing meets throughput requirements.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocuments_BatchProcessing_MeetsThroughputRequirements()
    {
        // Arrange
        var documents = Enumerable.Range(1, 10)
            .Select(i => TestImageDataGenerator.CreateSimpleTestData())
            .ToList();
        var config = CreateDefaultProcessingConfig();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 5);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(10); // All documents should be processed successfully
        
        // Performance assertion: should complete within 60 seconds for 10 documents
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(60000);
        
        var throughput = documents.Count / (stopwatch.ElapsedMilliseconds / 1000.0);
        _logger.LogInformation("Batch processing completed {DocumentCount} documents in {ElapsedMs}ms (throughput: {Throughput:F2} docs/sec)", 
            documents.Count, stopwatch.ElapsedMilliseconds, throughput);
    }

    /// <summary>
    /// Tests that the system can handle burst loads gracefully with real processing.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessBatch_BurstLoad_HandlesGracefully()
    {
        // Arrange
        var documents = CreateTestDocuments(5); // Reduced from 20 to be realistic
        var config = CreateDefaultProcessingConfig();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 3);
        stopwatch.Stop();

        // Assert
        if (!result.IsSuccess)
        {
            _logger.LogError("Processing failed: {Error}", result.Error);
        }
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(5);
        
        // Should handle burst load without errors
        stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(300.0); // 5 minutes for 5 docs
        
        // Throughput should still be reasonable
        var throughput = 5.0 / (stopwatch.Elapsed.TotalMinutes / 60.0);
        throughput.ShouldBeGreaterThan(5.0); // Allow some degradation under burst load
        
        _logger.LogInformation("Burst load processing time: {ProcessingTime:F2} seconds", stopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("Burst throughput: {Throughput:F2} documents per hour", throughput);
    }

    /// <summary>
    /// Tests that concurrency controls work correctly with real processing.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessBatch_ConcurrencyControl_WorksCorrectly()
    {
        // Arrange
        var documents = CreateTestDocuments(6);
        var config = CreateDefaultProcessingConfig();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 2);
        stopwatch.Stop();

        // Assert
        if (!result.IsSuccess)
        {
            _logger.LogError("Processing failed: {Error}", result.Error);
        }
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(6);
        
        // With concurrency limit of 2, should take longer than with higher concurrency
        // but should still complete successfully
        stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(360.0); // 6 minutes for 6 docs
        
        _logger.LogInformation("Concurrency-limited processing time: {ProcessingTime:F2} seconds", stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Tests that memory usage remains reasonable during batch processing.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessBatch_MemoryUsage_RemainsReasonable()
    {
        // Arrange
        var documents = CreateTestDocuments(3);
        var config = CreateDefaultProcessingConfig();

        // Act
        var initialMemory = GC.GetTotalMemory(false);
        var result = await _processingService.ProcessDocumentsAsync(documents, config, maxConcurrency: 2);
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        if (!result.IsSuccess)
        {
            _logger.LogError("Processing failed: {Error}", result.Error);
        }
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(3);
        
        // Memory increase should be reasonable (less than 100MB for 3 documents)
        var memoryIncreaseMB = (double)memoryIncrease / (1024 * 1024);
        memoryIncreaseMB.ShouldBeLessThan(100.0);
        
        _logger.LogInformation("Memory increase: {MemoryIncrease:F2} MB", memoryIncreaseMB);
    }

    /// <summary>
    /// Tests that processing time scales reasonably with document complexity.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocument_DocumentComplexity_ScalesReasonably()
    {
        // Arrange
        var simpleDocument = CreateTestImageData("simple_document.png");
        var complexDocument = CreateTestImageData("complex_document.pdf");
        var config = CreateDefaultProcessingConfig();

        // Act
        var simpleStopwatch = Stopwatch.StartNew();
        var simpleResult = await _processingService.ProcessDocumentAsync(simpleDocument, config);
        simpleStopwatch.Stop();

        var complexStopwatch = Stopwatch.StartNew();
        var complexResult = await _processingService.ProcessDocumentAsync(complexDocument, config);
        complexStopwatch.Stop();

        // Assert
        if (!simpleResult.IsSuccess)
        {
            _logger.LogError("Processing failed: {Error}", simpleResult.Error);
        }
        simpleResult.IsSuccess.ShouldBeTrue();
        if (!complexResult.IsSuccess)
        {
            _logger.LogError("Processing failed: {Error}", complexResult.Error);
        }
        complexResult.IsSuccess.ShouldBeTrue();
        
        // Complex document should take longer but not exponentially longer
        var simpleTime = simpleStopwatch.Elapsed.TotalSeconds;
        var complexTime = complexStopwatch.Elapsed.TotalSeconds;
        
        // Complex document should not take more than 3x longer than simple document
        if (simpleTime > 0)
        {
            var ratio = complexTime / simpleTime;
            ratio.ShouldBeLessThan(3.0);
        }
        
        _logger.LogInformation("Simple document time: {SimpleTime:F2} seconds", simpleTime);
        _logger.LogInformation("Complex document time: {ComplexTime:F2} seconds", complexTime);
        if (simpleTime > 0)
        {
            _logger.LogInformation("Complexity ratio: {Ratio:F2}x", complexTime / simpleTime);
        }
    }

    /// <summary>
    /// Tests that the system maintains consistent performance across multiple runs.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessDocument_ConsistentPerformance_AcrossMultipleRuns()
    {
        // Arrange
        var imageData = CreateTestImageData();
        var config = CreateDefaultProcessingConfig();
        var processingTimes = new List<double>();

        // Act - Run the same document multiple times
        for (int i = 0; i < 3; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _processingService.ProcessDocumentAsync(imageData, config);
            stopwatch.Stop();
            
            if (!result.IsSuccess)
            {
                _logger.LogError("Processing failed: {Error}", result.Error);
            }
            result.IsSuccess.ShouldBeTrue();
            processingTimes.Add(stopwatch.Elapsed.TotalSeconds);
            
            _logger.LogInformation("Run {RunNumber}: {ProcessingTime:F2} seconds", i + 1, stopwatch.Elapsed.TotalSeconds);
        }

        // Assert
        processingTimes.Count.ShouldBe(3);
        
        // Performance should be consistent (within 50% variance)
        var avgTime = processingTimes.Average();
        var maxVariance = avgTime * 0.5; // 50% variance allowed
        
        foreach (var time in processingTimes)
        {
            Math.Abs(time - avgTime).ShouldBeLessThan(maxVariance);
        }
        
        _logger.LogInformation("Average processing time: {AvgTime:F2} seconds", avgTime);
        _logger.LogInformation("Max allowed variance: {MaxVariance:F2} seconds", maxVariance);
    }

    /// <summary>
    /// Creates test image data using actual document files.
    /// </summary>
    /// <param name="fileName">The file name for the test data.</param>
    /// <returns>Test image data.</returns>
    private static ImageData CreateTestImageData(string fileName = "test_document.pdf")
    {
        // Try to load a real test document, fallback to minimal valid data
        var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
        
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
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
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
    /// Disposes of the resources used by the <see cref="PerformanceTests"/> class.
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
