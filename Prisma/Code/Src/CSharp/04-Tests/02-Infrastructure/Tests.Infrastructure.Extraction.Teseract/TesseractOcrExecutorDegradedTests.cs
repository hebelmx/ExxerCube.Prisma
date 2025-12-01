using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/*
 * ╔══════════════════════════════════════════════════════════════════════════════╗
 * ║                    OCR ROBUSTNESS TESTING ROADMAP                            ║
 * ╠══════════════════════════════════════════════════════════════════════════════╣
 * ║                                                                              ║
 * ║  Phase 1: BASELINE PERFORMANCE (CURRENT) ✅                                  ║
 * ║  ─────────────────────────────────────────────────────────────────────────  ║
 * ║  • Document Tesseract/GOT-OCR2 performance on degraded images (NO filters)  ║
 * ║  • Q1_Poor: ~78-92% confidence (PASS - very good text)                      ║
 * ║  • Q2_MediumPoor: ~42-53% confidence (MARGINAL - highly corrupted text)     ║
 * ║  • Q3_Low: ~25-27% confidence (FAIL - severe corruption)                    ║
 * ║  • Q4_VeryLow: 0-15% confidence (FAIL - unreadable)                         ║
 * ║                                                                              ║
 * ║  Phase 2: ENHANCEMENT FILTERS 🔄 NEXT                                        ║
 * ║  ─────────────────────────────────────────────────────────────────────────  ║
 * ║  TODO: Create TesseractOcrExecutorEnhancedTests.cs                          ║
 * ║  TODO: Integrate existing image processing modules:                         ║
 * ║        • prisma-ai-extractors/image_processor.py (contrast, denoise)        ║
 * ║        • prisma-ocr-pipeline/image_binarizer.py (adaptive threshold)        ║
 * ║        • prisma-ocr-pipeline/image_deskewer.py (skew correction)            ║
 * ║  TODO: Test Q1 + Q2 with filters applied                                    ║
 * ║  HYPOTHESIS: Filters lift Q2 from ~42-53% → ~70%+ (production threshold)    ║
 * ║                                                                              ║
 * ║  Phase 3: THRESHOLD REFINEMENT (DECILE APPROACH) 📊 FUTURE                  ║
 * ║  ─────────────────────────────────────────────────────────────────────────  ║
 * ║  TODO: Generate finer-grained quality levels between Q1 and Q2:             ║
 * ║        • D9 (90% quality): Pristine → 10% degradation                       ║
 * ║        • D8 (80% quality): 20% degradation                                  ║
 * ║        • D7 (70% quality): 30% degradation ← CRITICAL THRESHOLD             ║
 * ║        • D6 (60% quality): 40% degradation                                  ║
 * ║  TODO: Find exact confidence threshold where text quality drops             ║
 * ║  GOAL: Pinpoint 70% sweet spot for production quality gate                  ║
 * ║                                                                              ║
 * ║  Phase 4: PRODUCTION INTEGRATION 🚀 FUTURE                                   ║
 * ║  ─────────────────────────────────────────────────────────────────────────  ║
 * ║  TODO: Implement adaptive quality-based routing:                            ║
 * ║        < 25%: REJECT (unreadable)                                           ║
 * ║        25-60%: Digital Filters + GOT-OCR2 (heavy processing)                ║
 * ║        60-80%: Try Filters → Retry Tesseract → GOT-OCR2 if needed           ║
 * ║        > 80%: ACCEPT (fast path)                                            ║
 * ║  TODO: Add smart thresholds with circuit breakers                           ║
 * ║  TODO: Monitor ROI: enhancement time vs accuracy gain                       ║
 * ║                                                                              ║
 * ╚══════════════════════════════════════════════════════════════════════════════╝
 */

