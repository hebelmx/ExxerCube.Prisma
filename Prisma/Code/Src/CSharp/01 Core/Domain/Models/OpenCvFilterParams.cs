namespace ExxerCube.Prisma.Domain.Models;

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