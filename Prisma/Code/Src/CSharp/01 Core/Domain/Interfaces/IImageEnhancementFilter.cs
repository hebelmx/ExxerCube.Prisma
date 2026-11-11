using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the contract for image enhancement filters.
/// Implementations provide specific enhancement algorithms (PIL, OpenCV, etc.)
/// </summary>
public interface IImageEnhancementFilter
{
    /// <summary>
    /// Gets the type of this filter.
    /// </summary>
    ImageFilterType FilterType { get; }

    /// <summary>
    /// Gets the display name of this filter.
    /// </summary>
    string FilterName { get; }

    /// <summary>
    /// Applies enhancement to an image to improve OCR quality.
    /// </summary>
    /// <param name="imageData">The source image data.</param>
    /// <param name="config">The filter configuration.</param>
    /// <returns>A result containing the enhanced image or an error.</returns>
    Task<Result<ImageData>> EnhanceAsync(ImageData imageData, ImageFilterConfig config);

    /// <summary>
    /// Validates whether this filter can process the given image.
    /// </summary>
    /// <param name="imageData">The image to validate.</param>
    /// <returns>True if the filter can process this image; otherwise, false.</returns>
    bool CanProcess(ImageData imageData);
}
