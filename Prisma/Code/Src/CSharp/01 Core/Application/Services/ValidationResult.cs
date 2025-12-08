namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents validation results for a specific configuration section.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}