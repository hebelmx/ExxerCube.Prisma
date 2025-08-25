using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using CSnakes.Runtime;
using CSnakes.Runtime.Python;
using System.Linq;

namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Adapter that provides a clean interface over CSnakes dynamic Python module.
/// This adapter handles the dynamic nature of CSnakes-generated code.
/// </summary>
public class PrismaOcrWrapperAdapter : IPythonInteropService, IImagePreprocessor, IOcrExecutor, IFieldExtractor, IDisposable
{
    private readonly ILogger<PrismaOcrWrapperAdapter> _logger;
    private readonly IPrismaOcrWrapper _ocrWrapper;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaOcrWrapperAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PrismaOcrWrapperAdapter(ILogger<PrismaOcrWrapperAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            _ocrWrapper = PrismaPythonEnvironment.Env.PrismaOcrWrapper();
            _logger.LogInformation("Initialized Prisma OCR wrapper adapter with CSnakes-generated interface");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Prisma OCR wrapper: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes OCR on an image using CSnakes Python integration.
    /// </summary>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));
        if (config == null) throw new ArgumentNullException(nameof(config));

        _logger.LogInformation("Executing OCR on image {SourcePath} using CSnakes", imageData.SourcePath);

        return await Task.Run(() =>
        {
            try
            {
                // Create config dictionary
                var configDict = new Dictionary<string, object>
                {
                    ["language"] = config.Language,
                    ["fallback_language"] = config.FallbackLanguage,
                    ["oem"] = config.OEM,
                    ["psm"] = config.PSM
                };

                // Execute OCR using CSnakes-generated interface
                using var pyConfigDict = PyObject.From(configDict);
                var pyReadOnlyDict = pyConfigDict.As<IReadOnlyDictionary<string, PyObject>>();
                var result = _ocrWrapper.ExecuteOcr(imageData.Data, pyReadOnlyDict);
                
                // Result is already a dictionary
                var resultDict = result as IDictionary<string, object> ?? new Dictionary<string, object>();

                // Check for errors
                if (resultDict.TryGetValue("error", out var errorObj) && errorObj != null)
                {
                    var error = errorObj.ToString();
                    _logger.LogError("CSnakes OCR execution failed: {Error}", error);
                    return Result<OCRResult>.Failure($"CSnakes OCR execution failed: {error}");
                }

                // Convert to C# domain object
                var ocrResult = new OCRResult
                {
                    Text = resultDict.TryGetValue("text", out var text) ? text?.ToString() ?? "" : "",
                    ConfidenceAvg = resultDict.TryGetValue("confidence_avg", out var avgConf) ? Convert.ToSingle(avgConf) : 0.0f,
                    ConfidenceMedian = resultDict.TryGetValue("confidence_median", out var medConf) ? Convert.ToSingle(medConf) : 0.0f,
                    Confidences = resultDict.TryGetValue("confidences", out var confs) && confs is IList<object> confList
                        ? confList.Select(c => Convert.ToSingle(c)).ToList()
                        : new List<float>(),
                    LanguageUsed = resultDict.TryGetValue("language_used", out var lang) ? lang?.ToString() ?? config.Language : config.Language
                };

                _logger.LogInformation("CSnakes OCR execution completed with confidence {Confidence}%", ocrResult.ConfidenceAvg);
                return Result<OCRResult>.Success(ocrResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing OCR using CSnakes");
                return Result<OCRResult>.Failure($"CSnakes OCR execution failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Preprocesses an image using Python modules.
    /// </summary>
    public async Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        // Since preprocessing is done within the execute_ocr function in Python,
        // we just return the original image data here
        return await Task.FromResult(Result<ImageData>.Success(imageData));
    }

    /// <summary>
    /// Extracts structured fields from OCR text using Python modules.
    /// </summary>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        return await ExtractFieldsFromTextAsync(text, confidence);
    }

    /// <summary>
    /// Extracts structured fields from text using CSnakes.
    /// </summary>
    public async Task<Result<ExtractedFields>> ExtractFieldsFromTextAsync(string text, double confidence)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<ExtractedFields>.Success(new ExtractedFields());
        }

        _logger.LogInformation("Extracting fields from text with confidence {Confidence} using CSnakes", confidence);

        return await Task.Run(() =>
        {
            try
            {
                // Extract fields using CSnakes-generated interface
                var result = _ocrWrapper.ExtractFieldsFromText(text, confidence);
                var resultDict = result as IDictionary<string, object> ?? new Dictionary<string, object>();

                // Check for errors
                if (resultDict.TryGetValue("error", out var errorObj) && errorObj != null)
                {
                    var error = errorObj.ToString();
                    _logger.LogError("CSnakes field extraction failed: {Error}", error);
                    return Result<ExtractedFields>.Failure($"CSnakes field extraction failed: {error}");
                }

                // Convert to C# domain object
                var extractedFields = new ExtractedFields
                {
                    Expediente = resultDict.TryGetValue("expediente", out var exp) ? exp?.ToString() ?? "" : "",
                    Causa = resultDict.TryGetValue("causa", out var causa) ? causa?.ToString() ?? "" : "",
                    AccionSolicitada = resultDict.TryGetValue("accion_solicitada", out var accion) ? accion?.ToString() ?? "" : "",
                    Fechas = resultDict.TryGetValue("fechas", out var fechas) && fechas is IList<object> fechasList
                        ? fechasList.Select(f => f?.ToString() ?? "").ToList()
                        : new List<string>(),
                    Montos = resultDict.TryGetValue("montos", out var montos) && montos is IList<object> montosList
                        ? ConvertAmountsFromPython(montosList)
                        : new List<AmountData>()
                };

                _logger.LogInformation("CSnakes field extraction completed with confidence {Confidence}", confidence);
                return Result<ExtractedFields>.Success(extractedFields);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting fields from text using CSnakes");
                return Result<ExtractedFields>.Failure($"CSnakes field extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Removes watermarks from an image using the Python pipeline.
    /// </summary>
    public async Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        // For now, return the original image data since watermark removal is handled by the Python pipeline
        return await Task.FromResult(Result<ImageData>.Success(imageData));
    }

    /// <summary>
    /// Deskews an image using the Python pipeline.
    /// </summary>
    public async Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        // For now, return the original image data since deskewing is handled by the Python pipeline
        return await Task.FromResult(Result<ImageData>.Success(imageData));
    }

    /// <summary>
    /// Binarizes an image using the Python pipeline.
    /// </summary>
    public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
    {
        // For now, return the original image data since binarization is handled by the Python pipeline
        return await Task.FromResult(Result<ImageData>.Success(imageData));
    }

    /// <summary>
    /// Extracts expediente (case file number) from text using CSnakes.
    /// </summary>
    public async Task<Result<string?>> ExtractExpedienteAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = _ocrWrapper.ExtractExpedienteFromText(text)?.ToString();
                return Result<string?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting expediente from text using CSnakes");
                return Result<string?>.Failure($"CSnakes expediente extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Extracts causa (cause) from text using CSnakes.
    /// </summary>
    public async Task<Result<string?>> ExtractCausaAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = _ocrWrapper.ExtractCausaFromText(text)?.ToString();
                return Result<string?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting causa from text using CSnakes");
                return Result<string?>.Failure($"CSnakes causa extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Extracts accion solicitada (requested action) from text using CSnakes.
    /// </summary>
    public async Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = _ocrWrapper.ExtractAccionSolicitadaFromText(text)?.ToString();
                return Result<string?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting accion solicitada from text using CSnakes");
                return Result<string?>.Failure($"CSnakes accion solicitada extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Extracts dates from text using CSnakes.
    /// </summary>
    public async Task<Result<List<string>>> ExtractDatesAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<List<string>>.Success(new List<string>());
        }

        return await Task.Run(() =>
        {
            try
            {
                var dates = _ocrWrapper.ExtractDatesFromText(text);
                var datesList = dates as IList<object> ?? new List<object>();
                var result = datesList.Select(d => d?.ToString() ?? "").ToList();
                return Result<List<string>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting dates from text using CSnakes");
                return Result<List<string>>.Failure($"CSnakes dates extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Extracts monetary amounts from text using CSnakes.
    /// </summary>
    public async Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<List<AmountData>>.Success(new List<AmountData>());
        }

        return await Task.Run(() =>
        {
            try
            {
                var amounts = _ocrWrapper.ExtractAmountsFromText(text);
                var amountsList = amounts as IList<object> ?? new List<object>();
                var result = ConvertAmountsFromPython(amountsList);
                return Result<List<AmountData>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting amounts from text using CSnakes");
                return Result<List<AmountData>>.Failure($"CSnakes amounts extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Disposes the Python environment.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _ocrWrapper?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Converts Python amount data to C# AmountData objects.
    /// </summary>
    private static List<AmountData> ConvertAmountsFromPython(IList<object> pythonAmounts)
    {
        var amounts = new List<AmountData>();

        foreach (var amountObj in pythonAmounts)
        {
            if (amountObj is IDictionary<string, object> amountDict)
            {
                var amount = new AmountData
                {
                    Value = amountDict.TryGetValue("value", out var val) ? Convert.ToDecimal(val) : 0.0m,
                    Currency = amountDict.TryGetValue("currency", out var curr) ? curr?.ToString() ?? "" : "",
                    OriginalText = amountDict.TryGetValue("original_text", out var orig) ? orig?.ToString() ?? "" : ""
                };
                amounts.Add(amount);
            }
        }

        return amounts;
    }
}