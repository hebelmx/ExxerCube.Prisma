namespace ExxerCube.Prisma.Testing.Infrastructure.Logging;

/// <summary>
/// Logger adapter that wraps ITestOutputHelper for use in test projects.
/// Note: This class should only be used in test projects where ITestOutputHelper is available.
/// </summary>
public class XUnitLoggerAdapter : ITestLogger
{
    private readonly object _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="XUnitLoggerAdapter"/> class.
    /// </summary>
    /// <param name="output">The test output helper (ITestOutputHelper from xUnit).</param>
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

