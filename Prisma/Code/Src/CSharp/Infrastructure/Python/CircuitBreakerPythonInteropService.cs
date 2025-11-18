namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Circuit breaker pattern implementation for Python interop service.
/// Provides advanced error handling and recovery mechanisms.
/// </summary>
public class CircuitBreakerPythonInteropService : IPythonInteropService, IDisposable
{
    private readonly ILogger<CircuitBreakerPythonInteropService> _logger;
    private readonly IPythonInteropService _innerService;
    private readonly CircuitBreakerState _circuitBreaker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerPythonInteropService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="innerService">The inner Python interop service.</param>
    /// <param name="failureThreshold">The number of failures before opening the circuit.</param>
    /// <param name="resetTimeout">The timeout before attempting to close the circuit.</param>
    public CircuitBreakerPythonInteropService(
        ILogger<CircuitBreakerPythonInteropService> logger,
        IPythonInteropService innerService,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _circuitBreaker = new CircuitBreakerState(failureThreshold, resetTimeout ?? TimeSpan.FromMinutes(1));
        
        _logger.LogInformation("Initializing circuit breaker Python interop service with failure threshold: {Threshold}", failureThreshold);
    }

    /// <summary>
    /// Executes OCR on an image with circuit breaker protection.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExecuteOcrAsync(imageData, config), "OCR execution").ConfigureAwait(false);
    }

    /// <summary>
    /// Preprocesses an image with circuit breaker protection.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    public async Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.PreprocessAsync(imageData, config), "image preprocessing").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts structured fields from OCR text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractFieldsAsync(text, confidence), "field extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes watermarks from an image with circuit breaker protection.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.RemoveWatermarkAsync(imageData), "watermark removal").ConfigureAwait(false);
    }

    /// <summary>
    /// Deskews an image with circuit breaker protection.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.DeskewAsync(imageData), "image deskewing").ConfigureAwait(false);
    }

    /// <summary>
    /// Binarizes an image with circuit breaker protection.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.BinarizeAsync(imageData), "image binarization").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts expediente from text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted expediente or an error.</returns>
    public async Task<Result<string?>> ExtractExpedienteAsync(string text)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractExpedienteAsync(text), "expediente extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts causa from text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted causa or an error.</returns>
    public async Task<Result<string?>> ExtractCausaAsync(string text)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractCausaAsync(text), "causa extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts accion solicitada from text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted accion solicitada or an error.</returns>
    public async Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractAccionSolicitadaAsync(text), "accion solicitada extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts dates from text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted dates or an error.</returns>
    public async Task<Result<List<string>>> ExtractDatesAsync(string text)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractDatesAsync(text), "date extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts amounts from text with circuit breaker protection.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted amounts or an error.</returns>
    public async Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
    {
        return await ExecuteWithCircuitBreakerAsync(() => _innerService.ExtractAmountsAsync(text), "amount extraction").ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <returns>A result containing the operation result or an error.</returns>
    private async Task<Result<T>> ExecuteWithCircuitBreakerAsync<T>(Func<Task<Result<T>>> operation, string operationName)
    {
        if (!_circuitBreaker.CanExecute())
        {
            _logger.LogWarning("Circuit breaker is open for {OperationName}. Request rejected.", operationName);
            return Result<T>.WithFailure($"Circuit breaker is open for {operationName}. Service temporarily unavailable.");
        }

        try
        {
            _logger.LogDebug("Executing {OperationName} with circuit breaker protection", operationName);
            var result = await operation().ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                _circuitBreaker.OnSuccess();
                _logger.LogDebug("{OperationName} completed successfully", operationName);
            }
            else
            {
                _circuitBreaker.OnFailure();
                _logger.LogWarning("{OperationName} failed: {Error}", operationName, result.Error);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _circuitBreaker.OnFailure();
            _logger.LogError(ex, "Exception during {OperationName}", operationName);
            return Result<T>.WithFailure($"Exception during {operationName}: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Disposes the circuit breaker service.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the circuit breaker service.
    /// </summary>
    /// <param name="disposing">True if disposing, false if finalizing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _logger.LogInformation("Disposing circuit breaker Python interop service");
            if (_innerService is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
internal class CircuitBreakerState
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private CircuitBreakerStatus _status;
    private DateTime _lastFailureTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerState"/> class.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before opening the circuit.</param>
    /// <param name="resetTimeout">The timeout before attempting to close the circuit.</param>
    public CircuitBreakerState(int failureThreshold, TimeSpan resetTimeout)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout;
        _status = CircuitBreakerStatus.Closed;
        _failureCount = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the circuit breaker can execute operations.
    /// </summary>
    /// <returns>True if operations can be executed; otherwise, false.</returns>
    public bool CanExecute()
    {
        switch (_status)
        {
            case CircuitBreakerStatus.Closed:
                return true;
            case CircuitBreakerStatus.Open:
                if (DateTime.UtcNow - _lastFailureTime >= _resetTimeout)
                {
                    _status = CircuitBreakerStatus.HalfOpen;
                    return true;
                }
                return false;
            case CircuitBreakerStatus.HalfOpen:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Called when an operation succeeds.
    /// </summary>
    public void OnSuccess()
    {
        switch (_status)
        {
            case CircuitBreakerStatus.Closed:
                _failureCount = 0;
                break;
            case CircuitBreakerStatus.HalfOpen:
                _status = CircuitBreakerStatus.Closed;
                _failureCount = 0;
                break;
        }
    }

    /// <summary>
    /// Called when an operation fails.
    /// </summary>
    public void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_status == CircuitBreakerStatus.Closed && _failureCount >= _failureThreshold)
        {
            _status = CircuitBreakerStatus.Open;
        }
        else if (_status == CircuitBreakerStatus.HalfOpen)
        {
            _status = CircuitBreakerStatus.Open;
        }
    }
}

/// <summary>
/// Represents the status of a circuit breaker.
/// </summary>
internal enum CircuitBreakerStatus
{
    /// <summary>
    /// Circuit is closed - operations are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - operations are blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - one operation is allowed to test recovery.
    /// </summary>
    HalfOpen
}
