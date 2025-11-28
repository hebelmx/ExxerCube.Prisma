namespace ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

/// <summary>
/// Normalizes image quality features to [0, 1] range for polynomial model input.
/// Uses min-max normalization based on empirically observed feature ranges.
/// </summary>
public class FeatureNormalizer
{
    // Empirical feature ranges from EmguCvImageQualityAnalyzer
    // TODO: Update these ranges based on your filtering study dataset statistics

    /// <summary>
    /// Blur score range (Laplacian variance).
    /// Pristine: >3500, Poor: <100
    /// </summary>
    private const double BlurScoreMin = 0.0;
    private const double BlurScoreMax = 5000.0;

    /// <summary>
    /// Noise level range (standard deviation).
    /// Low: <10, High: >50
    /// </summary>
    private const double NoiseLevelMin = 0.0;
    private const double NoiseLevelMax = 100.0;

    /// <summary>
    /// Contrast level range (already normalized in analyzer but stored as 0-127.5).
    /// </summary>
    private const double ContrastLevelMin = 0.0;
    private const double ContrastLevelMax = 127.5;

    /// <summary>
    /// Sharpness level range (gradient magnitude, normalized to 0-50).
    /// </summary>
    private const double SharpnessLevelMin = 0.0;
    private const double SharpnessLevelMax = 50.0;

    /// <summary>
    /// Normalizes image quality assessment to feature vector [0, 1]^4.
    /// </summary>
    /// <param name="assessment">Image quality assessment from analyzer.</param>
    /// <returns>Normalized feature vector [blur, noise, contrast, sharpness].</returns>
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

    /// <summary>
    /// Normalizes blur score to [0, 1].
    /// Higher blur score = sharper image = higher normalized value.
    /// </summary>
    private double NormalizeBlur(float blurScore)
    {
        return Clamp01((blurScore - BlurScoreMin) / (BlurScoreMax - BlurScoreMin));
    }

    /// <summary>
    /// Normalizes noise level to [0, 1].
    /// Higher noise level = more noise = higher normalized value.
    /// </summary>
    private double NormalizeNoise(float noiseLevel)
    {
        // Note: NoiseLevel in assessment is already divided by 100, so we need to scale back
        double actualNoise = noiseLevel * 100.0;
        return Clamp01((actualNoise - NoiseLevelMin) / (NoiseLevelMax - NoiseLevelMin));
    }

    /// <summary>
    /// Normalizes contrast level to [0, 1].
    /// Higher contrast = better = higher normalized value.
    /// </summary>
    private double NormalizeContrast(float contrastLevel)
    {
        // ContrastLevel is already normalized to 0-1 in the analyzer, but stored as stddev/127.5
        // We need to denormalize and renormalize to our range
        double actualContrast = contrastLevel * 127.5;
        return Clamp01((actualContrast - ContrastLevelMin) / (ContrastLevelMax - ContrastLevelMin));
    }

    /// <summary>
    /// Normalizes sharpness level to [0, 1].
    /// Higher sharpness = sharper edges = higher normalized value.
    /// </summary>
    private double NormalizeSharpness(float sharpnessLevel)
    {
        // SharpnessLevel is already normalized to 0-1 in the analyzer (mean/50)
        // We need to denormalize and renormalize
        double actualSharpness = sharpnessLevel * 50.0;
        return Clamp01((actualSharpness - SharpnessLevelMin) / (SharpnessLevelMax - SharpnessLevelMin));
    }

    /// <summary>
    /// Clamps a value to [0, 1] range.
    /// </summary>
    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// Updates normalization ranges from dataset statistics.
    /// Call this after analyzing your filtering study dataset.
    /// </summary>
    /// <param name="blurMin">Minimum blur score observed.</param>
    /// <param name="blurMax">Maximum blur score observed.</param>
    /// <param name="noiseMin">Minimum noise level observed.</param>
    /// <param name="noiseMax">Maximum noise level observed.</param>
    /// <param name="contrastMin">Minimum contrast level observed.</param>
    /// <param name="contrastMax">Maximum contrast level observed.</param>
    /// <param name="sharpnessMin">Minimum sharpness level observed.</param>
    /// <param name="sharpnessMax">Maximum sharpness level observed.</param>
    /// <returns>A new FeatureNormalizer with updated ranges.</returns>
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
