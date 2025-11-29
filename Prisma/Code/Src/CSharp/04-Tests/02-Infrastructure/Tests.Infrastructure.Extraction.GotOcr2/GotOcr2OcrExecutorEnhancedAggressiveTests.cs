namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// GOT-OCR2 performance tests on AGGRESSIVE enhanced images (Q1+Q2 with aggressive filters applied).
///
/// Tests OCR accuracy on aggressively enhanced images to measure if aggressive filters
/// can rescue Q2 images better than standard enhancement.
///
/// AGGRESSIVE ENHANCEMENT PIPELINE (EXPERIMENTAL):
/// - Fast Non-local Means Denoising (h=30) - more aggressive than standard
/// - CLAHE contrast enhancement
/// - Adaptive Thresholding (BINARIZATION) ⚠️ - may harm ML-based OCR
/// - Contour-based Deskewing ⚠️ - previously caused catastrophic failures
///
/// GOAL: Measure if aggressive filters improve OCR confidence vs standard enhancement.
/// COMPARE: Results against standard enhanced (PRP1_Enhanced) and degraded baseline.
///
/// KEY QUESTION: Can 333BBB (69.62% standard) and 333ccc (60.14% standard) reach 70%?
///
/// WARNING: Binarization may harm GOT-OCR2 (ML model expects grayscale).
///          This is an EXPERIMENTAL test to validate hypothesis.
///
/// NO EXTRACTION - Just OCR confidence measurement.
/// </summary>
[Collection(nameof(GotOcr2DegradedCollection))]
public class GotOcr2OcrExecutorEnhancedAggressiveTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GotOcr2OcrExecutor> _logger;
    private readonly GotOcr2Fixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public GotOcr2OcrExecutorEnhancedAggressiveTests(ITestOutputHelper output, GotOcr2Fixture fixture)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        _output = output;
        _logger = XUnitLogger.CreateLogger<GotOcr2OcrExecutor>(output);
        _fixture = fixture;

        _logger.LogInformation("=== Initializing GOT-OCR2 AGGRESSIVE Enhanced Image Test Instance ===");
        _logger.LogInformation("Using shared Python environment from collection fixture");
        _logger.LogInformation("WARNING: Testing AGGRESSIVE enhancement (binarization + deskewing)");

        _scope = _fixture.Host.Services.CreateScope();
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _logger.LogInformation("GOT-OCR2 executor created for aggressive enhanced image testing");
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    /// <summary>
    /// Verify aggressive enhanced fixtures exist for Q1 and Q2.
    /// </summary>
    [Theory(DisplayName = "GOT-OCR2 aggressive enhanced fixtures should exist",
            Skip = "GotOcr2 feature frozen - tests disabled",
            Timeout = 5000)]
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
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        _logger.LogInformation($"\n=== Checking Aggressive Enhanced Fixture: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive", qualityLevel, imageName);
        _logger.LogInformation($"Constructed path: {fixturePath}");
        _logger.LogInformation($"File.Exists check: {File.Exists(fixturePath)}");

        // Check parent directories
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        _logger.LogInformation($"Fixtures directory exists: {Directory.Exists(fixturesDir)}");

        var prp1AggressiveDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive");
        _logger.LogInformation($"PRP1_Enhanced_Aggressive directory exists: {Directory.Exists(prp1AggressiveDir)}");

        var qualityDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive", qualityLevel);
        _logger.LogInformation($"{qualityLevel} directory exists: {Directory.Exists(qualityDir)}");

        if (Directory.Exists(qualityDir))
        {
            var files = Directory.GetFiles(qualityDir);
            _logger.LogInformation($"Files in {qualityLevel}: {string.Join(", ", files.Select(Path.GetFileName))}");
        }

        // Assert
        File.Exists(fixturePath).ShouldBeTrue($"Aggressive enhanced fixture should exist at {fixturePath}");
        var fileInfo = new FileInfo(fixturePath);
        fileInfo.Length.ShouldBeGreaterThan(0, "Aggressive enhanced fixture should not be empty");

        await Task.CompletedTask;
    }

    /// <summary>
    /// AGGRESSIVE ENHANCEMENT ROI TEST: GOT-OCR2 on aggressively enhanced images (Q1+Q2).
    ///
    /// Measures:
    /// - Text extraction quality after aggressive enhancement filters
    /// - Confidence score improvements vs standard enhancement
    /// - Execution time on aggressive enhanced images
    /// - ROI validation for aggressive filter pipeline
    ///
    /// Expected behavior:
    /// - Q1_Poor Aggressive: May degrade from standard (binarization harms ML OCR)
    /// - Q2_MediumPoor Aggressive: Can 333BBB (69.62% standard) and 333ccc (60.14% standard) reach 70%?
    ///
    /// CRITICAL TEST: Does binarization harm GOT-OCR2 (ML model expects grayscale)?
    /// </summary>
    [Theory(DisplayName = "GOT-OCR2 aggressive enhancement ROI test on filtered images",
            Skip = "GotOcr2 feature frozen - tests disabled",
            Timeout = 3_000_000)]
    [InlineData("Q1_Poor", "222AAA-44444444442025_page-0001.jpg", 70.0f)]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png", 70.0f)]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png", 70.0f)]
    [InlineData("Q1_Poor", "555CCC-66666662025_page1.png", 70.0f)]
    [InlineData("Q2_MediumPoor", "222AAA-44444444442025_page-0001.jpg", 60.0f)]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png", 60.0f)]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png", 60.0f)]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png", 60.0f)]
    public async Task ExecuteOcrAsync_AggressiveEnhancedImages_MeasuresRoi(
        string qualityLevel,
        string imageName,
        float expectedMinConfidence)
    {
        Assert.Skip("Slow test (~140s per image × 16 = ~37 mins). Enable manually for robustness testing.");

        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PRP1_Enhanced_Aggressive", qualityLevel, imageName);
        _logger.LogInformation($"\n=== AGGRESSIVE ENHANCEMENT ROI TEST: {qualityLevel}/{imageName} ===");
        _logger.LogInformation($"Quality Level: {qualityLevel}");
        _logger.LogInformation($"Expected Min Confidence: {expectedMinConfidence}%");
        _logger.LogInformation($"Fixture path: {fixturePath}");

        // Log baseline and standard enhancement performance for comparison
        if (qualityLevel == "Q1_Poor")
        {
            _logger.LogInformation($"BASELINE (degraded): 78-92% confidence");
            _logger.LogInformation($"STANDARD ENHANCED: 80-93% confidence");
            _logger.LogInformation($"HYPOTHESIS: Binarization may HARM GOT-OCR2 (ML model expects grayscale)");
        }
        else if (qualityLevel == "Q2_MediumPoor")
        {
            _logger.LogInformation($"BASELINE (degraded): 42-53% confidence");
            _logger.LogInformation($"STANDARD ENHANCED: 60-75% (2/4 failed: 333BBB @ 69.62%, 333ccc @ 60.14%)");
            _logger.LogInformation($"CRITICAL: Can aggressive filters rescue 333BBB and 333ccc to 70%+?");
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

        // Act - MEASURE PERFORMANCE ON AGGRESSIVE ENHANCED IMAGE
        _logger.LogInformation($"Starting GOT-OCR2 execution on {qualityLevel} AGGRESSIVE enhanced...");
        _logger.LogInformation(">>> TIMER START <<<");
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _executor.ExecuteOcrAsync(imageData, config);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($">>> TIMER END: {elapsed.TotalSeconds:F2}s <<<");

            // Assert
            result.IsSuccess.ShouldBeTrue($"OCR execution should succeed on {qualityLevel} aggressive enhanced");
            result.Value.ShouldNotBeNull("OCR result should not be null");

            var ocrResult = result.Value;

            // LOG DETAILED RESULTS
            _logger.LogInformation($"\n=== GOT-OCR2 AGGRESSIVE ENHANCED RESULTS ({qualityLevel}) ===");
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
            Dictionary<string, float> standardEnhancedScores = new()
            {
                // Q1_Poor standard enhanced scores (from Phase 2 results)
                ["Q1_Poor_222AAA"] = 85.73f,
                ["Q1_Poor_333BBB"] = 83.58f,
                ["Q1_Poor_333ccc"] = 80.84f,
                ["Q1_Poor_555CCC"] = 93.11f,
                // Q2_MediumPoor standard enhanced scores (from Phase 2 results)
                ["Q2_MediumPoor_222AAA"] = 74.13f,
                ["Q2_MediumPoor_333BBB"] = 69.62f, // THE HEARTBREAKER - 0.38% short
                ["Q2_MediumPoor_333ccc"] = 60.14f, // HARD CASE - 9.86% short
                ["Q2_MediumPoor_555CCC"] = 75.57f,
            };

            var key = $"{qualityLevel}_{Path.GetFileNameWithoutExtension(imageName).Split('-')[0]}";
            float standardScore = standardEnhancedScores.GetValueOrDefault(key, 0f);
            float improvementFromStandard = ocrResult.ConfidenceAvg - standardScore;

            _logger.LogInformation($"\n=== AGGRESSIVE ENHANCEMENT ROI ===");
            _logger.LogInformation($"  Standard Enhanced:   {standardScore:F2}%");
            _logger.LogInformation($"  Aggressive Enhanced: {ocrResult.ConfidenceAvg:F2}%");
            _logger.LogInformation($"  Improvement:         {improvementFromStandard:+0.00;-0.00}%");

            if (improvementFromStandard < 0)
            {
                _logger.LogWarning($"  ⚠️ Aggressive enhancement performed WORSE than standard ({improvementFromStandard:F2}%)");
                _logger.LogInformation($"  💡 CONCLUSION: Binarization likely harmed ML-based OCR");
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

                    // Special call-out for hard cases
                    if (imageName.Contains("333BBB"))
                    {
                        _logger.LogInformation($"  🎊 BREAKTHROUGH: Rescued 333BBB from {standardScore:F2}% → {ocrResult.ConfidenceAvg:F2}% (was 0.38% short)!");
                    }
                    else if (imageName.Contains("333ccc"))
                    {
                        _logger.LogInformation($"  🎊 BREAKTHROUGH: Rescued 333ccc from {standardScore:F2}% → {ocrResult.ConfidenceAvg:F2}% (was 9.86% short)!");
                    }
                }
                else
                {
                    _logger.LogWarning($"  ⚠️ BELOW TARGET: Q2 aggressive enhanced did not reach 70% threshold");

                    // Special call-out for hard cases
                    if (imageName.Contains("333BBB"))
                    {
                        _logger.LogWarning($"  ❌ HARD LIMIT CONFIRMED: 333BBB @ {ocrResult.ConfidenceAvg:F2}% (standard: {standardScore:F2}%, gap: {70.0f - ocrResult.ConfidenceAvg:F2}%)");
                    }
                    else if (imageName.Contains("333ccc"))
                    {
                        _logger.LogWarning($"  ❌ HARD LIMIT CONFIRMED: 333ccc @ {ocrResult.ConfidenceAvg:F2}% (standard: {standardScore:F2}%, gap: {70.0f - ocrResult.ConfidenceAvg:F2}%)");
                    }
                }
            }

            // Assertions for aggressive enhanced images (relaxed - just measure capability)
            ocrResult.ShouldSatisfyAllConditions(
                () => ocrResult.Text.ShouldNotBeNullOrWhiteSpace("Should extract text from aggressive enhanced image"),
                () => ocrResult.Text.Length.ShouldBeGreaterThan(50,
                    $"Should extract meaningful text from {qualityLevel} aggressive enhanced (minimum 50 chars)"),
                () => ocrResult.ConfidenceAvg.ShouldBeGreaterThanOrEqualTo(0,
                    "Confidence should be non-negative"),
                () => ocrResult.Confidences.ShouldNotBeEmpty("Confidence list should not be empty"),
                () => ocrResult.LanguageUsed.ShouldBe("spa", "Should use Spanish as primary language")
            );

            _logger.LogInformation($"✓ GOT-OCR2 successfully processed {qualityLevel} AGGRESSIVE enhanced image");
            _logger.LogInformation($"✓ Aggressive enhancement ROI measured for {imageName}");
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError($">>> TIMER END (FAILED): {elapsed.TotalSeconds:F2}s <<<");
            _logger.LogError(ex, $"GOT-OCR2 FAILED on {qualityLevel} aggressive enhanced");
            _logger.LogError(ex.InnerException, "Inner exception");
            throw;
        }
    }

    /// <summary>
    /// Tests that the executor rejects null image data (contract validation).
    /// </summary>
    [Fact(DisplayName = "GOT-OCR2 should reject null image data (aggressive enhanced tests)",
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
    [Fact(DisplayName = "GOT-OCR2 should reject empty image data (aggressive enhanced tests)",
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