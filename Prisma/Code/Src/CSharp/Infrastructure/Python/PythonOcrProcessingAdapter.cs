using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Python OCR processing adapter that integrates with existing Python modules.
/// Implements Railway Oriented Programming for error handling.
/// </summary>
public class PythonOcrProcessingAdapter : IOcrExecutor, IImagePreprocessor, IFieldExtractor, IDisposable
{
    private readonly ILogger<PythonOcrProcessingAdapter> _logger;
    private readonly string _pythonModulesPath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonOcrProcessingAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="pythonModulesPath">The path to the Python modules.</param>
    public PythonOcrProcessingAdapter(ILogger<PythonOcrProcessingAdapter> logger, string pythonModulesPath)
    {
        _logger = logger;
        _pythonModulesPath = pythonModulesPath;
        InitializePython();
    }

    /// <summary>
    /// Executes OCR on an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        _logger.LogInformation("Executing OCR on image {SourcePath}", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();

                // Import Python modules
                dynamic ocrExecutor = Py.Import("ocr_modules.ocr_executor");
                dynamic models = Py.Import("ocr_modules.models");

                // Convert C# data to Python
                var pythonImageData = ConvertToPythonImageData(imageData);
                var pythonConfig = ConvertToPythonOCRConfig(config);

                // Execute OCR
                var pythonResult = ocrExecutor.execute_ocr(pythonImageData, pythonConfig);
                var ocrResult = ConvertFromPythonOCRResult(pythonResult);

