using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Abstract interface for Python interoperability services.
/// This interface isolates Python implementation details from the domain layer.
/// </summary>
public interface IPythonInteropService
{
    /// <summary>
    /// Executes OCR processing using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config);

    /// <summary>
    /// Preprocesses an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config);

    /// <summary>
    /// Extracts structured fields from OCR text using Python modules.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence);

    /// <summary>
    /// Removes watermarks from an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData);

    /// <summary>
    /// Deskews an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    Task<Result<ImageData>> DeskewAsync(ImageData imageData);
}
