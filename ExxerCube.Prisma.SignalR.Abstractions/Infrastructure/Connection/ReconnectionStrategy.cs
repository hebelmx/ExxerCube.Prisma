namespace ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;

/// <summary>
/// Configuration for reconnection strategy.
/// </summary>
public class ReconnectionStrategy
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial delay in milliseconds before the first retry.
    /// </summary>
    public int InitialDelay { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum delay in milliseconds between retries.
    /// </summary>
    public int MaxDelay { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Calculates the delay for a specific retry attempt.
    /// </summary>
    /// <param name="attempt">The retry attempt number (0-based).</param>
    /// <returns>The delay in milliseconds.</returns>
    public int CalculateDelay(int attempt)
    {
        var delay = (int)(InitialDelay * Math.Pow(BackoffMultiplier, attempt));
        return Math.Min(delay, MaxDelay);
    }
}

