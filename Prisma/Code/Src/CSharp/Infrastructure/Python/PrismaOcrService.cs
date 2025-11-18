namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// OCR processing service using CSnakes for type-safe Python integration.
/// </summary>
public class PrismaOcrService : IOcrProcessingService
{
    private readonly ILogger<PrismaOcrService> _logger;
    private readonly IPythonEnvironment _pythonEnv;

    /// <summary>
    /// Initializes a new instance of the PrismaOcrService.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PrismaOcrService(ILogger<PrismaOcrService> logger)
    {
        _logger = logger;
        _pythonEnv = PrismaPythonEnvironment.Env;
    }

    /// <summary>
    /// Processes a document image and extracts structured data.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the processing result or an error.</returns>
    public Task<Result<ProcessingResult>> ProcessDocumentAsync(ImageData imageData, ProcessingConfig config, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OCR processing cancelled before starting");
            return Task.FromResult(ResultExtensions.Cancelled<ProcessingResult>());
        }

        try
        {
            _logger.LogInformation("Starting OCR processing for image {SourcePath}", imageData.SourcePath);

            // TODO: Replace with actual CSnakes generated method once bindings are available
            // For now, return a placeholder implementation
            _logger.LogWarning("CSnakes bindings not yet generated. Using placeholder implementation.");

            // Create placeholder OCR result
            var ocrResultEntity = new OCRResult
            {
                Text = "Placeholder OCR text - CSnakes bindings not yet generated",
                ConfidenceAvg = 0.0f,
                ConfidenceMedian = 0.0f,
                Confidences = new List<float>(),
                LanguageUsed = config.OCRConfig.Language
            };

            // Create placeholder extracted fields
            var extractedFields = new ExtractedFields
            {
                Expediente = "Placeholder expediente",
                Causa = "Placeholder causa",
                AccionSolicitada = "Placeholder accion solicitada",
                Fechas = new List<string>(),
                Montos = new List<AmountData>()
            };

            // Create processing result
            var processingResult = new ProcessingResult(
                sourcePath: imageData.SourcePath,
                pageNumber: imageData.PageNumber,
                ocrResult: ocrResultEntity,
                extractedFields: extractedFields,
                outputPath: null,
                processingErrors: new List<string> { "CSnakes bindings not yet generated" }
            );

            _logger.LogInformation("OCR processing completed successfully for image {SourcePath}", imageData.SourcePath);
            return Task.FromResult(Result<ProcessingResult>.Success(processingResult));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("OCR processing cancelled for image {SourcePath}", imageData.SourcePath);
            return Task.FromResult(ResultExtensions.Cancelled<ProcessingResult>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OCR processing for image {SourcePath}", imageData.SourcePath);
            return Task.FromResult(Result<ProcessingResult>.WithFailure($"OCR processing failed: {ex.Message}", default, ex));
        }
    }

    /// <summary>
    /// Processes multiple documents concurrently.
    /// </summary>
    /// <param name="imageDataList">The list of image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent operations.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of processing results or an error.</returns>
    public async Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(IEnumerable<ImageData> imageDataList, ProcessingConfig config, int maxConcurrency = 5, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Batch OCR processing cancelled before starting");
            return ResultExtensions.Cancelled<List<ProcessingResult>>();
        }

        try
        {
            _logger.LogInformation("Starting batch OCR processing for {Count} images", imageDataList.Count());

            var results = new List<ProcessingResult>();
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = imageDataList.Select(async imageData =>
            {
                // CRITICAL: Pass cancellation token to WaitAsync to prevent hanging
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var result = await ProcessDocumentAsync(imageData, config, cancellationToken).ConfigureAwait(false);
                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var taskResults = await Task.WhenAll(tasks).ConfigureAwait(false);

            var successfulResults = new List<ProcessingResult>();
            var cancelledResults = taskResults.Where(r => r.IsCancelled()).ToList();
            var failedResults = taskResults.Where(r => !r.IsSuccess && !r.IsCancelled()).ToList();

            // Extract successful results
            foreach (var result in taskResults)
            {
                if (result.IsSuccess)
                {
                    var value = result.Value;
                    if (value != null)
                    {
                        successfulResults.Add(value);
                    }
                }
            }

            // Handle cancellation with partial results
            var wasCancelled = cancellationToken.IsCancellationRequested || cancelledResults.Any();
            
            if (wasCancelled)
            {
                if (successfulResults.Count > 0)
                {
                    // Return partial results with warning about cancellation
                    var totalRequested = imageDataList.Count();
                    var completed = successfulResults.Count;
                    var cancelled = cancelledResults.Count;
                    var confidence = (double)completed / totalRequested;
                    var missingDataRatio = (double)(cancelled + failedResults.Count) / totalRequested;
                    
                    _logger.LogWarning(
                        "Batch OCR processing cancelled. Returning {CompletedCount} of {TotalCount} processed images. " +
                        "Cancelled: {CancelledCount}, Failed: {FailedCount}",
                        completed, totalRequested, cancelled, failedResults.Count);
                    
                    return Result<List<ProcessingResult>>.WithWarnings(
                        warnings: new[] { $"Operation was cancelled. Processed {completed} of {totalRequested} images." },
                        value: successfulResults,
                        confidence: confidence,
                        missingDataRatio: missingDataRatio
                    );
                }
                else
                {
                    // No partial results - return cancelled
                    _logger.LogWarning("Batch OCR processing cancelled with no completed results");
                    return ResultExtensions.Cancelled<List<ProcessingResult>>();
                }
            }

            // No cancellation - check for failures
            if (failedResults.Any())
            {
                var errorMessages = string.Join("; ", failedResults.Select(r => r.Error));
                _logger.LogError("Batch processing failed with errors: {Errors}", errorMessages);
                return Result<List<ProcessingResult>>.WithFailure($"Batch processing failed: {errorMessages}");
            }
            
            _logger.LogInformation("Batch OCR processing completed successfully for {Count} images", successfulResults.Count);
            return Result<List<ProcessingResult>>.Success(successfulResults);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch OCR processing cancelled");
            return ResultExtensions.Cancelled<List<ProcessingResult>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during batch OCR processing");
            return Result<List<ProcessingResult>>.WithFailure($"Batch OCR processing failed: {ex.Message}", default, ex);
        }
    }
}
