namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Common;

/// <summary>
/// Tests for the ResultExtensions class.
/// </summary>
public class ResultExtensionsTests
{
    /// <summary>
    /// Tests that Cancelled returns a failure result with cancellation message.
    /// </summary>
    [Fact]
    public void Cancelled_Returns_FailureResultWithCancellationMessage()
    {
        // Act
        var result = ResultExtensions.Cancelled();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ResultExtensions.OperationCancelled);
    }

    /// <summary>
    /// Tests that Cancelled&lt;T&gt; returns a failure result with cancellation message.
    /// </summary>
    [Fact]
    public void CancelledT_Returns_FailureResultWithCancellationMessage()
    {
        // Act
        var result = ResultExtensions.Cancelled<string>();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ResultExtensions.OperationCancelled);
    }

    /// <summary>
    /// Tests that IsCancelled returns true for cancelled results.
    /// </summary>
    [Fact]
    public void IsCancelled_WithCancelledResult_ReturnsTrue()
    {
        // Arrange
        var result = ResultExtensions.Cancelled();

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IsCancelled returns false for non-cancelled failure results.
    /// </summary>
    [Fact]
    public void IsCancelled_WithNonCancelledFailure_ReturnsFalse()
    {
        // Arrange
        var result = Result.WithFailure("Different error");

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that IsCancelled returns false for success results.
    /// </summary>
    [Fact]
    public void IsCancelled_WithSuccessResult_ReturnsFalse()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that IsCancelled&lt;T&gt; returns true for cancelled results.
    /// </summary>
    [Fact]
    public void IsCancelledT_WithCancelledResult_ReturnsTrue()
    {
        // Arrange
        var result = ResultExtensions.Cancelled<int>();

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IsCancelled&lt;T&gt; returns false for non-cancelled failure results.
    /// </summary>
    [Fact]
    public void IsCancelledT_WithNonCancelledFailure_ReturnsFalse()
    {
        // Arrange
        var result = Result<int>.WithFailure("Different error");

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that IsCancelled&lt;T&gt; returns false for success results.
    /// </summary>
    [Fact]
    public void IsCancelledT_WithSuccessResult_ReturnsFalse()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var isCancelled = result.IsCancelled();

        // Assert
        isCancelled.ShouldBeFalse();
    }
}

