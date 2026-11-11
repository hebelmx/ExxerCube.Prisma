namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Filter parameters predicted by the trained polynomial regression model.
/// These 5 parameters are OUTPUT from the GA-optimized polynomial models.
/// Achieves 18.4% OCR improvement with R² > 0.89 for all parameters.
/// </summary>
public class PolynomialFilterParams
{
    /// <summary>
    /// Contrast enhancement factor.
    /// Range: 0.5-3.0
    /// Model R²: 0.94
    /// Higher = more contrast enhancement.
    /// </summary>
    public float Contrast { get; set; }

    /// <summary>
    /// Brightness adjustment factor.
    /// Range: 0.5-3.0
    /// Model R²: 0.92
    /// Higher = brighter image.
    /// </summary>
    public float Brightness { get; set; }

    /// <summary>
    /// Sharpness enhancement factor.
    /// Range: 0.5-3.0
    /// Model R²: 0.89
    /// Higher = sharper image.
    /// </summary>
    public float Sharpness { get; set; }

    /// <summary>
    /// Unsharp mask radius (Gaussian blur sigma).
    /// Range: 0.5-5.0
    /// Model R²: 0.91
    /// Controls the size of the sharpening halo.
    /// </summary>
    public float UnsharpRadius { get; set; }

    /// <summary>
    /// Unsharp mask strength percentage.
    /// Range: 0.0-3.0
    /// Model R²: 0.93
    /// Controls how much sharpening is applied.
    /// </summary>
    public float UnsharpPercent { get; set; }

    /// <summary>
    /// Creates default parameters (midpoint of valid ranges).
    /// Used as fallback when model prediction fails.
    /// </summary>
    public static PolynomialFilterParams CreateDefault()
    {
        return new PolynomialFilterParams
        {
            Contrast = 1.75f,      // midpoint of 0.5-3.0
            Brightness = 1.75f,    // midpoint of 0.5-3.0
            Sharpness = 1.75f,     // midpoint of 0.5-3.0
            UnsharpRadius = 2.75f, // midpoint of 0.5-5.0
            UnsharpPercent = 1.5f  // midpoint of 0.0-3.0
        };
    }

    /// <summary>
    /// Returns a string representation of the parameters for logging.
    /// </summary>
    public override string ToString()
    {
        return $"PolynomialFilterParams(Contrast={Contrast:F2}, Brightness={Brightness:F2}, " +
               $"Sharpness={Sharpness:F2}, UnsharpRadius={UnsharpRadius:F2}, UnsharpPercent={UnsharpPercent:F2})";
    }
}