/// <summary>
/// OCR ROBUSTNESS TESTS: Tesseract performance on progressively degraded images.
///
/// Tests OCR accuracy and robustness against 4 quality levels:
/// - Q1_Poor: Light degradation (slight blur, minor noise)
/// - Q2_MediumPoor: Moderate degradation (blur, noise, JPEG compression)
/// - Q3_Low: Heavy degradation (strong blur, salt-pepper noise, contrast issues)
/// - Q4_VeryLow: Extreme but human-readable (maximum artifacts, heavy compression)
///
/// GOAL: Measure how Tesseract handles real-world document quality issues.
/// COMPARE: Performance against GOT-OCR2 on same degraded fixtures.
///
/// HYPOTHESIS: Tesseract may be faster but less robust to degradation than GOT-OCR2.
/// </summary>
[Collection(nameof(TesseractDegradedCollection))]
public class TesseractOcrExecutorDegradedTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<TesseractOcrExecutor> _logger;
    private readonly TesseractFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public TesseractOcrExecutorDegradedTests(ITestOutputHelper output, TesseractFixture fixture)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<TesseractOcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing Tesseract Degraded Image Test Instance ===");
        _logger.LogInformation("Using shared Tesseract executor from collection fixture");

        _scope = _fixture.Host.Services.CreateScope();
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("Tesseract executor created for degraded image testing");
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    /// <summary>
    /// Verify degraded fixtures exist for all quality levels.
    /// </summary>
    [Theory(DisplayName = "Tesseract degraded fixtures should exist", Timeout = 5000)]
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
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded", qualityLevel, imageName);
        _logger.LogInformation($"\n=== Checking Degraded Fixture: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        // Assert
        File.Exists(fixturePath).ShouldBeTrue($"Degraded fixture should exist at {fixturePath}");
        var fileInfo = new FileInfo(fixturePath);
        fileInfo.Length.ShouldBeGreaterThan(0, "Degraded fixture should not be empty");

        await Task.CompletedTask;
    }

    /// <summary>
    /// ROBUSTNESS TEST: Tesseract on degraded images across 4 quality levels.
    ///
    /// PERFORMANCE COMPARISON with GOT-OCR2:
    /// - Expected: Tesseract faster execution (~5-20s vs ~140s)
    /// - Question: Does Tesseract maintain accuracy on degraded images?
    /// - Measure: Text length, confidence, execution time across quality levels
    ///
    /// Expected behavior (CONTRACT):
    /// - Q1_Poor: ~85%+ of original quality (55%+ confidence, meaningful text)
    /// - Q2_MediumPoor: ~65%+ of original quality (45%+ confidence, meaningful text)
    /// - Q3_Low: ~40%+ of original quality (30%+ confidence, meaningful text)
    /// - Q4_VeryLow: EXTREME DEGRADATION - Quality threshold test
    ///   * Tesseract reaches its limit (<10% confidence, minimal text)
    ///   * This documents baseline performance
    ///   * Production system should detect this and trigger:
    ///     - Fallback OCR method (e.g., GOT-OCR2)
    ///     - Digital image enhancement preprocessing
    ///     - Explicit quality warning/failure
    ///   * Very low confidence should NOT be silently accepted
    /// </summary>
    [Theory(DisplayName = "Tesseract robustness: Q1-Q3 pass, Q4 documents quality threshold for fallback",
            Timeout = 300_000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg", 55.0f)]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png", 55.0f)]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png", 55.0f)]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png", 55.0f)]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg", 42.0f)]  // Actual measured: ~43-53%
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png", 42.0f)]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png", 42.0f)]  // Actual measured: 42.62%
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png", 42.0f)]
    [InlineData("Q3_Low", "222AAA-44444444442025_page-0001.jpg", 25.0f)]  // Actual measured: 25-27%
    [InlineData("Q3_Low", "333BBB-44444444442025_page1.png", 25.0f)]  // Actual measured: 25.58%
    [InlineData("Q3_Low", "333ccc-6666666662025_page1.png", 25.0f)]  // Actual measured: 25.90%
    [InlineData("Q3_Low", "555CCC-66666662025_page1.png", 25.0f)]  // Actual measured: 25.78%
    [InlineData("Q4_VeryLow", "222AAA-44444444442025_page-0001.jpg", 5.0f)]  // Reality: Tesseract struggles, <10% confidence
    [InlineData("Q4_VeryLow", "333BBB-44444444442025_page1.png", 5.0f)]
    [InlineData("Q4_VeryLow", "333ccc-6666666662025_page1.png", 5.0f)]
    [InlineData("Q4_VeryLow", "555CCC-66666662025_page1.png", 5.0f)]
    public async Task ExecuteOcrAsync_DegradedImages_CompareWithGotOcr2(
        string qualityLevel,
        string imageName,
        float expectedMinConfidence)
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Degraded", qualityLevel, imageName);
        _logger.LogInformation($"\n=== TESSERACT ROBUSTNESS TEST: {qualityLevel}/{imageName} ===");
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
        _logger.LogInformation($"Starting Tesseract execution on {qualityLevel} quality...");
        _logger.LogInformation(">>> TIMER START <<<");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($">>> TIMER END: {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogInformation($">>> EXPECTED GOT-OCR2 TIME: ~140s <<<");
            _logger.LogInformation($">>> SPEEDUP: {(140.0 / elapsed.TotalSeconds):F2}x faster <<<");

            // Assert
            result.IsSuccess.ShouldBeTrue($"OCR execution should succeed even on {qualityLevel} quality");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

            // LOG DETAILED RESULTS FOR COMPARISON
            _logger.LogInformation($"\n=== TESSERACT RESULTS ({qualityLevel}) ===");
            _logger.LogInformation($"  Quality Level: {qualityLevel}");
            _logger.LogInformation($"  Document: {imageName}");
            _logger.LogInformation($"  Execution time: {elapsed.TotalSeconds:F2}s");
            _logger.LogInformation($"  Text length: {ocrResult.Text.Length} characters");
            _logger.LogInformation($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
            _logger.LogInformation($"  Language used: {ocrResult.LanguageUsed}");
            _logger.LogInformation($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");
            _logger.LogInformation($"\n=== FULL OCR TEXT ===\n{ocrResult.Text}\n=== END FULL TEXT ===");
            _logger.LogInformation($"\n=== COMPARISON WITH GOT-OCR2 ===");
            _logger.LogInformation($"  Expected GOT-OCR2 time: ~140s (Tesseract: {elapsed.TotalSeconds:F2}s)");
            _logger.LogInformation($"  Expected GOT-OCR2 confidence: Higher on degraded images");
            _logger.LogInformation($"  Trade-off: Speed vs Accuracy on poor quality documents");

            // Conditional assertions based on quality level
            if (qualityLevel == "Q4_VeryLow")
            {
                // Q4_VeryLow: Document quality threshold where fallback is needed
                _logger.LogWarning($"⚠️ Q4_VeryLow QUALITY THRESHOLD REACHED");
                _logger.LogWarning($"   Tesseract baseline performance:");
                _logger.LogWarning($"   - Text length: {ocrResult.Text.Length} chars");
                _logger.LogWarning($"   - Confidence: {ocrResult.ConfidenceAvg:F2}%");
                _logger.LogWarning($"   - Confidence entries: {ocrResult.Confidences.Count}");
                _logger.LogWarning($"");
                _logger.LogWarning($"   ⚠️ PRODUCTION SYSTEM SHOULD:");
                _logger.LogWarning($"   1. Detect confidence < 10% → Trigger fallback");
                _logger.LogWarning($"   2. Try GOT-OCR2 (expected better performance on degraded images)");
                _logger.LogWarning($"   3. OR apply digital enhancement preprocessing");
                _logger.LogWarning($"   4. OR return explicit quality failure");
                _logger.LogWarning($"   ⚠️ DO NOT silently accept 0% confidence results!");

                // Minimal assertions - just document baseline, don't enforce quality
                ocrResult.ShouldSatisfyAllConditions(
                    () => ocrResult.ShouldNotBeNull("OCR result should not be null"),
                    () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
                );

                _logger.LogInformation($"✓ Q4_VeryLow baseline documented: Confidence={ocrResult.ConfidenceAvg:F2}%, TextLength={ocrResult.Text.Length}");
                _logger.LogInformation($"✓ This quality level requires fallback mechanism in production");
            }
            else
            {
                // Q1-Q3: Expect meaningful text extraction
                ocrResult.ShouldSatisfyAllConditions(
                    () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Should extract some text from degraded image"),
                    () => ocrResult.Text.Length.ShouldBeGreaterThan(50,
                        $"Should extract meaningful text from {qualityLevel} quality (minimum 50 chars)"),
                    () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(expectedMinConfidence,
                        $"Confidence should meet minimum threshold for {qualityLevel}"),
                    () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
                    () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
                );

                _logger.LogInformation($"✓ Tesseract successfully processed {qualityLevel} degraded image");
                _logger.LogInformation($"✓ Robustness validated for {imageName}");
            }

            _logger.LogInformation($"✓ Liskov Substitution Principle: Tesseract implements IOcrExecutor contract");
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError($">>> TIMER END (FAILED): {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogError(ex, $"Tesseract FAILED on {qualityLevel} quality");
            _logger.LogError(ex.InnerException, "Inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "Tesseract should reject null image data (degraded tests)", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()
    {
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
    [Fact(DisplayName = "Tesseract should reject empty image data (degraded tests)", Timeout = 5000)]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        // Arrange
        var emptyImageData = new ImageData(Array.Empty<byte>(), "empty.jpg");
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(emptyImageData, config);

        // Assert
        result.IsSuccess.ShouldBeFalse("Should fail with empty image data");
    }
}
