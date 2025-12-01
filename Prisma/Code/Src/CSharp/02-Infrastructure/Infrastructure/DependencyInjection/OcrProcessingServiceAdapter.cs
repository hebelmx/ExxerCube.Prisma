namespace ExxerCube.Prisma.Infrastructure.DependencyInjection;

/// <summary>
/// Infrastructure adapter that implements IOcrProcessingService by delegating to Application's OcrProcessingService.
/// This maintains architectural compliance: Domain interfaces are implemented in Infrastructure, not Application.
/// </summary>
public sealed class OcrProcessingServiceAdapter : IOcrProcessingService
{
    private readonly IOcrProcessingService _ocrProcessingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessingServiceAdapter"/> class.
    /// </summary>
    /// <param name="ocrProcessingService">The Application OCR processing service to delegate to.</param>
    public OcrProcessingServiceAdapter(IOcrProcessingService ocrProcessingService)
    {
        _ocrProcessingService = ocrProcessingService;
    }

    /// <summary>
    /// Processes a document image and extracts structured data.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the processing result or an error.</returns>
    public Task<Result<ProcessingResult>> ProcessDocumentAsync(
        ImageData imageData,
        ProcessingConfig config,
        CancellationToken cancellationToken = default) =>
        _ocrProcessingService.ProcessDocumentAsync(imageData, config, cancellationToken);

    /// <summary>
    /// Processes multiple documents concurrently.
    /// </summary>
    /// <param name="imageDataList">The list of image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent operations.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of processing results or an error.</returns>
    public Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(
        IEnumerable<ImageData> imageDataList,
        ProcessingConfig config,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default) =>
        _ocrProcessingService.ProcessDocumentsAsync(imageDataList, config, maxConcurrency, cancellationToken);
}