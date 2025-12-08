using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Lightweight factory for creating loggers in xUnit tests without coupling to specific sinks.
/// Falls back to null loggers to keep infrastructure tests running when output helpers are absent.
/// </summary>
public static class XUnitLogger
{
    /// <summary>
    /// Creates a logger for the given category type. Output sink is ignored to keep tests decoupled from logging pipelines.
    /// </summary>
    /// <typeparam name="T">Category type for the logger.</typeparam>
    /// <param name="output">Optional output helper from xUnit.</param>
    /// <returns>A logger instance suitable for tests.</returns>
    public static ILogger<T> CreateLogger<T>(object? output = null)
    {
        // Output helper is currently ignored; we provide a deterministic logger for test runs.
        return NullLoggerFactory.Instance.CreateLogger<T>();
    }
}
