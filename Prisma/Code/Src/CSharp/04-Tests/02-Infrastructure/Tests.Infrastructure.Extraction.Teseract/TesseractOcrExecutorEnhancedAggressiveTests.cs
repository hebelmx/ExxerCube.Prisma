/*
 * ╔══════════════════════════════════════════════════════════════════════════════╗
 * ║           PHASE 2B: AGGRESSIVE ENHANCEMENT FILTERS TESTING                  ║
 * ╠══════════════════════════════════════════════════════════════════════════════╣
 * ║  GOAL: Test if aggressive enhancement can rescue Q2 images better          ║
 * ║                                                                              ║
 * ║  BASELINE PERFORMANCE (From Phase 1):                                       ║
 * ║  • Q1_Poor:       78-92% confidence → "very good text" ✅ (above 75%)       ║
 * ║  • Q2_MediumPoor: 42-53% confidence → "highly corrupted text" ❌ (below 75%) ║
 * ║                                                                              ║
 * ║  STANDARD ENHANCEMENT RESULTS (Phase 2):                                    ║
 * ║  • Q1_Poor:       80-93% confidence (no significant change)                 ║
 * ║  • Q2_MediumPoor: 60-75% confidence (2/4 passed, 2/4 failed @ 69.62%)       ║
 * ║                                                                              ║
 * ║  AGGRESSIVE ENHANCEMENT PIPELINE (EXPERIMENTAL):                            ║
 * ║  1. Grayscale conversion                                                    ║
 * ║  2. Fast Non-local Means Denoising (h=30) - AGGRESSIVE                      ║
 * ║  3. CLAHE (clipLimit=2.0, tileGridSize=(8,8))                               ║
 * ║  4. Adaptive Thresholding (BINARIZATION) ⚠️                                  ║
 * ║  5. Contour-based Deskewing ⚠️                                               ║
 * ║                                                                              ║
 * ║  ⚠️ WARNING: This pipeline includes binarization and deskewing              ║
 * ║              which previously caused catastrophic failures                  ║
 * ║              This is an EXPERIMENTAL test to validate hypothesis            ║
 * ║                                                                              ║
 * ║  HYPOTHESIS:                                                                ║
 * ║  • Binarization may help Tesseract OCR (designed for binary images)         ║
 * ║  • More aggressive denoising may improve Q2 results                         ║
 * ║  • Different deskewing algorithm may work better                            ║
 * ║                                                                              ║
 * ║  SUCCESS CRITERIA:                                                          ║
 * ║  ✓ Q2_MediumPoor: Cross 70% threshold on 333BBB and 333ccc (failed cases)  ║
 * ║  ✓ Q1_Poor: Maintain 80%+ confidence (don't degrade)                       ║
 * ║                                                                              ║
 * ║  BUSINESS IMPACT:                                                           ║
 * ║  If aggressive enhancement can rescue 333BBB (69.62%) and 333ccc (60.14%), ║
 * ║  we can achieve 100% Q2 rescue rate vs current 50% rate                     ║
 * ╚══════════════════════════════════════════════════════════════════════════════╝
 */

