using CSnakes.Runtime.Python;
using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Infrastructure.Python.Wrappers;

/// <summary>
/// CSnakes wrapper for Prisma OCR processing.
/// Provides type-safe Python integration for OCR operations.
/// </summary>
public class PrismaOcrWrapper : IPrismaOcrWrapper, IDisposable
{
    private readonly PyObject _wrapperObject;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaOcrWrapper"/> class.
    /// </summary>
    /// <param name="wrapperObject">The Python wrapper object.</param>
    internal PrismaOcrWrapper(PyObject wrapperObject)
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
            var wrapperObject = env.PrismaOcrWrapper();
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
            // Call the Python execute_ocr function
            var result = _wrapperObject.execute_ocr(imageData, config);
            
            // Convert PyObject result to C# dictionary
            return ConvertPyObjectToDictionary(result);
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
            // Call the Python extract_fields_from_text function
            var result = _wrapperObject.extract_fields_from_text(text, confidence);
            
            // Convert PyObject result to C# dictionary
            return ConvertPyObjectToDictionary(result);
        }
        catch (Exception ex)
        {
            // Return error result in the expected format
            return new Dictionary<string, object>
            {
                ["expediente"] = null,
                ["causa"] = null,
                ["accion_solicitada"] = null,
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
            // Call the Python extract_expediente_from_text function
            var result = _wrapperObject.extract_expediente_from_text(text);
            
            // Convert PyObject result to string or null
            return ConvertPyObjectToString(result);
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
            // Call the Python extract_causa_from_text function
            var result = _wrapperObject.extract_causa_from_text(text);
            
            // Convert PyObject result to string or null
            return ConvertPyObjectToString(result);
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
            // Call the Python extract_accion_solicitada_from_text function
            var result = _wrapperObject.extract_accion_solicitada_from_text(text);
            
            // Convert PyObject result to string or null
            return ConvertPyObjectToString(result);
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
            // Call the Python extract_dates_from_text function
            var result = _wrapperObject.extract_dates_from_text(text);
            
            // Convert PyObject result to list of strings
            return ConvertPyObjectToListOfStrings(result);
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
            // Call the Python extract_amounts_from_text function
            var result = _wrapperObject.extract_amounts_from_text(text);
            
            // Convert PyObject result to list of dictionaries
            return ConvertPyObjectToListOfDictionaries(result);
        }
        catch (Exception)
        {
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Converts a PyObject to a C# dictionary.
    /// </summary>
    /// <param name="pyObject">The Python object to convert.</param>
    /// <returns>A C# dictionary representation.</returns>
    private static Dictionary<string, object> ConvertPyObjectToDictionary(PyObject pyObject)
    {
        if (pyObject == null) return new Dictionary<string, object>();
        
        try
        {
            // Use CSnakes built-in conversion
            return pyObject.As<Dictionary<string, object>>();
        }
        catch
        {
            // Fallback: try to convert manually
            var result = new Dictionary<string, object>();
            foreach (var key in pyObject.Keys)
            {
                var value = pyObject[key];
                result[key] = ConvertPyObjectValue(value);
            }
            return result;
        }
    }

    /// <summary>
    /// Converts a PyObject to a string or null.
    /// </summary>
    /// <param name="pyObject">The Python object to convert.</param>
    /// <returns>A string or null.</returns>
    private static string? ConvertPyObjectToString(PyObject pyObject)
    {
        if (pyObject == null) return null;
        
        try
        {
            return pyObject.As<string>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a PyObject to a list of strings.
    /// </summary>
    /// <param name="pyObject">The Python object to convert.</param>
    /// <returns>A list of strings.</returns>
    private static List<string> ConvertPyObjectToListOfStrings(PyObject pyObject)
    {
        if (pyObject == null) return new List<string>();
        
        try
        {
            return pyObject.As<List<string>>();
        }
        catch
        {
            // Fallback: try to convert manually
            var result = new List<string>();
            var length = pyObject.Length;
            for (int i = 0; i < length; i++)
            {
                var item = pyObject[i];
                var str = ConvertPyObjectToString(item);
                if (str != null)
                    result.Add(str);
            }
            return result;
        }
    }

    /// <summary>
    /// Converts a PyObject to a list of dictionaries.
    /// </summary>
    /// <param name="pyObject">The Python object to convert.</param>
    /// <returns>A list of dictionaries.</returns>
    private static List<Dictionary<string, object>> ConvertPyObjectToListOfDictionaries(PyObject pyObject)
    {
        if (pyObject == null) return new List<Dictionary<string, object>>();
        
        try
        {
            return pyObject.As<List<Dictionary<string, object>>>();
        }
        catch
        {
            // Fallback: try to convert manually
            var result = new List<Dictionary<string, object>>();
            var length = pyObject.Length;
            for (int i = 0; i < length; i++)
            {
                var item = pyObject[i];
                var dict = ConvertPyObjectToDictionary(item);
                result.Add(dict);
            }
            return result;
        }
    }

    /// <summary>
    /// Converts a PyObject value to a C# object.
    /// </summary>
    /// <param name="pyObject">The Python object to convert.</param>
    /// <returns>A C# object.</returns>
    private static object ConvertPyObjectValue(PyObject pyObject)
    {
        if (pyObject == null) return null!;
        
        try
        {
            // Try common types
            if (pyObject.IsString) return pyObject.As<string>();
            if (pyObject.IsNumber) return pyObject.As<double>();
            if (pyObject.IsBool) return pyObject.As<bool>();
            if (pyObject.IsList) return ConvertPyObjectToListOfStrings(pyObject);
            if (pyObject.IsDict) return ConvertPyObjectToDictionary(pyObject);
            
            // Default to string representation
            return pyObject.ToString();
        }
        catch
        {
            return pyObject.ToString();
        }
    }

    /// <summary>
    /// Disposes the wrapper and releases Python resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _wrapperObject?.Dispose();
            _disposed = true;
        }
    }


}
