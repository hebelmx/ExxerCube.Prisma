namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Defines the available DOCX extraction strategies for handling different document formats and quality levels.
/// Follows the same pattern as image enhancement filters (Polynomial, Analytical, Manual).
/// </summary>
public enum DocxExtractionStrategy
{
    /// <summary>
    /// Unknown or unspecified strategy.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Structured extraction using regex patterns.
    /// Best for well-formatted CNBV standard documents.
    /// High confidence when patterns match.
    /// </summary>
    Structured = 1,

    /// <summary>
    /// Contextual extraction using label-value pairs.
    /// Looks for labels ("Expediente:", "RFC:") and extracts adjacent values.
    /// Works for semi-structured documents with consistent labeling.
    /// </summary>
    Contextual = 2,

    /// <summary>
    /// Table-based extraction from DOCX tables.
    /// Maps column headers to field definitions.
    /// High confidence for tabular data.
    /// </summary>
    TableBased = 3,

    /// <summary>
    /// Fuzzy matching extraction with error tolerance.
    /// Uses Levenshtein distance for typo detection.
    /// Selective application: ONLY for name fields, NOT for accounts/amounts.
    /// </summary>
    Fuzzy = 4,

    /// <summary>
    /// Complement extraction strategy.
    /// CRITICAL: Fills gaps when XML/OCR missing data.
    /// Example: "por la cantidad arriba mencionada" - amount only in DOCX.
    /// This is EXPECTED behavior, not a failure mode.
    /// </summary>
    Complement = 5,

    /// <summary>
    /// Search extraction strategy for cross-references.
    /// Resolves references like "arriba mencionada", "anteriormente indicado".
    /// Searches backward/forward in document for referenced values.
    /// </summary>
    Search = 6,

    /// <summary>
    /// Hybrid strategy combining multiple approaches.
    /// Uses all applicable strategies and merges results by confidence.
    /// </summary>
    Hybrid = 99
}
