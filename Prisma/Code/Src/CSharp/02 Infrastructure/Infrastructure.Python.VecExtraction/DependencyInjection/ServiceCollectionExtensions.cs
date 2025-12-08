namespace ExxerCube.Prisma.Infrastructure.Python.VecExtraction.DependencyInjection;

/// <summary>
/// Extension methods for configuring VEC extraction Python interop services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds VEC extraction Python environment and configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pythonLibPath">Path to Python library (optional, will use redistributable if not provided).</param>
    /// <param name="venvPath">Path to Python virtual environment (optional, will create if not exists).</param>
    /// <param name="requirementsPath">Path to requirements.txt file (optional).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVecExtractionPythonEnvironment(
        this IServiceCollection services,
        string? pythonLibPath = null,
        string? venvPath = null,
        string? requirementsPath = null)
    {
        // Configure CSnakes Python environment for VEC extraction
        var builder = services.WithPython();

        // Use redistributable Python 3.13 if no custom path provided
        if (string.IsNullOrEmpty(pythonLibPath))
        {
            builder.FromRedistributable("3.13");
        }
        else
        {
            builder.WithHome(pythonLibPath);
        }

        // Configure virtual environment if provided
        if (!string.IsNullOrEmpty(venvPath))
        {
            builder.WithVirtualEnvironment(venvPath, ensureEnvironment: true);
        }

        // Register requirements.txt for pip installation
        if (!string.IsNullOrEmpty(requirementsPath))
        {
            builder.WithPipInstaller(requirementsPath);
        }
        else
        {
            // Default: look for requirements.txt in output directory
            builder.WithPipInstaller("requirements.txt");
        }

        return services;
    }

    /// <summary>
    /// Adds VEC extraction Python environment with manual setup configuration.
    /// This assumes the Python environment has been set up manually using setup_environment_manual.ps1
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseDirectory">Base directory where .venv_vec is located.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVecExtractionPythonEnvironmentManual(
        this IServiceCollection services,
        string baseDirectory)
    {
        var pythonLibPath = Path.Combine(baseDirectory, "python");
        var venvPath = Path.Combine(baseDirectory, ".venv_vec");
        var requirementsPath = Path.Combine(baseDirectory, "python", "requirements.txt");

        return services.AddVecExtractionPythonEnvironment(
            pythonLibPath: pythonLibPath,
            venvPath: venvPath,
            requirementsPath: requirementsPath
        );
    }
}
