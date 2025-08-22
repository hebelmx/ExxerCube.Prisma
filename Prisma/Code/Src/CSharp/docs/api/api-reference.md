# API Reference Documentation

## Overview

This document provides a comprehensive reference for the ExxerCube OCR Pipeline C# API, including all public interfaces, classes, and methods built on .NET 10 with Railway Oriented Programming patterns.

## Namespace: ExxerCube.Ocr.Domain

### Core Entities

#### ImageData

Represents a document image with metadata for OCR processing.

```csharp
/// <summary>
/// Represents a document image with metadata for OCR processing.
/// </summary>
public class ImageData
{
    /// <summary>
    /// Gets or sets the raw image data as a byte array.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the source file path of the image.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number within the document.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages in the document.
    /// </summary>
    public int TotalPages { get; set; }
}
```

#### OCRResult

Represents the result of OCR processing on a document image.

```csharp
/// <summary>
/// Represents the result of OCR processing on a document image.
/// </summary>
public class OCRResult
{
    /// <summary>
    /// Gets or sets the extracted text from the document.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the average confidence score for the OCR result.
    /// </summary>
    /// <remarks>
    /// The confidence score ranges from 0.0 to 1.0, where 1.0 represents
    /// 100% confidence in the OCR accuracy.
    /// </remarks>
    public float ConfidenceAvg { get; set; }

    /// <summary>
    /// Gets or sets the median confidence score for the OCR result.
    /// </summary>
    public float ConfidenceMedian { get; set; }

    /// <summary>
    /// Gets or sets the list of confidence scores for each word.
    /// </summary>
    public List<float> Confidences { get; set; } = new();

    /// <summary>
    /// Gets or sets the language used for OCR processing.
    /// </summary>
    public string LanguageUsed { get; set; } = string.Empty;
}
```

#### ExtractedFields

Represents the structured data extracted from a document.

```csharp
/// <summary>
/// Represents the structured data extracted from a document.
/// </summary>
public class ExtractedFields
{
    /// <summary>
    /// Gets or sets the expediente (case file number).
    /// </summary>
    public string? Expediente { get; set; }

    /// <summary>
    /// Gets or sets the causa (case type).
    /// </summary>
    public string? Causa { get; set; }

    /// <summary>
    /// Gets or sets the accion solicitada (requested action).
    /// </summary>
    public string? AccionSolicitada { get; set; }

    /// <summary>
    /// Gets or sets the list of dates found in the document.
    /// </summary>
    public List<string> Fechas { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of monetary amounts found in the document.
    /// </summary>
    public List<AmountData> Montos { get; set; } = new();
}
```

#### ProcessingConfig

Configuration for OCR processing pipeline.

```csharp
/// <summary>
/// Configuration for OCR processing pipeline.
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// Gets or sets whether to remove watermarks from images.
    /// </summary>
    public bool RemoveWatermark { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to deskew (straighten) images.
    /// </summary>
    public bool Deskew { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to binarize (convert to black and white) images.
    /// </summary>
    public bool Binarize { get; set; } = true;

    /// <summary>
    /// Gets or sets the OCR configuration.
    /// </summary>
    public OCRConfig OcrConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to extract document sections.
    /// </summary>
    public bool ExtractSections { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to normalize text output.
    /// </summary>
    public bool NormalizeText { get; set; } = true;
}
```

### Railway Oriented Programming

#### Result<T>

Core result type for Railway Oriented Programming.

```csharp
/// <summary>
/// Represents a result that can be either a success or failure.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the success value if the result is successful.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message if the result is a failure.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) => new(value, true, null);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failure result.</returns>
    public static Result<T> Failure(string error) => new(default, false, error);

    private Result(T? value, bool isSuccess, string? error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }
}
```

#### ResultExtensions

Extension methods for fluent Result<T> operations.

