namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Abstraction for lightweight test logging that works in both libraries and test projects,
/// including scenarios where xUnit's <c>ITestOutputHelper</c> is unavailable (e.g., shared test utilities).
/// </summary>
public interface ITestLogger
{
    /// <summary>
    /// Logs an informational message intended for humans reading test output.
    /// </summary>
    /// <param name="message">The message to log; should already be formatted.</param>
    void Log(string message);

    /// <summary>
    /// Logs a debug message for low-level diagnostics.
    /// </summary>
    /// <param name="message">The message to log; include context that aids troubleshooting.</param>
    void LogDebug(string message);

    /// <summary>
    /// Logs an error message and optional exception details.
    /// </summary>
    /// <param name="message">The error message to record.</param>
    /// <param name="exception">Optional exception associated with the failure; implementations should render both message and exception.</param>
    void LogError(string message, Exception? exception = null);
}
