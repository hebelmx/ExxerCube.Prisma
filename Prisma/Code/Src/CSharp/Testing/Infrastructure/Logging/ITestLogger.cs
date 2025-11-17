namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Abstraction for test logging that works in both libraries and test projects.
/// Supports deferred execution when ITestOutputHelper is not available.
/// </summary>
public interface ITestLogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Log(string message);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogDebug(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">Optional exception details.</param>
    void LogError(string message, Exception? exception = null);
}

