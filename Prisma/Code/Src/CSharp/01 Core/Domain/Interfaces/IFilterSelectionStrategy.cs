using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the strategy for selecting the optimal image enhancement filter.
/// Implements the Strategy pattern for filter selection based on image quality.
/// </summary>
public interface IFilterSelectionStrategy
{
    /// <summary>
    /// Selects the optimal filter configuration for an image.
    /// </summary>
    /// <param name="assessment">The quality assessment of the image.</param>
    /// <returns>The optimal filter configuration.</returns>
    ImageFilterConfig SelectFilter(ImageQualityAssessment assessment);

    /// <summary>
    /// Selects the optimal filter configuration based on quality level.
    /// </summary>
    /// <param name="qualityLevel">The quality level of the image.</param>
    /// <returns>The optimal filter configuration.</returns>
    ImageFilterConfig SelectFilterByQuality(ImageQualityLevel qualityLevel);

    /// <summary>
    /// Gets the filter configuration for a specific filter type.
    /// </summary>
    /// <param name="filterType">The type of filter requested.</param>
    /// <returns>The filter configuration.</returns>
    ImageFilterConfig GetFilterConfig(ImageFilterType filterType);
}
