namespace ExxerCube.Prisma.Infrastructure.Python;

/// <summary>
/// CSnakes Python environment manager for Prisma OCR processing.
/// Provides type-safe Python integration following the TransformersSharp pattern.
/// </summary>
public static class PrismaPythonEnvironment
{
    private static readonly IPythonEnvironment? _env;
    private static readonly Lock _setupLock = new();

    static PrismaPythonEnvironment()
    {
        lock (_setupLock)
        {
            IHostBuilder builder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Use Local App Data folder for Python installation
                    string appDataPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrismaPython");

                    // Create the directory if it doesn't exist
                    if (!Directory.Exists(appDataPath))
                        Directory.CreateDirectory(appDataPath);

                    // If user has an environment variable PRISMA_PYTHON_VENV_PATH, use that instead
                    string? envPath = Environment.GetEnvironmentVariable("PRISMA_PYTHON_VENV_PATH");
                    string venvPath;
                    if (envPath != null)
                        venvPath = envPath;
                    else
                        venvPath = Path.Join(appDataPath, "venv");

                    // Write requirements to appDataPath
                    string requirementsPath = Path.Join(appDataPath, "requirements.txt");

                    // Prisma-specific Python requirements
                    string[] requirements =
                    {
                        "pytesseract",
                        "Pillow",
                        "opencv-python",
                        "numpy",
                        "pandas",
                        "python-dateutil",
                        "regex"
                    };

                    File.WriteAllText(requirementsPath, string.Join('\n', requirements));

                    services
                            .WithPython()
                            .WithHome(Directory.GetCurrentDirectory()) // CSnakes will find the Python files
                            .FromNuGet("3.12.4")
                            .FromEnvironmentVariable("Python3_ROOT_DIR", "3.12.14")
                            .WithVirtualEnvironment(venvPath)
                            .WithUvInstaller() // Use pip to install packages from requirements.txt
                                               //.WithPipInstaller()
                            .FromRedistributable(); // Download Python 3.12 and store it locally
                });

            var app = builder.Build();

            _env = app.Services.GetRequiredService<IPythonEnvironment>();
        }
    }

    /// <summary>
    /// Gets the Python environment instance.
    /// </summary>
    public static IPythonEnvironment Env => _env ?? throw new InvalidOperationException("Python environment is not initialized.");

    /// <summary>
    /// Disposes the Python environment and releases resources.
    /// </summary>
    public static void Dispose()
    {
        _env?.Dispose();
    }
}