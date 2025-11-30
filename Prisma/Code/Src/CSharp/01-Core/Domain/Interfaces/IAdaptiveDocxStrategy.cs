namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Defines an adaptive DOCX extraction strategy that autonomously extracts structured data from DOCX documents.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Strategy Pattern with autonomous, confidence-based selection.
/// Unlike <see cref="IDocxExtractionStrategy"/>, adaptive strategies operate without field definitions
/// and extract all discoverable data using pattern recognition and document structure analysis.
/// </para>
/// <para>
/// <strong>Design Principles:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Returns <see cref="ExtractedFields"/> DTO, NOT <see cref="Entities.Expediente"/> entity</description></item>
///   <item><description>Entity mapping (ExtractedFields → Expediente) is separate business logic</description></item>
///   <item><description>Strategies are stateless and can be tested with mocks (ITDD / Liskov Substitution Principle)</description></item>
///   <item><description>Confidence-based selection enables multiple strategies to coexist</description></item>
/// </list>
/// <para>
/// <strong>Implementation Guidelines:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Extract core fields (Expediente, Causa, AccionSolicitada) into ExtractedFields properties</description></item>
///   <item><description>Extract extended data (names, accounts, dates) into AdditionalFields dictionary</description></item>
///   <item><description>Extract monetary amounts into Montos list with AmountData value objects</description></item>
///   <item><description>Use AdditionalFields for Mexican name parts: "Paterno", "Materno", "Nombre"</description></item>
///   <item><description>Return null if strategy cannot extract meaningful data from document</description></item>
/// </list>
/// <para>
/// <strong>Example Strategy Types:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>StructuredDocxStrategy - Regex patterns for well-formatted CNBV documents</description></item>
///   <item><description>ContextualDocxStrategy - Label-value extraction with variations</description></item>
///   <item><description>TableBasedDocxStrategy - Extract from DOCX table structures</description></item>
///   <item><description>ComplementExtractionStrategy - Fill XML/OCR gaps (EXPECTED workflow)</description></item>
///   <item><description>SearchExtractionStrategy - Resolve cross-references ("cantidad arriba mencionada")</description></item>
/// </list>
/// </remarks>
public interface IAdaptiveDocxStrategy
{
    /// <summary>
    /// Gets the strategy name for logging and diagnostics.
    /// </summary>
    /// <remarks>
    /// Examples: "StructuredDocx", "ContextualDocx", "TableBased", "Complement", "Search"
    /// </remarks>
    string StrategyName { get; }

    /// <summary>
    /// Extracts structured data from DOCX document text using this strategy.
    /// </summary>
    /// <param name="docxText">The full text content extracted from DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// An <see cref="ExtractedFields"/> DTO containing discovered data, or null if strategy cannot extract.
    /// Core fields (Expediente, Causa, AccionSolicitada) go in properties.
    /// Extended fields (names, accounts, dates) go in AdditionalFields dictionary.
    /// Monetary amounts go in Montos list.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Return null (not empty ExtractedFields) if this strategy cannot meaningfully extract from the document.
    /// This allows orchestrator to try other strategies.
    /// </para>
    /// <para>
    /// <strong>Mexican Name Extraction:</strong> Use AdditionalFields for:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>AdditionalFields["Paterno"] - Paternal last name</description></item>
    ///   <item><description>AdditionalFields["Materno"] - Maternal last name</description></item>
    ///   <item><description>AdditionalFields["Nombre"] - Given name(s)</description></item>
    ///   <item><description>AdditionalFields["NombreCompleto"] - Full name as found in document</description></item>
    /// </list>
    /// <para>
    /// <strong>Account Extraction:</strong> Use AdditionalFields for:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>AdditionalFields["NumeroCuenta"] - Account number</description></item>
    ///   <item><description>AdditionalFields["CLABE"] - 18-digit CLABE</description></item>
    ///   <item><description>AdditionalFields["Banco"] - Bank name</description></item>
    /// </list>
    /// </remarks>
    Task<ExtractedFields?> ExtractAsync(string docxText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this strategy can extract data from the given document.
    /// </summary>
    /// <param name="docxText">The full text content extracted from DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// True if this strategy can process the document (confidence > 0); false otherwise.
    /// </returns>
    /// <remarks>
    /// This is a fast preliminary check. If this returns false, ExtractAsync will not be called.
    /// Use this to quickly reject incompatible document structures.
    /// </remarks>
    Task<bool> CanExtractAsync(string docxText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates confidence score indicating how well this strategy can extract from the document.
    /// </summary>
    /// <param name="docxText">The full text content extracted from DOCX document.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Confidence score from 0 to 100:
    /// <list type="bullet">
    ///   <item><description>0 - Strategy cannot extract from this document</description></item>
    ///   <item><description>1-30 - Low confidence (backup option)</description></item>
    ///   <item><description>31-60 - Medium confidence (complement/fallback)</description></item>
    ///   <item><description>61-80 - High confidence (primary option)</description></item>
    ///   <item><description>81-100 - Very high confidence (ideal match)</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Confidence is based on document structure indicators:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>StructuredDocx: 90 if 3+ standard labels found ("Expediente No.:", "Oficio:")</description></item>
    ///   <item><description>ContextualDocx: 75 if 2+ contextual patterns match</description></item>
    ///   <item><description>TableBased: 85 if table structure + headers detected</description></item>
    ///   <item><description>Complement: 50 (always available, lower priority)</description></item>
    ///   <item><description>Search: 80 if cross-reference keywords found, 0 otherwise</description></item>
    /// </list>
    /// </remarks>
    Task<int> GetConfidenceAsync(string docxText, CancellationToken cancellationToken = default);
}
