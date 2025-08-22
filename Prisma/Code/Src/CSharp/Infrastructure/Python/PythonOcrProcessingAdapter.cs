using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Legacy Python.NET OCR processing adapter (DEPRECATED).
/// This adapter will be replaced by CSnakesOcrProcessingAdapter.
/// Implements Railway Oriented Programming for error handling.
/// </summary>
[Obsolete("This adapter uses Python.NET and will be replaced by CSnakesOcrProcessingAdapter. Use IPythonInteropService instead.")]
public class PythonOcrProcessingAdapter : IOcrExecutor, IImagePreprocessor, IFieldExtractor, IDisposable
{
    // TODO: This class is temporarily commented out due to Python.NET removal
    // It will be completely removed once CSnakes integration is fully implemented
    
    private readonly ILogger<PythonOcrProcessingAdapter> _logger;
    private readonly string _pythonModulesPath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonOcrProcessingAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pythonModulesPath">The path to the Python modules.</param>
    public PythonOcrProcessingAdapter(ILogger<PythonOcrProcessingAdapter> logger, string pythonModulesPath)
    {
        _logger = logger;
        _pythonModulesPath = pythonModulesPath;
        throw new NotImplementedException("PythonOcrProcessingAdapter is deprecated. Use CSnakesOcrProcessingAdapter instead.");
    }

    // All methods throw NotImplementedException to prevent usage
    /// <summary>
    /// Executes OCR on an image (DEPRECATED).
    /// </summary>
    public Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Preprocesses an image (DEPRECATED).
    /// </summary>
    public Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts structured fields from OCR text (DEPRECATED).
    /// </summary>
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Removes watermarks from an image (DEPRECATED).
    /// </summary>
    public Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Deskews an image (DEPRECATED).
    /// </summary>
    public Task<Result<ImageData>> DeskewAsync(ImageData imageData) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Binarizes an image (DEPRECATED).
    /// </summary>
    public Task<Result<ImageData>> BinarizeAsync(ImageData imageData) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts expediente from text (DEPRECATED).
    /// </summary>
    public Task<Result<string?>> ExtractExpedienteAsync(string text) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts causa from text (DEPRECATED).
    /// </summary>
    public Task<Result<string?>> ExtractCausaAsync(string text) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts accion solicitada from text (DEPRECATED).
    /// </summary>
    public Task<Result<string?>> ExtractAccionSolicitadaAsync(string text) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts dates from text (DEPRECATED).
    /// </summary>
    public Task<Result<List<string>>> ExtractDatesAsync(string text) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Extracts monetary amounts from text (DEPRECATED).
    /// </summary>
    public Task<Result<List<AmountData>>> ExtractAmountsAsync(string text) => 
        throw new NotImplementedException("Use CSnakesOcrProcessingAdapter instead.");

    /// <summary>
    /// Disposes the adapter.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the adapter.
    /// </summary>
    /// <param name="disposing">True if disposing, false if finalizing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }
}
