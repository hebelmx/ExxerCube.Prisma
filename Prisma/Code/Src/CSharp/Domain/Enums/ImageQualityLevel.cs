namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Defines the quality level of a document image.
/// Used to select the appropriate enhancement filter.
/// </summary>
public enum ImageQualityLevel
{
    /// <summary>
    /// Q1 - Poor quality: heavily degraded, multiple artifacts.
    /// Requires aggressive enhancement.
    /// </summary>
    Q1_Poor = 1,

    /// <summary>
    /// Q2 - Medium-Poor quality: moderate degradation, some artifacts.
    /// Primary target for NSGA-II optimization.
    /// PIL filter achieves ~44% OCR improvement.
    /// </summary>
    Q2_MediumPoor = 2,

    /// <summary>
    /// Q3 - Low quality: minor degradation.
    /// Light enhancement may help.
    /// </summary>
    Q3_Low = 3,

    /// <summary>
    /// Q4 - Very Low quality: minimal degradation.
    /// May not need enhancement.
    /// </summary>
    Q4_VeryLow = 4,

    /// <summary>
    /// Pristine - No degradation detected.
    /// No enhancement needed.
    /// </summary>
    Pristine = 5
}
