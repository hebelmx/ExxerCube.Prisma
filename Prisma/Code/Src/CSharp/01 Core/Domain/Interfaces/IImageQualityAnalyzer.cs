using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the contract for analyzing image quality to determine optimal filter selection.
/// </summary>
public interface IImageQualityAnalyzer
{
    /// <summary>
    /// Analyzes the quality of an image and returns a quality assessment.
    /// </summary>
    /// <param name="imageData">The image to analyze.</param>
    /// <returns>A result containing the quality assessment or an error.</returns>
    Task<Result<ImageQualityAssessment>> AnalyzeAsync(ImageData imageData);

    /// <summary>
    /// Determines the quality level of an image.
    /// </summary>
    /// <param name="imageData">The image to analyze.</param>
    /// <returns>A result containing the quality level or an error.</returns>
    Task<Result<ImageQualityLevel>> GetQualityLevelAsync(ImageData imageData);
}