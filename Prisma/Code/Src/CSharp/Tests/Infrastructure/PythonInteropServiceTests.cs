using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Python;
using System.Linq;

namespace ExxerCube.Prisma.Tests.Infrastructure;

/// <summary>
/// Tests for the Python interop service using real Python modules.
/// </summary>
public class PythonInteropServiceTests : IDisposable
{
    private readonly ILogger<PrismaOcrWrapperAdapter> _logger;
    private readonly string _pythonModulesPath;
    private readonly string _testDataPath;
    private readonly PrismaOcrWrapperAdapter _adapter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInteropServiceTests"/> class.
    /// </summary>
    public PythonInteropServiceTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PrismaOcrWrapperAdapter>();
        _pythonModulesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CSharp", "Python", "ocr_modules");
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        
        // Ensure test data directory exists
        if (!Directory.Exists(_testDataPath))
        {
            Directory.CreateDirectory(_testDataPath);
        }
        
        _adapter = new PrismaOcrWrapperAdapter(_logger);
    }

    /// <summary>
    /// Tests that the PrismaOcrWrapperAdapter can be created successfully.
    /// </summary>
    [Fact]
    public void PrismaOcrWrapperAdapter_ShouldCreateSuccessfully()
    {
        // Act
        var adapter = new PrismaOcrWrapperAdapter(_logger);

        // Assert
        adapter.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the PrismaOcrWrapperAdapter implements the correct interface.
    /// </summary>
    [Fact]
    public void PrismaOcrWrapperAdapter_ShouldImplementIPythonInteropService()
    {
        // Act
        var adapter = new PrismaOcrWrapperAdapter(_logger);

        // Assert
        adapter.ShouldBeAssignableTo<IPythonInteropService>();
    }

    /// <summary>
    /// Tests that expediente extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractExpediente_WithRealDocument_ReturnsActualExpediente()
    {
        // Arrange
        var testText = "En relación al Expediente: ABC-123/2023, se requiere...";
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("ABC-123/2023");
    }

    /// <summary>
    /// Tests that date extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractDates_WithRealDocument_ReturnsActualDates()
    {
        // Arrange
        var testText = "Fecha: 15 de octubre de 2023 y también 2023-12-25";
        
        // Act
        var result = await _adapter.ExtractDatesAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("2023-10-15");
        result.Value.ShouldContain("2023-12-25");
    }

    /// <summary>
    /// Tests that amount extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractAmounts_WithRealDocument_ReturnsActualAmounts()
    {
        // Arrange
        var testDataPath = Path.Combine(_testDataPath, "sample_document.txt");
        if (!File.Exists(testDataPath))
        {
            // Skip test if test data not available
            return;
        }
        
        var testText = File.ReadAllText(testDataPath);
        
        // Act
        var result = await _adapter.ExtractAmountsAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0);
        
        var values = result.Value.Select(a => a.Value).ToList();
        values.ShouldContain(50000.00m); // $50,000.00 from test data
    }

    /// <summary>
    /// Tests that image binarization works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task BinarizeImage_WithRealImage_ReturnsBinarizedImage()
    {
        // Arrange
        var testImagePath = Path.Combine(_testDataPath, "DumyPrisma1.png");
        if (!File.Exists(testImagePath))
        {
            // Skip test if test image not available
            return;
        }
        
        var imageData = new ImageData
        {
            Data = File.ReadAllBytes(testImagePath),
            SourcePath = "DumyPrisma1.png",
            PageNumber = 1,
            TotalPages = 1
        };
        
        // Act
        var result = await _adapter.BinarizeAsync(imageData);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Data.ShouldNotBeNull();
        result.Value.Data.Length.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Tests that the adapter handles empty text gracefully.
    /// </summary>
    [Fact]
    public async Task ExtractExpediente_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var emptyText = "";
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(emptyText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    /// <summary>
    /// Tests that expediente extraction returns null for null text.
    /// </summary>
    [Fact]
    public async Task ExtractExpediente_WithNullText_ReturnsNull()
    {
        // Arrange
        string? nullText = null;
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(nullText!);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    /// <summary>
    /// Tests that causa extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractCausa_WithRealDocument_ReturnsActualCausa()
    {
        // Arrange
        var testText = "En el presente juicio de naturaleza Civil, se solicita...";
        
        // Act
        var result = await _adapter.ExtractCausaAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("Civil");
    }

    /// <summary>
    /// Tests that accion solicitada extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractAccionSolicitada_WithRealDocument_ReturnsActualAccion()
    {
        // Arrange
        var testText = "Se solicita la compensación por daños y perjuicios...";
        
        // Act
        var result = await _adapter.ExtractAccionSolicitadaAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("compensación");
    }

    /// <summary>
    /// Tests that the circuit breaker service can be created successfully.
    /// </summary>
    [Fact]
    public void CircuitBreakerPythonInteropService_ShouldCreateSuccessfully()
    {
        // Arrange
        var innerService = _adapter;
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CircuitBreakerPythonInteropService>();

        // Act
        var circuitBreaker = new CircuitBreakerPythonInteropService(logger, innerService);

        // Assert
        circuitBreaker.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that the OcrProcessingAdapter can be created successfully.
    /// </summary>
    [Fact]
    public void OcrProcessingAdapter_ShouldCreateSuccessfully()
    {
        // Arrange
        var pythonInteropService = _adapter;
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<OcrProcessingAdapter>();

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
        var pythonInteropService = _adapter;
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<OcrProcessingAdapter>();

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
    [Trait("Category", "Integration")]
    public async Task OcrProcessingAdapter_ShouldDelegateToPythonInteropService()
    {
        // Arrange
        var pythonInteropService = _adapter;
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<OcrProcessingAdapter>();
        var adapter = new OcrProcessingAdapter(logger, pythonInteropService);

        var testText = "En relación al Expediente: ABC-123/2023, se requiere...";

        // Act
        var result = await adapter.ExtractExpedienteAsync(testText);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("ABC-123/2023");
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

    /// <summary>
    /// Tests that the Python integration is working correctly.
    /// </summary>
    [Fact]
    public async Task PythonIntegration_ShouldWorkCorrectly()
    {
        // Arrange
        var testText = "En el expediente ABC123/2023 se solicita...";
        
        // Log the paths being used
        _logger.LogInformation("Python modules path: {ModulesPath}", _pythonModulesPath);
        _logger.LogInformation("Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(testText);
        
        // Assert
        if (!result.IsSuccess)
        {
            _logger.LogError("Python integration test failed: {Error}", result.Error);
            // Also log the expected script path
            var expectedScriptPath = Path.Combine(_pythonModulesPath, "..", "expediente_cli.py");
            _logger.LogError("Expected script path: {ScriptPath}", expectedScriptPath);
            _logger.LogError("Script path exists: {Exists}", File.Exists(expectedScriptPath));
        }
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("ABC123/2023");
        
        _logger.LogInformation("Python integration test passed. Extracted expediente: {Expediente}", result.Value);
    }

    /// <summary>
    /// Tests that the path construction is working correctly.
    /// </summary>
    [Fact]
    public void PathConstruction_ShouldWorkCorrectly()
    {
        // Arrange
        var modulesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CSharp", "Python", "ocr_modules");
        var expectedScriptPath = Path.Combine(modulesPath, "..", "expediente_cli.py");
        
        // Act & Assert
        Directory.Exists(modulesPath).ShouldBeTrue($"Modules path should exist: {modulesPath}");
        File.Exists(expectedScriptPath).ShouldBeTrue($"Script path should exist: {expectedScriptPath}");
        
        _logger.LogInformation("Modules path: {ModulesPath}", modulesPath);
        _logger.LogInformation("Script path: {ScriptPath}", expectedScriptPath);
    }

    /// <summary>
    /// Disposes the test resources.
    /// </summary>
    public void Dispose()
    {
        _adapter?.Dispose();
    }
}
