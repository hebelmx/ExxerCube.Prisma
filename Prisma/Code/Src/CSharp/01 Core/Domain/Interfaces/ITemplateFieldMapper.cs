using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides dynamic field mapping from source objects to template field definitions.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the contract for mapping fields from source data objects
/// (like <see cref="UnifiedMetadataRecord"/>) to target template fields using reflection
/// and the field mapping definitions from <see cref="TemplateDefinition"/>.
/// </para>
/// <para>
/// <strong>Key Capabilities:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Reflection-based field extraction using dot notation paths (e.g., "Expediente.NumeroExpediente")</description></item>
///   <item><description>Type conversion and formatting (e.g., DateTime to "yyyy-MM-dd")</description></item>
///   <item><description>Transformation expression evaluation (e.g., "ToUpper()", "Trim()")</description></item>
///   <item><description>Validation rule enforcement (e.g., regex patterns, range checks)</description></item>
///   <item><description>Default value fallback for missing/null fields</description></item>
/// </list>
/// <para>
/// <strong>ITDD Design:</strong> This interface is designed using Interface-Test-Driven Development.
/// Contract tests using mocks validate the abstraction before implementation.
/// </para>
/// </remarks>
public interface ITemplateFieldMapper
{
    /// <summary>
    /// Maps a single field from the source object using the field mapping definition.
    /// </summary>
    /// <param name="sourceObject">The source object to extract the field value from.</param>
    /// <param name="mapping">The field mapping definition specifying how to extract and transform the field.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the mapped field value as a string if successful,
    /// or an error message if the mapping fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs the following operations:
    /// </para>
    /// <list type="number">
    ///   <item><description>Extracts the field value from <paramref name="sourceObject"/> using <see cref="FieldMapping.SourceFieldPath"/></description></item>
    ///   <item><description>Applies data type conversion if needed</description></item>
    ///   <item><description>Applies formatting (e.g., date formats, number formats)</description></item>
    ///   <item><description>Applies transformation expressions (e.g., ToUpper, Trim)</description></item>
    ///   <item><description>Validates against validation rules</description></item>
    ///   <item><description>Returns default value if field is null/missing and default is specified</description></item>
    /// </list>
    /// <para>
    /// <strong>Examples:</strong>
    /// </para>
    /// <code>
    /// // Simple property access
    /// SourceFieldPath = "Expediente.NumeroExpediente"
    /// Result: "EXP-2024-001"
    ///
    /// // Collection indexing
    /// SourceFieldPath = "Personas[0].Nombre"
    /// Result: "Juan Pérez"
    ///
    /// // With transformation
    /// SourceFieldPath = "Email", TransformExpression = "ToUpper()"
    /// Result: "USER@EXAMPLE.COM"
    /// </code>
    /// </remarks>
    Task<Result<string>> MapFieldAsync(
        object sourceObject,
        FieldMapping mapping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps all fields from the source object according to the template definition.
    /// </summary>
    /// <param name="sourceObject">The source object to extract field values from.</param>
    /// <param name="template">The template definition containing all field mappings.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing a dictionary of target field names to their mapped values if successful,
    /// or an error message if the mapping fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method iterates through all <see cref="TemplateDefinition.FieldMappings"/> and maps each field
    /// using <see cref="MapFieldAsync"/>. Fields are mapped in order according to <see cref="FieldMapping.DisplayOrder"/>.
    /// </para>
    /// <para>
    /// <strong>Behavior:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Required fields (<see cref="FieldMapping.IsRequired"/> = true) that fail to map cause the entire operation to fail</description></item>
    ///   <item><description>Optional fields that fail to map are logged but don't cause failure</description></item>
    ///   <item><description>Returns a dictionary where keys are <see cref="FieldMapping.TargetField"/> values</description></item>
    /// </list>
    /// </remarks>
    Task<Result<Dictionary<string, string>>> MapAllFieldsAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a field mapping can be successfully applied to a source object type.
    /// </summary>
    /// <param name="sourceType">The type of the source object.</param>
    /// <param name="mapping">The field mapping to validate.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success if the mapping is valid,
    /// or an error message describing why the mapping is invalid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs static validation without requiring an actual source object instance.
    /// It checks:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>The <see cref="FieldMapping.SourceFieldPath"/> exists on the source type</description></item>
    ///   <item><description>The field path is syntactically valid (e.g., proper dot notation, valid array indices)</description></item>
    ///   <item><description>The target data type is compatible with the source field type</description></item>
    ///   <item><description>Transformation expressions are valid and can be parsed</description></item>
    ///   <item><description>Validation rules are syntactically correct</description></item>
    /// </list>
    /// <para>
    /// This is useful for validating template definitions before they are deployed to production.
    /// </para>
    /// </remarks>
    Task<Result> ValidateMappingAsync(
        Type sourceType,
        FieldMapping mapping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a transformation expression to a field value.
    /// </summary>
    /// <param name="value">The field value to transform.</param>
    /// <param name="transformExpression">The transformation expression (e.g., "ToUpper()", "Trim()", "Substring(0, 10)").</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the transformed value if successful,
    /// or an error message if the transformation fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Supported Transformations:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>ToUpper()</c> - Convert to uppercase</description></item>
    ///   <item><description><c>ToLower()</c> - Convert to lowercase</description></item>
    ///   <item><description><c>Trim()</c> - Remove leading/trailing whitespace</description></item>
    ///   <item><description><c>Substring(start, length)</c> - Extract substring</description></item>
    ///   <item><description><c>Replace(oldValue, newValue)</c> - String replacement</description></item>
    ///   <item><description><c>PadLeft(totalWidth, paddingChar)</c> - Pad string on left</description></item>
    ///   <item><description><c>PadRight(totalWidth, paddingChar)</c> - Pad string on right</description></item>
    /// </list>
    /// <para>
    /// Transformations can be chained using the pipe operator: <c>"Trim() | ToUpper()"</c>
    /// </para>
    /// </remarks>
    Task<Result<string>> ApplyTransformationAsync(
        string value,
        string transformExpression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a field value against validation rules.
    /// </summary>
    /// <param name="value">The field value to validate.</param>
    /// <param name="mapping">The field mapping containing validation rules.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success if the value passes all validation rules,
    /// or an error message describing which validation rule failed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Supported Validation Rules (from <see cref="FieldMapping.ValidationRules"/>):</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>Regex:^[A-Z0-9-]+$</c> - Matches regular expression pattern</description></item>
    ///   <item><description><c>Range:1,100</c> - Numeric value within range (inclusive)</description></item>
    ///   <item><description><c>MinLength:5</c> - Minimum string length</description></item>
    ///   <item><description><c>MaxLength:50</c> - Maximum string length</description></item>
    ///   <item><description><c>EmailAddress</c> - Valid email format</description></item>
    ///   <item><description><c>Required</c> - Value cannot be null or empty</description></item>
    /// </list>
    /// <para>
    /// All validation rules in the <see cref="FieldMapping.ValidationRules"/> list must pass for validation to succeed.
    /// </para>
    /// </remarks>
    Task<Result> ValidateFieldValueAsync(
        string value,
        FieldMapping mapping,
        CancellationToken cancellationToken = default);
}
