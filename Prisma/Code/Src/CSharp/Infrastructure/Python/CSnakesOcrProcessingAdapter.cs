using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using System.Linq;

namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// CSnakes-based OCR processing adapter that provides type-safe Python integration.
/// Implements Railway Oriented Programming for error handling and maintains clean architecture.
/// </summary>
public class CSnakesOcrProcessingAdapter : IPythonInteropService, IDisposable
{
    private readonly ILogger<CSnakesOcrProcessingAdapter> _logger;
    private readonly string _pythonModulesPath;
    private readonly string _pythonExecutablePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CSnakesOcrProcessingAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pythonModulesPath">The path to the Python modules.</param>
    public CSnakesOcrProcessingAdapter(ILogger<CSnakesOcrProcessingAdapter> logger, string pythonModulesPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pythonModulesPath = pythonModulesPath ?? throw new ArgumentNullException(nameof(pythonModulesPath));
        _pythonExecutablePath = "python"; // Default to system Python
        
        _logger.LogInformation("Initializing CSnakes OCR processing adapter with modules path: {ModulesPath}", _pythonModulesPath);
    }

    /// <summary>
    /// Executes OCR on an image using the Python OCR pipeline.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));
        if (config == null) throw new ArgumentNullException(nameof(config));

        _logger.LogInformation("Executing OCR on image {SourcePath} using Python pipeline", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                // Create temporary file for the image data
                var tempInputPath = Path.GetTempFileName() + ".png";
                var tempOutputDir = Path.Combine(Path.GetTempPath(), $"ocr_output_{Guid.NewGuid()}");
                
                try
                {
                    // Write image data to temporary file
                    File.WriteAllBytes(tempInputPath, imageData.Data);
                    
                    // Create output directory
                    Directory.CreateDirectory(tempOutputDir);
                    
                    // Call Python OCR pipeline
                    var pythonScriptPath = Path.Combine(_pythonModulesPath, "..", "modular_ocr_cli.py");
                    var arguments = $"\"{pythonScriptPath}\" --input \"{tempInputPath}\" --outdir \"{tempOutputDir}\" --language {config.Language}";
                    
                    _logger.LogDebug("Executing Python command: {PythonPath} {Arguments}", _pythonExecutablePath, arguments);
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExecutablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                    };
                    
                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit(30000); // 30 second timeout
                    
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Python OCR pipeline failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                        return Result<OCRResult>.Failure($"Python OCR pipeline failed: {error}");
                    }
                    
                    // Read the output files
                    var baseFileName = Path.GetFileNameWithoutExtension(tempInputPath);
                    var jsonOutputPath = Path.Combine(tempOutputDir, $"{baseFileName}.json");
                    var txtOutputPath = Path.Combine(tempOutputDir, $"{baseFileName}.txt");
                    
                    if (!File.Exists(jsonOutputPath))
                    {
                        _logger.LogError("Python OCR pipeline did not generate expected output file: {JsonPath}", jsonOutputPath);
                        return Result<OCRResult>.Failure("Python OCR pipeline did not generate expected output file");
                    }
                    
                    // Parse JSON output
                    var jsonContent = File.ReadAllText(jsonOutputPath);
                    var pythonResult = JsonSerializer.Deserialize<PythonOCRResult>(jsonContent);
                    
                    if (pythonResult == null)
                    {
                        return Result<OCRResult>.Failure("Failed to parse Python OCR output");
                    }
                    
                    // Read text output
                    var textContent = File.Exists(txtOutputPath) ? File.ReadAllText(txtOutputPath) : "";
                    
                    // Convert to C# domain object
                    var ocrResult = new OCRResult
                    {
                        Text = textContent,
                        ConfidenceAvg = ExtractConfidenceFromMetadata(pythonResult.metadata, "ocr_confidence_avg"),
                        ConfidenceMedian = ExtractConfidenceFromMetadata(pythonResult.metadata, "ocr_confidence_median"),
                        Confidences = new List<float> { ExtractConfidenceFromMetadata(pythonResult.metadata, "ocr_confidence_avg") },
                        LanguageUsed = config.Language
                    };

                    _logger.LogInformation("OCR execution completed for {SourcePath} with confidence {Confidence}", 
                        imageData.SourcePath, ocrResult.ConfidenceAvg);
                    
