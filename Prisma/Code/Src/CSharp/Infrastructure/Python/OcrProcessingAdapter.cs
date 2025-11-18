namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// OCR processing adapter that implements domain interfaces using the abstract Python interop service.
/// This adapter maintains clean architecture by delegating to the abstract IPythonInteropService.
/// </summary>
public class OcrProcessingAdapter : IOcrExecutor, IImagePreprocessor, IFieldExtractor
{
    private readonly ILogger<OcrProcessingAdapter> _logger;
    private readonly IPythonInteropService _pythonInteropService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessingAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pythonInteropService">The Python interop service.</param>
    public OcrProcessingAdapter(ILogger<OcrProcessingAdapter> logger, IPythonInteropService pythonInteropService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pythonInteropService = pythonInteropService ?? throw new ArgumentNullException(nameof(pythonInteropService));
        
        _logger.LogInformation("Initializing OCR processing adapter with Python interop service");
    }

    /// <summary>
    /// Executes OCR on an image using the Python interop service.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        _logger.LogInformation("Executing OCR on image {SourcePath}", imageData.SourcePath);
        return await _pythonInteropService.ExecuteOcrAsync(imageData, config).ConfigureAwait(false);
    }

    /// <summary>
    /// Preprocesses an image using the Python interop service.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    public async Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        _logger.LogInformation("Preprocessing image {SourcePath}", imageData.SourcePath);
        return await _pythonInteropService.PreprocessAsync(imageData, config).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts structured fields from OCR text using the Python interop service.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        _logger.LogInformation("Extracting fields from text with confidence {Confidence}", confidence);
        return await _pythonInteropService.ExtractFieldsAsync(text, confidence).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes watermarks from an image using the Python interop service.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        _logger.LogInformation("Removing watermark from image {SourcePath}", imageData.SourcePath);
        return await _pythonInteropService.RemoveWatermarkAsync(imageData).ConfigureAwait(false);
    }

    /// <summary>
    /// Deskews an image using the Python interop service.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        _logger.LogInformation("Deskewing image {SourcePath}", imageData.SourcePath);
        return await _pythonInteropService.DeskewAsync(imageData).ConfigureAwait(false);
    }

    /// <summary>
    /// Binarizes an image using the Python interop service.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
    {
        _logger.LogInformation("Binarizing image {SourcePath}", imageData.SourcePath);
        
        try
        {
            // Call Python binarization module through interop service
            var result = await _pythonInteropService.BinarizeAsync(imageData).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Image binarization completed for {SourcePath}", imageData.SourcePath);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Image binarization failed for {SourcePath}: {Error}", 
                    imageData.SourcePath, errorMessage);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image binarization for {SourcePath}", imageData.SourcePath);
            return Result<ImageData>.WithFailure($"Image binarization failed: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Extracts expediente (file number) from text using the Python interop service.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted expediente or an error.</returns>
    public async Task<Result<string?>> ExtractExpedienteAsync(string text)
    {
        _logger.LogInformation("Extracting expediente from text");
        
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<string?>.Success(null);
            }
            
            // Call Python expediente extraction module through interop service
            var result = await _pythonInteropService.ExtractExpedienteAsync(text).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                var expediente = result.Value ?? string.Empty;
                _logger.LogInformation("Expediente extraction completed: {Expediente}", expediente);
                return Result<string?>.Success(expediente);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Expediente extraction failed: {Error}", errorMessage);
                return Result<string?>.Success(null); // Return null instead of failure for missing expediente
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expediente extraction");
            return Result<string?>.WithFailure(value: default, errors: new[] { $"Expediente extraction failed: {ex.Message}" }, exception: ex);
        }
    }

    /// <summary>
    /// Extracts causa (cause) from text using the Python interop service.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted causa or an error.</returns>
    public async Task<Result<string?>> ExtractCausaAsync(string text)
    {
        _logger.LogInformation("Extracting causa from text");
        
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<string?>.Success(null);
            }
            
            // Call Python section extraction module for causa
            var result = await _pythonInteropService.ExtractCausaAsync(text).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                var causa = result.Value ?? string.Empty;
                _logger.LogInformation("Causa extraction completed: {Causa}", causa);
                return Result<string?>.Success(causa);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Causa extraction failed: {Error}", errorMessage);
                return Result<string?>.Success(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during causa extraction");
            return Result<string?>.WithFailure(value: default, errors: new[] { $"Causa extraction failed: {ex.Message}" }, exception: ex);
        }
    }

    /// <summary>
    /// Extracts accion solicitada (requested action) from text using the Python interop service.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted accion solicitada or an error.</returns>
    public async Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
    {
        _logger.LogInformation("Extracting accion solicitada from text");
        
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<string?>.Success(null);
            }
            
            // Call Python section extraction module for accion solicitada
            var result = await _pythonInteropService.ExtractAccionSolicitadaAsync(text).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                var accion = result.Value ?? string.Empty;
                _logger.LogInformation("Accion solicitada extraction completed: {Accion}", accion);
                return Result<string?>.Success(accion);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Accion solicitada extraction failed: {Error}", errorMessage);
                return Result<string?>.Success(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during accion solicitada extraction");
            return Result<string?>.WithFailure(value: default, errors: new[] { $"Accion solicitada extraction failed: {ex.Message}" }, exception: ex);
        }
    }

    /// <summary>
    /// Extracts dates from text using the Python interop service.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted dates or an error.</returns>
    public async Task<Result<List<string>>> ExtractDatesAsync(string text)
    {
        _logger.LogInformation("Extracting dates from text");
        
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<List<string>>.Success(new List<string>());
            }
            
            // Call Python date extraction module through interop service
            var result = await _pythonInteropService.ExtractDatesAsync(text).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                var dates = result.Value ?? new List<string>();
                _logger.LogInformation("Date extraction completed: {DateCount} dates found", dates.Count);
                return Result<List<string>>.Success(dates);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Date extraction failed: {Error}", errorMessage);
                return Result<List<string>>.Success(new List<string>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during date extraction");
            return Result<List<string>>.WithFailure($"Date extraction failed: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Extracts monetary amounts from text using the Python interop service.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted amounts or an error.</returns>
    public async Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
    {
        _logger.LogInformation("Extracting amounts from text");
        
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<List<AmountData>>.Success(new List<AmountData>());
            }
            
            // Call Python amount extraction module through interop service
            var result = await _pythonInteropService.ExtractAmountsAsync(text).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                var amounts = result.Value ?? new List<AmountData>();
                _logger.LogInformation("Amount extraction completed: {AmountCount} amounts found", amounts.Count);
                return Result<List<AmountData>>.Success(amounts);
            }
            else
            {
                var errorMessage = result.Error is not null ? result.Error : "Unknown error";
                _logger.LogWarning("Amount extraction failed: {Error}", errorMessage);
                return Result<List<AmountData>>.Success(new List<AmountData>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during amount extraction");
            return Result<List<AmountData>>.WithFailure($"Amount extraction failed: {ex.Message}", default, ex);
        }
    }
}