```csharp
/// <summary>
/// Extension methods for Result<T> to enable fluent operations.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Binds a function to a result, continuing the railway if successful.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to bind.</param>
    /// <returns>A new result.</returns>
    public static async Task<Result<TResult>> Bind<T, TResult>(
        this Result<T> result,
        Func<T, Task<Result<TResult>>> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return await func(result.Value!);
    }

    /// <summary>
    /// Maps a function over a successful result.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to map.</param>
    /// <returns>A new result.</returns>
    public static Result<TResult> Map<T, TResult>(
        this Result<T> result,
        Func<T, TResult> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return Result<TResult>.Success(func(result.Value!));
    }

    /// <summary>
    /// Executes an action on a successful result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original result.</returns>
    public static Result<T> Tap<T>(
        this Result<T> result,
        Action<T> action)
    {
        if (result.IsSuccess)
            action(result.Value!);

        return result;
    }
}
```

### Domain Interfaces (Ports)

#### IOcrProcessingService

Defines the contract for OCR processing services.

```csharp
/// <summary>
/// Defines the contract for OCR processing services.
/// </summary>
/// <remarks>
/// Implementations should handle the complete OCR workflow using Railway Oriented Programming
/// to provide error handling without exceptions.
/// </remarks>
public interface IOcrProcessingService
{
    /// <summary>
    /// Executes OCR processing on the provided image data.
    /// </summary>
    /// <param name="image">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    Task<Result<OCRResult>> ExecuteOcrAsync(ImageData image, ProcessingConfig config);
}
```

#### IImagePreprocessor

Defines the contract for image preprocessing services.

```csharp
/// <summary>
/// Defines the contract for image preprocessing services.
/// </summary>
public interface IImagePreprocessor
{
    /// <summary>
    /// Preprocesses an image for OCR processing.
    /// </summary>
    /// <param name="image">The image to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    Task<Result<ImageData>> PreprocessImageAsync(ImageData image, ProcessingConfig config);
}
```

#### ITextFieldExtractor

Defines the contract for text field extraction services.

```csharp
/// <summary>
/// Defines the contract for text field extraction services.
/// </summary>
public interface ITextFieldExtractor
{
    /// <summary>
    /// Extracts structured fields from OCR text.
    /// </summary>
    /// <param name="text">The OCR text to extract fields from.</param>
    /// <param name="confidence">The confidence score of the OCR result.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence);
}
```

#### IFileLoader

Defines the contract for file loading services.

```csharp
/// <summary>
/// Defines the contract for file loading services.
/// </summary>
public interface IFileLoader
{
    /// <summary>
    /// Loads images from the specified path.
    /// </summary>
    /// <param name="path">The path to load images from.</param>
    /// <returns>A result containing the loaded images or an error.</returns>
    Task<Result<List<ImageData>>> LoadImagesAsync(string path);
}
```

#### IOutputWriter

Defines the contract for output writing services.

```csharp
/// <summary>
/// Defines the contract for output writing services.
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Writes processing results to the specified output path.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The path to write the output to.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<Unit>> WriteOutputAsync(ProcessingResult result, string outputPath);
}
```

## Namespace: ExxerCube.Ocr.Application

### Commands and Handlers

#### ProcessDocumentCommand

Command for processing a document.

```csharp
/// <summary>
/// Command for processing a document.
/// </summary>
public class ProcessDocumentCommand
{
    /// <summary>
    /// Gets or sets the input path containing documents to process.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output path for processing results.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processing configuration.
    /// </summary>
    public ProcessingConfig Config { get; set; } = new();
}
```

#### ProcessDocumentHandler

Handles document processing commands using Railway Oriented Programming.

