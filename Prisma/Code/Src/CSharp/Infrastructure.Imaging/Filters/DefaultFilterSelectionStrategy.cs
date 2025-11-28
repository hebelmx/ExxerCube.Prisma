using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Filters;

/// <summary>
/// Default implementation of filter selection strategy.
/// Uses NSGA-II optimized parameters based on quality level.
/// </summary>
public class DefaultFilterSelectionStrategy : IFilterSelectionStrategy
{
    /// <inheritdoc />
    public ImageFilterConfig SelectFilter(ImageQualityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        // Use the recommended filter from quality analysis
        var config = GetFilterConfig(assessment.RecommendedFilter);

        // Adjust parameters based on specific quality characteristics
        if (assessment.NoiseLevel > 0.7f && config.FilterType == ImageFilterType.PilSimple)
        {
            // High noise - increase median filter size
            config.PilParams.MedianSize = 5;
        }

        if (assessment.ContrastLevel < 0.3f)
        {
            // Low contrast - increase contrast factor
            config.PilParams.ContrastFactor = Math.Min(config.PilParams.ContrastFactor * 1.2f, 2.5f);
        }

        return config;
    }

    /// <inheritdoc />
    public ImageFilterConfig SelectFilterByQuality(ImageQualityLevel qualityLevel)
    {
        return qualityLevel switch
        {
            ImageQualityLevel.Q1_Poor => CreateQ1Config(),
            ImageQualityLevel.Q2_MediumPoor => ImageFilterConfig.CreateQ2Optimized(),
            ImageQualityLevel.Q3_Low => CreateQ3Config(),
            ImageQualityLevel.Q4_VeryLow => CreateQ4Config(),
            ImageQualityLevel.Pristine => CreatePristineConfig(),
            _ => ImageFilterConfig.CreateQ2Optimized()
        };
    }

    /// <inheritdoc />
    public ImageFilterConfig GetFilterConfig(ImageFilterType filterType)
    {
        return filterType switch
        {
            ImageFilterType.None => new ImageFilterConfig
            {
                FilterType = ImageFilterType.None,
                EnableEnhancement = false
            },
            ImageFilterType.PilSimple => ImageFilterConfig.CreateQ2Optimized(),
            ImageFilterType.OpenCvAdvanced => new ImageFilterConfig
            {
                FilterType = ImageFilterType.OpenCvAdvanced,
                EnableEnhancement = true,
                OpenCvParams = OpenCvFilterParams.CreateDefault()
            },
            ImageFilterType.Adaptive => ImageFilterConfig.CreateAdaptive(),
            _ => ImageFilterConfig.CreateQ2Optimized()
        };
    }

    /// <summary>
    /// Creates configuration for Q1 (Poor quality) documents.
    /// Uses aggressive OpenCV enhancement.
    /// </summary>
    private static ImageFilterConfig CreateQ1Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.OpenCvAdvanced,
            EnableEnhancement = true,
            OpenCvParams = OpenCvFilterParams.CreateAggressive()
        };
    }

    /// <summary>
    /// Creates configuration for Q3 (Low quality) documents.
    /// Uses light PIL enhancement.
    /// </summary>
    private static ImageFilterConfig CreateQ3Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = new PilFilterParams
            {
                ContrastFactor = 1.1f,
                MedianSize = 1  // No median filtering
            }
        };
    }

    /// <summary>
    /// Creates configuration for Q4 (Very Low quality) documents.
    /// Minimal or no enhancement needed.
    /// </summary>
    private static ImageFilterConfig CreateQ4Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = PilFilterParams.CreatePassThrough()
        };
    }

    /// <summary>
    /// Creates configuration for pristine documents.
    /// No enhancement needed.
    /// </summary>
    private static ImageFilterConfig CreatePristineConfig()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.None,
            EnableEnhancement = false
        };
    }
}
