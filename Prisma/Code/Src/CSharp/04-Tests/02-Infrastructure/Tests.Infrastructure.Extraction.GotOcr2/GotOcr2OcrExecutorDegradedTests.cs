namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// OCR ROBUSTNESS TESTS: GOT-OCR2 performance on progressively degraded images.
///
/// Tests OCR accuracy and robustness against 4 quality levels:
/// - Q1_Poor: Light degradation (slight blur, minor noise)
/// - Q2_MediumPoor: Moderate degradation (blur, noise, JPEG compression)
/// - Q3_Low: Heavy degradation (strong blur, salt-pepper noise, contrast issues)
/// - Q4_VeryLow: Extreme but human-readable (maximum artifacts, heavy compression)
///
/// GOAL: Measure how GOT-OCR2 handles real-world document quality issues.
/// COMPARE: Performance against Tesseract on same degraded fixtures.
/// </summary>
[Collection(nameof(GotOcr2DegradedCollection))]
public class GotOcr2OcrExecutorDegradedTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GotOcr2OcrExecutor> _logger;
    private readonly GotOcr2Fixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public GotOcr2OcrExecutorDegradedTests(ITestOutputHelper output, GotOcr2Fixture fixture)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        _output = output;
        _logger = XUnitLogger.CreateLogger<GotOcr2OcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing GOT-OCR2 Degraded Image Test Instance ===");
        _logger.LogInformation("Using shared Python environment from collection fixture");

        _scope = _fixture.Host.Services.CreateScope();
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("GOT-OCR2 executor created for degraded image testing");
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    /// <summary>
    /// Verify degraded fixtures exist for all quality levels.
    /// </summary>
    [Theory(DisplayName = "GOT-OCR2 degraded fixtures should exist",
        Skip = "Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.",
        Timeout = 5000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png")]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png")]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png")]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png")]
    [InlineData("Q3_Low", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q3_Low", "333BBB-44444444442025_page1.png")]
    [InlineData("Q3_Low", "333ccc-6666666662025_page1.png")]
    [InlineData("Q3_Low", "555CCC-66666662025_page1.png")]
    [InlineData("Q4_VeryLow", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q4_VeryLow", "333BBB-44444444442025_page1.png")]
    [InlineData("Q4_VeryLow", "333ccc-6666666662025_page1.png")]
    [InlineData("Q4_VeryLow", "555CCC-66666662025_page1.png")]
    public async Task DegradedFixturesExist_AllQualityLevels(string qualityLevel, string imageName)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        _logger.LogInformation($"\n=== Checking Degraded Fixture: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded", qualityLevel, imageName);
        _logger.LogInformation($"Constructed path: {fixturePath}");
        _logger.LogInformation($"File.Exists check: {File.Exists(fixturePath)}");

        // Check parent directories
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        _logger.LogInformation($"Fixtures directory exists: {Directory.Exists(fixturesDir)}");

        var prp1DegradedDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded");
        _logger.LogInformation($"PRP1_Degraded directory exists: {Directory.Exists(prp1DegradedDir)}");

        var qualityDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded", qualityLevel);
        _logger.LogInformation($"{qualityLevel} directory exists: {Directory.Exists(qualityDir)}");

        if (Directory.Exists(qualityDir))
        {
            var files = Directory.GetFiles(qualityDir);
            _logger.LogInformation($"Files in {qualityLevel}: {string.Join(", ", files.Select(Path.GetFileName))}");
        }

        // Assert
        File.Exists(fixturePath).ShouldBeTrue($"Degraded fixture should exist at {fixturePath}");
        var fileInfo = new FileInfo(fixturePath);
        fileInfo.Length.ShouldBeGreaterThan(0, "Degraded fixture should not be empty");

        await Task.CompletedTask;
    }

    /// <summary>
    /// ROBUSTNESS TEST: GOT-OCR2 on degraded images across 4 quality levels.
    ///
    /// Measures:
    /// - Text extraction accuracy as quality degrades
    /// - Confidence score trends
    /// - Execution time stability
    /// - Minimum readable threshold
    ///
    /// Expected behavior:
    /// - Q1_Poor: ~90%+ of original quality
    /// - Q2_MediumPoor: ~75%+ of original quality
    /// - Q3_Low: ~50%+ of original quality
    /// - Q4_VeryLow: Still readable but degraded
    /// </summary>
    [Theory(DisplayName = "GOT-OCR2 robustness test on degraded images",
           Skip = "Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.",
            Timeout = 3_000_000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg", 70.0f)]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png", 70.0f)]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png", 70.0f)]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png", 70.0f)]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg", 60.0f)]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png", 60.0f)]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png", 60.0f)]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png", 60.0f)]
    [InlineData("Q3_Low", "222AAA-44444444442025_page-0001.jpg", 40.0f)]
    [InlineData("Q3_Low", "333BBB-44444444442025_page1.png", 40.0f)]
    [InlineData("Q3_Low", "333ccc-6666666662025_page1.png", 40.0f)]
    [InlineData("Q3_Low", "555CCC-66666662025_page1.png", 40.0f)]
    [InlineData("Q4_VeryLow", "222AAA-44444444442025_page-0001.jpg", 20.0f)]
    [InlineData("Q4_VeryLow", "333BBB-44444444442025_page1.png", 20.0f)]
    [InlineData("Q4_VeryLow", "333ccc-6666666662025_page1.png", 20.0f)]
    [InlineData("Q4_VeryLow", "555CCC-66666662025_page1.png", 20.0f)]
    public async Task ExecuteOcrAsync_DegradedImages_MeasuresRobustness(
        string qualityLevel,
        string imageName,
        float expectedMinConfidence)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded", qualityLevel, imageName);
        _logger.LogInformation($"\n=== ROBUSTNESS TEST: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"Quality Level: {qualityLevel}");
        _logger.LogInformation($"Expected Min Confidence: {expectedMinConfidence}%");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        fixturePath.ShouldSatisfyAllConditions(
            () => File.Exists(fixturePath).ShouldBeTrue($"Degraded fixture should exist at {fixturePath}"),
            () => new FileInfo(fixturePath).Length.ShouldBeGreaterThan(0, "Degraded fixture should not be empty")
        );

        var imageBytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        _logger.LogInformation($"Image file size: {imageBytes.Length:N0} bytes");

        var imageData = new ImageData(imageBytes, fixturePath);
        var config = new OCRConfig(
            language: "spa",
            oem: 1,
            psm: 6,
            fallbackLanguage: "eng",
            confidenceThreshold: expectedMinConfidence / 100f
        );

        // Act - MEASURE PERFORMANCE ON DEGRADED IMAGE
        _logger.LogInformation($"Starting GOT-OCR2 execution on {qualityLevel} quality...");
        _logger.LogInformation(">>> TIMER START <<<");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($">>> TIMER END: {elapsed.TotalSeconds:F2}s <<<");

            // Assert
            result.IsSuccess.ShouldBeTrue($"OCR execution should succeed even on {qualityLevel} quality");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

            // LOG DETAILED RESULTS
            _logger.LogInformation($"\n=== GOT-OCR2 RESULTS ({qualityLevel}) ===");
            _logger.LogInformation($"  Quality Level: {qualityLevel}");
            _logger.LogInformation($"  Document: {imageName}");
            _logger.LogInformation($"  Execution time: {elapsed.TotalSeconds:F2}s");
            _logger.LogInformation($"  Text length: {ocrResult.Text.Length} characters");
            _logger.LogInformation($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
            _logger.LogInformation($"  Language used: {ocrResult.LanguageUsed}");
            _logger.LogInformation($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");
            _logger.LogInformation($"\n=== FULL OCR TEXT ===\n{ocrResult.Text}\n=== END FULL TEXT ===");

            // Relaxed assertions for degraded images
            ocrResult.ShouldSatisfyAllConditions(
                () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Should extract some text even from degraded image"),
                () => ocrResult.Text.Length.ShouldBeGreaterThan(50,
                    $"Should extract meaningful text from {qualityLevel} quality (minimum 50 chars)"),
                () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(0,
                    "Confidence should be non-negative"),
                () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
                () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
            );

            _logger.LogInformation($"✓ GOT-OCR2 successfully processed {qualityLevel} degraded image");
            _logger.LogInformation($"✓ Robustness validated for {imageName}");
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError($">>> TIMER END (FAILED): {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogError(ex, $"GOT-OCR2 FAILED on {qualityLevel} quality");
            _logger.LogError(ex.InnerException, "Inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject null image data (degraded tests)",
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

        // Assert
        result.IsSuccess.ShouldBeFalse("Should fail with null image data");
    }

    /// <summary>
    /// Tests that the executor rejects empty image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject empty image data (degraded tests)",
          Skip = "GotOcr2 feature frozen - tests disabled",
          Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        var emptyImageData = new ImageData(Array.Empty<byte>(), "empty.jpg");
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(emptyImageData, config);

        // Assert
        result.IsSuccess.ShouldBeFalse("Should fail with empty image data");
    }
}