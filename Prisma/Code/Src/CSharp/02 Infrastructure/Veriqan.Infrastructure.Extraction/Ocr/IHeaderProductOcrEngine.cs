using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;

/// <summary>
/// OCR engine seam for reading the product-name heading off a rendered page-1 header-band
/// crop (design doc <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §4.C). Veriqan-owned —
/// deliberately independent of Prisma's <c>Infrastructure.Extraction.Ocr.Teseract.TesseractOcrExecutor</c>
/// (different bounded context; no reference should be added to it).
/// </summary>
public interface IHeaderProductOcrEngine
{
    /// <summary>
    /// Runs OCR over a PNG-encoded crop and returns the raw recognized text.
    /// </summary>
    /// <param name="cropPngBytes">PNG-encoded bytes of the rendered header-band crop.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with the raw OCR text (never <see langword="null"/>, may
    /// be empty when nothing was recognized); a cancelled result when
    /// <paramref name="cancellationToken"/> is triggered; a failure result only for genuine
    /// infrastructure errors (native engine failure, corrupt image bytes).
    /// </returns>
    Task<Result<string>> RecognizeAsync(byte[] cropPngBytes, CancellationToken cancellationToken = default);
}
