using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Filters;

/// <summary>
/// No-operation filter that passes through the image unchanged.
/// </summary>
public class NoOpEnhancementFilter : IImageEnhancementFilter
{
    /// <inheritdoc />
    public ImageFilterType FilterType => ImageFilterType.None;

    /// <inheritdoc />
    public string FilterName => "No Enhancement";

    /// <inheritdoc />
    public Task<Result<ImageData>> EnhanceAsync(ImageData imageData, ImageFilterConfig config)
    {
        return Task.FromResult(Result<ImageData>.Success(imageData));
    }

    /// <inheritdoc />
    public bool CanProcess(ImageData imageData)
    {
        if (imageData == null || imageData.Data == null || imageData.Data.Length == 0)
        {
            return false;
        }

        var hasValidPage = imageData.PageNumber >= 1 &&
                           imageData.TotalPages >= imageData.PageNumber;

        var hasSource = !string.IsNullOrWhiteSpace(imageData.SourcePath);

        // Accept any format; this is intentionally a pass-through filter.
        return hasValidPage || hasSource;
    }
}
