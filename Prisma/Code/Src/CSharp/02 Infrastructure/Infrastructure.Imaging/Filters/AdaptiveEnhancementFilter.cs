using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Filters;

/// <summary>
/// Adaptive image enhancement filter that selects between PIL and OpenCV
/// based on image quality analysis.
/// </summary>
public class AdaptiveEnhancementFilter : IImageEnhancementFilter
{
    private readonly ILogger<AdaptiveEnhancementFilter> _logger;
    private readonly IImageQualityAnalyzer _qualityAnalyzer;
    private readonly PilSimpleEnhancementFilter _pilFilter;
    private readonly OpenCvAdvancedEnhancementFilter _openCvFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveEnhancementFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="qualityAnalyzer">The image quality analyzer.</param>
    /// <param name="pilFilter">The PIL filter implementation.</param>
    /// <param name="openCvFilter">The OpenCV filter implementation.</param>
    public AdaptiveEnhancementFilter(
        ILogger<AdaptiveEnhancementFilter> logger,
        IImageQualityAnalyzer qualityAnalyzer,
        PilSimpleEnhancementFilter pilFilter,
        OpenCvAdvancedEnhancementFilter openCvFilter)
    {
        _logger = logger;
        _qualityAnalyzer = qualityAnalyzer;
        _pilFilter = pilFilter;
        _openCvFilter = openCvFilter;
    }

    /// <inheritdoc />
    public ImageFilterType FilterType => ImageFilterType.Adaptive;

    /// <inheritdoc />
    public string FilterName => "Adaptive Enhancement";

    /// <inheritdoc />
    public async Task<Result<ImageData>> EnhanceAsync(ImageData imageData, ImageFilterConfig config)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            if (!config.EnableEnhancement)
            {
                _logger.LogDebug("Enhancement disabled, returning original image");
                return Result<ImageData>.Success(imageData);
            }

            // Analyze image quality to determine optimal filter
            var assessmentResult = await _qualityAnalyzer.AnalyzeAsync(imageData);
            if (!assessmentResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Quality analysis failed, defaulting to PIL filter: {Error}",
                    assessmentResult.Error);
                return await _pilFilter.EnhanceAsync(imageData, config);
            }

            var assessment = assessmentResult.Value!;
            _logger.LogInformation(
                "Quality assessment: Level={QualityLevel}, Recommended={RecommendedFilter}, Confidence={Confidence}",
                assessment.QualityLevel, assessment.RecommendedFilter, assessment.Confidence);

            // Select and apply the recommended filter
            var selectedFilter = SelectFilter(assessment.RecommendedFilter, config);
            return await selectedFilter.EnhanceAsync(imageData, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in adaptive enhancement, falling back to PIL filter");
            return await _pilFilter.EnhanceAsync(imageData, config);
        }
    }

    /// <inheritdoc />
    public bool CanProcess(ImageData imageData)
    {
        return imageData?.Data != null && imageData.Data.Length > 0;
    }

    /// <summary>
    /// Selects the appropriate filter based on the recommended type.
    /// </summary>
    private IImageEnhancementFilter SelectFilter(ImageFilterType recommended, ImageFilterConfig config) => recommended.Value switch
    {
        2 => _openCvFilter,
        1 => _pilFilter,
        0 => new NoOpEnhancementFilter(),
        _ => _pilFilter  // Default to PIL for Q2 optimization
    };
}
