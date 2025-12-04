using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Execution;

/// <summary>
/// Main OCR processing service that orchestrates the entire pipeline.
/// Implements Railway Oriented Programming for error handling and performance monitoring.
/// </summary>
public class OcrProcessingService : IOcrProcessingService
{
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<IOcrProcessingService> _logger;
    private readonly IProcessingMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessingService"/> class.
    /// </summary>
    /// <param name="imagePreprocessor">The image preprocessor service.</param>
    /// <param name="ocrExecutor">The OCR executor service.</param>
    /// <param name="fieldExtractor">The field extractor service.</param>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metricsService">The metrics service for performance monitoring.</param>
    public OcrProcessingService(
        IImagePreprocessor imagePreprocessor,
        IOcrExecutor ocrExecutor,
        IFieldExtractor fieldExtractor,
        IEventPublisher eventPublisher,
        ILogger<IOcrProcessingService> logger,
        IProcessingMetricsService metricsService)
    {
        _imagePreprocessor = imagePreprocessor;
        _ocrExecutor = ocrExecutor;
        _fieldExtractor = fieldExtractor;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Processes a document image and extracts structured data using Railway Oriented Programming.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the processing result or an error.</returns>
    public async Task<Result<ProcessingResult>> ProcessDocumentAsync(ImageData imageData, ProcessingConfig config, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Document processing cancelled before starting");
            return ResultExtensions.Cancelled<ProcessingResult>();
        }

        // Validate input first - return failure for null inputs
        if (imageData == null)
            return Result<ProcessingResult>.WithFailure($"Argument cannot be null: {nameof(imageData)}");

        if (config == null)
            return Result<ProcessingResult>.WithFailure($"Argument cannot be null: {nameof(config)}");

        var documentId = Guid.NewGuid().ToString();
        IProcessingContext? processingContext = null;

        try
        {
            // Validate image data content
            var validationResult = ValidateImageData(imageData);
            if (!validationResult.IsSuccess)
            {
                // Create a temporary context for error tracking
                processingContext = await _metricsService.StartProcessingAsync(documentId, "unknown").ConfigureAwait(false);
                await _metricsService.RecordErrorAsync(processingContext, validationResult.Error!).ConfigureAwait(false);
                return Result<ProcessingResult>.WithFailure(validationResult.Error!);
            }

            _logger.LogInformation("Starting document processing for {SourcePath}", imageData.SourcePath);

            // Start metrics tracking
            processingContext = await _metricsService.StartProcessingAsync(documentId, imageData.SourcePath).ConfigureAwait(false);

            // Check for cancellation before preprocessing
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Document processing cancelled before preprocessing");
                return ResultExtensions.Cancelled<ProcessingResult>();
            }

            var preprocessResult = await _imagePreprocessor.PreprocessAsync(imageData, config).ConfigureAwait(false);

            // Propagate cancellation from dependencies
            if (preprocessResult.IsCancelled())
            {
                _logger.LogWarning("Document processing cancelled by preprocessor");
                return ResultExtensions.Cancelled<ProcessingResult>();
            }

            if (preprocessResult.IsSuccess)
            {
                var preprocessedImage = preprocessResult.Value;
                if (preprocessedImage == null)
                {
                    await _metricsService.RecordErrorAsync(processingContext, "Preprocessing returned null result").ConfigureAwait(false);
                    return Result<ProcessingResult>.WithFailure("Preprocessing failed: No result returned");
                }

                // Check for cancellation before OCR
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Document processing cancelled before OCR");
                    return ResultExtensions.Cancelled<ProcessingResult>();
                }

                var ocrResult = await _ocrExecutor.ExecuteOcrAsync(preprocessedImage, config.OCRConfig).ConfigureAwait(false);

                // Propagate cancellation from dependencies
                if (ocrResult.IsCancelled())
                {
                    _logger.LogWarning("Document processing cancelled by OCR executor");
                    return ResultExtensions.Cancelled<ProcessingResult>();
                }

                if (ocrResult.IsSuccess)
                {
                    var ocrResultValue = ocrResult.Value;
                    if (ocrResultValue == null)
                    {
                        await _metricsService.RecordErrorAsync(processingContext, "OCR execution returned null result").ConfigureAwait(false);
                        return Result<ProcessingResult>.WithFailure("OCR execution failed: No result returned");
                    }

                    // Check for cancellation before field extraction
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Document processing cancelled before field extraction");
                        return ResultExtensions.Cancelled<ProcessingResult>();
                    }

                    var extractResult = await _fieldExtractor.ExtractFieldsAsync(ocrResultValue.Text, ocrResultValue.ConfidenceAvg).ConfigureAwait(false);

                    // Propagate cancellation from dependencies
                    if (extractResult.IsCancelled())
                    {
                        _logger.LogWarning("Document processing cancelled by field extractor");
                        return ResultExtensions.Cancelled<ProcessingResult>();
                    }

                    if (extractResult.IsSuccess)
                    {
                        var extractedFields = extractResult.Value;
                        if (extractedFields == null)
                        {
                            await _metricsService.RecordErrorAsync(processingContext, "Field extraction returned null result").ConfigureAwait(false);
                            return Result<ProcessingResult>.WithFailure("Field extraction failed: No result returned");
                        }

                        var processingResult = CreateProcessingResult(imageData, ocrResultValue, extractedFields);
                        await LogProcessingResult(processingResult);

                        // Publish OcrCompletedEvent for real-time monitoring
                        _eventPublisher.Publish(new OcrCompletedEvent
                        {
                            FileId = Guid.TryParse(documentId, out var fileGuid) ? fileGuid : Guid.NewGuid(),
                            OcrEngine = "Tesseract/GOT-OCR2", // TODO: Track actual engine used
                            Confidence = (decimal)ocrResultValue.ConfidenceAvg,
                            ExtractedTextLength = ocrResultValue.Text?.Length ?? 0,
                            ProcessingTime = TimeSpan.FromSeconds(1), // TODO: Calculate actual processing time
                            FallbackTriggered = false // TODO: Track fallback status
                        });

                        // Record successful completion
                        await _metricsService.CompleteProcessingAsync(processingContext, processingResult, true).ConfigureAwait(false);

                        return Result<ProcessingResult>.Success(processingResult);
                    }
                    else
                    {
                        await _metricsService.RecordErrorAsync(processingContext, extractResult.Error ?? "Field extraction failed").ConfigureAwait(false);
                        return Result<ProcessingResult>.WithFailure(extractResult.Error ?? "Field extraction failed");
                    }
                }
                else
                {
                    await _metricsService.RecordErrorAsync(processingContext, ocrResult.Error ?? "OCR execution failed").ConfigureAwait(false);
                    return Result<ProcessingResult>.WithFailure(ocrResult.Error ?? "OCR execution failed");
                }
            }
            else
            {
                await _metricsService.RecordErrorAsync(processingContext, preprocessResult.Error ?? "Preprocessing failed").ConfigureAwait(false);
                return Result<ProcessingResult>.WithFailure(preprocessResult.Error ?? "Preprocessing failed");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document processing cancelled for {SourcePath}", imageData.SourcePath);

            if (processingContext != null)
            {
                await _metricsService.RecordErrorAsync(processingContext, "Operation cancelled").ConfigureAwait(false);
            }

            return ResultExtensions.Cancelled<ProcessingResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing document {SourcePath}", imageData.SourcePath);

            if (processingContext != null)
            {
                await _metricsService.RecordErrorAsync(processingContext, ex.Message).ConfigureAwait(false);
            }
            else
            {
                // If we couldn't create a processing context, create a temporary one for error tracking
                var tempContext = await _metricsService.StartProcessingAsync(documentId, imageData.SourcePath).ConfigureAwait(false);
                await _metricsService.RecordErrorAsync(tempContext, ex.Message).ConfigureAwait(false);
                tempContext.Dispose();
            }

            return Result<ProcessingResult>.WithFailure($"Unexpected error: {ex.Message}", default, ex);
        }
        finally
        {
            processingContext?.Dispose();
        }
    }

    /// <summary>
    /// Processes multiple documents concurrently with proper error handling.
    /// </summary>
    /// <param name="imageDataList">The list of image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent operations.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of processing results or an error.</returns>
    public async Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(
        IEnumerable<ImageData> imageDataList,
        ProcessingConfig config,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Batch document processing cancelled before starting");
            return ResultExtensions.Cancelled<List<ProcessingResult>>();
        }

        // Validate inputs
        if (imageDataList == null)
            return Result<List<ProcessingResult>>.WithFailure($"Argument cannot be null: {nameof(imageDataList)}");

        if (config == null)
            return Result<List<ProcessingResult>>.WithFailure($"Argument cannot be null: {nameof(config)}");

        var imageDataArray = imageDataList.ToArray();
        _logger.LogInformation("Starting batch processing of {DocumentCount} documents", imageDataArray.Length);

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = imageDataArray.Select(async imageData =>
        {
            // CRITICAL FIX: Pass cancellation token to WaitAsync to prevent hanging
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ProcessDocumentAsync(imageData, config, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            var successfulResults = new List<ProcessingResult>();
            var cancelledResults = new List<Result<ProcessingResult>>();
            var failedResults = new List<Result<ProcessingResult>>();

            foreach (var result in results)
            {
                if (result.IsCancelled())
                {
                    cancelledResults.Add(result);
                }
                else if (result.IsSuccess)
                {
                    var value = result.Value;
                    if (value != null)
                    {
                        successfulResults.Add(value);
                    }
                }
                else
                {
                    failedResults.Add(result);
                }
            }

            // Handle cancellation with partial results
            var wasCancelled = cancellationToken.IsCancellationRequested || cancelledResults.Any();

            if (wasCancelled)
            {
                if (successfulResults.Count > 0)
                {
                    // Return partial results with warning about cancellation
                    var totalRequested = imageDataArray.Length;
                    var completed = successfulResults.Count;
                    var cancelled = cancelledResults.Count;
                    var confidence = (double)completed / totalRequested;
                    var missingDataRatio = (double)(cancelled + failedResults.Count) / totalRequested;

                    _logger.LogWarning(
                        "Batch document processing cancelled. Returning {CompletedCount} of {TotalCount} processed documents. " +
                        "Cancelled: {CancelledCount}, Failed: {FailedCount}",
                        completed, totalRequested, cancelled, failedResults.Count);

                    return Result<List<ProcessingResult>>.WithWarnings(
                        warnings: new[] { $"Operation was cancelled. Processed {completed} of {totalRequested} documents." },
                        value: successfulResults,
                        confidence: confidence,
                        missingDataRatio: missingDataRatio
                    );
                }
                else
                {
                    // No partial results - return cancelled
                    _logger.LogWarning("Batch document processing cancelled with no completed results");
                    return ResultExtensions.Cancelled<List<ProcessingResult>>();
                }
            }

            // No cancellation - check for failures
            if (failedResults.Any())
            {
                _logger.LogWarning("Batch processing completed with {FailedCount} failures out of {TotalCount}",
                    failedResults.Count, imageDataArray.Length);
            }

            return Result<List<ProcessingResult>>.Success(successfulResults);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // If we catch cancellation exception, try to collect any partial results
            // Note: Task.WhenAll may have completed some tasks before cancellation
            _logger.LogInformation("Batch document processing cancelled");

            // If we have no way to collect partial results here, return cancelled
            // (In practice, Task.WhenAll will complete all tasks even if one throws)
            return ResultExtensions.Cancelled<List<ProcessingResult>>();
        }
    }

    /// <summary>
    /// Validates the image data for processing.
    /// </summary>
    /// <param name="imageData">The image data to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private static Result<ImageData> ValidateImageData(ImageData imageData)
    {
        if (imageData == null)
            return Result<ImageData>.WithFailure("Image data cannot be null");

        if (string.IsNullOrEmpty(imageData.SourcePath))
            return Result<ImageData>.WithFailure("Image source path is required");

        if (imageData.Data == null || imageData.Data.Length == 0)
            return Result<ImageData>.WithFailure("Image data is empty");

        if (imageData.PageNumber <= 0)
            return Result<ImageData>.WithFailure("Page number must be greater than 0");

        if (imageData.TotalPages <= 0)
            return Result<ImageData>.WithFailure("Total pages must be greater than 0");

        return Result<ImageData>.Success(imageData);
    }

    /// <summary>
    /// Creates a processing result from the extracted data.
    /// </summary>
    /// <param name="imageData">The original image data.</param>
    /// <param name="ocrResult">The OCR result.</param>
    /// <param name="extractedFields">The extracted fields.</param>
    /// <returns>A processing result.</returns>
    private static ProcessingResult CreateProcessingResult(ImageData imageData, OCRResult? ocrResult, ExtractedFields extractedFields)
    {
        return new ProcessingResult(
            sourcePath: imageData.SourcePath,
            pageNumber: imageData.PageNumber,
            ocrResult: ocrResult ?? new OCRResult(),
            extractedFields: extractedFields);
    }

    /// <summary>
    /// Logs the processing result.
    /// </summary>
    /// <param name="result">The processing result to log.</param>
    private Task LogProcessingResult(ProcessingResult result)
    {
        _logger.LogInformation("Completed processing document {SourcePath} with {FieldCount} extracted fields",
            result.SourcePath,
            result.ExtractedFields.Fechas.Count + result.ExtractedFields.Montos.Count);

        return Task.CompletedTask;
    }
}