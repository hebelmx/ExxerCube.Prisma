using CSnakes.Runtime.Python;
using CSnakes.Runtime;
using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Infrastructure.Python.Wrappers;

/// <summary>
/// CSnakes wrapper for Prisma OCR processing.
/// Provides type-safe Python integration for OCR operations.
/// </summary>
public class PrismaOcrWrapper : IPrismaOcrWrapper, IDisposable
{
    private readonly dynamic _wrapperObject;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaOcrWrapper"/> class.
    /// </summary>
    /// <param name="wrapperObject">The Python wrapper object.</param>
    internal PrismaOcrWrapper(dynamic wrapperObject)
    {
        _wrapperObject = wrapperObject ?? throw new ArgumentNullException(nameof(wrapperObject));
    }

    /// <summary>
    /// Creates a new Prisma OCR wrapper instance.
    /// </summary>
    /// <returns>A new Prisma OCR wrapper.</returns>
    public static PrismaOcrWrapper Create()
    {
        try
        {
            // Get the Python environment and create the wrapper object
            var env = PrismaPythonEnvironment.Env;
            
            // Use the CSnakes-generated extension method
            var wrapperObject = PrismaOcrWrapperExtensions.PrismaOcrWrapper(env);
            return new PrismaOcrWrapper(wrapperObject);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create Prisma OCR wrapper: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes OCR on image data using Python modules.
    /// </summary>
    /// <param name="imageData">The raw image bytes.</param>
    /// <param name="config">The OCR configuration dictionary.</param>
    /// <returns>A dictionary containing OCR results.</returns>
    public Dictionary<string, object> ExecuteOcr(byte[] imageData, Dictionary<string, object> config)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python execute_ocr function using CSnakes-generated wrapper
            var result = _wrapperObject.ExecuteOcr(imageData, config);

            // Convert result to C# dictionary
            return ConvertToDictionary(result);
        }
        catch (Exception ex)
        {
            // Return error result in the expected format
            return new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["text"] = "",
                ["confidence_avg"] = 0.0,
                ["confidence_median"] = 0.0,
                ["confidences"] = new List<float>(),
                ["language_used"] = "spa",
                ["processing_errors"] = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Extracts structured fields from OCR text.
    /// </summary>
    /// <param name="text">The OCR text content.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A dictionary containing extracted fields.</returns>
    public Dictionary<string, object> ExtractFieldsFromText(string text, double confidence)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_fields_from_text function using CSnakes-generated wrapper
            var result = _wrapperObject.ExtractFieldsFromText(text, confidence);

            // Convert result to C# dictionary
            return ConvertToDictionary(result);
        }
        catch (Exception ex)
        {
            // Return error result in the expected format
            return new Dictionary<string, object>
            {
                ["expediente"] = string.Empty,
                ["causa"] = string.Empty,
                ["accion_solicitada"] = string.Empty,
                ["fechas"] = new List<string>(),
                ["montos"] = new List<object>(),
                ["confidence"] = confidence,
                ["error"] = ex.Message
            };
        }
    }

    /// <summary>
    /// Extracts expediente (case file number) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted expediente or null.</returns>
    public string? ExtractExpedienteFromText(string text)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_expediente_from_text function using CSnakes-generated wrapper
            return _wrapperObject.ExtractExpedienteFromText(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts causa (cause) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted causa or null.</returns>
    public string? ExtractCausaFromText(string text)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_causa_from_text function using CSnakes-generated wrapper
            return _wrapperObject.ExtractCausaFromText(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts accion solicitada (requested action) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted accion solicitada or null.</returns>
    public string? ExtractAccionSolicitadaFromText(string text)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_accion_solicitada_from_text function using CSnakes-generated wrapper
            return _wrapperObject.ExtractAccionSolicitadaFromText(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts dates from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A list of extracted dates.</returns>
    public List<string> ExtractDatesFromText(string text)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_dates_from_text function using CSnakes-generated wrapper
            return _wrapperObject.ExtractDatesFromText(text);
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Extracts monetary amounts from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A list of extracted amounts with currency and value.</returns>
    public List<Dictionary<string, object>> ExtractAmountsFromText(string text)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrismaOcrWrapper));

        try
        {
            // Call the Python extract_amounts_from_text function using CSnakes-generated wrapper
            var result = _wrapperObject.ExtractAmountsFromText(text);

            // Convert result to list of dictionaries
            return ConvertToListOfDictionaries(result);
        }
        catch (Exception)
        {
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Converts a dynamic result to a C# dictionary.
    /// </summary>
    /// <param name="result">The dynamic result to convert.</param>
    /// <returns>A C# dictionary representation.</returns>
    private static Dictionary<string, object> ConvertToDictionary(dynamic result)
    {
        if (result == null) return new Dictionary<string, object>();

        try
        {
            var dict = new Dictionary<string, object>();
            foreach (var property in result.GetType().GetProperties())
            {
                dict[property.Name] = property.GetValue(result);
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Converts a dynamic result to a list of dictionaries.
    /// </summary>
    /// <param name="result">The dynamic result to convert.</param>
    /// <returns>A list of dictionaries.</returns>
    private static List<Dictionary<string, object>> ConvertToListOfDictionaries(dynamic result)
    {
        if (result == null) return new List<Dictionary<string, object>>();

        try
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var item in result)
            {
                list.Add(ConvertToDictionary(item));
            }
            return list;
        }
        catch
        {
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Disposes the wrapper and releases Python resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}