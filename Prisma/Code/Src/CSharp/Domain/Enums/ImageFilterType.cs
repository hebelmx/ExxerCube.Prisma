namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Defines the type of image enhancement filter to apply before OCR.
/// These filters are optimized using NSGA-II multi-objective optimization.
/// </summary>
public enum ImageFilterType
{
    /// <summary>
    /// No filter applied - pass through original image.
    /// </summary>
    None = 0,

    /// <summary>
    /// PIL-based simple enhancement filter.
    /// Uses contrast adjustment + median filter.
    /// Optimized for Q2 (Medium-Poor) quality documents.
    /// Parameters: contrast_factor, median_size
    /// </summary>
    PilSimple = 1,

    /// <summary>
    /// OpenCV-based advanced enhancement filter.
    /// Uses denoise + CLAHE + bilateral filter + unsharp mask.
    /// Optimized for complex degradation patterns.
    /// Parameters: denoise_h, clahe_clip, bilateral_d, sigma_color, sigma_space, unsharp_amount, unsharp_radius
    /// </summary>
    OpenCvAdvanced = 2,

    /// <summary>
    /// Adaptive filter that selects PIL or OpenCV based on image quality analysis.
    /// Automatically chooses the best filter for the document.
    /// </summary>
    Adaptive = 3
}
