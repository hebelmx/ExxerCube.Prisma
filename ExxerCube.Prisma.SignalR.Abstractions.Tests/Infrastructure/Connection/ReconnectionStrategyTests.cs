namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Infrastructure.Connection;

/// <summary>
/// Tests for the ReconnectionStrategy class.
/// </summary>
public class ReconnectionStrategyTests
{
    /// <summary>
    /// Tests that CalculateDelay returns initial delay for first attempt.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithFirstAttempt_ReturnsInitialDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0
        };

        // Act
        var delay = strategy.CalculateDelay(0);

        // Assert
        delay.ShouldBe(1000);
    }

    /// <summary>
    /// Tests that CalculateDelay applies exponential backoff.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithMultipleAttempts_AppliesExponentialBackoff()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act & Assert
        strategy.CalculateDelay(0).ShouldBe(1000);   // 1000 * 2^0 = 1000
        strategy.CalculateDelay(1).ShouldBe(2000);   // 1000 * 2^1 = 2000
        strategy.CalculateDelay(2).ShouldBe(4000);   // 1000 * 2^2 = 4000
        strategy.CalculateDelay(3).ShouldBe(8000);   // 1000 * 2^3 = 8000
    }

    /// <summary>
    /// Tests that CalculateDelay respects MaxDelay limit.
    /// </summary>
    [Fact]
    public void CalculateDelay_ExceedingMaxDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 5000
        };

        // Act
        var delay = strategy.CalculateDelay(10); // Would be 1024000 without limit

        // Assert
        delay.ShouldBe(5000);
    }

    /// <summary>
    /// Tests that CalculateDelay handles custom backoff multiplier.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithCustomMultiplier_AppliesCorrectBackoff()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 500,
            BackoffMultiplier = 1.5,
            MaxDelay = 10000
        };

        // Act & Assert
        strategy.CalculateDelay(0).ShouldBe(500);     // 500 * 1.5^0 = 500
        strategy.CalculateDelay(1).ShouldBe(750);   // 500 * 1.5^1 = 750
        strategy.CalculateDelay(2).ShouldBe(1125);  // 500 * 1.5^2 = 1125 (rounded)
    }
}

