namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Extended extraction strategy interface for complement and search strategies.
/// These strategies need access to XML and OCR data to fill gaps or resolve references.
/// </summary>
public interface IComplementDocxExtractionStrategy : IDocxExtractionStrategy
{
    /// <summary>
    /// Extracts fields from DOCX to complement missing data from XML/OCR sources.
    /// CRITICAL: This is EXPECTED behavior, not a failure mode.
    /// Example: "por la cantidad arriba mencionada" - amount only in DOCX.
    /// </summary>
    /// <param name="source">The DOCX document source.</param>
    /// <param name="fieldDefinitions">The field definitions specifying which fields to extract.</param>
    /// <param name="xmlFields">Fields already extracted from XML (may have gaps).</param>
    /// <param name="ocrFields">Fields already extracted from OCR/PDF (may have gaps).</param>
    /// <returns>A result containing the complementary fields.</returns>
    Task<Result<ExtractedFields>> ExtractComplementAsync(
        DocxSource source,
        FieldDefinition[] fieldDefinitions,
        ExtractedFields xmlFields,
        ExtractedFields ocrFields);
}