```csharp
/// <summary>
/// Handles document processing commands using Railway Oriented Programming.
/// </summary>
public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand, Result<List<ProcessingResult>>>
{
    private readonly IOcrProcessingService _ocrService;
    private readonly IImagePreprocessor _preprocessor;
    private readonly ITextFieldExtractor _extractor;
    private readonly IFileLoader _fileLoader;
    private readonly IOutputWriter _outputWriter;
    private readonly ILogger<ProcessDocumentHandler> _logger;
    private readonly IMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the ProcessDocumentHandler class.
    /// </summary>
    /// <param name="ocrService">The OCR processing service.</param>
    /// <param name="preprocessor">The image preprocessor.</param>
    /// <param name="extractor">The text field extractor.</param>
    /// <param name="fileLoader">The file loader.</param>
    /// <param name="outputWriter">The output writer.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    public ProcessDocumentHandler(
        IOcrProcessingService ocrService,
        IImagePreprocessor preprocessor,
        ITextFieldExtractor extractor,
        IFileLoader fileLoader,
        IOutputWriter outputWriter,
        ILogger<ProcessDocumentHandler> logger,
        IMetrics metrics)
    {
        _ocrService = ocrService;
        _preprocessor = preprocessor;
        _extractor = extractor;
        _fileLoader = fileLoader;
        _outputWriter = outputWriter;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Handles the document processing command.
    /// </summary>
    /// <param name="request">The processing command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing the processing results or an error.</returns>
    public async Task<Result<List<ProcessingResult>>> Handle(
        ProcessDocumentCommand request, 
        CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.StartActivity("ProcessDocument");
        
        try
        {
            _logger.LogInformation("Starting document processing for path: {InputPath}", request.InputPath);
            _metrics.IncrementCounter("documents_processing_started_total");

            return await Result<string>
                .Success(request.InputPath)
                .Bind(ValidateInputPath)
                .Bind(LoadImages)
                .Bind(images => ProcessImagesAsync(images, request.Config))
                .Bind(results => SaveResultsAsync(results, request.OutputPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document processing");
            _metrics.IncrementCounter("documents_processing_errors_total");
            return Result<List<ProcessingResult>>.Failure($"Processing failed: {ex.Message}");
        }
    }

    private static Result<string> ValidateInputPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Result<string>.Failure("Input path cannot be null or empty");

        if (!Directory.Exists(path))
            return Result<string>.Failure($"Input directory does not exist: {path}");

        return Result<string>.Success(path);
    }

    private async Task<Result<List<ImageData>>> LoadImages(string path)
    {
        return await _fileLoader.LoadImagesAsync(path);
    }

    private async Task<Result<List<ProcessingResult>>> ProcessImagesAsync(
        List<ImageData> images, 
        ProcessingConfig config)
    {
        var results = new List<ProcessingResult>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var image in images)
        {
            var result = await ProcessImageAsync(image, config);
            results.Add(result);
        }

        stopwatch.Stop();
        _metrics.RecordHistogram("batch_processing_duration_seconds", stopwatch.Elapsed.TotalSeconds);

        return Result<List<ProcessingResult>>.Success(results);
    }

    private async Task<ProcessingResult> ProcessImageAsync(ImageData image, ProcessingConfig config)
    {
        var result = await Result<ImageData>
            .Success(image)
            .Bind(img => _preprocessor.PreprocessImageAsync(img, config))
            .Bind(preprocessed => _ocrService.ExecuteOcrAsync(preprocessed, config))
            .Bind(ocrResult => _extractor.ExtractFieldsAsync(ocrResult.Text, ocrResult.ConfidenceAvg));

        return new ProcessingResult
        {
            ImagePath = image.SourcePath,
            IsSuccess = result.IsSuccess,
            ExtractedFields = result.IsSuccess ? result.Value : null,
            Error = result.IsSuccess ? null : result.Error,
            ProcessingTime = DateTime.UtcNow
        };
    }

    private async Task<Result<List<ProcessingResult>>> SaveResultsAsync(
        List<ProcessingResult> results, 
        string outputPath)
    {
        foreach (var result in results)
        {
            var saveResult = await _outputWriter.WriteOutputAsync(result, outputPath);
            if (!saveResult.IsSuccess)
            {
                _logger.LogWarning("Failed to save result for {ImagePath}: {Error}", 
                    result.ImagePath, saveResult.Error);
            }
        }

        return Result<List<ProcessingResult>>.Success(results);
    }
}
```

## Namespace: ExxerCube.Ocr.Infrastructure

### Adapters

#### PythonOcrProcessingAdapter

Adapter for Python OCR processing modules with Railway Oriented Programming.

