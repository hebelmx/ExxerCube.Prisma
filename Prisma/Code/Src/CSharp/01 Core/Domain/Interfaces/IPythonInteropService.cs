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

    /// <summary>
    /// Binarizes an image using the Python binarization module.
    /// </summary>
    /// <param name="imageData">The image data to binarize.</param>
    /// <returns>A result containing the binarized image or an error.</returns>
    Task<Result<ImageData>> BinarizeAsync(ImageData imageData);

    /// <summary>
    /// Extracts expediente (case file number) from text using the Python expediente extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted expediente or an error.</returns>
    Task<Result<string?>> ExtractExpedienteAsync(string text);

    /// <summary>
    /// Extracts causa (cause) from text using the Python section extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted causa or an error.</returns>
    Task<Result<string?>> ExtractCausaAsync(string text);

    /// <summary>
    /// Extracts accion solicitada (requested action) from text using the Python section extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted accion solicitada or an error.</returns>
    Task<Result<string?>> ExtractAccionSolicitadaAsync(string text);

    /// <summary>
    /// Extracts dates from text using the Python date extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted dates or an error.</returns>
    Task<Result<List<string>>> ExtractDatesAsync(string text);

    /// <summary>
    /// Extracts monetary amounts from text using the Python amount extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted amounts or an error.</returns>
    Task<Result<List<AmountData>>> ExtractAmountsAsync(string text);
}
