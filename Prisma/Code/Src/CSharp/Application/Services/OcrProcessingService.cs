using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Main OCR processing service that orchestrates the entire pipeline.
/// Implements Railway Oriented Programming for error handling and performance monitoring.
/// </summary>
public class OcrProcessingService : IOcrProcessingService
{
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly ILogger<OcrProcessingService> _logger;
    private readonly ProcessingMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessingService"/> class.
    /// </summary>
    /// <param name="imagePreprocessor">The image preprocessor service.</param>
    /// <param name="ocrExecutor">The OCR executor service.</param>
    /// <param name="fieldExtractor">The field extractor service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metricsService">The metrics service for performance monitoring.</param>
    public OcrProcessingService(
        IImagePreprocessor imagePreprocessor,
        IOcrExecutor ocrExecutor,
        IFieldExtractor fieldExtractor,
        ILogger<OcrProcessingService> logger,
        ProcessingMetricsService metricsService)
    {
        _imagePreprocessor = imagePreprocessor;
        _ocrExecutor = ocrExecutor;
        _fieldExtractor = fieldExtractor;
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Processes a document image and extracts structured data using Railway Oriented Programming.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the processing result or an error.</returns>
    public async Task<Result<ProcessingResult>> ProcessDocumentAsync(ImageData imageData, ProcessingConfig config)
    {
        // Validate input first - throw ArgumentNullException for null inputs
        if (imageData == null)
            throw new ArgumentNullException(nameof(imageData));
        
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var documentId = Guid.NewGuid().ToString();
        ProcessingContext? processingContext = null;

        try
        {
            // Validate image data content
            var validationResult = ValidateImageData(imageData);
            if (!validationResult.IsSuccess)
            {
                // Create a temporary context for error tracking
                processingContext = await _metricsService.StartProcessingAsync(documentId, "unknown");
                await _metricsService.RecordErrorAsync(processingContext, validationResult.Error!);
                return Result<ProcessingResult>.Failure(validationResult.Error!);
            }

            _logger.LogInformation("Starting document processing for {SourcePath}", imageData.SourcePath);
            
            // Start metrics tracking
            processingContext = await _metricsService.StartProcessingAsync(documentId, imageData.SourcePath);

            var preprocessResult = await _imagePreprocessor.PreprocessAsync(imageData, config);
            if (!preprocessResult.IsSuccess)
            {
                await _metricsService.RecordErrorAsync(processingContext, preprocessResult.Error!);
                return Result<ProcessingResult>.Failure(preprocessResult.Error!);
            }

            var ocrResult = await _ocrExecutor.ExecuteOcrAsync(preprocessResult.Value!, config.OCRConfig);
            if (!ocrResult.IsSuccess)
            {
                await _metricsService.RecordErrorAsync(processingContext, ocrResult.Error!);
                return Result<ProcessingResult>.Failure(ocrResult.Error!);
            }

            var extractResult = await _fieldExtractor.ExtractFieldsAsync(ocrResult.Value!.Text, ocrResult.Value!.ConfidenceAvg);
            if (!extractResult.IsSuccess)
            {
                await _metricsService.RecordErrorAsync(processingContext, extractResult.Error!);
                return Result<ProcessingResult>.Failure(extractResult.Error!);
            }

            var processingResult = CreateProcessingResult(imageData, ocrResult.Value!, extractResult.Value!);
            await LogProcessingResult(processingResult);

            // Record successful completion
            await _metricsService.CompleteProcessingAsync(processingContext, processingResult, true);

            return Result<ProcessingResult>.Success(processingResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing document {SourcePath}", imageData.SourcePath);
            
            if (processingContext != null)
            {
                await _metricsService.RecordErrorAsync(processingContext, ex.Message);
            }
            else
            {
                // If we couldn't create a processing context, create a temporary one for error tracking
                var tempContext = await _metricsService.StartProcessingAsync(documentId, imageData.SourcePath);
                await _metricsService.RecordErrorAsync(tempContext, ex.Message);
                tempContext.Dispose();
            }
            
            return Result<ProcessingResult>.Failure($"Unexpected error: {ex.Message}");
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
    /// <returns>A result containing the list of processing results or an error.</returns>
    public async Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(
        IEnumerable<ImageData> imageDataList, 
        ProcessingConfig config, 
        int maxConcurrency = 5)
    {
        // Validate inputs
        if (imageDataList == null)
            throw new ArgumentNullException(nameof(imageDataList));
        
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var imageDataArray = imageDataList.ToArray();
        _logger.LogInformation("Starting batch processing of {DocumentCount} documents", imageDataArray.Length);

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = imageDataArray.Select(async imageData =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProcessDocumentAsync(imageData, config);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var successfulResults = results.Where(r => r.IsSuccess).Select(r => r.Value!).ToList();
        var failedResults = results.Where(r => !r.IsSuccess).ToList();

        if (failedResults.Any())
        {
            _logger.LogWarning("Batch processing completed with {FailedCount} failures out of {TotalCount}", 
                failedResults.Count, imageDataArray.Length);
        }

        return Result<List<ProcessingResult>>.Success(successfulResults);
    }

    /// <summary>
    /// Validates the image data for processing.
    /// </summary>
    /// <param name="imageData">The image data to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private static Result<ImageData> ValidateImageData(ImageData imageData)
    {
        if (imageData == null)
            return Result<ImageData>.Failure("Image data cannot be null");

        if (string.IsNullOrEmpty(imageData.SourcePath))
            return Result<ImageData>.Failure("Image source path is required");

        if (imageData.Data == null || imageData.Data.Length == 0)
            return Result<ImageData>.Failure("Image data is empty");

        if (imageData.PageNumber <= 0)
            return Result<ImageData>.Failure("Page number must be greater than 0");

        if (imageData.TotalPages <= 0)
            return Result<ImageData>.Failure("Total pages must be greater than 0");

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
