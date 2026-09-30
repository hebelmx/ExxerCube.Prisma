namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the image preprocessing service for preparing images for OCR.
/// </summary>
public interface IImagePreprocessor
{
    /// <summary>
    /// Preprocesses an image for OCR processing.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config);

    /// <summary>
    /// Removes watermarks from an image.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData);

    /// <summary>
    /// Deskews (straightens) an image.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    Task<Result<ImageData>> DeskewAsync(ImageData imageData);

    /// <summary>
    /// Binarizes an image (converts to black and white).
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    Task<Result<ImageData>> BinarizeAsync(ImageData imageData);
}
