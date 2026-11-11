namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Logger adapter that wraps xUnit's <c>ITestOutputHelper</c> for use in test projects.
/// Note: This class should only be used where <c>ITestOutputHelper</c> is available.
/// </summary>
public class XUnitLoggerAdapter : ITestLogger
{
    private readonly object _output;

    /// <summary>
    /// Initializes an adapter that forwards messages to the provided xUnit output helper.
    /// </summary>
    /// <param name="output">The test output helper (<c>ITestOutputHelper</c> from xUnit).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
    public XUnitLoggerAdapter(object output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <inheritdoc />
    public void Log(string message)
    {
        // Use reflection to call WriteLine on ITestOutputHelper
        var writeLineMethod = _output.GetType().GetMethod("WriteLine", new[] { typeof(string) });
        writeLineMethod?.Invoke(_output, new[] { message });
    }

    /// <inheritdoc />
    public void LogDebug(string message)
    {
        var writeLineMethod = _output.GetType().GetMethod("WriteLine", new[] { typeof(string) });
        writeLineMethod?.Invoke(_output, new[] { $"[DEBUG] {message}" });
    }

    /// <inheritdoc />
    public void LogError(string message, Exception? exception = null)
    {
        var writeLineMethod = _output.GetType().GetMethod("WriteLine", new[] { typeof(string) });
        if (exception != null)
        {
            writeLineMethod?.Invoke(_output, new[] { $"[ERROR] {message}" });
            writeLineMethod?.Invoke(_output, new[] { $"Exception: {exception}" });
        }
        else
        {
            writeLineMethod?.Invoke(_output, new[] { $"[ERROR] {message}" });
        }
    }
}

