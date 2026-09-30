namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Image properties extracted for polynomial model prediction.
/// These 4 features are INPUT to the polynomial regression trained on GA-optimized clusters.
/// </summary>
public class ImagePropertyFeatures
{
    /// <summary>
    /// Laplacian variance - higher = sharper image.
    /// Typical range: 0-10000+
    /// Used to detect blur and overall sharpness quality.
    /// </summary>
    public double BlurScore { get; set; }

    /// <summary>
    /// Standard deviation of grayscale intensity.
    /// Typical range: 0-80
    /// Measures overall contrast in the image.
    /// </summary>
    public double Contrast { get; set; }

    /// <summary>
    /// Mean absolute Laplacian - high frequency energy.
    /// Typical range: 0-100+
    /// Estimates noise level and texture complexity.
    /// </summary>
    public double NoiseEstimate { get; set; }

    /// <summary>
    /// Ratio of Canny edge pixels to total pixels.
    /// Typical range: 0-0.15
    /// Measures edge density and structural content.
    /// </summary>
    public double EdgeDensity { get; set; }

    /// <summary>
    /// Returns features as array in model order [BlurScore, Contrast, NoiseEstimate, EdgeDensity].
    /// This order must match the training data feature order.
    /// </summary>
    /// <returns>Feature array for polynomial model input.</returns>
    public double[] ToArray() => new[] { BlurScore, Contrast, NoiseEstimate, EdgeDensity };

    /// <summary>
    /// Returns a string representation of the features for logging.
    /// </summary>
    public override string ToString()
    {
        return $"ImagePropertyFeatures(BlurScore={BlurScore:F2}, Contrast={Contrast:F2}, " +
               $"NoiseEstimate={NoiseEstimate:F2}, EdgeDensity={EdgeDensity:F4})";
    }
}
