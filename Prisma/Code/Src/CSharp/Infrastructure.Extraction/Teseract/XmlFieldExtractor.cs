namespace ExxerCube.Prisma.Infrastructure.Extraction.Teseract;

/// <summary>
/// Dummy implementation of IFieldExtractor for XmlSource.
/// This is a temporary placeholder implementation to allow the application to compile.
/// A full implementation will be added in a future release.
/// </summary>
public class XmlFieldExtractor : IFieldExtractor<XmlSource>
{
    private const string NotImplementedMessage = "XmlFieldExtractor is not yet implemented. This is a temporary placeholder.";

    /// <summary>
    /// Extracts structured fields from the specified XML document source.
    /// </summary>
    /// <param name="source">The XML document source to process.</param>
    /// <param name="fieldDefinitions">The field definitions specifying which fields to extract.</param>
    /// <returns>A failure result indicating the extractor is not yet implemented.</returns>
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(XmlSource source, FieldDefinition[] fieldDefinitions) =>
        Task.FromResult(Result<ExtractedFields>.WithFailure(NotImplementedMessage));

    /// <summary>
    /// Extracts a specific field by name from the specified XML document source.
    /// </summary>
    /// <param name="source">The XML document source to process.</param>
    /// <param name="fieldName">The name of the field to extract.</param>
    /// <returns>A failure result indicating the extractor is not yet implemented.</returns>
    public Task<Result<FieldValue>> ExtractFieldAsync(XmlSource source, string fieldName) =>
        Task.FromResult(Result<FieldValue>.WithFailure(NotImplementedMessage));
}

