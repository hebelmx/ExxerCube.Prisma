namespace ExxerCube.Prisma.Domain.Models;

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