namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Logger implementation that does nothing (no-op).
/// Use this in libraries where logging is not available.
/// Test projects should use XUnitLoggerAdapter instead.
/// </summary>
public class NoOpLogger : ITestLogger
{
    /// <inheritdoc />
    public void Log(string message)
    {
        // No-op - logging not available in library context
    }

    /// <inheritdoc />
    public void LogDebug(string message)
    {
        // No-op - logging not available in library context
    }

    /// <inheritdoc />
    public void LogError(string message, Exception? exception = null)
    {
        // No-op - logging not available in library context
    }
}

