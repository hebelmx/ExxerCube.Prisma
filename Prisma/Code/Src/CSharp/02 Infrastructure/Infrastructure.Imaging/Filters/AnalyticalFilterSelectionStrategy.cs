using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Filters;

/// <summary>
/// Analytical filter selection strategy based on NSGA-II optimization and baseline testing results.
///
/// Based on comprehensive testing (820 OCR runs across 41 filters):
/// - Q0 Pristine: NO FILTER wins (30 edits vs 339 OpenCV vs 491 PIL)
/// - Q1 Light: OpenCV wins (404 edits, 24.9% better than 538 baseline)
/// - Q2 Moderate: PIL wins (1444 edits, 78.1% better than 6590 baseline!)
/// - Q3-Q4 Heavy: PIL aggressive (75-80% improvement potential)
///
/// See: Prisma/Fixtures/comprehensive_with_baseline_matrix.json
/// </summary>
public class AnalyticalFilterSelectionStrategy : IFilterSelectionStrategy
{
    // Thresholds derived from baseline testing correlation analysis
    private const float PristineNoiseThreshold = 0.6f;      // Below this → pristine quality

    private const float LightNoiseThreshold = 4.5f;         // Below this → light degradation
    private const float ModerateNoiseThreshold = 8.0f;      // Above this → heavy degradation

    private const float PristineBlurThreshold = 3500f;      // Above this → ultra-sharp
    private const float GoodBlurThreshold = 1500f;          // Above this → normal quality

    private const float GoodContrastThreshold = 35f;        // Above this → good contrast
    private const float PoorContrastThreshold = 28f;        // Below this → poor contrast

    /// <inheritdoc />
    public ImageFilterConfig SelectFilter(ImageQualityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        // Use analytical classification based on measured metrics
        var qualityLevel = ClassifyQualityLevel(assessment);
        var config = SelectFilterByQuality(qualityLevel);

        // Fine-tune based on specific characteristics
        return RefineConfig(config, assessment);
    }

    /// <inheritdoc />
    public ImageFilterConfig SelectFilterByQuality(ImageQualityLevel qualityLevel) => qualityLevel.Value switch
    {
        5 => CreatePristineConfig(),
        1 => CreateQ1Config(),
        2 => CreateQ2Config(),
        3 => CreateQ3Config(),
        4 => CreateQ4Config(),
        _ => CreateQ2Config() // Default to moderate enhancement
    };

    /// <inheritdoc />
    public ImageFilterConfig GetFilterConfig(ImageFilterType filterType) => filterType.Value switch
    {
        0 => CreatePristineConfig(),
        1 => CreateQ2Config(),
        2 => CreateQ1Config(),
        3 => CreateAdaptiveConfig(),
        _ => CreateQ2Config()
    };

    /// <summary>
    /// Classifies quality level based on measured image metrics.
    /// Uses thresholds derived from correlation analysis and baseline testing.
    /// </summary>
    private ImageQualityLevel ClassifyQualityLevel(ImageQualityAssessment assessment)
    {
        var metrics = assessment;

        // Pristine detection (baseline testing: NO FILTER wins with 0-30 edits)
        // Characteristics: ultra-high blur (sharpness), very low noise
        if (metrics.BlurScore > PristineBlurThreshold &&
            metrics.NoiseLevel < PristineNoiseThreshold &&
            metrics.ContrastLevel > GoodContrastThreshold)
        {
            return ImageQualityLevel.Pristine;
        }

        // Q1 Light degradation (OpenCV wins: 404 edits vs 538 baseline)
        // Characteristics: normal blur, low noise, good contrast
        if (metrics.NoiseLevel < LightNoiseThreshold &&
            metrics.BlurScore > GoodBlurThreshold &&
            metrics.ContrastLevel > PoorContrastThreshold)
        {
            return ImageQualityLevel.Q1_Poor;
        }

        // Q2 Moderate degradation (PIL wins: 1444 edits vs 6590 baseline!)
        // Characteristics: moderate blur, moderate noise, lower contrast
        if (metrics.NoiseLevel < ModerateNoiseThreshold &&
            metrics.BlurScore > 1000f)
        {
            return ImageQualityLevel.Q2_MediumPoor;
        }

        // Q3 Heavy degradation (PIL aggressive: 50% degradation)
        // Characteristics: high noise, low blur, poor contrast
        if (metrics.NoiseLevel < 12.0f)
        {
            return ImageQualityLevel.Q3_Low;
        }

        // Q4 Extreme degradation (PIL maximum: 75% degradation)
        return ImageQualityLevel.Q4_VeryLow;
    }