```csharp
/// <summary>
/// Adapter for Python OCR processing modules using Railway Oriented Programming.
/// </summary>
public class PythonOcrProcessingAdapter : IOcrProcessingService
{
    private readonly ILogger<PythonOcrProcessingAdapter> _logger;
    private readonly IMetrics _metrics;
    private readonly ITelemetry _telemetry;

    /// <summary>
    /// Initializes a new instance of the PythonOcrProcessingAdapter class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    /// <param name="telemetry">The telemetry service.</param>
    public PythonOcrProcessingAdapter(
        ILogger<PythonOcrProcessingAdapter> logger,
        IMetrics metrics,
        ITelemetry telemetry)
    {
        _logger = logger;
        _metrics = metrics;
        _telemetry = telemetry;
    }

    /// <summary>
    /// Executes OCR processing using Python modules.
    /// </summary>
    /// <param name="image">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData image, ProcessingConfig config)
    {
        using var activity = _telemetry.StartActivity("PythonOcrProcessing");
        activity?.SetTag("image.path", image.SourcePath);
        activity?.SetTag("image.size", image.Data.Length);

        try
        {
            _logger.LogInformation("Starting Python OCR processing for {ImagePath}", image.SourcePath);
            _metrics.IncrementCounter("python_ocr_processing_started_total");

            var stopwatch = Stopwatch.StartNew();

            // Python interop code here
            var pythonResult = await ExecutePythonOcrAsync(image, config);

            stopwatch.Stop();
            _metrics.RecordHistogram("python_ocr_processing_duration_seconds", stopwatch.Elapsed.TotalSeconds);

            if (pythonResult.IsSuccess)
            {
                _logger.LogInformation("Successfully completed Python OCR processing for {ImagePath}", image.SourcePath);
                _metrics.IncrementCounter("python_ocr_processing_success_total");
                return pythonResult;
            }
            else
            {
                _logger.LogWarning("Python OCR processing failed for {ImagePath}: {Error}", 
                    image.SourcePath, pythonResult.Error);
                _metrics.IncrementCounter("python_ocr_processing_failed_total");
                return pythonResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Python OCR processing for {ImagePath}", image.SourcePath);
            _metrics.IncrementCounter("python_ocr_processing_errors_total");
            return Result<OCRResult>.Failure($"Python OCR processing failed: {ex.Message}");
        }
    }

    private async Task<Result<OCRResult>> ExecutePythonOcrAsync(ImageData image, ProcessingConfig config)
    {
        // Implementation of Python interop
        // This would use csnakes or similar library to call Python modules
        return await Task.FromResult(Result<OCRResult>.Success(new OCRResult()));
    }
}
```

#### FileSystemLoader

Adapter for file system operations with Railway Oriented Programming.

```csharp
/// <summary>
/// Adapter for file system operations using Railway Oriented Programming.
/// </summary>
public class FileSystemLoader : IFileLoader
{
    private readonly ILogger<FileSystemLoader> _logger;
    private readonly IMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the FileSystemLoader class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    public FileSystemLoader(ILogger<FileSystemLoader> logger, IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Loads images from the file system.
    /// </summary>
    /// <param name="path">The path to load images from.</param>
    /// <returns>A result containing the loaded images or an error.</returns>
    public async Task<Result<List<ImageData>>> LoadImagesAsync(string path)
    {
        try
        {
            _logger.LogInformation("Loading images from path: {Path}", path);
            _metrics.IncrementCounter("file_loading_started_total");

            var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".tiff", ".pdf" };
            var files = Directory.GetFiles(path)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToList();

            if (!files.Any())
            {
                _logger.LogWarning("No supported image files found in {Path}", path);
                return Result<List<ImageData>>.Failure($"No supported image files found in {path}");
            }

            var images = new List<ImageData>();
            foreach (var file in files)
            {
                var imageResult = await LoadImageAsync(file);
                if (imageResult.IsSuccess)
                {
                    images.Add(imageResult.Value!);
                }
                else
                {
                    _logger.LogWarning("Failed to load image {File}: {Error}", file, imageResult.Error);
                }
            }

            _logger.LogInformation("Successfully loaded {Count} images from {Path}", images.Count, path);
            _metrics.IncrementCounter("file_loading_success_total", images.Count);

            return Result<List<ImageData>>.Success(images);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load images from {Path}", path);
            _metrics.IncrementCounter("file_loading_errors_total");
            return Result<List<ImageData>>.Failure($"Failed to load images: {ex.Message}");
        }
    }

    private async Task<Result<ImageData>> LoadImageAsync(string filePath)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(filePath);
            return Result<ImageData>.Success(new ImageData
            {
                Data = data,
                SourcePath = filePath,
                PageNumber = 1,
                TotalPages = 1
            });
        }
        catch (Exception ex)
        {
            return Result<ImageData>.Failure($"Failed to load image {filePath}: {ex.Message}");
        }
    }
}
```

