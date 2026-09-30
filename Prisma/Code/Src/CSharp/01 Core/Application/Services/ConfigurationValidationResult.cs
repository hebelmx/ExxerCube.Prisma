namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents the result of configuration validation.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the validated configuration.
    /// </summary>
    public ProcessingConfig? ValidatedConfig { get; set; }
}