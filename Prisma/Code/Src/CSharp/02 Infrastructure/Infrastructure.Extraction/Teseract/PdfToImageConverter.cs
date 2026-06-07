using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// Converts PDF pages to PNG images using the PDFtoImage library (SkiaSharp under the hood).
/// </summary>
/// <remarks>
/// This is the production adapter for <see cref="IPdfToImageConverter"/>. It was extracted from
/// <see cref="PdfOcrFieldExtractor"/> so the extractor can be unit-tested against a mocked
/// converter rather than driving real PDF rasterization.
/// </remarks>
public sealed class PdfToImageConverter : IPdfToImageConverter
{
    private readonly ILogger<PdfToImageConverter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfToImageConverter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PdfToImageConverter(ILogger<PdfToImageConverter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<byte[]>>> ConvertToImagesAsync(
        byte[] pdfBytes,
        int dpi = 300,
        CancellationToken cancellationToken = default)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<byte[]>>.WithFailure("PDF content cannot be null or empty"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<byte[]>>());
        }

        var imagePages = new List<byte[]>();

        try
        {
            _logger.LogInformation("Converting PDF pages to images at {DPI} DPI using PDFtoImage", dpi);

            var options = new RenderOptions(Dpi: dpi);

            // PDFtoImage doesn't expose a page count, so iterate until a page returns null.
            // IMPORTANT: create a NEW stream for each page because Conversion.ToImage() closes it.
            int pageIndex = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var pdfStream = new MemoryStream(pdfBytes);

#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
                    using var skBitmap = Conversion.ToImage(pdfStream, pageIndex, options: options);
#pragma warning restore CA1416

                    if (skBitmap == null)
                    {
                        break; // No more pages
                    }

                    using var image = Image.LoadPixelData<Rgba32>(
                        skBitmap.GetPixelSpan(),
                        skBitmap.Width,
                        skBitmap.Height);

                    using var outputMs = new MemoryStream();
                    image.SaveAsPng(outputMs);
                    imagePages.Add(outputMs.ToArray());

                    _logger.LogInformation("PDF page {PageNumber} converted ({Size} bytes)", pageIndex + 1, outputMs.Length);
                    pageIndex++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Break on error (typically signals "no more pages" from PDFtoImage).
                    _logger.LogWarning("Stopping PDF conversion at page {PageIndex}: {ExceptionType} - {Message}",
                        pageIndex, ex.GetType().Name, ex.Message);
                    break;
                }
            }

            _logger.LogInformation("PDF conversion complete: {PageCount} pages converted to images", imagePages.Count);
            return Task.FromResult(Result<IReadOnlyList<byte[]>>.Success(imagePages));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<byte[]>>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF to images");
            return Task.FromResult(Result<IReadOnlyList<byte[]>>.WithFailure(
                $"PDF to image conversion failed: {ex.Message}", default, ex));
        }
    }
}
