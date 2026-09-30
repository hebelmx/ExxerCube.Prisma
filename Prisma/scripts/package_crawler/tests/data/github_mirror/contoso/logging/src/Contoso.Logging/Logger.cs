namespace Contoso.Logging;

/// <summary>Logs structured events.</summary>
public class Logger
{
    public void Write(string message)
    {
        System.Console.WriteLine(message);
    }
}
