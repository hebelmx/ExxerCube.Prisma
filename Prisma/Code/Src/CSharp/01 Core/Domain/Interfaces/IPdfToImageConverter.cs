namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Converts the pages of a PDF document into raster images suitable for OCR.
/// </summary>
/// <remarks>
/// This port isolates the PDF-rasterization concern (e.g. PDFtoImage/SkiaSharp) from the
/// field-extraction pipeline so that <see cref="IFieldExtractor{T}"/> implementations remain
/// unit-testable with a mocked converter (no real PDF rendering required).
/// </remarks>
public interface IPdfToImageConverter
{
    /// <summary>
    /// Converts every page of a PDF document to PNG image bytes (one entry per page, in order).
    /// </summary>
    /// <param name="pdfBytes">The PDF file content.</param>
    /// <param name="dpi">Render resolution in dots-per-inch (default 300 for high-quality OCR).</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>
    /// A result containing one PNG byte array per page. A document that renders to zero pages
    /// returns a successful, empty list (the caller decides whether that is a failure).
    /// </returns>
    Task<Result<IReadOnlyList<byte[]>>> ConvertToImagesAsync(
        byte[] pdfBytes,
        int dpi = 300,
        CancellationToken cancellationToken = default);
}
