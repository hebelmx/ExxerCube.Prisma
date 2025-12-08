using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Detects schema evolution and drift between source data and template definitions.
///
/// Schema drift occurs when:
/// - Bank adds new fields to their response format
/// - Bank removes or renames fields
/// - Template hasn't been updated to reflect schema changes
///
/// This interface enables:
/// - Automatic detection of schema changes
/// - Alerts when templates become outdated
/// - Recommendations for template updates
/// - Fuzzy matching for renamed fields
/// </summary>
/// <remarks>
/// ITDD Pattern: This interface defines the contract for schema evolution detection.
/// Implementations must satisfy all contract tests defined in ISchemaEvolutionDetectorContractTests.
/// </remarks>
public interface ISchemaEvolutionDetector
{
    /// <summary>
    /// Detects schema drift by comparing source object fields against template field mappings.
    /// </summary>
    /// <param name="sourceObject">The source data object to analyze (e.g., UnifiedMetadataRecord).</param>
    /// <param name="template">The template definition to compare against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success with SchemaDriftReport containing:
    /// - NewFields: Fields in source not mapped in template
    /// - MissingFields: Required template fields not found in source
    /// - RenamedFields: Potential field renames detected via fuzzy matching
    /// - Severity: Overall drift severity (None, Low, Medium, High)
    ///
    /// Failure if:
    /// - sourceObject is null
    /// - template is null
    /// </returns>
    /// <remarks>
    /// Detection algorithm:
    /// 1. Extract all field paths from source object using reflection
    /// 2. Compare against template.FieldMappings
    /// 3. Identify new fields (in source, not in template)
    /// 4. Identify missing fields (in template, not in source)
    /// 5. Apply fuzzy matching to detect renames (Levenshtein distance, soundex, etc.)
    /// 6. Calculate severity based on field importance and count
    /// </remarks>
    Task<Result<SchemaDriftReport>> DetectDriftAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects schema drift using the active template for a given template type.
    /// </summary>
    /// <param name="sourceObject">The source data object to analyze.</param>
    /// <param name="templateType">The template type (Excel, XML, DOCX).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success with SchemaDriftReport if active template exists.
    /// Failure if:
    /// - sourceObject is null
    /// - No active template found for templateType
    /// </returns>
    /// <remarks>
    /// This method loads the active template via ITemplateRepository and delegates to DetectDriftAsync.
    /// </remarks>
    Task<Result<SchemaDriftReport>> DetectDriftForActiveTemplateAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a source object and suggests field mappings for a new template.
    /// Useful when creating templates from scratch based on actual data.
    /// </summary>
    /// <param name="sourceObject">The source data object to analyze.</param>
    /// <param name="templateType">The template type to create suggestions for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success with suggested FieldMapping collection.
    /// Each suggestion includes:
    /// - SourceFieldPath: Detected field path in source
    /// - TargetField: Suggested target name (humanized from field path)
    /// - DataType: Detected data type
    /// - IsRequired: Suggested based on nullability
    ///
    /// Failure if sourceObject is null.
    /// </returns>
    /// <remarks>
    /// This method helps bootstrap template creation by:
    /// 1. Discovering all fields in source object
    /// 2. Detecting data types
    /// 3. Analyzing nullability
    /// 4. Suggesting human-readable target names
    /// </remarks>
    Task<Result<FieldMapping[]>> SuggestFieldMappingsAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates similarity score between two field names using fuzzy matching.
    /// Used to detect renamed fields.
    /// </summary>
    /// <param name="fieldName1">First field name.</param>
    /// <param name="fieldName2">Second field name.</param>
    /// <returns>
    /// Similarity score between 0.0 (completely different) and 1.0 (identical).
    /// Typically uses:
    /// - Levenshtein distance for edit distance
    /// - Soundex for phonetic matching
    /// - Token-based matching for multi-word fields
    /// </returns>
    /// <remarks>
    /// Threshold recommendations:
    /// - 0.9-1.0: Very likely same field (minor typo)
    /// - 0.7-0.9: Probably renamed (suggest to user)
    /// - 0.5-0.7: Possibly related (show as low-confidence suggestion)
    /// - 0.0-0.5: Likely different fields
    /// </remarks>
    double CalculateSimilarity(string fieldName1, string fieldName2);

    /// <summary>
    /// Validates whether a template is compatible with source data structure.
    /// </summary>
    /// <param name="sourceObject">The source data object.</param>
    /// <param name="template">The template definition to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success if template is compatible (all required fields can be mapped).
    /// Failure with validation errors if:
    /// - Required template fields are missing in source
    /// - Field types are incompatible
    /// </returns>
    Task<Result> ValidateTemplateCompatibilityAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default);
}
