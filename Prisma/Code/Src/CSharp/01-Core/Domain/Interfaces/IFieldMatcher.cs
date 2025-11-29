namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the field matcher service for matching field values across different document formats.
/// </summary>
/// <typeparam name="T">The document source type (e.g., <see cref="DocxSource"/>, <see cref="PdfSource"/>, <see cref="XmlSource"/>).</typeparam>
public interface IFieldMatcher<T>
{
    /// <summary>
    /// Matches field values across XML, DOCX, and PDF sources.
    /// </summary>
    /// <param name="sources">The list of document sources to match.</param>
    /// <param name="fieldDefinitions">The field definitions specifying which fields to match.</param>
    /// <returns>A result containing matched fields with confidence scores or an error.</returns>
    Task<Result<MatchedFields>> MatchFieldsAsync(List<T> sources, FieldDefinition[] fieldDefinitions);

    /// <summary>
    /// Generates a unified metadata record from matched fields.
    /// </summary>
    /// <param name="matchedFields">The matched fields result.</param>
    /// <param name="expediente">The expediente information (optional).</param>
    /// <param name="classification">The classification result (optional).</param>
    /// <returns>A result containing the unified metadata record or an error.</returns>
    Task<Result<UnifiedMetadataRecord>> GenerateUnifiedRecordAsync(
        MatchedFields matchedFields,
        Expediente? expediente = null,
        ClassificationResult? classification = null);

    /// <summary>
    /// Validates completeness and consistency of final match result.
    /// </summary>
    /// <param name="matchedFields">The matched fields to validate.</param>
    /// <param name="requiredFields">The list of required field names.</param>
    /// <returns>A result indicating validation success or failure with details.</returns>
    Task<Result> ValidateCompletenessAsync(MatchedFields matchedFields, List<string> requiredFields);
}