#### FileSystemOutputWriter

Adapter for file system output operations with Railway Oriented Programming.

```csharp
/// <summary>
/// Adapter for file system output operations using Railway Oriented Programming.
/// </summary>
public class FileSystemOutputWriter : IOutputWriter
{
    private readonly ILogger<FileSystemOutputWriter> _logger;
    private readonly IMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the FileSystemOutputWriter class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    public FileSystemOutputWriter(ILogger<FileSystemOutputWriter> logger, IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Writes processing results to the file system.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The path to write the output to.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<Unit>> WriteOutputAsync(ProcessingResult result, string outputPath)
    {
        try
        {
            _logger.LogInformation("Writing output for {ImagePath} to {OutputPath}", 
                result.ImagePath, outputPath);

            Directory.CreateDirectory(outputPath);

            var fileName = Path.GetFileNameWithoutExtension(result.ImagePath);
            var jsonPath = Path.Combine(outputPath, $"{fileName}.json");
            var txtPath = Path.Combine(outputPath, $"{fileName}.txt");

            if (result.IsSuccess && result.ExtractedFields != null)
            {
                var json = JsonSerializer.Serialize(result.ExtractedFields, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(jsonPath, json);

                var text = FormatExtractedFields(result.ExtractedFields);
                await File.WriteAllTextAsync(txtPath, text);

                _logger.LogInformation("Successfully wrote output files for {ImagePath}", result.ImagePath);
                _metrics.IncrementCounter("output_writing_success_total");
                return Result<Unit>.Success(Unit.Value);
            }
            else
            {
                var errorContent = $"Error: {result.Error}\nProcessing Time: {result.ProcessingTime}";
                await File.WriteAllTextAsync(txtPath, errorContent);

                _logger.LogWarning("Wrote error output for {ImagePath}: {Error}", 
                    result.ImagePath, result.Error);
                _metrics.IncrementCounter("output_writing_error_total");
                return Result<Unit>.Success(Unit.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write output for {ImagePath}", result.ImagePath);
            _metrics.IncrementCounter("output_writing_errors_total");
            return Result<Unit>.Failure($"Failed to write output: {ex.Message}");
        }
    }

    private static string FormatExtractedFields(ExtractedFields fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Extracted Fields:");
        sb.AppendLine("================");
        
        if (!string.IsNullOrEmpty(fields.Expediente))
            sb.AppendLine($"Expediente: {fields.Expediente}");
        
        if (!string.IsNullOrEmpty(fields.Causa))
            sb.AppendLine($"Causa: {fields.Causa}");
        
        if (!string.IsNullOrEmpty(fields.AccionSolicitada))
            sb.AppendLine($"Acción Solicitada: {fields.AccionSolicitada}");
        
        if (fields.Fechas.Any())
        {
            sb.AppendLine("Fechas:");
            foreach (var fecha in fields.Fechas)
                sb.AppendLine($"  - {fecha}");
        }
        
        if (fields.Montos.Any())
        {
            sb.AppendLine("Montos:");
            foreach (var monto in fields.Montos)
                sb.AppendLine($"  - {monto.Value} {monto.Currency}");
        }

        return sb.ToString();
    }
}
```

## Configuration

### OCRConfig

Configuration for OCR engine settings.

