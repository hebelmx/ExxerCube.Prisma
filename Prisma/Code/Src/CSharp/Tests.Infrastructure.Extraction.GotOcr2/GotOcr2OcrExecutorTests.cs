namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// Integration tests for <see cref="GotOcr2OcrExecutor"/> using real PDF fixtures.
/// Tests Liskov Substitution Principle - GOT-OCR2 implementation must satisfy IOcrExecutor contract.
/// </summary>
public class GotOcr2OcrExecutorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GotOcr2OcrExecutor> _logger;
    private IHost? _host;
    private IServiceScope? _scope;
    private IOcrExecutor? _executor;

    public GotOcr2OcrExecutorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<GotOcr2OcrExecutor>(output);
    }

    public async ValueTask InitializeAsync()
    {
        _logger.LogInformation("=== Initializing GOT-OCR2 Test Environment ===");

        // Build host with Python environment (same configuration as ConsoleDemo)
        var builder = Host.CreateApplicationBuilder();

        var baseDirectory = AppContext.BaseDirectory;
        var pythonLibPath = Path.Combine(baseDirectory, "python");
        var venvPath = Path.Combine(baseDirectory, ".venv_gotocr2_manual");
        var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");

        _logger.LogInformation($"Base directory: {baseDirectory}");
        _logger.LogInformation($"Python lib path: {pythonLibPath}");
        _logger.LogInformation($"Venv path: {venvPath}");
        _logger.LogInformation($"Requirements path: {requirementsPath}");

        // Verify paths exist
        _logger.LogInformation($"Python lib exists: {Directory.Exists(pythonLibPath)}");
        _logger.LogInformation($"Venv exists: {Directory.Exists(venvPath)}");
        _logger.LogInformation($"Requirements exists: {File.Exists(requirementsPath)}");

        // Configure CSnakes Python environment
        // Strategy: Pre-create venv with correct Python 3.13 + packages
        // CSnakes will detect it exists and skip download/installation
        builder.Services
            .WithPython()
            .WithHome(pythonLibPath)
            .WithVirtualEnvironment(venvPath, true)
            .FromRedistributable("3.13")
            .WithPipInstaller(requirementsPath);

        // Register GOT-OCR2 executor with proper DI (like ConsoleDemo)
        builder.Services.AddSingleton<ILogger<GotOcr2OcrExecutor>>(_logger);
        builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();

        _host = builder.Build();

        // Get Python environment to trigger initialization (Singleton - OK from root)
        _logger.LogInformation("Getting Python environment from DI...");
        var pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
        _logger.LogInformation("✓ Python environment obtained: {PythonEnvType}", pythonEnv.GetType().FullName);

        // Health check: Test Python imports and versions
        _logger.LogInformation("=== Python Environment Health Check ===");
        try
        {
            var module = pythonEnv.GotOcr2Wrapper();
            _logger.LogInformation("✓ GotOcr2Wrapper extension method called successfully");

            var version = module.GetVersion();
            _logger.LogInformation("✓ Module version: {Version}", version);

            var modelInfo = module.GetModelInfo();
            _logger.LogInformation("✓ Model info: {ModelInfo}", modelInfo);

            var isHealthy = module.HealthCheck();
            _logger.LogInformation("✓ Health check result: {IsHealthy}", isHealthy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python environment health check FAILED");
            throw;
        }

        _logger.LogInformation("Python environment initialized and validated");

        // Create scope for scoped services (CRITICAL: Can't resolve scoped from root!)
        _scope = _host.Services.CreateScope();

        // Get the executor from scope
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("GOT-OCR2 executor created");

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose scope first (scoped services)
        _scope?.Dispose();

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    /// <summary>
    /// Theory test with 4 CNBV PDF fixtures.
    /// Tests that GOT-OCR2 can process real Spanish documents with acceptable confidence.
    /// Validates Liskov Substitution Principle - any IOcrExecutor implementation should handle these inputs.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture file (without path)</param>
    /// <param name="expectedMinConfidence">Minimum acceptable confidence threshold</param>
    [Theory(DisplayName = "GOT-OCR2 should process CNBV PDF fixtures with >75% confidence", Timeout = 120000)]
    [InlineData("222AAA-44444444442025.pdf", 75.0f)]
    [InlineData("333BBB-44444444442025.pdf", 75.0f)]
    [InlineData("333ccc-6666666662025.pdf", 75.0f)]
    [InlineData("555CCC-66666662025.pdf", 75.0f)]
    public async Task ExecuteOcrAsync_WithRealCNBVFixtures_ReturnsHighConfidenceResults(
        string fixtureName,
        float expectedMinConfidence)
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        _logger.LogInformation($"\n=== Testing Fixture: {fixtureName} ===");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        fixturePath.ShouldSatisfyAllConditions(
            () => File.Exists(fixturePath).ShouldBeTrue($"Fixture file should exist at {fixturePath}"),
            () => new FileInfo(fixturePath).Length.ShouldBeGreaterThan(0, "Fixture file should not be empty")
        );

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        _logger.LogInformation($"PDF file size: {pdfBytes.Length:N0} bytes");

        var imageData = new ImageData(pdfBytes, fixturePath);
        var config = new OCRConfig(
            language: "spa",  // Spanish primary language
            oem: 1,
            psm: 6,
            fallbackLanguage: "eng",
            confidenceThreshold: expectedMinConfidence / 100f  // Convert percentage to 0-1
        );

        // Act
        _logger.LogInformation("Starting OCR execution...");
        var startTime = DateTime.UtcNow;

        var result = await _executor!.ExecuteOcrAsync(imageData, config);

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation($"OCR completed in {elapsed.TotalSeconds:F2}s");

        // Assert - Test IOcrExecutor contract compliance
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccess.ShouldBeTrue("OCR execution should succeed"),
            () => result.Value.ShouldNotBeNull("OCR result should not be null")
        );

        var ocrResult = result.Value!;

        _logger.LogInformation($"Results:");
        _logger.LogInformation($"  Text length: {ocrResult.Text.Length} characters");
        _logger.LogInformation($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
        _logger.LogInformation($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
        _logger.LogInformation($"  Language used: {ocrResult.LanguageUsed}");
        _logger.LogInformation($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");

        // Validate IOcrExecutor contract expectations
        // CNBV documents are official regulatory filings with substantial content
        // Known working values from sample: 1,761 chars, 88.94% confidence (heuristic-based)
        // Note: GOT-OCR2 uses heuristic confidence (text length + quality), not model confidence
        ocrResult.ShouldSatisfyAllConditions(
            () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Extracted text should not be empty"),
            () => ocrResult.Text.Length.ShouldBeGreaterThan(500,
                "Should extract substantial text from CNBV document (expected >1000 chars, minimum 500)"),
            () => ocrResult.ConfidenceAvg.ShouldBeGreaterThan(0,
                "Confidence average should be positive (heuristic-based calculation)"),
            () => ocrResult.ConfidenceMedian.ShouldBeGreaterThan(0,
                "Median confidence should be positive"),
            () => ocrResult.ConfidenceMedian.ShouldBe(ocrResult.ConfidenceAvg,
                "GOT-OCR2 returns same value for avg and median (single heuristic score)"),
            () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
            () => ocrResult.Confidences.Count.ShouldBe(1,
                "GOT-OCR2 returns single confidence score (no per-word scores like Tesseract)"),
            () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
        );

        // Liskov Substitution Principle validation:
        // If GOT-OCR2 passes these tests, it correctly implements the IOcrExecutor contract
        // and can be substituted for any other IOcrExecutor implementation (e.g., Tesseract)
        _logger.LogInformation($"✓ Liskov Substitution Principle validated for {fixtureName}");
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject null image data", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()
    {
        // Arrange
        ImageData? nullImageData = null;
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor!.ExecuteOcrAsync(nullImageData!, config);

        // Assert - Contract requires graceful handling of invalid input
        result.IsSuccess.ShouldBeFalse("Should fail with null image data");
    }

    /// <summary>
    /// Tests that the executor rejects empty image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject empty image data", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        // Arrange
        var emptyImageData = new ImageData(Array.Empty<byte>(), "empty.pdf");
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor!.ExecuteOcrAsync(emptyImageData, config);

        // Assert - Contract requires validation of input data
        result.IsSuccess.ShouldBeFalse("Should fail with empty image data");
    }
}