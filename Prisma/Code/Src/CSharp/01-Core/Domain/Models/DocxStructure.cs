namespace ExxerCube.Prisma.Domain.Models;

using ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the structural analysis of a DOCX document.
/// Used to select the best extraction strategy.
/// </summary>
public sealed class DocxStructure
{
    /// <summary>
    /// Gets or sets whether the document contains tables.
    /// </summary>
    public bool HasTables { get; set; }

    /// <summary>
    /// Gets or sets the total number of paragraphs in the document.
    /// </summary>
    public int ParagraphCount { get; set; }

    /// <summary>
    /// Gets or sets whether the document has bold-formatted labels (e.g., "Expediente:" in bold).
    /// Indicates semi-structured format with visual field indicators.
    /// </summary>
    public bool HasBoldLabels { get; set; }

    /// <summary>
    /// Gets or sets whether the document follows key-value pair patterns.
    /// Example: "Campo: Valor" patterns throughout the document.
    /// </summary>
    public bool HasKeyValuePairs { get; set; }

    /// <summary>
    /// Gets or sets whether the document matches known CNBV template structure.
    /// High confidence indicator for structured extraction.
    /// </summary>
    public bool HasStructuredFormat { get; set; }

    /// <summary>
    /// Gets or sets the table structure information.
    /// Null if no tables present.
    /// </summary>
    public DocxTableStructure? TableStructure { get; set; }

    /// <summary>
    /// Gets or sets the count of styled elements (headings, bold, italic).
    /// Higher count indicates more structured document.
    /// </summary>
    public int StyledElementCount { get; set; }

    /// <summary>
    /// Gets or sets whether the document contains cross-references.
    /// Example: "arriba mencionada", "anteriormente indicado", "según anexo".
    /// Indicates need for Search strategy.
    /// </summary>
    public bool HasCrossReferences { get; set; }

    /// <summary>
    /// Gets the recommended extraction strategy based on structure analysis.
    /// </summary>
    public DocxExtractionStrategy RecommendedStrategy => DetermineRecommendedStrategy();

    private DocxExtractionStrategy DetermineRecommendedStrategy()
    {
        // Structured format (CNBV standard) - use structured regex
        if (HasStructuredFormat)
            return DocxExtractionStrategy.Structured;

        // Has tables - use table-based extraction
        if (HasTables && TableStructure?.RowCount > 1)
            return DocxExtractionStrategy.TableBased;

        // Has labels and key-value pairs - use contextual extraction
        if (HasBoldLabels && HasKeyValuePairs)
            return DocxExtractionStrategy.Contextual;

        // Has cross-references - needs search strategy
        if (HasCrossReferences)
            return DocxExtractionStrategy.Hybrid; // Combine contextual + search

        // Semi-structured with some patterns - hybrid approach
        if (HasKeyValuePairs || StyledElementCount > 5)
            return DocxExtractionStrategy.Hybrid;

        // Unstructured chaos - use fuzzy + contextual
        return DocxExtractionStrategy.Fuzzy;
    }
}