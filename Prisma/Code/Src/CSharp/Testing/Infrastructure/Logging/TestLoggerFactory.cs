namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Factory for creating test loggers with deferred execution support.
/// </summary>
public static class TestLoggerFactory
{
    /// <summary>
    /// Creates a test logger. If output is provided, uses XUnitLoggerAdapter,
    /// otherwise uses TestContextLogger for deferred execution.
    /// </summary>
    /// <param name="output">Optional test output helper (ITestOutputHelper from xUnit). If provided, uses direct output.</param>
    /// <returns>A test logger instance.</returns>
    public static ITestLogger Create(object? output = null)
    {
        return output != null
            ? new XUnitLoggerAdapter(output)
            : new NoOpLogger();
    }

    /// <summary>
    /// Creates a no-op logger for use in libraries where logging is not available.
    /// Test projects should use Create() with ITestOutputHelper instead.
    /// </summary>
    /// <returns>A no-op test logger.</returns>
    public static ITestLogger CreateDeferred() => new NoOpLogger();
}

