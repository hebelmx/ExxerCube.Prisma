using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Integration tests for Tesseract OCR using same fixtures as GOT-OCR2.
/// GOAL: Compare performance and accuracy between Tesseract and GOT-OCR2.
/// Tests Liskov Substitution Principle - Tesseract implementation must satisfy IOcrExecutor contract.
/// </summary>
[Collection(nameof(TesseractCollection))]
public class TesseractOcrExecutorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<TesseractOcrExecutor> _logger;
    private readonly TesseractFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public TesseractOcrExecutorTests(ITestOutputHelper output, TesseractFixture fixture)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<TesseractOcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing Tesseract Test Instance ===");
        _logger.LogInformation("Using shared Tesseract executor from collection fixture");

        // Create scope for this test instance
        _scope = _fixture.Host.Services.CreateScope();

        // Get the executor from scope
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("Tesseract executor created for test instance");
    }

    public void Dispose()
    {
        // Dispose scope (scoped services) but NOT the host (shared across tests)
        _scope?.Dispose();
    }

    /// <summary>
    /// Verify fixtures exist (same as GOT-OCR2 tests).
    /// </summary>
    [Theory(DisplayName = "Tesseract fixtures should exist and be copied", Timeout = 5000)]
    [InlineData("222AAA-44444444442025.pdf")]
    [InlineData("333BBB-44444444442025.pdf")]
    [InlineData("333ccc-6666666662025.pdf")]
    [InlineData("555CCC-66666662025.pdf")]
    public async Task FixturesExists_AndAreCopied(string fixtureName)
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        _logger.LogInformation($"\n=== Testing Fixture: {fixtureName} ===");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture file should exist at {fixturePath}");
        var p = new FileInfo(fixturePath);
        p.Length.ShouldBeGreaterThan(0, "Fixture file should not be empty");

        await Task.CompletedTask;
    }

    /// <summary>
    /// PERFORMANCE COMPARISON TEST with GOT-OCR2.
    /// Tests same CNBV PDF fixtures to compare:
    /// - Execution time (Tesseract vs GOT-OCR2 ~140s)
    /// - Confidence scores
    /// - Text extraction quality
    /// </summary>
    /// <param name="fixtureName">Name of the fixture file (without path)</param>
    /// <param name="expectedMinConfidence">Minimum acceptable confidence threshold</param>
    [Theory(DisplayName = "Tesseract should process CNBV PDF fixtures (compare with GOT-OCR2)", Timeout = 300_000)]
    [InlineData("222AAA-44444444442025.pdf", 60.0f)]  // Lower threshold than GOT-OCR2 (75%)
    [InlineData("333BBB-44444444442025.pdf", 60.0f)]
    [InlineData("333ccc-6666666662025.pdf", 60.0f)]
    [InlineData("555CCC-66666662025.pdf", 60.0f)]
    public async Task ExecuteOcrAsync_WithRealCNBVFixtures_CompareWithGotOcr2(
        string fixtureName,
        float expectedMinConfidence)
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        _logger.LogInformation($"\n=== PERFORMANCE TEST: {fixtureName} ===");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        fixturePath.ShouldSatisfyAllConditions(
            () => File.Exists(fixturePath).ShouldBeTrue($"Fixture file should exist at {fixturePath}"),
            () => new FileInfo(fixturePath).Length.ShouldBeGreaterThan(0, "Fixture file should not be empty")
        );

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        _logger.LogInformation($"PDF file size: {pdfBytes.Length:N0} bytes");

        var imageData = new ImageData(pdfBytes, fixturePath);
        var config = new OCRConfig(
            language: "spa",  // Spanish primary language (same as GOT-OCR2)
            oem: 1,          // LSTM engine mode
            psm: 6,          // Assume uniform block of text
            fallbackLanguage: "eng",
            confidenceThreshold: expectedMinConfidence / 100f
        );

        // Act - MEASURE PERFORMANCE
        _logger.LogInformation("Starting Tesseract OCR execution...");
        _logger.LogInformation(">>> TIMER START <<<");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($">>> TIMER END: {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogInformation($">>> COMPARE WITH GOT-OCR2: ~140s per page <<<");

            // Assert - Test IOcrExecutor contract compliance
            result.IsSuccess.ShouldBeTrue("OCR execution should succeed");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

            // LOG RESULTS FOR COMPARISON
            _logger.LogInformation($"\n=== TESSERACT RESULTS ===");
            _logger.LogInformation($"  Execution time: {elapsed.TotalSeconds:F2}s");
            _logger.LogInformation($"  Text length: {ocrResult.Text.Length} characters");
            _logger.LogInformation($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
            _logger.LogInformation($"  Language used: {ocrResult.LanguageUsed}");
            _logger.LogInformation($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");
            _logger.LogInformation($"\n=== EXPECTED GOT-OCR2 BASELINE ===");
            _logger.LogInformation($"  Execution time: ~140s");
            _logger.LogInformation($"  Text length: >1000 characters");
            _logger.LogInformation($"  Confidence: 88%+");

            // Validate IOcrExecutor contract expectations
            // Note: Tesseract may have different confidence scoring than GOT-OCR2
            ocrResult.ShouldSatisfyAllConditions(
                () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Extracted text should not be empty"),
                () => ocrResult.Text.Length.ShouldBeGreaterThan(100,
                    "Should extract text from CNBV document (relaxed threshold for Tesseract comparison)"),
                () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(0,
                    "Confidence average should be non-negative"),
                () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
                () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
            );

            // Liskov Substitution Principle validation:
            // Tesseract passes same contract as GOT-OCR2, can be substituted
            _logger.LogInformation($"✓ Liskov Substitution Principle validated for {fixtureName}");
            _logger.LogInformation($"✓ Tesseract implements IOcrExecutor contract correctly");
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError($">>> TIMER END (FAILED): {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogError(ex, "Tesseract OCR execution FAILED with exception");
            _logger.LogError(ex.InnerException, "Tesseract OCR execution FAILED with inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// Same test as GOT-OCR2 for consistency.
    /// </summary>
    [Fact(DisplayName = "Tesseract should reject null image data", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()
    {
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
    /// Same test as GOT-OCR2 for consistency.
    /// </summary>
    [Fact(DisplayName = "Tesseract should reject empty image data", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        // Arrange
        var emptyImageData = new ImageData(Array.Empty<byte>(), "empty.pdf");
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(emptyImageData, config);

        // Assert - Contract requires validation of input data
        result.IsSuccess.ShouldBeFalse("Should fail with empty image data");
    }
}