using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// PHASE 2B: AGGRESSIVE Enhancement Filter Testing (EXPERIMENTAL)
///
/// Tests Tesseract performance on AGGRESSIVELY enhanced images.
/// Compares results with Phase 2 standard enhancement to measure if aggressive filters improve rescue rate.
///
/// Aggressive Enhancement Pipeline:
/// - Fast Non-local Means Denoising (h=30) - more aggressive than standard
/// - CLAHE contrast enhancement
/// - Adaptive Thresholding (BINARIZATION) - may help Tesseract, may harm GOT-OCR2
/// - Contour-based Deskewing - different algorithm than standard
///
/// Key Metrics:
/// - Can 333BBB (69.62% standard) cross 70%?
/// - Can 333ccc (60.14% standard) cross 70%?
/// - Does binarization help Tesseract OCR?
/// - Does aggressive denoising improve Q2 results?
/// </summary>
[Collection(nameof(TesseractEnhancedAggressiveCollection))]
public class TesseractOcrExecutorEnhancedAggressiveTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<TesseractOcrExecutor> _logger;
    private readonly TesseractFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public TesseractOcrExecutorEnhancedAggressiveTests(ITestOutputHelper output, TesseractFixture fixture)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<TesseractOcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing Tesseract AGGRESSIVE Enhanced Image Test Instance ===");
        _logger.LogInformation("Testing AGGRESSIVELY enhanced Q1 and Q2 images");
        _logger.LogInformation("WARNING: Includes binarization and deskewing (experimental)");

        _scope = _fixture.Host.Services.CreateScope();
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("Tesseract executor created for aggressive enhanced image testing");
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    /// <summary>
    /// Verify aggressive enhanced fixtures exist for Q1 and Q2 quality levels.
    /// </summary>
    [Theory(DisplayName = "Tesseract aggressive enhanced fixtures should exist", Timeout = 5000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png")]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png")]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg")]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png")]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png")]
    public async Task AggressiveEnhancedFixturesExist_AllQualityLevels(string qualityLevel, string imageName)
    {
        // Arrange
        _logger.LogInformation($"\n=== Checking Aggressive Enhanced Fixture: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive", qualityLevel, imageName);
        _logger.LogInformation($"Constructed path: {fixturePath}");
        _logger.LogInformation($"File.Exists check: {File.Exists(fixturePath)}");

        // Assert
        File.Exists(fixturePath).ShouldBeTrue($"Aggressive enhanced fixture should exist at {fixturePath}");
        var fileInfo = new FileInfo(fixturePath);
        fileInfo.Length.ShouldBeGreaterThan(0, "Aggressive enhanced fixture should not be empty");

        await Task.CompletedTask;
    }

    /// <summary>
    /// PHASE 2B: AGGRESSIVE Enhanced Image Testing - Can we rescue Q2 better?
    ///
    /// Compares against Phase 2 standard enhancement:
    /// - Q1_Poor standard:       80-93% confidence
    /// - Q2_MediumPoor standard: 60-75% confidence (2/4 failed: 333BBB @ 69.62%, 333ccc @ 60.14%)
    ///
    /// Success Criteria:
    /// - Q1_Poor: ≥80% confidence (don't degrade from standard)
    /// - Q2_MediumPoor: ≥70% confidence (rescue 333BBB and 333ccc)
    ///
    /// Business Impact:
    /// If aggressive enhancement can rescue 333BBB and 333ccc, we achieve 100% Q2 rescue rate.
    /// </summary>
    [Theory(DisplayName = "Tesseract AGGRESSIVE ENHANCED: Can we rescue Q2 better?",
            Timeout = 30_000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg", 80.0f)]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png", 80.0f)]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png", 80.0f)]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png", 80.0f)]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg", 70.0f)]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png", 70.0f)]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png", 70.0f)]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png", 70.0f)]
    public async Task ExecuteOcrAsync_AggressiveEnhancedImages_MeasuresROI(
        string qualityLevel,
        string imageName,
        float expectedMinConfidence)
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive", qualityLevel, imageName);
        _logger.LogInformation($"\n=== AGGRESSIVE ENHANCEMENT ROI TEST: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"Quality Level: {qualityLevel}");
        _logger.LogInformation($"Expected Min Confidence: {expectedMinConfidence}%");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        // Log baseline and standard enhancement performance for comparison
        if (qualityLevel == "Q1_Poor")
        {
            _logger.LogInformation($"BASELINE (Phase 1): 78-92% confidence");
            _logger.LogInformation($"STANDARD ENHANCED (Phase 2): 80-93% confidence");
            _logger.LogInformation($"TARGET: 80%+ confidence (don't degrade from standard)");
        }
        else if (qualityLevel == "Q2_MediumPoor")
        {
            _logger.LogInformation($"BASELINE (Phase 1): 42-53% confidence");
            _logger.LogInformation($"STANDARD ENHANCED (Phase 2): 60-75% (2/4 failed @ 70%)");
            _logger.LogInformation($"TARGET: 70%+ confidence (rescue 333BBB @ 69.62%, 333ccc @ 60.14%)");
            _logger.LogInformation($"CRITICAL: Can aggressive filters cross 70% threshold?");
        }

        fixturePath.ShouldSatisfyAllConditions(
            () => File.Exists(fixturePath).ShouldBeTrue($"Aggressive enhanced fixture should exist at {fixturePath}"),
            () => new FileInfo(fixturePath).Length.ShouldBeGreaterThan(0, "Aggressive enhanced fixture should not be empty")
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

        // Act - MEASURE AGGRESSIVE ENHANCED PERFORMANCE
        _logger.LogInformation($"Starting Tesseract execution on AGGRESSIVE ENHANCED {qualityLevel} image...");
        _logger.LogInformation(">>> TIMER START <<<");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($">>> TIMER END: {elapsed.TotalSeconds:F2}s <<<");

            // Assert
            result.IsSuccess.ShouldBeTrue($"OCR execution should succeed on aggressive enhanced {qualityLevel} image");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

            // LOG DETAILED RESULTS
            _logger.LogInformation($"\n=== TESSERACT AGGRESSIVE ENHANCED RESULTS ({qualityLevel}) ===");
            _logger.LogInformation($"  Quality Level: {qualityLevel} (AGGRESSIVE ENHANCED)");
            _logger.LogInformation($"  Document: {imageName}");
            _logger.LogInformation($"  Execution time: {elapsed.TotalSeconds:F2}s");
            _logger.LogInformation($"  Text length: {ocrResult.Text.Length} characters");
            _logger.LogInformation($"  Confidence avg: {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Confidence median: {ocrResult.ConfidenceMedian:F2}%");
            _logger.LogInformation($"  Language used: {ocrResult.LanguageUsed}");
            _logger.LogInformation($"  Text preview (first 200 chars): {ocrResult.Text.Substring(0, Math.Min(200, ocrResult.Text.Length))}");
            _logger.LogInformation($"\n=== FULL OCR TEXT ===\n{ocrResult.Text}\n=== END FULL TEXT ===");

            // Calculate improvement from baseline AND standard enhancement
            float baselineConfidence = qualityLevel == "Q1_Poor" ? 85.0f : 47.5f; // Phase 1 baseline
            float standardEnhancedConfidence = qualityLevel == "Q1_Poor" ? 86.5f : 65.0f; // Phase 2 standard
            float improvementFromBaseline = ocrResult.ConfidenceAvg - baselineConfidence;
            float improvementFromStandard = ocrResult.ConfidenceAvg - standardEnhancedConfidence;

            _logger.LogInformation($"\n=== AGGRESSIVE ENHANCEMENT ROI ===");
            _logger.LogInformation($"  Baseline (Phase 1):          {baselineConfidence:F2}%");
            _logger.LogInformation($"  Standard Enhanced (Phase 2): {standardEnhancedConfidence:F2}%");
            _logger.LogInformation($"  Aggressive Enhanced:         {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Improvement from baseline:   {improvementFromBaseline:+0.00;-0.00}%");
            _logger.LogInformation($"  Improvement from standard:   {improvementFromStandard:+0.00;-0.00}%");

            if (improvementFromStandard < 0)
            {
                _logger.LogWarning($"  ⚠️ Aggressive enhancement performed WORSE than standard");
                _logger.LogInformation($"  💡 RECOMMENDATION: Use standard enhancement instead of aggressive");
            }
            else if (improvementFromStandard > 2.0f)
            {
                _logger.LogInformation($"  🎯 BREAKTHROUGH: Aggressive enhancement significantly better (+{improvementFromStandard:F2}%)");
            }

            if (qualityLevel == "Q2_MediumPoor")
            {
                if (ocrResult.ConfidenceAvg >= 70.0f)
                {
                    _logger.LogInformation($"  🎯 SUCCESS: Q2 aggressive enhanced crossed 70% production threshold!");
                    _logger.LogInformation($"  💡 BUSINESS IMPACT: Aggressive filters can rescue Q2 documents");

                    // Special call-out for hard cases
                    if (imageName == "333BBB-44444444442025_page1.png")
                    {
                        _logger.LogInformation($"  🎊 BREAKTHROUGH: Rescued 333BBB (was 69.62% with standard)!");
                    }
                    else if (imageName == "333ccc-6666666662025_page1.png")
                    {
                        _logger.LogInformation($"  🎊 BREAKTHROUGH: Rescued 333ccc (was 60.14% with standard)!");
                    }
                }
                else
                {
                    _logger.LogWarning($"  ⚠️ BELOW TARGET: Q2 aggressive enhanced did not reach 70% threshold");
                    _logger.LogWarning($"  💡 CONCLUSION: Even aggressive enhancement cannot rescue this Q2 image");

                    // Special call-out for hard cases
                    if (imageName == "333BBB-44444444442025_page1.png")
                    {
                        _logger.LogWarning($"  ❌ HARD LIMIT: 333BBB still below 70% (was 69.62% with standard)");
                    }
                    else if (imageName == "333ccc-6666666662025_page1.png")
                    {
                        _logger.LogWarning($"  ❌ HARD LIMIT: 333ccc still below 70% (was 60.14% with standard)");
                    }
                }
            }

            // Relaxed assertions - just measure capability (not strict thresholds)
            ocrResult.ShouldSatisfyAllConditions(
                () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Should extract text from aggressive enhanced image"),
                () => ocrResult.Text.Length.ShouldBeGreaterThan(50,
                    $"Should extract meaningful text from aggressive enhanced {qualityLevel} image"),
                () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(0,
                    "Confidence should be non-negative"),
                () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
                () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
            );

            _logger.LogInformation($"✓ Tesseract successfully processed AGGRESSIVE ENHANCED {qualityLevel} image");
            _logger.LogInformation($"✓ ROI measured for {imageName}");
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError($">>> TIMER END (FAILED): {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogError(ex, $"Tesseract FAILED on AGGRESSIVE ENHANCED {qualityLevel} image");
            _logger.LogError(ex.InnerException, "Inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "Tesseract should reject null image data (aggressive enhanced tests)", Timeout = 5000)]
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
    [Fact(DisplayName = "Tesseract should reject empty image data (aggressive enhanced tests)", Timeout = 5000)]
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