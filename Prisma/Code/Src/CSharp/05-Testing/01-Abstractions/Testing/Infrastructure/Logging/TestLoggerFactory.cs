namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Factory for creating test loggers that work whether or not <c>ITestOutputHelper</c> is available.
/// </summary>
public static class TestLoggerFactory
{
    /// <summary>
    /// Creates a test logger. When output is provided, returns <see cref="XUnitLoggerAdapter"/>;
    /// otherwise falls back to <see cref="NoOpLogger"/> to avoid failing in library contexts.
    /// </summary>
    /// <param name="output">Optional xUnit test output helper passed as <c>ITestOutputHelper</c>.</param>
    /// <returns>An <see cref="ITestLogger"/> that either forwards to xUnit output or safely ignores messages.</returns>
    public static ITestLogger Create(object? output = null)
    {
        return output != null
            ? new XUnitLoggerAdapter(output)
            : new NoOpLogger();
    }

    /// <summary>
    /// Creates a no-op logger for libraries or background components where logging sinks are unavailable.
    /// Test projects should prefer <see cref="Create(object?)"/> with an <c>ITestOutputHelper</c>.
    /// </summary>
    /// <returns>An <see cref="ITestLogger"/> that silently ignores log messages.</returns>
    public static ITestLogger CreateDeferred() => new NoOpLogger();
}
