namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Deprecated dummy implementation of IPythonInteropService.
/// This service is deprecated and will be removed in a future release.
/// All methods return failure results indicating the service is deprecated.
/// </summary>
public class DeprecatedPythonInteropService : IPythonInteropService
{
    private const string DeprecationMessage = "IPythonInteropService is deprecated and will be removed in a future release. This is a temporary dummy implementation.";

    /// <summary>
    /// Executes OCR processing using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config) =>
        Task.FromResult(Result<OCRResult>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Preprocesses an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config) =>
        Task.FromResult(Result<ImageData>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts structured fields from OCR text using Python modules.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence) =>
        Task.FromResult(Result<ExtractedFields>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Removes watermarks from an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData) =>
        Task.FromResult(Result<ImageData>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Deskews an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<ImageData>> DeskewAsync(ImageData imageData) =>
        Task.FromResult(Result<ImageData>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Binarizes an image using the Python binarization module.
    /// </summary>
    /// <param name="imageData">The image data to binarize.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<ImageData>> BinarizeAsync(ImageData imageData) =>
        Task.FromResult(Result<ImageData>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts expediente (case file number) from text using the Python expediente extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<string?>> ExtractExpedienteAsync(string text) =>
        Task.FromResult(Result<string?>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts causa (cause) from text using the Python section extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<string?>> ExtractCausaAsync(string text) =>
        Task.FromResult(Result<string?>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts accion solicitada (requested action) from text using the Python section extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<string?>> ExtractAccionSolicitadaAsync(string text) =>
        Task.FromResult(Result<string?>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts dates from text using the Python date extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<List<string>>> ExtractDatesAsync(string text) =>
        Task.FromResult(Result<List<string>>.WithFailure(DeprecationMessage));

    /// <summary>
    /// Extracts monetary amounts from text using the Python amount extractor.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A failure result indicating the service is deprecated.</returns>
    public Task<Result<List<AmountData>>> ExtractAmountsAsync(string text) =>
        Task.FromResult(Result<List<AmountData>>.WithFailure(DeprecationMessage));
}

