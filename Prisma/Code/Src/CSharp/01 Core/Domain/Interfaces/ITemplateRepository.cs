using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Repository for loading and managing bank export template definitions.
/// </summary>
/// <remarks>
/// <para>
/// Provides access to versioned template definitions for adaptive export generation.
/// Templates are stored in database with full version history and audit trail.
/// </para>
/// <para>
/// <strong>Template Versioning:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Semantic versioning: MAJOR.MINOR.PATCH</description></item>
///   <item><description>Active flag controls which template is used</description></item>
///   <item><description>Effective/expiration dates control template lifecycle</description></item>
/// </list>
/// <para>
/// <strong>Typical Usage:</strong>
/// </para>
/// <code>
/// var template = await repo.GetLatestTemplateAsync("Excel", cancellationToken);
/// var specificVersion = await repo.GetTemplateAsync("Excel", "1.0.0", cancellationToken);
/// var allVersions = await repo.GetAllTemplateVersionsAsync("XML", cancellationToken);
/// </code>
/// </remarks>
public interface ITemplateRepository
{
    /// <summary>
    /// Gets a template definition by type and version.
    /// </summary>
    /// <param name="templateType">The template type (Excel, XML, PDF).</param>
    /// <param name="version">The semantic version (e.g., "1.0.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The template definition if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// Returns null when no template exists with the specified type and version.
    /// Does not consider IsActive flag - returns inactive templates if they exist.
    /// </remarks>
    Task<TemplateDefinition?> GetTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest active template definition for the specified type.
    /// </summary>
    /// <param name="templateType">The template type (Excel, XML, PDF).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The latest active template definition if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns the highest version number template that:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Is marked as active (IsActive = true)</description></item>
    ///   <item><description>Has effective date &lt;= current date</description></item>
    ///   <item><description>Has no expiration date OR expiration date &gt; current date</description></item>
    /// </list>
    /// <para>
    /// Returns null when no active templates exist for the specified type.
    /// </para>
    /// </remarks>
    Task<TemplateDefinition?> GetLatestTemplateAsync(
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all template versions for the specified type, ordered by version descending.
    /// </summary>
    /// <param name="templateType">The template type (Excel, XML, PDF).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// List of template definitions ordered by version descending (newest first).
    /// Empty list when no templates exist for the specified type.
    /// </returns>
    /// <remarks>
    /// Returns all templates regardless of IsActive flag or effective/expiration dates.
    /// Useful for template version history and audit purposes.
    /// </remarks>
    Task<IReadOnlyList<TemplateDefinition>> GetAllTemplateVersionsAsync(
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new template definition or updates an existing one.
    /// </summary>
    /// <param name="template">The template definition to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result indicating success or failure.
    /// Returns failure if template validation fails or database error occurs.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Validation Rules:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>TemplateType and Version cannot be null or empty</description></item>
    ///   <item><description>Version must be valid semantic version (MAJOR.MINOR.PATCH)</description></item>
    ///   <item><description>FieldMappings must contain at least one mapping</description></item>
    ///   <item><description>EffectiveDate must be &lt;= ExpirationDate (if expiration is set)</description></item>
    /// </list>
    /// <para>
    /// If template with same TemplateType and Version exists, it will be updated.
    /// Otherwise, a new template record is created.
    /// </para>
    /// </remarks>
    Task<IndQuestResults.Result> SaveTemplateAsync(
        TemplateDefinition template,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template definition by type and version.
    /// </summary>
    /// <param name="templateType">The template type (Excel, XML, PDF).</param>
    /// <param name="version">The semantic version (e.g., "1.0.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result indicating success or failure.
    /// Returns failure if template not found or is currently active.
    /// </returns>
    /// <remarks>
    /// Cannot delete templates that are currently active (IsActive = true).
    /// Soft-deletes the template (marks as deleted but preserves audit trail).
    /// </remarks>
    Task<IndQuestResults.Result> DeleteTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a specific template version and deactivates all others of the same type.
    /// </summary>
    /// <param name="templateType">The template type (Excel, XML, PDF).</param>
    /// <param name="version">The semantic version to activate (e.g., "1.0.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result indicating success or failure.
    /// Returns failure if template not found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Atomically sets IsActive = true for the specified template and
    /// IsActive = false for all other templates of the same type.
    /// </para>
    /// <para>
    /// This ensures only one template version is active at a time per template type.
    /// </para>
    /// </remarks>
    Task<IndQuestResults.Result> ActivateTemplateAsync(
        string templateType,
        string version,
        CancellationToken cancellationToken = default);
}
