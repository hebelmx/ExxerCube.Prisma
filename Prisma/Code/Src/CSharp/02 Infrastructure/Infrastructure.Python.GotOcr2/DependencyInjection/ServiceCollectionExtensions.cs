namespace ExxerCube.Prisma.Infrastructure.Python.GotOcr2.DependencyInjection;

/// <summary>
/// Extension methods for configuring GOT-OCR2 Python interop services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds GOT-OCR2 Python environment and configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pythonLibPath">Path to Python library (optional, will use redistributable if not provided).</param>
    /// <param name="venvPath">Path to Python virtual environment (optional, will create if not exists).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGotOcr2PythonEnvironment(
        this IServiceCollection services,
        string? pythonLibPath = null,
        string? venvPath = null)
    {
        // Configure CSnakes Python environment for GOT-OCR2
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
        builder.WithPipInstaller("requirements.txt");

        return services;
    }
}
