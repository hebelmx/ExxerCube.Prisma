namespace Siara.Simulator.Services;

/// <summary>
/// Provides methods for calculating random time intervals based on statistical distributions.
/// </summary>
public static class DistributionService
{
    private static readonly Random _random = new();

    /// <summary>
    /// Calculates a random delay based on a Poisson process.
    /// The time between events in a Poisson process follows an exponential distribution.
    /// </summary>
    /// <param name="averageArrivalsPerMinute">The average number of events (cases) that should occur per minute.</param>
    /// <returns>A TimeSpan representing the random delay until the next event.</returns>
    public static TimeSpan GetNextPoissonDelay(double averageArrivalsPerMinute)
    {
        if (averageArrivalsPerMinute <= 0)
        {
            // Return a very large timespan to effectively stop new arrivals if rate is zero or negative.
            return TimeSpan.MaxValue;
        }

        // Lambda is the rate parameter, i.e., the average number of events per second.
        double lambda = averageArrivalsPerMinute / 60.0;

        // Generate a uniform random number between 0.0 and 1.0.
        double u = _random.NextDouble();

        // Calculate the time until the next event using the inverse transform method for the exponential distribution.
        // t = -ln(1-U) / lambda
        double delayInSeconds = -Math.Log(1.0 - u) / lambda;

        return TimeSpan.FromSeconds(delayInSeconds);
    }
}
