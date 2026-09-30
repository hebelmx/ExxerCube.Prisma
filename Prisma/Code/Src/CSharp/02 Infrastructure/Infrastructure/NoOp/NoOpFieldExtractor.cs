namespace ExxerCube.Prisma.Infrastructure.NoOp;

/// <summary>
/// No-op field extractor that returns empty extracted fields.
/// Used when field extraction is not needed or handled separately (e.g., XML parsing for structured data).
/// Field extraction is handled by typed extractors: XmlFieldExtractor, PdfOcrFieldExtractor, DocxFieldExtractor.
/// </summary>
public class NoOpFieldExtractor : IFieldExtractor
{
    /// <summary>
    /// Returns an empty ExtractedFields result.
    /// </summary>
    /// <param name="text">The OCR text.</param>
    /// <param name="confidence">The OCR confidence score.</param>
    /// <returns>A successful result with empty extracted fields.</returns>
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(string text, float confidence)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure("Text cannot be null or empty"));
        }

        // Return empty fields - field extraction is handled separately
        var emptyFields = new ExtractedFields(null, null, null, new List<string>(), new List<AmountData>());
        return Task.FromResult(Result<ExtractedFields>.WithSuccess(emptyFields));
    }

    /// <summary>
    /// Returns null for expediente extraction (not implemented).
    /// </summary>
    public Task<Result<string?>> ExtractExpedienteAsync(string text)
    {
        return Task.FromResult(Result<string?>.WithSuccess(null));
    }

    /// <summary>
    /// Returns null for causa extraction (not implemented).
    /// </summary>
    public Task<Result<string?>> ExtractCausaAsync(string text)
    {
        return Task.FromResult(Result<string?>.WithSuccess(null));
    }

    /// <summary>
    /// Returns null for accion solicitada extraction (not implemented).
    /// </summary>
    public Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
    {
        return Task.FromResult(Result<string?>.WithSuccess(null));
    }

    /// <summary>
    /// Returns empty list for dates extraction (not implemented).
    /// </summary>
    public Task<Result<List<string>>> ExtractDatesAsync(string text)
    {
        return Task.FromResult(Result<List<string>>.WithSuccess(new List<string>()));
    }

    /// <summary>
    /// Returns empty list for amounts extraction (not implemented).
    /// </summary>
    public Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
    {
        return Task.FromResult(Result<List<AmountData>>.WithSuccess(new List<AmountData>()));
    }
}
