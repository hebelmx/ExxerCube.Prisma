namespace SnakeWorker.Python;

/// <summary>
/// Configuration options for CSnakes Python integration
/// </summary>
public class CSnakesOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "CSnakes";

    /// <summary>
    /// Path to Python DLL (optional, auto-detected if not specified)
    /// </summary>
    public string? PythonDll { get; set; }

    /// <summary>
    /// Python home directory (optional)
    /// </summary>
    public string? PythonHome { get; set; }

    /// <summary>
    /// Additional Python paths to add to sys.path
    /// </summary>
    public string[] PythonPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default timeout for Python function calls (seconds)
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of concurrent Python executions
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Enable detailed Python debugging
    /// </summary>
    public bool EnableDebugMode { get; set; } = false;

    /// <summary>
    /// Python modules to preload at startup
    /// </summary>
    public string[] PreloadModules { get; set; } = Array.Empty<string>();
}