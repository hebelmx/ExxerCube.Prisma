using System.Threading.Tasks;
using GotOcr2Sample.Domain.Models;
using GotOcr2Sample.Domain.ValueObjects;
using IndQuestResults;

namespace GotOcr2Sample.Domain.Interfaces;

/// <summary>
/// Defines the OCR executor service for performing OCR on images.
/// </summary>
public interface IOcrExecutor
{
    /// <summary>
    /// Executes OCR on an image and returns the extracted text with confidence scores.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config);
}
