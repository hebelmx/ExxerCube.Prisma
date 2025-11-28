using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

/// <summary>
/// Trained polynomial regression model for filter parameter prediction.
/// Uses coefficients from GA-optimized cluster data achieving 18.4% OCR improvement.
/// Model performance: R² > 0.89 for all parameters, validated on 32 unseen images.
/// </summary>
public class TrainedPolynomialModel
{
    // StandardScaler normalization parameters
    private static readonly double[] ScalerMean = { 565.758125, 29.1111, 15.2173, 4.3990 };
    private static readonly double[] ScalerScale = { 1225.0172, 5.8784, 18.2808, 2.5322 };

    // Polynomial coefficients for each parameter (degree 2, 15 features)
    // Feature order: 1, x0, x1, x2, x3, x0², x0x1, x0x2, x0x3, x1², x1x2, x1x3, x2², x2x3, x3²
    // where x0=BlurScore, x1=Contrast, x2=NoiseEstimate, x3=EdgeDensity

    // Contrast parameter (R²=0.949, MAE=0.052, range=[0.5, 2.0])
    private static readonly double[] ContrastCoef = {
        0.0, 0.1096, -0.4311, 0.0875, 0.1243,
        0.0147, 0.2337, -0.3083, -0.4505,
        0.1190, -0.1537, 0.0157,
        0.2605, 0.3578, 0.0455
    };
    private static readonly double ContrastIntercept = 1.0011;

    // Brightness parameter (R²=0.987, MAE=0.004, range=[0.8, 1.3])
    private static readonly double[] BrightnessCoef = {
        0.0, 0.0030, 0.0185, 0.0022, -0.0428,
        -0.0066, -0.0039, 0.0132, -0.0546,
        -0.0031, 0.0135, 0.0006,
        0.0068, -0.0222, 0.0009
    };
    private static readonly double BrightnessIntercept = 1.0735;

    // Sharpness parameter (R²=0.947, MAE=0.089, range=[0.5, 3.0])
    private static readonly double[] SharpnessCoef = {
        0.0, 0.0314, 0.3771, 0.0752, -0.1629,
        0.0101, -0.1100, 0.1108, -0.2895,
        0.1683, -0.1517, -0.2783,
        -0.0136, -0.0722, -0.0408
    };
    private static readonly double SharpnessIntercept = 2.2279;

    // UnsharpRadius parameter (R²=0.938, MAE=0.197, range=[0.0, 5.0])
    private static readonly double[] UnsharpRadiusCoef = {
        0.0, -0.1262, -0.0350, -0.1980, -0.6791,
        -0.0582, -0.2502, 0.3043, -0.1707,
        -0.4983, 0.8135, 0.5182,
        -0.1420, -0.6486, 0.0650
    };
    private static readonly double UnsharpRadiusIntercept = 2.5032;

    // UnsharpPercent parameter (R²=0.897, MAE=16.4, range=[0, 250])
    private static readonly double[] UnsharpPercentCoef = {
        0.0, -59.83, 42.22, -57.19, 20.20,
        21.89, -107.31, 80.69, 257.05,
        -62.80, 111.17, 24.40,
        -124.69, -111.20, -3.62
    };
    private static readonly double UnsharpPercentIntercept = 173.38;

    /// <summary>
    /// Predicts optimal filter parameters from extracted image features.
    /// </summary>
    /// <param name="features">Extracted image property features (4D vector).</param>
    /// <returns>Predicted filter parameters optimized for OCR enhancement.</returns>
    public PolynomialFilterParams Predict(ImagePropertyFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);

        // Step 1: Normalize features using StandardScaler
        var normalized = NormalizeFeatures(features.ToArray());

        // Step 2: Generate polynomial features (degree 2 with interactions)
        var polyFeatures = GeneratePolynomialFeatures(normalized);

        // Step 3: Predict each parameter using trained coefficients
        var contrast = Clamp(DotProduct(polyFeatures, ContrastCoef) + ContrastIntercept, 0.5, 2.0);
        var brightness = Clamp(DotProduct(polyFeatures, BrightnessCoef) + BrightnessIntercept, 0.8, 1.3);
        var sharpness = Clamp(DotProduct(polyFeatures, SharpnessCoef) + SharpnessIntercept, 0.5, 3.0);
        var unsharpRadius = Clamp(DotProduct(polyFeatures, UnsharpRadiusCoef) + UnsharpRadiusIntercept, 0.0, 5.0);
        var unsharpPercent = Clamp(DotProduct(polyFeatures, UnsharpPercentCoef) + UnsharpPercentIntercept, 0, 250);

        return new PolynomialFilterParams
        {
            Contrast = (float)contrast,
            Brightness = (float)brightness,
            Sharpness = (float)sharpness,
            UnsharpRadius = (float)unsharpRadius,
            UnsharpPercent = (float)unsharpPercent
        };
    }

    /// <summary>
    /// Normalizes features using StandardScaler (z-score normalization).
    /// Formula: (x - mean) / scale
    /// </summary>
    private static double[] NormalizeFeatures(double[] features)
    {
        var normalized = new double[4];
        for (int i = 0; i < 4; i++)
        {
            normalized[i] = (features[i] - ScalerMean[i]) / ScalerScale[i];
        }
        return normalized;
    }

    /// <summary>
    /// Generates degree-2 polynomial features with bias.
    /// Order: [1, x0, x1, x2, x3, x0², x0x1, x0x2, x0x3, x1², x1x2, x1x3, x2², x2x3, x3²]
    /// Total: 15 features (1 bias + 4 linear + 4 quadratic + 6 interaction)
    /// </summary>
    private static double[] GeneratePolynomialFeatures(double[] x)
    {
        return new double[]
        {
            1.0,           // bias term
            x[0], x[1], x[2], x[3],  // linear terms
            x[0] * x[0],   // x0² (BlurScore²)
            x[0] * x[1],   // x0*x1 (BlurScore * Contrast)
            x[0] * x[2],   // x0*x2 (BlurScore * NoiseEstimate)
            x[0] * x[3],   // x0*x3 (BlurScore * EdgeDensity)
            x[1] * x[1],   // x1² (Contrast²)
            x[1] * x[2],   // x1*x2 (Contrast * NoiseEstimate)
            x[1] * x[3],   // x1*x3 (Contrast * EdgeDensity)
            x[2] * x[2],   // x2² (NoiseEstimate²)
            x[2] * x[3],   // x2*x3 (NoiseEstimate * EdgeDensity)
            x[3] * x[3]    // x3² (EdgeDensity²)
        };
    }

    /// <summary>
    /// Computes dot product of two vectors.
    /// </summary>
    private static double DotProduct(double[] a, double[] b)
    {
        var sum = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }

    /// <summary>
    /// Clamps value to specified range.
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
