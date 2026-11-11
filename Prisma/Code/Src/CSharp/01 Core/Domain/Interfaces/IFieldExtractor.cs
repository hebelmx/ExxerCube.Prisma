namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the field extractor service for extracting structured data from OCR text.
/// </summary>
public interface IFieldExtractor
{
    /// <summary>
    /// Extracts structured fields from OCR text.
    /// </summary>
    /// <param name="text">The OCR text to process.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence);

    /// <summary>
    /// Extracts expediente (file number) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted expediente or an error.</returns>
    Task<Result<string?>> ExtractExpedienteAsync(string text);

    /// <summary>
    /// Extracts causa (cause) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted causa or an error.</returns>
    Task<Result<string?>> ExtractCausaAsync(string text);

    /// <summary>
    /// Extracts accion solicitada (requested action) from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted accion solicitada or an error.</returns>
    Task<Result<string?>> ExtractAccionSolicitadaAsync(string text);

    /// <summary>
    /// Extracts dates from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted dates or an error.</returns>
    Task<Result<List<string>>> ExtractDatesAsync(string text);

    /// <summary>
    /// Extracts monetary amounts from text.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>A result containing the extracted amounts or an error.</returns>
    Task<Result<List<AmountData>>> ExtractAmountsAsync(string text);
}
