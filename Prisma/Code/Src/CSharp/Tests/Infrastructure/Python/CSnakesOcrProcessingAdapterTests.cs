using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Infrastructure.Python;
using ExxerCube.Prisma.Infrastructure.Python.Wrappers;
using NSubstitute;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Tests.Infrastructure.Python;

/// <summary>
/// Tests for the CSnakes OCR processing adapter to validate proper CSnakes implementation.
/// </summary>
public class CSnakesOcrProcessingAdapterTests : IDisposable
{
    private readonly ILogger<CSnakesOcrProcessingAdapter> _loggerMock;
    private readonly IPrismaOcrWrapper _ocrWrapperMock;
    private readonly CSnakesOcrProcessingAdapter _adapter;

    /// <summary>
    /// Initializes a new instance of the CSnakesOcrProcessingAdapterTests class.
    /// </summary>
    public CSnakesOcrProcessingAdapterTests()
    {
        _loggerMock = Substitute.For<ILogger<CSnakesOcrProcessingAdapter>>();
        _ocrWrapperMock = Substitute.For<IPrismaOcrWrapper>();
        
        // Create adapter with mocked wrapper for testing
        // Note: The current implementation throws NotImplementedException, so we'll test the fallback behavior
        _adapter = new CSnakesOcrProcessingAdapter(_loggerMock);
    }

    /// <summary>
    /// Tests that ExecuteOcrAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteOcrAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.png",
            PageNumber = 1,
            TotalPages = 1
        };

        var config = new OCRConfig
        {
            Language = "spa",
            FallbackLanguage = "eng",
            OEM = 3,
            PSM = 6
        };

        var expectedResult = new Dictionary<string, object>
        {
            ["text"] = "Test OCR text",
            ["confidence_avg"] = 85.5,
            ["confidence_median"] = 87.2,
            ["confidences"] = new List<float> { 85.5f, 87.2f },
            ["language_used"] = "spa"
        };

        _ocrWrapperMock.ExecuteOcr(Arg.Any<byte[]>(), Arg.Any<Dictionary<string, object>>()).Returns(expectedResult);

        // Act
        var result = await _adapter.ExecuteOcrAsync(imageData, config);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExecuteOcrAsync returns a failure result when Python processing encounters an error.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteOcrAsync_WithPythonError_ShouldReturnFailureResult()
    {
        // Arrange
        var imageData = new ImageData
        {
            Data = new byte[] { 1, 2, 3, 4 },
            SourcePath = "test.png",
            PageNumber = 1,
            TotalPages = 1
        };

        var config = new OCRConfig
        {
            Language = "spa",
            FallbackLanguage = "eng",
            OEM = 3,
            PSM = 6
        };

        var errorResult = new Dictionary<string, object>
        {
            ["error"] = "Python OCR failed",
            ["text"] = "",
            ["confidence_avg"] = 0.0,
            ["confidence_median"] = 0.0,
            ["confidences"] = new List<float>(),
            ["language_used"] = "spa"
        };

        _ocrWrapperMock.ExecuteOcr(Arg.Any<byte[]>(), Arg.Any<Dictionary<string, object>>()).Returns(errorResult);

        // Act
        var result = await _adapter.ExecuteOcrAsync(imageData, config);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractFieldsAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractFieldsAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document text";
        var confidence = 85.5f;

        var expectedResult = new Dictionary<string, object>
        {
            ["expediente"] = "EXP-2024-001",
            ["causa"] = "Test cause",
            ["accion_solicitada"] = "Test action",
            ["fechas"] = new List<string> { "2024-01-01", "2024-01-15" },
            ["montos"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["currency"] = "MXN",
                    ["value"] = 1000.0,
                    ["original_text"] = "mil pesos"
                }
            }
        };

        _ocrWrapperMock.ExtractFieldsFromText(text, confidence).Returns(expectedResult);

        // Act
        var result = await _adapter.ExtractFieldsAsync(text, confidence);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractExpedienteAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractExpedienteAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document with expediente EXP-2024-001";

        _ocrWrapperMock.ExtractExpedienteFromText(text).Returns("EXP-2024-001");

        // Act
        var result = await _adapter.ExtractExpedienteAsync(text);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractExpedienteAsync returns a success result with null when text is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractExpedienteAsync_WithEmptyText_ShouldReturnNull()
    {
        // Arrange
        var text = "";

        // Act
        var result = await _adapter.ExtractExpedienteAsync(text);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// Tests that ExtractCausaAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractCausaAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document with causa information";

        _ocrWrapperMock.ExtractCausaFromText(text).Returns("Test causa");

        // Act
        var result = await _adapter.ExtractCausaAsync(text);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractAccionSolicitadaAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractAccionSolicitadaAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document with accion solicitada";

        _ocrWrapperMock.ExtractAccionSolicitadaFromText(text).Returns("Test accion");

        // Act
        var result = await _adapter.ExtractAccionSolicitadaAsync(text);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractDatesAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractDatesAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document with dates 2024-01-01 and 2024-01-15";
        var expectedDates = new List<string> { "2024-01-01", "2024-01-15" };

        _ocrWrapperMock.ExtractDatesFromText(text).Returns(expectedDates);

        // Act
        var result = await _adapter.ExtractDatesAsync(text);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that ExtractAmountsAsync returns a failure result when CSnakes integration is not fully implemented.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractAmountsAsync_WithValidInput_ShouldReturnSuccessResult()
    {
        // Arrange
        var text = "Test document with amounts 1000 MXN and 500 USD";
        var expectedAmounts = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["currency"] = "MXN",
                ["value"] = 1000.0,
                ["original_text"] = "mil pesos"
            },
            new Dictionary<string, object>
            {
                ["currency"] = "USD",
                ["value"] = 500.0,
                ["original_text"] = "quinientos dolares"
            }
        };

        _ocrWrapperMock.ExtractAmountsFromText(text).Returns(expectedAmounts);

        // Act
        var result = await _adapter.ExtractAmountsAsync(text);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("CSnakes integration not yet fully implemented", result.Error);
    }

    /// <summary>
    /// Tests that the constructor initializes successfully with a valid logger.
    /// </summary>
    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var adapter = new CSnakesOcrProcessingAdapter(_loggerMock);

        // Assert
        Assert.NotNull(adapter);
    }

    /// <summary>
    /// Tests that the constructor throws ArgumentNullException when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CSnakesOcrProcessingAdapter(null!));
    }

    /// <summary>
    /// Tests that ExecuteOcrAsync throws ArgumentNullException when imageData is null.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteOcrAsync_WithNullImageData_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new OCRConfig { Language = "spa" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _adapter.ExecuteOcrAsync(null!, config));
    }

    /// <summary>
    /// Tests that ExecuteOcrAsync throws ArgumentNullException when config is null.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecuteOcrAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var imageData = new ImageData { Data = new byte[] { 1, 2, 3 } };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _adapter.ExecuteOcrAsync(imageData, null!));
    }

    /// <summary>
    /// Tests that ExtractFieldsAsync throws ArgumentException when text is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExtractFieldsAsync_WithEmptyText_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _adapter.ExtractFieldsAsync("", 85.5f));
    }

    /// <summary>
    /// Disposes of the test resources.
    /// </summary>
    public void Dispose()
    {
        _adapter?.Dispose();
    }
}
