using System.Collections.Generic;

namespace ExxerCube.Prisma.Infrastructure.Python.Wrappers;

/// <summary>
/// CSnakes wrapper interface for Prisma OCR processing.
/// Provides type-safe Python integration for OCR operations.
/// </summary>
public interface IPrismaOcrWrapper
{
    /// <summary>
    /// Executes OCR on image data using Python modules.
    /// </summary>
    /// <param name="imageData">The raw image bytes.</param>
    /// <param name="config">The OCR configuration dictionary.</param>
    /// <returns>A dictionary containing OCR results.</returns>
    Dictionary<string, object> ExecuteOcr(byte[] imageData, Dictionary<string, object> config);

    /// <summary>
    /// Extracts structured fields from OCR text.
    /// </summary>
    /// <param name="text">The OCR text content.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A dictionary containing extracted fields.</returns>
    Dictionary<string, object> ExtractFieldsFromText(string text, double confidence);

    /// <summary>
    /// Extracts expediente (case file number) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted expediente or null.</returns>
    string? ExtractExpedienteFromText(string text);

    /// <summary>
    /// Extracts causa (cause) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted causa or null.</returns>
    string? ExtractCausaFromText(string text);

    /// <summary>
    /// Extracts accion solicitada (requested action) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>The extracted accion solicitada or null.</returns>
    string? ExtractAccionSolicitadaFromText(string text);

    /// <summary>
    /// Extracts dates from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A list of extracted dates.</returns>
    List<string> ExtractDatesFromText(string text);

    /// <summary>
    /// Extracts monetary amounts from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A list of extracted amounts with currency and value.</returns>
    List<Dictionary<string, object>> ExtractAmountsFromText(string text);
}