```csharp
/// <summary>
/// Configuration for OCR engine settings.
/// </summary>
public class OCRConfig
{
    /// <summary>
    /// Gets or sets the primary language for OCR processing.
    /// </summary>
    public string Language { get; set; } = "spa";

    /// <summary>
    /// Gets or sets the fallback language for OCR processing.
    /// </summary>
    public string FallbackLanguage { get; set; } = "eng";

    /// <summary>
    /// Gets or sets the OCR engine configuration string.
    /// </summary>
    public string EngineConfig { get; set; } = "--oem 3 --psm 6";

    /// <summary>
    /// Gets or sets the minimum confidence threshold.
    /// </summary>
    public float MinConfidence { get; set; } = 0.7f;
}
```

## Usage Examples

### Basic Document Processing

```csharp
// Create processing configuration
var config = new ProcessingConfig
{
    RemoveWatermark = true,
    Deskew = true,
    Binarize = true,
    OcrConfig = new OCRConfig
    {
        Language = "spa",
        FallbackLanguage = "eng"
    }
};

// Create processing command
var command = new ProcessDocumentCommand
{
    InputPath = "C:\\Documents\\Input",
    OutputPath = "C:\\Documents\\Output",
    Config = config
};

// Process documents using Railway Oriented Programming
var handler = serviceProvider.GetRequiredService<ProcessDocumentHandler>();
var result = await handler.Handle(command, CancellationToken.None);

if (result.IsSuccess)
{
    Console.WriteLine($"Successfully processed {result.Value!.Count} documents");
}
else
{
    Console.WriteLine($"Processing failed: {result.Error}");
}
```

### Custom OCR Processing

```csharp
// Create image data
var imageData = new ImageData
{
    Data = await File.ReadAllBytesAsync("document.png"),
    SourcePath = "document.png",
    PageNumber = 1,
    TotalPages = 1
};

// Process with custom configuration using Railway Oriented Programming
var ocrService = serviceProvider.GetRequiredService<IOcrProcessingService>();
var ocrResult = await ocrService.ExecuteOcrAsync(imageData, config);

if (ocrResult.IsSuccess)
{
    var extractor = serviceProvider.GetRequiredService<ITextFieldExtractor>();
    var fieldsResult = await extractor.ExtractFieldsAsync(
        ocrResult.Value!.Text, 
        ocrResult.Value!.ConfidenceAvg);

    if (fieldsResult.IsSuccess)
    {
        Console.WriteLine($"Extracted expediente: {fieldsResult.Value!.Expediente}");
    }
    else
    {
        Console.WriteLine($"Field extraction failed: {fieldsResult.Error}");
    }
}
else
{
    Console.WriteLine($"OCR processing failed: {ocrResult.Error}");
}
```

### Fluent Railway Operations

```csharp
// Chain multiple operations using Railway Oriented Programming
var finalResult = await Result<Document>
    .Success(document)
    .Bind(ValidateDocument)
    .Bind(PreprocessImage)
    .Bind(ExecuteOcr)
    .Bind(ExtractFields)
    .Bind(SaveResults)
    .Tap(result => LogSuccess(result))
    .Map(result => new ProcessingSummary(result));
```

## Testing Examples

### Unit Test with Railway Oriented Programming

```csharp
[Fact]
public async Task ProcessDocument_ValidDocument_ReturnsSuccessResult()
{
    // Arrange
    var document = CreateValidDocument();
    var expectedResult = CreateExpectedOcrResult();
    
    _preprocessor.PreprocessAsync(Arg.Any<ImageData>())
        .Returns(Result<ImageData>.Success(CreatePreprocessedImage()));

    // Act
    var result = await _service.ProcessDocumentAsync(document);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldNotBeNull();
    result.Value!.Text.ShouldBe(expectedResult.Text);
    result.Value!.ConfidenceAvg.ShouldBe(expectedResult.ConfidenceAvg, 0.01f);
}

[Fact]
public async Task ProcessDocument_InvalidDocument_ReturnsFailureResult()
{
    // Arrange
    var document = CreateInvalidDocument();

    // Act
    var result = await _service.ProcessDocumentAsync(document);

    // Assert
    result.IsSuccess.ShouldBeFalse();
    result.Error.ShouldNotBeNullOrEmpty();
    result.Error.ShouldContain("validation failed");
}
```
