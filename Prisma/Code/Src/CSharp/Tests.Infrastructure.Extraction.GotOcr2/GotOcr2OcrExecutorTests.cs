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
    private IOcrExecutor? _executor;

    public GotOcr2OcrExecutorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<GotOcr2OcrExecutor>(output);
    }

    public async ValueTask InitializeAsync()
    {
        _output.WriteLine("=== Initializing GOT-OCR2 Test Environment ===");

        // Build host with Python environment (same configuration as ConsoleDemo)
        var builder = Host.CreateApplicationBuilder();

        var baseDirectory = AppContext.BaseDirectory;
        var pythonLibPath = Path.Combine(baseDirectory, "python");
        var venvPath = Path.Combine(baseDirectory, ".venv_gotor2_tests");
        var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");

        _output.WriteLine($"Base directory: {baseDirectory}");
        _output.WriteLine($"Python lib path: {pythonLibPath}");
        _output.WriteLine($"Venv path: {venvPath}");

        // Configure CSnakes Python environment
        builder.Services
            .WithPython()
            .WithHome(pythonLibPath)
            .WithVirtualEnvironment(venvPath)
            .FromRedistributable("3.13")
            .WithPipInstaller(requirementsPath);

        // Register GOT-OCR2 executor
        builder.Services.AddSingleton(_logger);
        builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();

        _host = builder.Build();

        // Get Python environment to trigger initialization
        var pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
        _output.WriteLine("Python environment initialized");

        // Get the executor
        _executor = _host.Services.GetRequiredService<IOcrExecutor>();
        _output.WriteLine("GOT-OCR2 executor created");

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
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
    [Theory(DisplayName = "GOT-OCR2 should process CNBV PDF fixtures with >75% confidence")]
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
        _output.WriteLine($"\n=== Testing Fixture: {fixtureName} ===");
        _output.WriteLine($"Fixture path: {fixturePath}");

        fixturePath.ShouldSatisfyAllConditions(
            () => File.Exists(fixturePath).ShouldBeTrue($"Fixture file should exist at {fixturePath}"),
            () => new FileInfo(fixturePath).Length.ShouldBeGreaterThan(0, "Fixture file should not be empty")
        );

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        _output.WriteLine($"PDF file size: {pdfBytes.Length:N0} bytes");

        var imageData = new ImageData(pdfBytes, fixturePath);
        var config = new OCRConfig(
            language: "spa",  // Spanish primary language
            oem: 1,
            psm: 6,
            fallbackLanguage: "eng",
            confidenceThreshold: expectedMinConfidence / 100f  // Convert percentage to 0-1
        );

        // Act
        _output.WriteLine("Starting OCR execution...");
        var startTime = DateTime.UtcNow;

        var result = await _executor!.ExecuteOcrAsync(imageData, config);

        var elapsed = DateTime.UtcNow - startTime;
        _output.WriteLine($"OCR completed in {elapsed.TotalSeconds:F2}s");

        // Assert - Test IOcrExecutor contract compliance
        result.ShouldSatisfyAllConditions(
            () => result.IsSuccess.ShouldBeTrue("OCR execution should succeed"),
            () => result.Value.ShouldNotBeNull("OCR result should not be null")
        );

        var ocrResult = result.Value!;

        _output.WriteLine($"Results:");
        _output.WriteLine($"  Text length: {ocrResult.Text.Length} characters");
        _output.WriteLine($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
        _output.WriteLine($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
        _output.WriteLine($"  Language used: {ocrResult.LanguageUsed}");
        _output.WriteLine($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");

        // Validate IOcrExecutor contract expectations
        ocrResult.ShouldSatisfyAllConditions(
            () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Extracted text should not be empty"),
            () => ocrResult.Text.Length.ShouldBeGreaterThan(100, "Should extract substantial text from CNBV document"),
            () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(expectedMinConfidence,
                $"Confidence should meet minimum threshold of {expectedMinConfidence}%"),
            () => ocrResult.ConfidenceMedian.ShouldBeGreaterThan(0, "Median confidence should be positive"),
            () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
            () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
        );

        // Liskov Substitution Principle validation:
        // If GOT-OCR2 passes these tests, it correctly implements the IOcrExecutor contract
        // and can be substituted for any other IOcrExecutor implementation (e.g., Tesseract)
        _output.WriteLine($"✓ Liskov Substitution Principle validated for {fixtureName}");
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject null image data")]
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
    [Fact(DisplayName = "GOT-OCR2 should reject empty image data")]
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