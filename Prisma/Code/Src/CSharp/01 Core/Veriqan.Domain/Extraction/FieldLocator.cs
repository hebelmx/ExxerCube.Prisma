namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Identifies where in a PDF document a field was found (or expected).
/// Coordinates use PDF points; origin is the bottom-left of the page as returned by PdfPig.
/// </summary>
/// <param name="PageNumber">
/// 1-based page number.  Set to <c>1</c> as a best-effort hint when the field was not found.
/// </param>
/// <param name="Left">
/// X coordinate (in PDF points) of the left edge of the bounding region, or <see langword="null"/>
/// when only a page-level hint is available (field not found).
/// </param>
/// <param name="Bottom">
/// Y coordinate (in PDF points) of the bottom edge of the bounding region, or <see langword="null"/>
/// when only a page-level hint is available.
/// </param>
/// <param name="Width">
/// Width of the bounding region in PDF points, or <see langword="null"/> when not available.
/// </param>
/// <param name="Height">
/// Height of the bounding region in PDF points, or <see langword="null"/> when not available.
/// </param>
public sealed record FieldLocator(
    int PageNumber,
    double? Left = null,
    double? Bottom = null,
    double? Width = null,
    double? Height = null)
{
    /// <summary>
    /// Returns a page-level hint locator — used when the field was not found
    /// and no precise coordinates are available.
    /// </summary>
    /// <param name="pageNumber">Expected page number (1-based).</param>
    public static FieldLocator PageHint(int pageNumber = 1) => new(pageNumber);

    /// <summary>
    /// Returns <see langword="true"/> when precise bounding-box coordinates are available.
    /// </summary>
    public bool HasBoundingBox =>
        Left is not null && Bottom is not null && Width is not null && Height is not null;
}
