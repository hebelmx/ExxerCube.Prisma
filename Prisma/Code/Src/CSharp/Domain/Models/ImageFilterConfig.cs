using ExxerCube.Prisma.Domain.Enums;

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
}

/// <summary>
/// Parameters for PIL-based image enhancement.
/// Simple 2-parameter pipeline: contrast adjustment + median filter.
/// </summary>
public class PilFilterParams
{
    /// <summary>
    /// Gets or sets the contrast enhancement factor.
    /// NSGA-II Optimized: 1.157 for Q2 documents.
    /// Range: 0.5 - 3.0 (1.0 = no change)
    /// </summary>
    public float ContrastFactor { get; set; } = 1.157f;

    /// <summary>
    /// Gets or sets the median filter kernel size (must be odd).
    /// NSGA-II Optimized: 3 for Q2 documents.
    /// Range: 1, 3, 5, 7 (1 = no filtering)
    /// </summary>
    public int MedianSize { get; set; } = 3;

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public PilFilterParams()
    {
    }

    /// <summary>
    /// Creates PIL parameters optimized for Q2 (Medium-Poor) quality documents.
    /// Based on NSGA-II multi-objective optimization results.
    /// Achieves ~44% OCR improvement on degraded documents.
    /// </summary>
    /// <returns>NSGA-II optimized parameters.</returns>
    public static PilFilterParams CreateQ2Optimized()
    {
        return new PilFilterParams
        {
            ContrastFactor = 1.157f,  // NSGA-II optimized
            MedianSize = 3            // NSGA-II optimized
        };
    }

    /// <summary>
    /// Creates PIL parameters with no enhancement (pass-through).
    /// </summary>
    /// <returns>Pass-through parameters.</returns>
    public static PilFilterParams CreatePassThrough()
    {
        return new PilFilterParams
        {
            ContrastFactor = 1.0f,
            MedianSize = 1
        };
    }
}

/// <summary>
/// Parameters for OpenCV-based advanced image enhancement.
/// 7-parameter pipeline: denoise + CLAHE + bilateral + unsharp mask.
/// </summary>
public class OpenCvFilterParams
{
    /// <summary>
    /// Gets or sets the denoising filter strength.
    /// Range: 1 - 20 (higher = more denoising)
    /// </summary>
    public float DenoiseH { get; set; } = 10.0f;

    /// <summary>
    /// Gets or sets the CLAHE clip limit for contrast enhancement.
    /// Range: 1.0 - 8.0 (higher = more contrast)
    /// </summary>
    public float ClaheClip { get; set; } = 2.0f;

    /// <summary>
    /// Gets or sets the bilateral filter diameter.
    /// Range: 3 - 15 (larger = more smoothing)
    /// </summary>
    public int BilateralD { get; set; } = 9;

    /// <summary>
    /// Gets or sets the bilateral filter sigma color.
    /// Range: 10 - 150 (higher = more color averaging)
    /// </summary>
    public float SigmaColor { get; set; } = 75.0f;

    /// <summary>
    /// Gets or sets the bilateral filter sigma space.
    /// Range: 10 - 150 (higher = more spatial averaging)
    /// </summary>
    public float SigmaSpace { get; set; } = 75.0f;

    /// <summary>
    /// Gets or sets the unsharp mask amount.
    /// Range: 0.0 - 3.0 (0 = no sharpening)
    /// </summary>
    public float UnsharpAmount { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets the unsharp mask radius.
    /// Range: 0.5 - 5.0
    /// </summary>
    public float UnsharpRadius { get; set; } = 1.0f;

    /// <summary>
    /// Initializes a new instance with default values.
    /// </summary>
    public OpenCvFilterParams()
    {
    }

    /// <summary>
    /// Creates OpenCV parameters with default balanced settings.
    /// </summary>
    /// <returns>Default balanced parameters.</returns>
    public static OpenCvFilterParams CreateDefault()
    {
        return new OpenCvFilterParams
        {
            DenoiseH = 10.0f,
            ClaheClip = 2.0f,
            BilateralD = 9,
            SigmaColor = 75.0f,
            SigmaSpace = 75.0f,
            UnsharpAmount = 1.5f,
            UnsharpRadius = 1.0f
        };
    }

    /// <summary>
    /// Creates OpenCV parameters with aggressive enhancement.
    /// Use for heavily degraded documents.
    /// </summary>
    /// <returns>Aggressive enhancement parameters.</returns>
    public static OpenCvFilterParams CreateAggressive()
    {
        return new OpenCvFilterParams
        {
            DenoiseH = 15.0f,
            ClaheClip = 4.0f,
            BilateralD = 11,
            SigmaColor = 100.0f,
            SigmaSpace = 100.0f,
            UnsharpAmount = 2.0f,
            UnsharpRadius = 1.5f
        };
    }
}
