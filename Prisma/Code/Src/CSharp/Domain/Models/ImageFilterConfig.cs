using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Configuration for image enhancement filters.
/// Contains NSGA-II optimized parameters for PIL and OpenCV pipelines.
/// </summary>
public class ImageFilterConfig
{
    /// <summary>
    /// Gets or sets the type of filter to apply.
    /// </summary>
    public ImageFilterType FilterType { get; set; } = ImageFilterType.PilSimple;

    /// <summary>
    /// Gets or sets a value indicating whether to apply enhancement.
    /// </summary>
    public bool EnableEnhancement { get; set; } = true;

    /// <summary>
    /// Gets or sets the PIL filter parameters.
    /// </summary>
    public PilFilterParams PilParams { get; set; } = new();

    /// <summary>
    /// Gets or sets the OpenCV filter parameters.
    /// </summary>
    public OpenCvFilterParams OpenCvParams { get; set; } = new();

    /// <summary>
    /// Gets or sets the polynomial filter parameters (GA-optimized, R² > 0.89).
    /// </summary>
    public PolynomialFilterParams? PolynomialParams { get; set; }

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public ImageFilterConfig()
    {
    }

    /// <summary>
    /// Creates a configuration optimized for Q2 (Medium-Poor) quality documents.
    /// Uses NSGA-II optimized PIL parameters.
    /// </summary>
    /// <returns>Optimized configuration for Q2 documents.</returns>
    public static ImageFilterConfig CreateQ2Optimized()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = PilFilterParams.CreateQ2Optimized()
        };
    }

    /// <summary>
    /// Creates a configuration for adaptive filter selection.
    /// </summary>
    /// <returns>Configuration with adaptive filter type.</returns>
    public static ImageFilterConfig CreateAdaptive()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.Adaptive,
            EnableEnhancement = true,
            PilParams = PilFilterParams.CreateQ2Optimized(),
            OpenCvParams = OpenCvFilterParams.CreateDefault()
        };
    }

    /// <summary>
    /// Creates a configuration for polynomial-based enhancement.
    /// Uses trained polynomial models (18.4% OCR improvement).
    /// Parameters are predicted dynamically from image features.
    /// </summary>
    /// <returns>Configuration with polynomial filter type.</returns>
    public static ImageFilterConfig CreatePolynomial()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.Polynomial,
            EnableEnhancement = true,
            PolynomialParams = null  // Will be predicted at runtime
        };
    }
}
