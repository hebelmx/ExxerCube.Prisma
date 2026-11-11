namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.GotOcr2;

/// <summary>
/// GOT-OCR2 implementation of IOcrExecutor using CSnakes Python interop.
/// Provides high-accuracy OCR using the GOT-OCR2 transformer model for complex documents.
/// </summary>
public class GotOcr2OcrExecutor : IOcrExecutor
{
    private readonly IPythonEnvironment _pythonEnvironment;
    private readonly ILogger<GotOcr2OcrExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GotOcr2OcrExecutor"/> class.
    /// </summary>
    /// <param name="pythonEnvironment">The Python environment for executing Python code.</param>
    /// <param name="logger">The logger instance.</param>
    public GotOcr2OcrExecutor(
        IPythonEnvironment pythonEnvironment,
        ILogger<GotOcr2OcrExecutor> logger)
    {
        _pythonEnvironment = pythonEnvironment ?? throw new ArgumentNullException(nameof(pythonEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes OCR on an image using GOT-OCR2 model via Python.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        try
        {
            // Validate input first (before any logging that accesses imageData properties)
            if (imageData == null)
            {
                _logger.LogWarning("Null image data provided");
                return Result<OCRResult>.Failure("Image data is null");
            }

            if (imageData.Data == null || imageData.Data.Length == 0)
            {
                _logger.LogWarning("Empty image data provided");
                return Result<OCRResult>.Failure("Image data is empty");
            }

            _logger.LogInformation(
                "Executing GOT-OCR2 OCR on image: {SourcePath}, Page: {PageNumber}/{TotalPages}",
                imageData.SourcePath,
                imageData.PageNumber,
                imageData.TotalPages);

            // Get the Python module wrapper using CSnakes-generated strongly-typed extension method
            var gotOcr2Module = _pythonEnvironment.GotOcr2Wrapper();

            // Execute OCR using the Python wrapper
            // Python function signature: execute_ocr(image_bytes: bytes, language: str, confidence_threshold: float)
            // Returns: (text: str, confidence_avg: float, confidence_median: float, confidences: List[float], language_used: str)
            _logger.LogDebug(
                "Calling Python execute_ocr with language={Language}, threshold={Threshold}",
                config.Language,
                config.ConfidenceThreshold);

            // Call Python method directly
            var pythonResult = gotOcr2Module.ExecuteOcr(
                imageData.Data,
                config.Language,
                config.ConfidenceThreshold
            );

            // Extract tuple results
            // Python returns: (str, float, float, list[float], str)
            string extractedText = pythonResult.Item1;
            double confidenceAvg = pythonResult.Item2;
            double confidenceMedian = pythonResult.Item3;
            dynamic confidencesListDynamic = pythonResult.Item4; // IEnumerable<double> from CSnakes
            string languageUsed = pythonResult.Item5;

            // Log if Python returned empty results (indicates an error was caught silently)
            if (string.IsNullOrEmpty(extractedText) && confidenceAvg == 0.0)
            {
                _logger.LogWarning(
                    "Python returned empty OCR result with 0 confidence. This typically indicates an exception was caught in Python. " +
                    "Check Python logs or enable Python diagnostic output.");
            }

            // Convert confidences to List<float> - cast dynamic to IEnumerable first
            var confidences = ((System.Collections.Generic.IEnumerable<double>)confidencesListDynamic)
                .Select(c => (float)c)
                .ToList();

            _logger.LogInformation(
                "GOT-OCR2 OCR completed. Text length: {TextLength}, Confidence avg: {ConfidenceAvg:F2}",
                extractedText.Length,
                confidenceAvg);

            // Create OCRResult
            var ocrResult = new OCRResult(
                text: extractedText,
                confidenceAvg: (float)confidenceAvg,
                confidenceMedian: (float)confidenceMedian,
                confidences: confidences,
                languageUsed: languageUsed
            );

            return Result<OCRResult>.Success(ocrResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOT-OCR2 OCR execution failed for {SourcePath}", imageData.SourcePath);
            return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
        }
    }
}
