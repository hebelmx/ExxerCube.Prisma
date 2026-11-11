namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// Configuration settings for Python interoperability.
/// </summary>
public class PythonConfiguration
{
    /// <summary>
    /// Gets or sets the path to the Python modules.
    /// </summary>
    public string ModulesPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Python executable path.
    /// </summary>
    public string PythonExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of concurrent Python operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for Python operations in seconds.
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to enable Python interop debugging.
    /// </summary>
    public bool EnableDebugging { get; set; } = false;

    /// <summary>
    /// Gets or sets the path to Python virtual environment (if using one).
    /// </summary>
    public string? VirtualEnvironmentPath { get; set; }

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ModulesPath) && 
               !string.IsNullOrWhiteSpace(PythonExecutablePath) &&
               MaxConcurrency > 0 &&
               OperationTimeoutSeconds > 0;
    }

    /// <summary>
    /// Creates a default configuration for development.
    /// </summary>
    /// <returns>A default Python configuration.</returns>
    public static PythonConfiguration CreateDefault()
    {
        return new PythonConfiguration
        {
            ModulesPath = "./Python",
            PythonExecutablePath = "python",
            MaxConcurrency = 5,
            OperationTimeoutSeconds = 30,
            EnableDebugging = false
        };
    }
}
