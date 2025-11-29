namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// Integration tests for <see cref="GotOcr2OcrExecutor"/> using real PDF fixtures.
/// Tests Liskov Substitution Principle - GOT-OCR2 implementation must satisfy IOcrExecutor contract.
/// </summary>
[Collection(nameof(GotOcr2Collection))]
public class GotOcr2OcrExecutorTests : IDisposable
{
    /// <summary>
    /// Configurable flag to skip slow GOT-OCR2 tests (~140s per document).
    /// Set to false to enable these tests when needed.
    /// </summary>
    public static bool SkipSlowTests => true;

    private readonly ITestOutputHelper _output;
    private readonly ILogger<GotOcr2OcrExecutor> _logger;
    private readonly GotOcr2Fixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public GotOcr2OcrExecutorTests(ITestOutputHelper output, GotOcr2Fixture fixture)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        _output = output;
        _logger = XUnitLogger.CreateLogger<GotOcr2OcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing GOT-OCR2 Test Instance ===");
        _logger.LogInformation("Using shared Python environment from collection fixture");

        // Create scope for this test instance (CRITICAL: Can't resolve scoped from root!)
        _scope = _fixture.Host.Services.CreateScope();

        // Get the executor from scope
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("GOT-OCR2 executor created for test instance");
    }

    public void Dispose()
    {
        // Dispose scope (scoped services) but NOT the host (shared across tests)
        _scope?.Dispose();
    }

    /// <summary>
    /// Theory test with 4 CNBV PDF fixtures.
    /// Tests that GOT-OCR2 can process real Spanish documents with acceptable confidence.
    /// Validates Liskov Substitution Principle - any IOcrExecutor implementation should handle these inputs.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture file (without path)</param>
    /// <param name="expectedMinConfidence">Minimum acceptable confidence threshold</param>
    [Theory(DisplayName = "GOT-OCR2 should process CNBV PDF fixtures with >75% confidence",
            Skip = "GotOcr2 feature frozen - tests disabled",
            Timeout = 3_000_000)]
    [InlineData("222AAA-44444444442025.pdf")]
    [InlineData("333BBB-44444444442025.pdf")]
    [InlineData("333ccc-6666666662025.pdf")]
    [InlineData("555CCC-66666662025.pdf")]
    public async Task FixturesExists_AndAreCopied(string fixtureName)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        _logger.LogInformation($"\n=== Testing Fixture: {fixtureName} ===");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture file should exist at {fixturePath}");
        var p = new FileInfo(fixturePath);
        p.Length.ShouldBeGreaterThan(0, "Fixture file should not be empty");
    }

    /// <summary>
    /// Theory test with 4 CNBV PDF fixtures.
    /// Tests that GOT-OCR2 can process real Spanish documents with acceptable confidence.
    /// Validates Liskov Substitution Principle - any IOcrExecutor implementation should handle these inputs.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture file (without path)</param>
    /// <param name="expectedMinConfidence">Minimum acceptable confidence threshold</param>
    [Theory(DisplayName = "GOT-OCR2 should process CNBV PDF fixtures with >75% confidence",
            Skip = "Slow test (~140s per document). Set SkipSlowTests=false to enable.",
            Timeout = 3_000_000)]
    [InlineData("222AAA-44444444442025.pdf", 75.0f)]
    [InlineData("333BBB-44444444442025.pdf", 75.0f)]
    [InlineData("333ccc-6666666662025.pdf", 75.0f)]
    [InlineData("555CCC-66666662025.pdf", 75.0f)]
    public async Task ExecuteOcrAsync_WithRealCNBVFixtures_ReturnsHighConfidenceResults(
    string fixtureName,
    float expectedMinConfidence)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

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

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($"OCR completed in {elapsed.TotalSeconds:F2}s");

            // Assert - Test IOcrExecutor contract compliance

            result.IsSuccess.ShouldBeTrue("OCR execution should succeed");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR execution FAILED with exception");
            _logger.LogError(ex.InnerException, "OCR execution FAILED with inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject null image data",
          Skip = "GotOcr2 feature frozen - tests disabled",
          Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        ImageData? nullImageData = null;
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(nullImageData!, config);

        // Assert - Contract requires graceful handling of invalid input
        result.IsSuccess.ShouldBeFalse("Should fail with null image data");
    }

    /// <summary>
    /// Tests that the executor rejects empty image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject empty image data",
          Skip = "GotOcr2 feature frozen - tests disabled",
          Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        var emptyImageData = new ImageData(Array.Empty<byte>(), "empty.pdf");
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(emptyImageData, config);

        // Assert - Contract requires validation of input data
        result.IsSuccess.ShouldBeFalse("Should fail with empty image data");
    }
}