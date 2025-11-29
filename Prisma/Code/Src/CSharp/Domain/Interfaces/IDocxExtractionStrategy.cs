namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Defines a strategy for extracting fields from DOCX documents.
/// Follows Strategy Pattern - multiple implementations for different document formats.
/// Similar to IImageEnhancementFilter (Polynomial, Analytical, Manual).
/// </summary>
public interface IDocxExtractionStrategy
{
    /// <summary>
    /// Gets the strategy type identifier.
    /// </summary>
    DocxExtractionStrategy StrategyType { get; }

    /// <summary>
    /// Extracts fields from a DOCX document source using this strategy.
    /// </summary>
    /// <param name="source">The DOCX document source.</param>
    /// <param name="fieldDefinitions">The field definitions specifying which fields to extract.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    Task<Result<ExtractedFields>> ExtractAsync(DocxSource source, FieldDefinition[] fieldDefinitions);

    /// <summary>
    /// Calculates confidence score for this strategy based on document analysis.
    /// Used to select best strategy for a given document.
    /// </summary>
    /// <param name="structure">The analyzed document structure.</param>
    /// <returns>Confidence score from 0.0 (not suitable) to 1.0 (highly suitable).</returns>
    float CalculateConfidence(DocxStructure structure);

    /// <summary>
    /// Determines if this strategy can handle the given document structure.
    /// </summary>
    /// <param name="structure">The analyzed document structure.</param>
    /// <returns>True if this strategy can process the document; otherwise, false.</returns>
    bool CanHandle(DocxStructure structure);
}

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
