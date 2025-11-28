/*
 * ╔══════════════════════════════════════════════════════════════════════════════╗
 * ║              ANALYTICAL FILTER SELECTION STRATEGY E2E TESTS                  ║
 * ╠══════════════════════════════════════════════════════════════════════════════╣
 * ║  PURPOSE: Validate end-to-end OCR improvement with analytical filter         ║
 * ║           selection based on NSGA-II optimization results                    ║
 * ║                                                                              ║
 * ║  TEST FLOW:                                                                  ║
 * ║  1. Load degraded image from Q2_MediumPoor spectrum                         ║
 * ║  2. Perform baseline OCR (no filter)                                        ║
 * ║  3. Calculate Levenshtein distance vs ground truth                          ║
 * ║  4. Analyze image quality metrics                                           ║
 * ║  5. Use AnalyticalFilterSelectionStrategy to select filter                  ║
 * ║  6. Apply selected filter to image                                          ║
 * ║  7. Perform OCR on enhanced image                                           ║
 * ║  8. Calculate new Levenshtein distance                                      ║
 * ║  9. Assert: enhanced distance < baseline distance (ANY improvement)         ║
 * ║                                                                              ║
 * ║  EVIDENCE BASE:                                                             ║
 * ║  • Baseline testing: 820 OCR runs (41 filters × 20 images)                  ║
 * ║  • Q2 MediumPoor baseline: 6,590 edits average                              ║
 * ║  • Q2 with analytical filter: 1,444 edits (78.1% improvement!)              ║
 * ║  • NSGA-II optimized parameters: contrast=1.157, median=3                   ║
 * ║                                                                              ║
 * ║  SUCCESS CRITERIA:                                                          ║
 * ║  ✓ Enhanced Levenshtein distance < Baseline distance                        ║
 * ║  ✓ Any improvement validates analytical strategy                            ║
 * ║  ✓ Detailed logging shows decision process                                  ║
 * ╚══════════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.GotOcr2;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// End-to-end tests validating OCR improvement with analytical filter selection.
///
/// These tests demonstrate the complete workflow:
/// 1. Baseline OCR on degraded image
/// 2. Quality analysis
/// 3. Filter selection using analytical strategy
/// 4. Filter application
/// 5. Enhanced OCR
/// 6. Improvement measurement using Levenshtein distance
///
/// Expected Results (based on baseline testing):
/// - Q2 MediumPoor: 78.1% improvement (6,590 → 1,444 edits)
/// - Q1 Poor: 24.9% improvement (538 → 404 edits)
/// </summary>
[Collection(nameof(AnalyticalFilterE2ECollection))]
public class AnalyticalFilterE2ETests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AnalyticalFilterE2ETests> _logger;
    private readonly TesseractFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFilterSelectionStrategy _filterStrategy;
    private readonly IImageQualityAnalyzer _qualityAnalyzer;

    /// <summary>
    /// Ground truth text for each test document.
    /// Obtained from baseline testing on pristine images.
    /// </summary>
    private static readonly Dictionary<string, string> GroundTruth = new()
    {
        ["222AAA"] = "EXPEDIENTE 222AAA-44444444442025\nFECHA: 15 DE ENERO DE 2025\nFOLIO: 001234\n\nDOCUMENTACIÓN REQUERIDA:\n• Identificación oficial vigente\n• Comprobante de domicilio reciente\n• Estado de cuenta bancario\n• RFC actualizado\n\nOBSERVACIONES:\nLa documentación debe presentarse en original y copia.\nTodos los documentos deben estar vigentes.\nLa verificación se realizará en un plazo de 48 horas.\n\nFIRMA AUTORIZADA:\n______________________\nDEPARTAMENTO DE VERIFICACIÓN",

        ["333BBB"] = "EXPEDIENTE 333BBB-44444444442025\nFECHA: 20 DE ENERO DE 2025\nFOLIO: 002456\n\nPROCESO DE VALIDACIÓN:\n1. Recepción de documentos\n2. Verificación de autenticidad\n3. Análisis de cumplimiento normativo\n4. Emisión de dictamen\n\nRESULTADO: APROBADO\n\nCONDICIONES:\n• Vigencia de 12 meses\n• Renovación obligatoria antes del vencimiento\n• Notificación de cambios en 30 días\n\nATENTAMENTE:\n______________________\nOFICIALÍA DE DOCUMENTACIÓN",

        ["333ccc"] = "EXPEDIENTE 333CCC-6666666662025\nFECHA: 25 DE ENERO DE 2025\nFOLIO: 003789\n\nREQUISITOS TÉCNICOS:\n• Formato PDF/A para archivo digital\n• Resolución mínima 300 DPI\n• Tamaño máximo 10 MB por archivo\n• Nomenclatura estandarizada\n\nCLASIFICACIÓN: CONFIDENCIAL\n\nTRATAMIENTO DE DATOS:\nLa información contenida es de carácter confidencial.\nEl acceso está restringido a personal autorizado.\nLa difusión no autorizada está sancionada.\n\nSELLO OFICIAL:\n______________________\nDEPARTAMENTO DE ARCHIVO",

        ["555CCC"] = "EXPEDIENTE 555CCC-66666662025\nFECHA: 30 DE ENERO DE 2025\nFOLIO: 004512\n\nVERIFICACIÓN DE CUMPLIMIENTO:\n✓ Documentación completa\n✓ Firmas validadas\n✓ Sellos auténticos\n✓ Fechas consistentes\n\nESTATUS: VALIDADO\n\nSEGUIMIENTO:\nNúmero de seguimiento: VLD-2025-001234\nConsultar en: www.verificacion.gob.mx\nVigencia: 12 meses a partir de la fecha\n\nVALIDACIÓN:\n______________________\nCOORDINACIÓN DE VALIDACIÓN"
    };

    public AnalyticalFilterE2ETests(ITestOutputHelper output, TesseractFixture fixture)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<AnalyticalFilterE2ETests>(output);
        _fixture = fixture;

        _logger.LogInformation("╔══════════════════════════════════════════════════════════════════╗");
        _logger.LogInformation("║         ANALYTICAL FILTER E2E TEST - INITIALIZATION            ║");
        _logger.LogInformation("╚══════════════════════════════════════════════════════════════════╝");

        _scope = _fixture.Host.Services.CreateScope();
        _ocrExecutor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();

        // Get analytical filter strategy
        _filterStrategy = new AnalyticalFilterSelectionStrategy();
        _logger.LogInformation("✓ AnalyticalFilterSelectionStrategy initialized (data-driven, 820 OCR runs)");

        // Get quality analyzer (if available)
        _qualityAnalyzer = _scope.ServiceProvider.GetService<IImageQualityAnalyzer>()
            ?? throw new InvalidOperationException("IImageQualityAnalyzer not registered");
        _logger.LogInformation("✓ IImageQualityAnalyzer initialized");

        _logger.LogInformation("✓ IOcrExecutor initialized (Tesseract)");
        _logger.LogInformation("");
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }

    /// <summary>
    /// Calculates Levenshtein edit distance between two strings.
    /// This is the classic dynamic programming algorithm with O(m*n) time complexity.
    ///
    /// The Levenshtein distance is the minimum number of single-character edits
    /// (insertions, deletions, or substitutions) required to change one string into another.
    ///
    /// Lower distance = higher similarity = better OCR quality.
    /// </summary>
    /// <param name="source">First string (ground truth).</param>
    /// <param name="target">Second string (OCR result).</param>
    /// <returns>Minimum edit distance.</returns>
    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        int m = source.Length;
        int n = target.Length;

        // Create distance matrix
        int[,] distance = new int[m + 1, n + 1];

        // Initialize first row and column
        for (int i = 0; i <= m; i++)
            distance[i, 0] = i;

        for (int j = 0; j <= n; j++)
            distance[0, j] = j;

        // Fill matrix using dynamic programming
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // Deletion
                        distance[i, j - 1] + 1),     // Insertion
                    distance[i - 1, j - 1] + cost);  // Substitution
            }
        }

        return distance[m, n];
    }

    /// <summary>
    /// Extracts document ID from filename (e.g., "333BBB-44444444442025_page1.png" → "333BBB").
    /// </summary>
    private static string ExtractDocumentId(string filename)
    {
        var parts = Path.GetFileNameWithoutExtension(filename).Split('-');
        return parts.Length > 0 ? parts[0] : filename;
    }

    /// <summary>
    /// E2E test validating analytical filter selection improves OCR quality.
    ///
    /// Test Flow:
    /// 1. Load degraded image
    /// 2. Baseline OCR (no filter) + Levenshtein distance
    /// 3. Quality analysis
    /// 4. Analytical filter selection
    /// 5. Apply filter
    /// 6. Enhanced OCR + Levenshtein distance
    /// 7. Assert improvement
    ///
    /// Expected Results (from baseline testing):
    /// - Q2: 78.1% improvement (6,590 → 1,444 edits)
    /// - Q1: 24.9% improvement (538 → 404 edits)
    /// </summary>
    [Theory(DisplayName = "Analytical filter should improve OCR quality (Levenshtein distance)", Timeout = 300000)]
    [InlineData("Q2_MediumPoor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q2_MediumPoor", "333ccc-6666666662025_page1.png")]
    [InlineData("Q2_MediumPoor", "555CCC-66666662025_page1.png")]
    [InlineData("Q1_Poor", "333BBB-44444444442025_page1.png")]
    [InlineData("Q1_Poor", "333ccc-6666666662025_page1.png")]
    public async Task AnalyticalFilter_ShouldImproveOcrQuality_MeasuredByLevenshteinDistance(
        string qualityLevel,
        string filename)
    {
        // ═══════════════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("╔══════════════════════════════════════════════════════════════════╗");
        _logger.LogInformation("║                    TEST: {Level,-12} | {File,-40} ║", qualityLevel, filename);
        _logger.LogInformation("╚══════════════════════════════════════════════════════════════════╝");
        _logger.LogInformation("");

        var stopwatch = Stopwatch.StartNew();

        // Get paths
        var degradedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "PRP1_Degraded",
            qualityLevel,
            filename);

        _logger.LogInformation("📁 Image Path: {Path}", degradedPath);

        File.Exists(degradedPath).ShouldBeTrue($"Degraded image not found: {degradedPath}");

        // Load image
        var degradedImageData = new ImageData(
            await File.ReadAllBytesAsync(degradedPath, TestContext.Current.CancellationToken),
            degradedPath);

        _logger.LogInformation("✓ Image loaded: {Size:N0} bytes", degradedImageData.Data.Length);

        // Get ground truth
        var documentId = ExtractDocumentId(filename);
        GroundTruth.ContainsKey(documentId).ShouldBeTrue($"No ground truth for document: {documentId}");
        var groundTruth = GroundTruth[documentId];

        _logger.LogInformation("✓ Ground truth loaded: {Chars} characters", groundTruth.Length);
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ACT 1: BASELINE OCR (NO FILTER)
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("┌──────────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("│ STEP 1: BASELINE OCR (NO FILTER)                                │");
        _logger.LogInformation("└──────────────────────────────────────────────────────────────────┘");

        var baselineStopwatch = Stopwatch.StartNew();
        var baselineConfig = new OCRConfig { Language = "spa" };
        var baselineResult = await _ocrExecutor.ExecuteOcrAsync(degradedImageData, baselineConfig);
        baselineStopwatch.Stop();

        baselineResult.IsSuccess.ShouldBeTrue("Baseline OCR should succeed");
        var baselineText = baselineResult.Value!.Text;

        _logger.LogInformation("✓ Baseline OCR completed: {Ms}ms", baselineStopwatch.ElapsedMilliseconds);
        _logger.LogInformation("  Extracted {Chars} characters", baselineText.Length);

        // Calculate baseline Levenshtein distance
        var baselineDistance = CalculateLevenshteinDistance(groundTruth, baselineText);
        var baselineErrorRate = (double)baselineDistance / groundTruth.Length * 100;

        _logger.LogInformation("");
        _logger.LogInformation("📊 BASELINE METRICS:");
        _logger.LogInformation("  Levenshtein Distance: {Distance} edits", baselineDistance);
        _logger.LogInformation("  Error Rate: {Rate:F2}% ({Dist}/{Total} chars)",
            baselineErrorRate, baselineDistance, groundTruth.Length);
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ACT 2: QUALITY ANALYSIS
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("┌──────────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("│ STEP 2: IMAGE QUALITY ANALYSIS                                  │");
        _logger.LogInformation("└──────────────────────────────────────────────────────────────────┘");

        var assessmentResult = await _qualityAnalyzer.AnalyzeAsync(degradedImageData);
        assessmentResult.IsSuccess.ShouldBeTrue("Quality analysis should succeed");
        var assessment = assessmentResult.Value!;

        _logger.LogInformation("✓ Quality analysis completed");
        _logger.LogInformation("  Quality Level: {Level}", assessment.QualityLevel);
        _logger.LogInformation("  Metrics:");
        _logger.LogInformation("    Blur Score:     {Score:F2}", assessment.BlurScore);
        _logger.LogInformation("    Noise Level:    {Level:F2}", assessment.NoiseLevel);
        _logger.LogInformation("    Contrast Level: {Level:F2}", assessment.ContrastLevel);
        _logger.LogInformation("    Sharpness:      {Sharpness:F2}", assessment.SharpnessLevel);
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ACT 3: ANALYTICAL FILTER SELECTION
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("┌──────────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("│ STEP 3: ANALYTICAL FILTER SELECTION                             │");
        _logger.LogInformation("└──────────────────────────────────────────────────────────────────┘");

        var filterConfig = _filterStrategy.SelectFilter(assessment);

        _logger.LogInformation("✓ Filter selected by analytical strategy (820 OCR baseline runs)");
        _logger.LogInformation("  Filter Type: {Type}", filterConfig.FilterType);
        _logger.LogInformation("  Enhancement Enabled: {Enabled}", filterConfig.EnableEnhancement);

        if (filterConfig.FilterType == ImageFilterType.PilSimple && filterConfig.PilParams != null)
        {
            _logger.LogInformation("  PIL Parameters (NSGA-II optimized):");
            _logger.LogInformation("    Contrast Factor: {Factor:F4}", filterConfig.PilParams.ContrastFactor);
            _logger.LogInformation("    Median Size: {Size}", filterConfig.PilParams.MedianSize);
        }
        else if (filterConfig.FilterType == ImageFilterType.OpenCvAdvanced && filterConfig.OpenCvParams != null)
        {
            _logger.LogInformation("  OpenCV Parameters:");
            _logger.LogInformation("    Denoise H: {H}", filterConfig.OpenCvParams.DenoiseH);
            _logger.LogInformation("    CLAHE Clip: {Clip:F2}", filterConfig.OpenCvParams.ClaheClip);
        }
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ACT 4: APPLY FILTER
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("┌──────────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("│ STEP 4: APPLY SELECTED FILTER                                   │");
        _logger.LogInformation("└──────────────────────────────────────────────────────────────────┘");

        ImageData enhancedImageData = degradedImageData;

        if (filterConfig.EnableEnhancement && filterConfig.FilterType != ImageFilterType.None)
        {
            var filterStopwatch = Stopwatch.StartNew();

            // Get appropriate filter from service provider
            IImageEnhancementFilter? filter = null;

            if (filterConfig.FilterType == ImageFilterType.PilSimple)
            {
                filter = _scope.ServiceProvider.GetKeyedService<IImageEnhancementFilter>(ImageFilterType.PilSimple);
            }
            else if (filterConfig.FilterType == ImageFilterType.OpenCvAdvanced)
            {
                filter = _scope.ServiceProvider.GetKeyedService<IImageEnhancementFilter>(ImageFilterType.OpenCvAdvanced);
            }

            filter.ShouldNotBeNull($"Filter {filterConfig.FilterType} should be registered");

            var enhancementResult = await filter.EnhanceAsync(degradedImageData, filterConfig);
            filterStopwatch.Stop();

            enhancementResult.IsSuccess.ShouldBeTrue("Filter enhancement should succeed");
            enhancedImageData = enhancementResult.Value!;

            _logger.LogInformation("✓ Filter applied: {Ms}ms", filterStopwatch.ElapsedMilliseconds);
            _logger.LogInformation("  Enhanced image: {Size:N0} bytes", enhancedImageData.Data.Length);
        }
        else
        {
            _logger.LogInformation("⚠ NO FILTER applied (pristine image detected)");
            _logger.LogInformation("  Strategy determined filtering would DEGRADE quality");
        }
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ACT 5: ENHANCED OCR
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("┌──────────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("│ STEP 5: ENHANCED OCR                                            │");
        _logger.LogInformation("└──────────────────────────────────────────────────────────────────┘");

        var enhancedStopwatch = Stopwatch.StartNew();
        var enhancedConfig = new OCRConfig { Language = "spa" };
        var enhancedResult = await _ocrExecutor.ExecuteOcrAsync(enhancedImageData, enhancedConfig);
        enhancedStopwatch.Stop();

        enhancedResult.IsSuccess.ShouldBeTrue("Enhanced OCR should succeed");
        var enhancedText = enhancedResult.Value!.Text;

        _logger.LogInformation("✓ Enhanced OCR completed: {Ms}ms", enhancedStopwatch.ElapsedMilliseconds);
        _logger.LogInformation("  Extracted {Chars} characters", enhancedText.Length);

        // Calculate enhanced Levenshtein distance
        var enhancedDistance = CalculateLevenshteinDistance(groundTruth, enhancedText);
        var enhancedErrorRate = (double)enhancedDistance / groundTruth.Length * 100;

        _logger.LogInformation("");
        _logger.LogInformation("📊 ENHANCED METRICS:");
        _logger.LogInformation("  Levenshtein Distance: {Distance} edits", enhancedDistance);
        _logger.LogInformation("  Error Rate: {Rate:F2}% ({Dist}/{Total} chars)",
            enhancedErrorRate, enhancedDistance, groundTruth.Length);
        _logger.LogInformation("");

        // ═══════════════════════════════════════════════════════════════════
        // ASSERT & RESULTS
        // ═══════════════════════════════════════════════════════════════════
        stopwatch.Stop();

        var improvement = baselineDistance - enhancedDistance;
        var improvementPercent = baselineDistance > 0
            ? (double)improvement / baselineDistance * 100
            : 0;

        _logger.LogInformation("╔══════════════════════════════════════════════════════════════════╗");
        _logger.LogInformation("║                        FINAL RESULTS                             ║");
        _logger.LogInformation("╠══════════════════════════════════════════════════════════════════╣");
        _logger.LogInformation("║ Baseline Distance:   {Distance,8} edits                             ║", baselineDistance);
        _logger.LogInformation("║ Enhanced Distance:   {Distance,8} edits                             ║", enhancedDistance);
        _logger.LogInformation("║ Improvement:         {Improvement,8} edits ({Percent,6:F2}%)                  ║",
            improvement, improvementPercent);
        _logger.LogInformation("║                                                                  ║");
        _logger.LogInformation("║ Total Time:          {Ms,8}ms                              ║", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation("║   Baseline OCR:      {Ms,8}ms                              ║", baselineStopwatch.ElapsedMilliseconds);
        _logger.LogInformation("║   Enhanced OCR:      {Ms,8}ms                              ║", enhancedStopwatch.ElapsedMilliseconds);
        _logger.LogInformation("╚══════════════════════════════════════════════════════════════════╝");
        _logger.LogInformation("");

        // CRITICAL ASSERTION: Enhanced should be better (lower distance) than baseline
        // ANY improvement validates the analytical filter selection strategy
        enhancedDistance.ShouldBeLessThan(baselineDistance,
            $"Enhanced Levenshtein distance ({enhancedDistance}) should be less than baseline ({baselineDistance}). " +
            $"Expected improvement based on baseline testing: Q2=78.1%, Q1=24.9%");

        _logger.LogInformation("✅ TEST PASSED: Analytical filter improved OCR quality by {Percent:F2}%", improvementPercent);
        _logger.LogInformation("");
    }
}
