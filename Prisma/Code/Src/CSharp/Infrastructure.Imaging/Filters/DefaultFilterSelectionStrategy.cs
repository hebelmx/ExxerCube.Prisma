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
    public ImageFilterConfig SelectFilterByQuality(ImageQualityLevel qualityLevel) => qualityLevel.Value switch
    {
        1 => CreateQ1Config(),
        2 => ImageFilterConfig.CreateQ2Optimized(),
        3 => CreateQ3Config(),
        4 => CreateQ4Config(),
        5 => CreatePristineConfig(),
        _ => ImageFilterConfig.CreateQ2Optimized()
    };

    /// <inheritdoc />
    public ImageFilterConfig GetFilterConfig(ImageFilterType filterType) => filterType.Value switch
    {
        0 => new ImageFilterConfig
        {
            FilterType = ImageFilterType.None,
            EnableEnhancement = false
        },
        1 => ImageFilterConfig.CreateQ2Optimized(),
        2 => new ImageFilterConfig
        {
            FilterType = ImageFilterType.OpenCvAdvanced,
            EnableEnhancement = true,
            OpenCvParams = OpenCvFilterParams.CreateDefault()
        },
        3 => ImageFilterConfig.CreateAdaptive(),
        _ => ImageFilterConfig.CreateQ2Optimized()
    };

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
