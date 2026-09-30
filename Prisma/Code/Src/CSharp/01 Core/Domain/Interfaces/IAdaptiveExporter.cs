using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Orchestrator for adaptive export operations using template-based configuration.
/// Coordinates ITemplateRepository and ITemplateFieldMapper to produce exports without code changes.
/// </summary>
public interface IAdaptiveExporter
{
    /// <summary>
    /// Exports source data to the specified format using the active template.
    /// </summary>
    /// <param name="sourceObject">The source object to export (e.g., UnifiedMetadataRecord).</param>
    /// <param name="templateType">The type of template to use (e.g., "Excel", "XML", "DOCX").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the exported file as byte array, or failure with error message.</returns>
    /// <remarks>
    /// This method:
    /// 1. Retrieves the active template from ITemplateRepository
    /// 2. Maps fields using ITemplateFieldMapper
    /// 3. Generates the output file based on template format
    /// 4. Returns the file as byte array
    /// </remarks>
    Task<Result<byte[]>> ExportAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports source data using a specific template version.
    /// </summary>
    /// <param name="sourceObject">The source object to export.</param>
    /// <param name="templateType">The type of template to use.</param>
    /// <param name="version">The specific template version to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the exported file as byte array, or failure with error message.</returns>
    /// <remarks>
    /// Use this for A/B testing or when you need to export using a specific template version.
    /// </remarks>
    Task<Result<byte[]>> ExportWithVersionAsync(
        object sourceObject,
        string templateType,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active template for the specified type.
    /// </summary>
    /// <param name="templateType">The type of template (e.g., "Excel", "XML").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the active template definition, or failure if not found.</returns>
    Task<Result<TemplateDefinition>> GetActiveTemplateAsync(
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the source object can be exported using the active template.
    /// </summary>
    /// <param name="sourceObject">The source object to validate.</param>
    /// <param name="templateType">The type of template to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with detailed validation errors.</returns>
    /// <remarks>
    /// This method performs pre-export validation:
    /// 1. Checks if active template exists
    /// 2. Validates all required fields are present in source object
    /// 3. Validates field types match expectations
    /// 4. Validates transformation expressions are valid
    /// Does NOT perform the actual export - use ExportAsync for that.
    /// </remarks>
    Task<Result> ValidateExportAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a preview of the mapped fields without creating the full export.
    /// </summary>
    /// <param name="sourceObject">The source object to preview.</param>
    /// <param name="templateType">The type of template to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing a dictionary of field mappings (target field → mapped value).</returns>
    /// <remarks>
    /// Useful for debugging template configurations and verifying field mappings
    /// before performing the actual export.
    /// </remarks>
    Task<Result<System.Collections.Generic.Dictionary<string, string>>> PreviewMappingAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the template cache, forcing reload on next export.
    /// </summary>
    /// <remarks>
    /// Use this when templates are updated externally and need to be reloaded immediately.
    /// Templates are cached for performance - this method forces a cache refresh.
    /// </remarks>
    void ClearTemplateCache();

    /// <summary>
    /// Checks if a specific template type is supported and has an active template.
    /// </summary>
    /// <param name="templateType">The type of template to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing true if template is available, false otherwise.</returns>
    Task<Result<bool>> IsTemplateAvailableAsync(
        string templateType,
        CancellationToken cancellationToken = default);
}
