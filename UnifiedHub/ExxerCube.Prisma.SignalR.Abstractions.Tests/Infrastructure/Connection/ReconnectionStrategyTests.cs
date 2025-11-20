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

    /// <summary>
    /// Tests that CalculateDelay returns InitialDelay when attempt is zero.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithAttemptZero_ReturnsInitialDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 2000,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act
        var delay = strategy.CalculateDelay(0);

        // Assert
        delay.ShouldBe(2000);
    }

    /// <summary>
    /// Tests that CalculateDelay caps at MaxDelay when calculated value exceeds MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithLargeAttempt_CapsAtMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 5000
        };

        // Act
        var delay = strategy.CalculateDelay(100); // Very large attempt

        // Assert
        delay.ShouldBe(5000);
        delay.ShouldBeLessThanOrEqualTo(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay returns MaxDelay when calculated equals MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenCalculatedEqualsMaxDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 8000
        };

        // Act - attempt 3: 1000 * 2^3 = 8000 (exactly MaxDelay)
        var delay = strategy.CalculateDelay(3);

        // Assert
        delay.ShouldBe(8000);
        delay.ShouldBe(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay applies exponential backoff correctly with relationship verification.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithExponentialGrowth_VerifiesRelationship()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 100,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act & Assert - Verify exponential growth
        var delay0 = strategy.CalculateDelay(0);
        var delay1 = strategy.CalculateDelay(1);
        var delay2 = strategy.CalculateDelay(2);
        var delay3 = strategy.CalculateDelay(3);
        var delay4 = strategy.CalculateDelay(4);

        delay0.ShouldBe(100);   // 100 * 2^0 = 100
        delay1.ShouldBe(200);   // 100 * 2^1 = 200
        delay2.ShouldBe(400);   // 100 * 2^2 = 400
        delay3.ShouldBe(800);   // 100 * 2^3 = 800
        delay4.ShouldBe(1600);  // 100 * 2^4 = 1600

        // Verify exponential relationship
        delay1.ShouldBe(delay0 * 2);
        delay2.ShouldBe(delay1 * 2);
        delay3.ShouldBe(delay2 * 2);
        delay4.ShouldBe(delay3 * 2);
    }

    /// <summary>
    /// Tests that CalculateDelay handles edge case when MaxDelay equals InitialDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenMaxDelayEqualsInitialDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 1000
        };

        // Act
        var delay0 = strategy.CalculateDelay(0);
        var delay1 = strategy.CalculateDelay(1);

        // Assert
        delay0.ShouldBe(1000);
        delay1.ShouldBe(1000); // Capped at MaxDelay
    }

    /// <summary>
    /// Tests that CalculateDelay handles very small InitialDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithVerySmallInitialDelay_AppliesCorrectly()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1,
            BackoffMultiplier = 2.0,
            MaxDelay = 1000
        };

        // Act & Assert
        strategy.CalculateDelay(0).ShouldBe(1);
        strategy.CalculateDelay(1).ShouldBe(2);
        strategy.CalculateDelay(2).ShouldBe(4);
        strategy.CalculateDelay(3).ShouldBe(8);
        strategy.CalculateDelay(10).ShouldBe(1000); // Capped at MaxDelay (1000)
    }

    /// <summary>
    /// Tests that CalculateDelay handles fractional backoff multiplier correctly.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithFractionalMultiplier_AppliesCorrectly()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 1.5,
            MaxDelay = 10000
        };

        // Act & Assert
        strategy.CalculateDelay(0).ShouldBe(1000);   // 1000 * 1.5^0 = 1000
        strategy.CalculateDelay(1).ShouldBe(1500);   // 1000 * 1.5^1 = 1500
        strategy.CalculateDelay(2).ShouldBe(2250);   // 1000 * 1.5^2 = 2250
        strategy.CalculateDelay(3).ShouldBe(3375);   // 1000 * 1.5^3 = 3375
    }

    /// <summary>
    /// Tests that CalculateDelay returns calculated value when less than MaxDelay.
    /// This tests the branch: delay < MaxDelay (returns calculated value, not MaxDelay).
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenCalculatedLessThanMaxDelay_ReturnsCalculatedValue()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act - attempt 2: 1000 * 2^2 = 4000 (less than MaxDelay of 10000)
        var delay = strategy.CalculateDelay(2);

        // Assert - Should return calculated value, not MaxDelay
        delay.ShouldBe(4000);
        delay.ShouldBeLessThan(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay returns MaxDelay when calculated exceeds MaxDelay.
    /// This tests the branch: delay >= MaxDelay (returns MaxDelay).
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenCalculatedExceedsMaxDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 5000
        };

        // Act - attempt 3: 1000 * 2^3 = 8000 (exceeds MaxDelay of 5000)
        var delay = strategy.CalculateDelay(3);

        // Assert - Should return MaxDelay, not calculated value
        delay.ShouldBe(5000);
        delay.ShouldBe(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay handles boundary when calculated exactly equals MaxDelay.
    /// This tests the boundary condition: delay == MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenCalculatedExactlyEqualsMaxDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 4000
        };

        // Act - attempt 2: 1000 * 2^2 = 4000 (exactly equals MaxDelay)
        var delay = strategy.CalculateDelay(2);

        // Assert - Should return MaxDelay
        delay.ShouldBe(4000);
        delay.ShouldBe(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay handles negative attempt numbers gracefully.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithNegativeAttempt_HandlesGracefully()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act - Negative attempt should result in very small delay
        var delay = strategy.CalculateDelay(-1);

        // Assert - Should return a value (implementation dependent, but should not throw)
        delay.ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Tests that CalculateDelay handles zero backoff multiplier.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithZeroBackoffMultiplier_ReturnsZero()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 0.0,
            MaxDelay = 10000
        };

        // Act
        var delay = strategy.CalculateDelay(1);

        // Assert - 1000 * 0^1 = 0, capped at MaxDelay
        delay.ShouldBe(0);
    }

    /// <summary>
    /// Tests that CalculateDelay handles very large attempt numbers.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithVeryLargeAttempt_CapsAtMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 5000
        };

        // Act - Very large attempt
        var delay = strategy.CalculateDelay(int.MaxValue);

        // Assert - Should cap at MaxDelay
        delay.ShouldBe(strategy.MaxDelay);
        delay.ShouldBeLessThanOrEqualTo(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay handles backoff multiplier of 1.0 (no exponential growth).
    /// </summary>
    [Fact]
    public void CalculateDelay_WithMultiplierOne_ReturnsConstantDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 1.0,
            MaxDelay = 10000
        };

        // Act & Assert - All attempts should return same delay
        strategy.CalculateDelay(0).ShouldBe(1000);
        strategy.CalculateDelay(1).ShouldBe(1000);
        strategy.CalculateDelay(2).ShouldBe(1000);
        strategy.CalculateDelay(10).ShouldBe(1000);
    }

    /// <summary>
    /// Tests that CalculateDelay handles very small backoff multiplier (less than 1.0).
    /// </summary>
    [Fact]
    public void CalculateDelay_WithSmallMultiplier_DecreasesDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 0.5,
            MaxDelay = 10000
        };

        // Act & Assert - Delay should decrease with each attempt
        var delay0 = strategy.CalculateDelay(0);
        var delay1 = strategy.CalculateDelay(1);
        var delay2 = strategy.CalculateDelay(2);

        delay0.ShouldBe(1000);   // 1000 * 0.5^0 = 1000
        delay1.ShouldBe(500);    // 1000 * 0.5^1 = 500
        delay2.ShouldBe(250);    // 1000 * 0.5^2 = 250
    }

    /// <summary>
    /// Tests that CalculateDelay handles zero InitialDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithZeroInitialDelay_ReturnsZero()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 0,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act & Assert - Should return 0 for all attempts
        strategy.CalculateDelay(0).ShouldBe(0);
        strategy.CalculateDelay(1).ShouldBe(0);
        strategy.CalculateDelay(10).ShouldBe(0);
    }

    /// <summary>
    /// Tests that CalculateDelay handles MaxDelay of zero.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithZeroMaxDelay_ReturnsZero()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 0
        };

        // Act & Assert - Should return 0 (capped at MaxDelay)
        strategy.CalculateDelay(0).ShouldBe(0);
        strategy.CalculateDelay(1).ShouldBe(0);
        strategy.CalculateDelay(10).ShouldBe(0);
    }

    /// <summary>
    /// Tests that CalculateDelay uses Math.Min correctly when delay exceeds MaxDelay.
    /// This tests the mutation: Math.Min(delay, MaxDelay) - ensures Min is used, not Max.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenDelayExceedsMaxDelay_UsesMin()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 5000
        };

        // Act - attempt 5: 1000 * 2^5 = 32000 (exceeds MaxDelay of 5000)
        var delay = strategy.CalculateDelay(5);

        // Assert - Should return Min(32000, 5000) = 5000, not Max
        delay.ShouldBe(5000);
        delay.ShouldBeLessThan(32000); // Verify Min was used, not Max
    }

    /// <summary>
    /// Tests that CalculateDelay uses Math.Min correctly when delay equals MaxDelay.
    /// This tests the boundary: Math.Min(delay, MaxDelay) when delay == MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenDelayEqualsMaxDelay_ReturnsMaxDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 4000
        };

        // Act - attempt 2: 1000 * 2^2 = 4000 (exactly equals MaxDelay)
        var delay = strategy.CalculateDelay(2);

        // Assert - Should return Min(4000, 4000) = 4000
        delay.ShouldBe(4000);
        delay.ShouldBe(strategy.MaxDelay);
    }

    /// <summary>
    /// Tests that CalculateDelay uses Math.Min correctly when delay is less than MaxDelay.
    /// This tests the boundary: Math.Min(delay, MaxDelay) when delay < MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_WhenDelayLessThanMaxDelay_ReturnsDelay()
    {
        // Arrange
        var strategy = new ReconnectionStrategy
        {
            InitialDelay = 1000,
            BackoffMultiplier = 2.0,
            MaxDelay = 10000
        };

        // Act - attempt 2: 1000 * 2^2 = 4000 (less than MaxDelay of 10000)
        var delay = strategy.CalculateDelay(2);

        // Assert - Should return Min(4000, 10000) = 4000 (the delay, not MaxDelay)
        delay.ShouldBe(4000);
        delay.ShouldBeLessThan(strategy.MaxDelay);
    }
}

