namespace ExxerCube.Prisma.Infrastructure.NoOp;

/// <summary>
/// No-op image preprocessor that passes through images without modification.
/// Used when preprocessing is not needed (e.g., PDF files processed directly by OCR engines).
/// Modern OCR engines (Tesseract, GOT-OCR2) handle preprocessing internally.
/// </summary>
public class NoOpImagePreprocessor : IImagePreprocessor
{
    /// <summary>
    /// Passes through the image data without any preprocessing.
    /// </summary>
    /// <param name="imageData">The input image data.</param>
    /// <param name="config">The processing configuration (ignored).</param>
    /// <returns>A successful result with the same image data.</returns>
    public Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        if (imageData == null)
        {
            return Task.FromResult(Result<ImageData>.WithFailure("Image data cannot be null"));
        }

        // Pass through without modification - modern OCR engines handle preprocessing internally
        return Task.FromResult(Result<ImageData>.WithSuccess(imageData));
    }

    /// <summary>
    /// No-op watermark removal - passes through image unchanged.
    /// </summary>
    public Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        return imageData == null
            ? Task.FromResult(Result<ImageData>.WithFailure("Image data cannot be null"))
            : Task.FromResult(Result<ImageData>.WithSuccess(imageData));
    }

    /// <summary>
    /// No-op deskew - passes through image unchanged.
    /// </summary>
    public Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        return imageData == null
            ? Task.FromResult(Result<ImageData>.WithFailure("Image data cannot be null"))
            : Task.FromResult(Result<ImageData>.WithSuccess(imageData));
    }

    /// <summary>
    /// No-op binarization - passes through image unchanged.
    /// </summary>
    public Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
    {
        return imageData == null
            ? Task.FromResult(Result<ImageData>.WithFailure("Image data cannot be null"))
            : Task.FromResult(Result<ImageData>.WithSuccess(imageData));
    }
}