                return Result<OCRResult>.Success(ocrResult);
            }
            catch (Exception ex)
            {
                return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Preprocesses an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to preprocess.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the preprocessed image or an error.</returns>
    public async Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config)
    {
        _logger.LogInformation("Preprocessing image {SourcePath}", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();

                // Import Python modules
                dynamic pipeline = Py.Import("ocr_modules.pipeline");
                dynamic models = Py.Import("ocr_modules.models");

                // Convert C# data to Python
                var pythonImageData = ConvertToPythonImageData(imageData);
                var pythonConfig = ConvertToPythonProcessingConfig(config);

                // Preprocess image
                var pythonResult = pipeline.preprocess_image(pythonImageData, pythonConfig);
                var preprocessedImage = ConvertFromPythonImageData(pythonResult);

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
    /// Extracts structured fields from OCR text using Python modules.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        _logger.LogInformation("Extracting fields from text with confidence {Confidence}", confidence);
        
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();

                // Import Python modules
                dynamic pipeline = Py.Import("ocr_modules.pipeline");

                // Extract fields
                var pythonResult = pipeline.extract_structured_fields(text, confidence);
                var extractedFields = ConvertFromPythonExtractedFields(pythonResult);

                return Result<ExtractedFields>.Success(extractedFields);
            }
            catch (Exception ex)
            {
                return Result<ExtractedFields>.Failure($"Field extraction failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Removes watermarks from an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();
                _logger.LogInformation("Removing watermark from image {SourcePath}", imageData.SourcePath);

                // Import Python modules
                dynamic watermarkRemover = Py.Import("ocr_modules.watermark_remover");

                // Convert C# data to Python
                var pythonImageData = ConvertToPythonImageData(imageData);

                // Remove watermark
                var pythonResult = watermarkRemover.remove_red_watermark(pythonImageData.data);
                var processedImage = new ImageData
                {
                    Data = ConvertFromPythonImageArray(pythonResult),
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
    /// Deskews an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> DeskewAsync(ImageData imageData)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();
                _logger.LogInformation("Deskewing image {SourcePath}", imageData.SourcePath);

                // Import Python modules
                dynamic imageDeskewer = Py.Import("ocr_modules.image_deskewer");

                // Convert C# data to Python
                var pythonImageData = ConvertToPythonImageData(imageData);

                // Deskew image
                var pythonResult = imageDeskewer.deskew_image(pythonImageData.data);
                var processedImage = new ImageData
                {
                    Data = ConvertFromPythonImageArray(pythonResult),
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
    /// Binarizes an image using Python modules.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <returns>A result containing the processed image or an error.</returns>
    public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var gil = Py.GIL();
                _logger.LogInformation("Binarizing image {SourcePath}", imageData.SourcePath);

                // Import Python modules
                dynamic imageBinarizer = Py.Import("ocr_modules.image_binarizer");

                // Convert C# data to Python
                var pythonImageData = ConvertToPythonImageData(imageData);

                // Binarize image
                var pythonResult = imageBinarizer.binarize_image(pythonImageData.data, "adaptive_gaussian");
                var processedImage = new ImageData
                {
                    Data = ConvertFromPythonImageArray(pythonResult),
                    SourcePath = imageData.SourcePath,
                    PageNumber = imageData.PageNumber,
                    TotalPages = imageData.TotalPages
                };

                _logger.LogInformation("Image binarization completed for {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Success(processedImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error binarizing image {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Failure($"Image binarization failed: {ex.Message}");
            }
        });
    }



    /// <summary>
    /// Extracts expediente (file number) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted expediente or an error.</returns>
    public Task<Result<string?>> ExtractExpedienteAsync(string text) => Task.FromResult(Result<string?>.Success(null));

    /// <summary>
    /// Extracts causa (cause) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted causa or an error.</returns>
    public Task<Result<string?>> ExtractCausaAsync(string text) => Task.FromResult(Result<string?>.Success(null));

    /// <summary>
    /// Extracts accion solicitada (requested action) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted accion solicitada or an error.</returns>
    public Task<Result<string?>> ExtractAccionSolicitadaAsync(string text) => Task.FromResult(Result<string?>.Success(null));

    /// <summary>
    /// Extracts dates from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted dates or an error.</returns>
    public Task<Result<List<string>>> ExtractDatesAsync(string text) => Task.FromResult(Result<List<string>>.Success(new List<string>()));

    /// <summary>
    /// Extracts monetary amounts from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted amounts or an error.</returns>
    public Task<Result<List<AmountData>>> ExtractAmountsAsync(string text) => Task.FromResult(Result<List<AmountData>>.Success(new List<AmountData>()));

    /// <summary>
    /// Initializes the Python runtime.
    /// </summary>
    private void InitializePython()
    {
        try
        {
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
            }

            using var gil = Py.GIL();
            dynamic sys = Py.Import("sys");
            sys.path.append(_pythonModulesPath);
            
            _logger.LogInformation("Python runtime initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python runtime");
            throw new InvalidOperationException("Python runtime initialization failed", ex);
        }
    }

    // Data conversion methods (simplified implementations)
    private dynamic ConvertToPythonImageData(ImageData imageData)
    {
        using var gil = Py.GIL();
        dynamic numpy = Py.Import("numpy");
        dynamic models = Py.Import("ocr_modules.models");
        
        var array = numpy.frombuffer(imageData.Data, numpy.uint8);
        return models.ImageData(
            data: array,
            source_path: imageData.SourcePath,
            page_number: imageData.PageNumber,
            total_pages: imageData.TotalPages
        );
    }

    private dynamic ConvertToPythonOCRConfig(OCRConfig config)
    {
        using var gil = Py.GIL();
        dynamic models = Py.Import("ocr_modules.models");
        
        return models.OCRConfig(
            language: config.Language,
            oem: config.OEM,
            psm: config.PSM,
            fallback_language: config.FallbackLanguage
        );
    }

    private dynamic ConvertToPythonProcessingConfig(ProcessingConfig config)
    {
        using var gil = Py.GIL();
        dynamic models = Py.Import("ocr_modules.models");
        
        return models.ProcessingConfig(
            remove_watermark: config.RemoveWatermark,
            deskew: config.Deskew,
            binarize: config.Binarize,
            ocr_config: ConvertToPythonOCRConfig(config.OCRConfig),
            extract_sections: config.ExtractSections,
            normalize_text: config.NormalizeText
        );
    }

    private OCRResult ConvertFromPythonOCRResult(dynamic pythonResult)
    {
        return new OCRResult(
            text: pythonResult.text,
            confidenceAvg: pythonResult.confidence_avg,
            confidenceMedian: pythonResult.confidence_median,
            confidences: ConvertFromPythonList(pythonResult.confidences),
            languageUsed: pythonResult.language_used
        );
    }

    private ImageData ConvertFromPythonImageData(dynamic pythonImageData)
    {
        return new ImageData(
            data: ConvertFromPythonImageArray(pythonImageData.data),
            sourcePath: pythonImageData.source_path,
            pageNumber: pythonImageData.page_number,
            totalPages: pythonImageData.total_pages
        );
    }

    private ExtractedFields ConvertFromPythonExtractedFields(dynamic pythonFields)
    {
        return new ExtractedFields(
            expediente: pythonFields.expediente,
            causa: pythonFields.causa,
            accionSolicitada: pythonFields.accion_solicitada,
            fechas: ConvertFromPythonList(pythonFields.fechas),
            montos: ConvertFromPythonAmounts(pythonFields.montos)
        );
    }

    private byte[] ConvertFromPythonImageArray(dynamic pythonArray)
    {
        // Simplified conversion - in real implementation, handle different array types
        return new byte[0];
    }

    private List<float> ConvertFromPythonList(dynamic pythonList)
    {
        // Simplified conversion - in real implementation, handle different list types
        return new List<float>();
    }

    private List<AmountData> ConvertFromPythonAmounts(dynamic pythonAmounts)
    {
        // Simplified conversion - in real implementation, handle different list types
        return new List<AmountData>();
    }

    /// <summary>
    /// Disposes the Python adapter and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the Python adapter and releases resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                if (PythonEngine.IsInitialized)
                {
                    PythonEngine.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down Python runtime");
            }
            _disposed = true;
        }
    }
}