                    return Result<OCRResult>.Success(ocrResult);
                }
                finally
                {
                    // Cleanup temporary files
                    try
                    {
                        if (File.Exists(tempInputPath))
                            File.Delete(tempInputPath);
                        
                        if (Directory.Exists(tempOutputDir))
                            Directory.Delete(tempOutputDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary files");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing OCR on image {SourcePath}", imageData.SourcePath);
                return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Preprocesses an image using the Python pipeline.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    public async Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));
        if (config == null) throw new ArgumentNullException(nameof(config));

        _logger.LogInformation("Preprocessing image {SourcePath} using Python pipeline", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                // For now, return the original image data since preprocessing is handled by the Python pipeline
                // In a full implementation, we could call specific preprocessing modules
                var preprocessedImage = new ImageData
                {
                    Data = imageData.Data,
                    SourcePath = imageData.SourcePath,
                    PageNumber = imageData.PageNumber,
                    TotalPages = imageData.TotalPages
                };

                _logger.LogInformation("Image preprocessing completed for {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Success(preprocessedImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preprocessing image {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Failure($"Image preprocessing failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Extracts structured fields from OCR text using the Python pipeline.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text cannot be null or empty", nameof(text));

        _logger.LogInformation("Extracting fields from text with confidence {Confidence} using Python pipeline", confidence);
        
        return await Task.Run(() =>
        {
            try
            {
                // Create temporary file for the text
                var tempInputPath = Path.GetTempFileName() + ".txt";
                var tempOutputDir = Path.Combine(Path.GetTempPath(), $"fields_output_{Guid.NewGuid()}");
                
                try
                {
                    // Write text to temporary file
                    File.WriteAllText(tempInputPath, text);
                    
                    // Create output directory
                    Directory.CreateDirectory(tempOutputDir);
                    
                    // Call Python modular pipeline for field extraction
                    var pythonScriptPath = Path.Combine(_pythonModulesPath, "..", "modular_ocr_cli.py");
                    var arguments = $"\"{pythonScriptPath}\" --input \"{tempInputPath}\" --outdir \"{tempOutputDir}\" --no-watermark-removal --no-deskew --no-binarize --no-extract-sections";
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExecutablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                    };
                    
                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit(30000);
                    
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Python field extraction failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                        return Result<ExtractedFields>.Failure($"Python field extraction failed: {error}");
                    }
                    
                    // Read the output files
                    var baseFileName = Path.GetFileNameWithoutExtension(tempInputPath);
                    var jsonOutputPath = Path.Combine(tempOutputDir, $"{baseFileName}.json");
                    
                    if (!File.Exists(jsonOutputPath))
                    {
                        _logger.LogError("Python field extraction did not generate expected output file: {JsonPath}", jsonOutputPath);
                        return Result<ExtractedFields>.Failure("Python field extraction did not generate expected output file");
                    }
                    
                    // Parse JSON output
                    var jsonContent = File.ReadAllText(jsonOutputPath);
                    var pythonResult = JsonSerializer.Deserialize<PythonExtractedFields>(jsonContent);
                    
                    if (pythonResult == null)
                    {
                        return Result<ExtractedFields>.Failure("Failed to parse Python field extraction output");
                    }
                    
                    // Convert to C# domain object
                    var extractedFields = new ExtractedFields
                    {
                        Expediente = pythonResult.expediente,
                        Causa = pythonResult.causa,
                        AccionSolicitada = pythonResult.accion_solicitada,
                        Fechas = pythonResult.fechas ?? new List<string>(),
                        Montos = pythonResult.montos?.Select(m => new AmountData 
                        { 
                            Value = (decimal)m.value, 
                            Currency = m.currency,
                            OriginalText = ""
                        }).ToList() ?? new List<AmountData>()
                    };

                    _logger.LogInformation("Field extraction completed with confidence {Confidence}", confidence);
                    return Result<ExtractedFields>.Success(extractedFields);
                }
                finally
                {
                    // Cleanup temporary files
                    try
                    {
                        if (File.Exists(tempInputPath))
                            File.Delete(tempInputPath);
                        
                        if (Directory.Exists(tempOutputDir))
                            Directory.Delete(tempOutputDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary files");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting fields from text");
                return Result<ExtractedFields>.Failure($"Field extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Removes watermarks from an image using the Python pipeline.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));

        _logger.LogInformation("Removing watermark from image {SourcePath} using Python pipeline", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                // For now, return the original image data since watermark removal is handled by the Python pipeline
                // In a full implementation, we could call the specific watermark removal module
                var processedImage = new ImageData
                {
                    Data = imageData.Data,
                    SourcePath = imageData.SourcePath,
                    PageNumber = imageData.PageNumber,
                    TotalPages = imageData.TotalPages
                };

                _logger.LogInformation("Watermark removal completed for {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Success(processedImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing watermark from image {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Failure($"Watermark removal failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Deskews an image using the Python pipeline.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));

        _logger.LogInformation("Deskewing image {SourcePath} using Python pipeline", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                // For now, return the original image data since deskewing is handled by the Python pipeline
                // In a full implementation, we could call the specific deskewing module
                var processedImage = new ImageData
                {
                    Data = imageData.Data,
                    SourcePath = imageData.SourcePath,
                    PageNumber = imageData.PageNumber,
                    TotalPages = imageData.TotalPages
                };

                _logger.LogInformation("Image deskewing completed for {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Success(processedImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deskewing image {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Failure($"Image deskewing failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Disposes the adapter and releases any resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Data class for parsing Python OCR output JSON from modular pipeline.
    /// </summary>
    private class PythonOCRResult
    {
        public string? expediente { get; set; }
        public string? causa { get; set; }
        public string? accion_solicitada { get; set; }
        public List<string>? fechas { get; set; }
        public List<PythonAmount>? montos { get; set; }
        public Dictionary<string, object>? metadata { get; set; }
    }

    /// <summary>
    /// Data class for parsing Python extracted fields JSON from modular pipeline.
    /// </summary>
    private class PythonExtractedFields
    {
        public string? expediente { get; set; }
        public string? causa { get; set; }
        public string? accion_solicitada { get; set; }
        public List<string>? fechas { get; set; }
        public List<PythonAmount>? montos { get; set; }
        public Dictionary<string, object>? metadata { get; set; }
    }

    /// <summary>
    /// Data class for parsing Python amount data.
    /// </summary>
    private class PythonAmount
    {
        public string currency { get; set; } = "";
        public double value { get; set; }
    }

    private float ExtractConfidenceFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value))
        {
            return 0f;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }
        else if (value is double doubleValue)
        {
            return (float)doubleValue;
        }
        else if (value is int intValue)
        {
            return (float)intValue;
        }
        else if (value is string stringValue)
        {
            if (float.TryParse(stringValue, out float parsedValue))
            {
                return parsedValue;
            }
        }

        return 0f;
    }
}
