using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

/// <summary>
/// Provides min-max normalization utilities for image quality features.
/// </summary>
public class FeatureNormalizer
{
    // Empirical feature ranges from EmguCvImageQualityAnalyzer
    // TODO: Update these ranges based on your filtering study dataset statistics

    private const double BlurScoreMin = 0.0;
    private const double BlurScoreMax = 5000.0;

    private const double NoiseLevelMin = 0.0;
    private const double NoiseLevelMax = 100.0;

    private const double ContrastLevelMin = 0.0;
    private const double ContrastLevelMax = 127.5;

    private const double SharpnessLevelMin = 0.0;
    private const double SharpnessLevelMax = 50.0;

    /// <summary>
    /// Normalizes an image quality assessment into a four-element feature vector.
    /// </summary>
    public double[] Normalize(ImageQualityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return new[]
        {
            NormalizeBlur(assessment.BlurScore),
            NormalizeNoise(assessment.NoiseLevel),
            NormalizeContrast(assessment.ContrastLevel),
            NormalizeSharpness(assessment.SharpnessLevel)
        };
    }

    private double NormalizeBlur(float blurScore)
    {
        return Clamp01((blurScore - BlurScoreMin) / (BlurScoreMax - BlurScoreMin));
    }

    private double NormalizeNoise(float noiseLevel)
    {
        // Note: NoiseLevel in assessment is already divided by 100, so we need to scale back
        double actualNoise = noiseLevel * 100.0;
        return Clamp01((actualNoise - NoiseLevelMin) / (NoiseLevelMax - NoiseLevelMin));
    }

    private double NormalizeContrast(float contrastLevel)
    {
        // ContrastLevel is already normalized to 0-1 in the analyzer, but stored as stddev/127.5
        // We need to denormalize and renormalize to our range
        double actualContrast = contrastLevel * 127.5;
        return Clamp01((actualContrast - ContrastLevelMin) / (ContrastLevelMax - ContrastLevelMin));
    }

    private double NormalizeSharpness(float sharpnessLevel)
    {
        // SharpnessLevel is already normalized to 0-1 in the analyzer (mean/50)
        // We need to denormalize and renormalize
        double actualSharpness = sharpnessLevel * 50.0;
        return Clamp01((actualSharpness - SharpnessLevelMin) / (SharpnessLevelMax - SharpnessLevelMin));
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// Creates a normalizer with dataset-derived ranges (placeholder returns default ranges).
    /// </summary>
    public static FeatureNormalizer CreateFromDataset(
        double blurMin, double blurMax,
        double noiseMin, double noiseMax,
        double contrastMin, double contrastMax,
        double sharpnessMin, double sharpnessMax)
    {
        // TODO: Implement custom range configuration when needed
        // For now, return default normalizer
        return new FeatureNormalizer();
    }
}
