using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Python;

namespace ExxerCube.Prisma.Tests.Infrastructure;

/// <summary>
/// Tests for the Python interop service architecture refactoring.
/// </summary>
public class PythonInteropServiceTests
{
    private readonly ILogger<CSnakesOcrProcessingAdapter> _logger;
    private readonly string _pythonModulesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInteropServiceTests"/> class.
    /// </summary>
    public PythonInteropServiceTests()
    {
        _logger = Substitute.For<ILogger<CSnakesOcrProcessingAdapter>>();
        _pythonModulesPath = "./TestPythonModules";
    }

    /// <summary>
    /// Tests that the CSnakes adapter can be created successfully.
    /// </summary>
    [Fact]
    public void CSnakesOcrProcessingAdapter_ShouldCreateSuccessfully()
    {
        // Act
        var adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);

        // Assert
        adapter.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the CSnakes adapter implements the correct interface.
    /// </summary>
    [Fact]
    public void CSnakesOcrProcessingAdapter_ShouldImplementIPythonInteropService()
    {
        // Act
        var adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);

        // Assert
        adapter.ShouldBeAssignableTo<IPythonInteropService>();
    }

    /// <summary>
    /// Tests that the CSnakes adapter can execute OCR successfully.
    /// </summary>
    [Fact]
    public async Task CSnakesOcrProcessingAdapter_ExecuteOcrAsync_ShouldReturnSuccess()
    {
        // Arrange
        var adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);
        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.jpg",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = new OCRConfig
        {
            Language = "es",
            ConfidenceThreshold = 0.8f
        };

        // Act
        var result = await adapter.ExecuteOcrAsync(imageData, config);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Text.ShouldNotBeNullOrEmpty();
        result.Value.ConfidenceAvg.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Tests that the CSnakes adapter can preprocess images successfully.
    /// </summary>
    [Fact]
    public async Task CSnakesOcrProcessingAdapter_PreprocessAsync_ShouldReturnSuccess()
    {
        // Arrange
        var adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);
        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.jpg",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = new ProcessingConfig
        {
            Deskew = true,
            RemoveWatermark = false,
            MaxConcurrency = 5
        };

        // Act
        var result = await adapter.PreprocessAsync(imageData, config);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.SourcePath.ShouldBe(imageData.SourcePath);
    }

    /// <summary>
    /// Tests that the CSnakes adapter can extract fields successfully.
    /// </summary>
    [Fact]
    public async Task CSnakesOcrProcessingAdapter_ExtractFieldsAsync_ShouldReturnSuccess()
    {
        // Arrange
        var adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);
        var text = "Sample OCR text for testing";
        var confidence = 0.95f;

        // Act
        var result = await adapter.ExtractFieldsAsync(text, confidence);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNullOrEmpty();
        result.Value.Montos.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the circuit breaker service can be created successfully.
    /// </summary>
    [Fact]
    public void CircuitBreakerPythonInteropService_ShouldCreateSuccessfully()
    {
        // Arrange
        var innerService = Substitute.For<IPythonInteropService>();
        var logger = Substitute.For<ILogger<CircuitBreakerPythonInteropService>>();

        // Act
        var circuitBreaker = new CircuitBreakerPythonInteropService(logger, innerService);

        // Assert
        circuitBreaker.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the circuit breaker allows operations when closed.
    /// </summary>
    [Fact]
    public async Task CircuitBreakerPythonInteropService_WhenClosed_ShouldAllowOperations()
    {
        // Arrange
        var innerService = Substitute.For<IPythonInteropService>();
        var logger = Substitute.For<ILogger<CircuitBreakerPythonInteropService>>();
        var circuitBreaker = new CircuitBreakerPythonInteropService(logger, innerService);

        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.jpg",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = new OCRConfig
        {
            Language = "es",
            ConfidenceThreshold = 0.8f
        };

        var expectedResult = new OCRResult
        {
            Text = "Test OCR result",
            ConfidenceAvg = 95.0f,
            ConfidenceMedian = 95.0f,
            Confidences = new List<float> { 95.0f },
            LanguageUsed = "es"
        };

        innerService.ExecuteOcrAsync(imageData, config).Returns(Result<OCRResult>.Success(expectedResult));

        // Act
        var result = await circuitBreaker.ExecuteOcrAsync(imageData, config);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedResult);
    }

    /// <summary>
    /// Tests that the OcrProcessingAdapter can be created successfully.
    /// </summary>
    [Fact]
    public void OcrProcessingAdapter_ShouldCreateSuccessfully()
    {
        // Arrange
        var pythonInteropService = Substitute.For<IPythonInteropService>();
        var logger = Substitute.For<ILogger<OcrProcessingAdapter>>();

        // Act
        var adapter = new OcrProcessingAdapter(logger, pythonInteropService);

        // Assert
        adapter.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the OcrProcessingAdapter implements the correct interfaces.
    /// </summary>
    [Fact]
    public void OcrProcessingAdapter_ShouldImplementDomainInterfaces()
    {
        // Arrange
        var pythonInteropService = Substitute.For<IPythonInteropService>();
        var logger = Substitute.For<ILogger<OcrProcessingAdapter>>();

        // Act
        var adapter = new OcrProcessingAdapter(logger, pythonInteropService);

        // Assert
        adapter.ShouldBeAssignableTo<IOcrExecutor>();
        adapter.ShouldBeAssignableTo<IImagePreprocessor>();
        adapter.ShouldBeAssignableTo<IFieldExtractor>();
    }

    /// <summary>
    /// Tests that the OcrProcessingAdapter delegates to the Python interop service.
    /// </summary>
    [Fact]
    public async Task OcrProcessingAdapter_ShouldDelegateToPythonInteropService()
    {
        // Arrange
        var pythonInteropService = Substitute.For<IPythonInteropService>();
        var logger = Substitute.For<ILogger<OcrProcessingAdapter>>();
        var adapter = new OcrProcessingAdapter(logger, pythonInteropService);

        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.jpg",
            PageNumber = 1,
            TotalPages = 1
        };
        var config = new OCRConfig
        {
            Language = "es",
            ConfidenceThreshold = 0.8f
        };

        var expectedResult = new OCRResult
        {
            Text = "Test OCR result",
            ConfidenceAvg = 95.0f,
            ConfidenceMedian = 95.0f,
            Confidences = new List<float> { 95.0f },
            LanguageUsed = "es"
        };

        pythonInteropService.ExecuteOcrAsync(imageData, config).Returns(Result<OCRResult>.Success(expectedResult));

        // Act
        var result = await adapter.ExecuteOcrAsync(imageData, config);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedResult);
        await pythonInteropService.Received(1).ExecuteOcrAsync(imageData, config);
    }

    /// <summary>
    /// Tests that the PythonConfiguration can be created with default values.
    /// </summary>
    [Fact]
    public void PythonConfiguration_CreateDefault_ShouldReturnValidConfiguration()
    {
        // Act
        var config = PythonConfiguration.CreateDefault();

        // Assert
        config.ShouldNotBeNull();
        config.IsValid().ShouldBeTrue();
        config.ModulesPath.ShouldBe("./Python");
        config.PythonExecutablePath.ShouldBe("python");
        config.MaxConcurrency.ShouldBe(5);
        config.OperationTimeoutSeconds.ShouldBe(30);
    }

    /// <summary>
    /// Tests that the PythonConfiguration validation works correctly.
    /// </summary>
    [Theory]
    [InlineData("", "python", 5, 30, false)] // Empty modules path
    [InlineData("./Python", "", 5, 30, false)] // Empty Python executable
    [InlineData("./Python", "python", 0, 30, false)] // Invalid concurrency
    [InlineData("./Python", "python", 5, 0, false)] // Invalid timeout
    [InlineData("./Python", "python", 5, 30, true)] // Valid configuration
    public void PythonConfiguration_IsValid_ShouldReturnCorrectResult(string modulesPath, string pythonExecutable, int maxConcurrency, int timeout, bool expectedIsValid)
    {
        // Arrange
        var config = new PythonConfiguration
        {
            ModulesPath = modulesPath,
            PythonExecutablePath = pythonExecutable,
            MaxConcurrency = maxConcurrency,
            OperationTimeoutSeconds = timeout
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.ShouldBe(expectedIsValid);
    }
}