    /// <summary>
    /// Creates configuration for pristine documents.
    /// NO FILTER - baseline testing showed filters DEGRADE pristine documents.
    /// Evidence: Q0 baseline 30 edits vs 339 OpenCV vs 491 PIL.
    /// </summary>
    private static ImageFilterConfig CreatePristineConfig()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.None,
            EnableEnhancement = false
        };
    }

    /// <summary>
    /// Creates configuration for Q1 (Light degradation) documents.
    /// OpenCV light enhancement - 24.9% improvement over baseline.
    /// Evidence: 404 edits vs 538 baseline (Q1).
    /// </summary>
    private static ImageFilterConfig CreateQ1Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.OpenCvAdvanced,
            EnableEnhancement = true,
            OpenCvParams = new OpenCvFilterParams
            {
                DenoiseH = 5,
                ClaheClip = 1.05f,
                BilateralD = 5,
                SigmaColor = 50,
                SigmaSpace = 50,
                UnsharpAmount = 1.0f,
                UnsharpRadius = 1.0f
            }
        };
    }

    /// <summary>
    /// Creates configuration for Q2 (Moderate degradation) documents.
    /// PIL enhancement - 78.1% improvement over baseline!
    /// Evidence: 1444 edits vs 6590 baseline (Q2).
    /// Uses NSGA-II optimized parameters from baseline testing.
    /// </summary>
    private static ImageFilterConfig CreateQ2Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = new PilFilterParams
            {
                ContrastFactor = 1.1573620712395511f,
                MedianSize = 3
            }
        };
    }

    /// <summary>
    /// Creates configuration for Q3 (Heavy degradation) documents.
    /// PIL aggressive enhancement - 50% expected degradation.
    /// </summary>
    private static ImageFilterConfig CreateQ3Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = new PilFilterParams
            {
                ContrastFactor = 1.5f,
                MedianSize = 5
            }
        };
    }

    /// <summary>
    /// Creates configuration for Q4 (Extreme degradation) documents.
    /// PIL maximum enhancement - 75% expected degradation.
    /// </summary>
    private static ImageFilterConfig CreateQ4Config()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = new PilFilterParams
            {
                ContrastFactor = 2.0f,
                MedianSize = 7
            }
        };
    }

    /// <summary>
    /// Creates adaptive configuration that adjusts based on metrics.
    /// </summary>
    private static ImageFilterConfig CreateAdaptiveConfig()
    {
        return ImageFilterConfig.CreateAdaptive();
    }

    /// <summary>
    /// Refines configuration based on specific quality characteristics.
    /// Uses correlation analysis: contrast has -0.963 correlation with OCR edits.
    /// </summary>
    private ImageFilterConfig RefineConfig(ImageFilterConfig config, ImageQualityAssessment assessment)
    {
        var metrics = assessment;

        // If using PIL, adjust contrast factor based on measured contrast
        if (config.FilterType == ImageFilterType.PilSimple && config.EnableEnhancement)
        {
            // Contrast has strongest negative correlation (-0.963)
            // Lower contrast → increase contrast enhancement
            if (metrics.ContrastLevel < 25f)
            {
                config.PilParams.ContrastFactor = Math.Min(config.PilParams.ContrastFactor * 1.3f, 2.5f);
            }
            else if (metrics.ContrastLevel > 35f)
            {
                // Good contrast → reduce enhancement to avoid over-processing
                config.PilParams.ContrastFactor = Math.Max(config.PilParams.ContrastFactor * 0.9f, 1.0f);
            }

            // Noise has +0.906 correlation
            // Higher noise → increase median filter size
            if (metrics.NoiseLevel > 8.0f)
            {
                config.PilParams.MedianSize = 7;
            }
            else if (metrics.NoiseLevel > 5.0f)
            {
                config.PilParams.MedianSize = 5;
            }
        }

        // If using OpenCV, adjust based on blur and noise
        if (config.FilterType == ImageFilterType.OpenCvAdvanced && config.EnableEnhancement)
        {
            // High noise → increase denoising
            if (metrics.NoiseLevel > 3.0f)
            {
                config.OpenCvParams.DenoiseH = Math.Min(config.OpenCvParams.DenoiseH + 5, 15);
            }

            // Low blur (less sharp) → increase sharpening
            if (metrics.BlurScore < 1500f)
            {
                config.OpenCvParams.UnsharpAmount = Math.Min(config.OpenCvParams.UnsharpAmount * 1.2f, 2.0f);
            }
        }

        return config;
    }
}
