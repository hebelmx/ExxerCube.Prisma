namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Logger implementation that intentionally drops all messages.
/// Use this in shared libraries or contexts where real logging sinks are unavailable;
/// test projects should favor <see cref="XUnitLoggerAdapter"/> for visible output.
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

