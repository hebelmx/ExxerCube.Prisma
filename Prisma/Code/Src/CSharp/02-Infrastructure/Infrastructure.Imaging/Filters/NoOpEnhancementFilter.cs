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
        return true;
    }
}
