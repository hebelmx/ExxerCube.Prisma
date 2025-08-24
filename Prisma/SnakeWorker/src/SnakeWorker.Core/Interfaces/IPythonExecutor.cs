using SnakeWorker.Core.Models;

namespace SnakeWorker.Core.Interfaces;

/// <summary>
/// Interface for executing Python code through CSnakes
/// </summary>
public interface IPythonExecutor
{
    /// <summary>
    /// Execute a Python function asynchronously
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task result</returns>
    Task<TaskResult> ExecuteAsync(WorkerTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a Python module is available
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <returns>True if module is available</returns>
    Task<bool> IsModuleAvailableAsync(string moduleName);

    /// <summary>
    /// Get available functions in a Python module
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <returns>List of function names</returns>
    Task<string[]> GetAvailableFunctionsAsync(string moduleName);

    /// <summary>
    /// Get Python environment information
    /// </summary>
    /// <returns>Environment info</returns>
    Task<Dictionary<string, object>> GetEnvironmentInfoAsync();
}