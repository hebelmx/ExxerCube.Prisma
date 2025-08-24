using CSnakes.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SnakeWorker.Core.Interfaces;
using SnakeWorker.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace SnakeWorker.Python;

/// <summary>
/// CSnakes implementation of Python executor
/// </summary>
public class CSnakesPythonExecutor : IPythonExecutor, IDisposable
{
    private readonly ILogger<CSnakesPythonExecutor> _logger;
    private readonly PythonEnvironment _pythonEnv;
    private readonly CSnakesOptions _options;
    private bool _disposed = false;

    public CSnakesPythonExecutor(
        ILogger<CSnakesPythonExecutor> logger,
        IOptions<CSnakesOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        // Initialize Python environment
        _pythonEnv = new PythonEnvironment(_options.PythonDll, _options.PythonHome);
        
        _logger.LogInformation("CSnakes Python executor initialized with Python {Version}", 
            _pythonEnv.Version);
    }

    /// <inheritdoc />
    public async Task<TaskResult> ExecuteAsync(WorkerTask task, CancellationToken cancellationToken = default)
    {
        var result = new TaskResult
        {
            TaskId = task.Id,
            StartedAt = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);

        try
        {
            _logger.LogInformation("Executing Python task {TaskId}: {Module}.{Function}", 
                task.Id, task.PythonModule, task.PythonFunction);

            // Import the module
            using var module = _pythonEnv.Import(task.PythonModule);
            
            // Get the function
            var function = module.Get(task.PythonFunction);
            
            // Parse input data
            object? inputData = null;
            if (!string.IsNullOrEmpty(task.InputData))
            {
                inputData = JsonSerializer.Deserialize<Dictionary<string, object>>(task.InputData);
            }

            // Execute with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(task.TimeoutSeconds));

            object? pythonResult;
            
            if (inputData is Dictionary<string, object> inputDict)
            {
                // Call with keyword arguments
                pythonResult = await Task.Run(() => function.Call(inputDict), timeoutCts.Token);
            }
            else if (inputData != null)
            {
                // Call with single argument
                pythonResult = await Task.Run(() => function.Call(inputData), timeoutCts.Token);
            }
            else
            {
                // Call with no arguments
                pythonResult = await Task.Run(() => function.Call(), timeoutCts.Token);
            }

            // Serialize result
            result.OutputData = JsonSerializer.Serialize(pythonResult);
            result.IsSuccess = true;

            _logger.LogInformation("Python task {TaskId} completed successfully in {Duration}ms", 
                task.Id, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Task was cancelled";
            _logger.LogWarning("Python task {TaskId} was cancelled", task.Id);
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Task timed out after {task.TimeoutSeconds} seconds";
            _logger.LogWarning("Python task {TaskId} timed out", task.Id);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ErrorDetails = ex.ToString();
            _logger.LogError(ex, "Python task {TaskId} failed", task.Id);
        }
        finally
        {
            stopwatch.Stop();
            result.CompletedAt = DateTime.UtcNow;
            result.CpuTimeMs = stopwatch.ElapsedMilliseconds;
            result.MemoryUsageBytes = GC.GetTotalMemory(false) - startMemory;
            
            // Force garbage collection to clean up Python objects
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> IsModuleAvailableAsync(string moduleName)
    {
        try
        {
            await Task.Run(() =>
            {
                using var module = _pythonEnv.Import(moduleName);
                return true;
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string[]> GetAvailableFunctionsAsync(string moduleName)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var module = _pythonEnv.Import(moduleName);
                using var builtins = _pythonEnv.Import("builtins");
                using var dirFunc = builtins.Get("dir");
                
                var result = dirFunc.Call(module);
                
                // Convert Python list to C# string array
                if (result is IEnumerable<object> items)
                {
                    return items.Select(item => item.ToString() ?? "").ToArray();
                }
                
                return Array.Empty<string>();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get functions for module {ModuleName}", moduleName);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, object>> GetEnvironmentInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new Dictionary<string, object>
            {
                ["python_version"] = _pythonEnv.Version,
                ["python_dll"] = _options.PythonDll ?? "default",
                ["python_home"] = _options.PythonHome ?? "default",
                ["initialized_at"] = DateTime.UtcNow,
                ["current_working_directory"] = Directory.GetCurrentDirectory()
            };

            try
            {
                using var sysModule = _pythonEnv.Import("sys");
                using var pathAttr = sysModule.Get("path");
                info["python_path"] = pathAttr.ToString();
            }
            catch (Exception ex)
            {
                info["python_path_error"] = ex.Message;
            }

            return info;
        });
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _pythonEnv?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}