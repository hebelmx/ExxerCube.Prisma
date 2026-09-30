using Contoso.Logging;

namespace Samples;

public static class SampleApp
{
    public static void Run()
    {
        var logger = new Logger();
        logger.Write("hi");
    }
}
