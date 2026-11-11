namespace ExxerCube.Prisma.Tests.Domain.Common;

/// <summary>
/// Tests for the Result class and Railway Oriented Programming pattern.
/// </summary>
public class ResultTests
{
    /// <summary>
    /// Tests the Success method of Resul to ensure it returns a successful result with the correct value.
    /// </summary>
    [Fact]
    public void Success_WithValue_ReturnsSuccessfulResult()
    {
        // Arrange
        var expectedValue = "test value";

        // Act
        var result = Result<string>.Success(expectedValue);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedValue);
        result.Error.ShouldBeNullOrEmpty();
    }

    /// <summary>
    /// Tests the Failure method of Result to ensure it returns a failure result with the correct error message.
    /// </summary>
    [Fact]
    public void Failure_WithError_ReturnsFailureResult()
    {
        // Arrange
        var expectedError = "test error";

        // Act
        var result = Result<string>.WithFailure(expectedError);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldBe(expectedError);
    }

    /// <summary>
    /// Tests the implicit conversion from a value to a successful Result.
    /// </summary>
    [Fact]
    public void ImplicitConversion_FromValue_ReturnsSuccessfulResult()
    {
        // Arrange
        var expectedValue = 42;

        // Act
        Result<int> result = expectedValue;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedValue);
        result.Error.ShouldBeNullOrEmpty();
    }

    /// <summary>
    /// Tests the Bind method to ensure it propagates failure results without executing the function.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Bind_WithFailureResult_ReturnsFailure()
    {
        // Arrange
        var initialResult = Result<int>.WithFailure("initial error");

        // Act - Use ResultAsync.BindAsync for async chaining
        var result = await ResultAsync.BindAsync(
            Task.FromResult(initialResult),
            value => Task.FromResult(Result<string>.Success(value.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("initial error");
    }

    /// <summary>
    /// Tests the Map method to ensure it transforms the value of a successful result.
    /// </summary>
    [Fact]
    public void Map_WithSuccessfulResult_TransformsValue()
    {
        // Arrange
        var initialResult = Result<int>.Success(5);
        var expectedValue = "5";

        // Act
        var result = initialResult.Map(value => value.ToString());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedValue);
    }

    /// <summary>
    /// Tests the Map method to ensure it propagates failure results without transforming the value.
    /// </summary>
    [Fact]
    public void Map_WithFailureResult_ReturnsFailure()
    {
        // Arrange
        var initialResult = Result<int>.WithFailure("initial error");

        // Act
        var result = initialResult.Map(value => value.ToString());

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("initial error");
    }

    /// <summary>
    /// Tests the MapAsync method to ensure it transforms the value of a successful result asynchronously.
    /// </summary>
    /// <returns></returns>

    [Fact]
    public async Task MapAsync_WithSuccessfulResult_TransformsValue()
    {
        // Arrange
        var initialResult = Result<int>.Success(5);
        var expectedValue = "5";

        // Act - Use ResultAsync.MapAsync for async mapping
        var result = await ResultAsync.MapAsync(
            Task.FromResult(initialResult),
            value => Task.FromResult(value.ToString()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedValue);
    }

    /// <summary>
    /// Tests the MapAsync method to ensure it propagates failure results without transforming the value.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task MapAsync_WithFailureResult_ReturnsFailure()
    {
        // Arrange
        var initialResult = Result<int>.WithFailure("initial error");

        // Act - Use ResultAsync.MapAsync for async mapping
        var result = await ResultAsync.MapAsync(
            Task.FromResult(initialResult),
            value => Task.FromResult(value.ToString()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("initial error");
    }

    /// <summary>
    /// Tests the Tap method to ensure it executes an action on a successful result without altering the result.
    /// </summary>
    [Fact]
    public void Tap_WithSuccessfulResult_ExecutesActionAndReturnsSameResult()
    {
        // Arrange
        var initialResult = Result<int>.Success(5);
        var actionExecuted = false;

        // Act
        var result = initialResult.Tap(value => actionExecuted = true);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
        actionExecuted.ShouldBeTrue();
    }

    /// <summary>
    /// Tests the Tap method to ensure it does not execute an action on a failure result and returns the same failure result.
    /// </summary>
    [Fact]
    public void Tap_WithFailureResult_DoesNotExecuteActionAndReturnsSameResult()
    {
        // Arrange
        var initialResult = Result<int>.WithFailure("error");
        var actionExecuted = false;

        // Act
        var result = initialResult.Tap(value => actionExecuted = true);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("error");
        actionExecuted.ShouldBeFalse();
    }

    /// <summary>
    /// Tests the TapAsync method to ensure it executes an asynchronous action on a successful result without altering the result.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task TapAsync_WithSuccessfulResult_ExecutesActionAndReturnsSameResult()
    {
        // Arrange
        var initialResult = Result<int>.Success(5);
        var actionExecuted = false;

        // Act - Use ResultAsync.TapAsync for async side effects
        var result = await ResultAsync.TapAsync(
            Task.FromResult(initialResult),
            async value =>
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
                actionExecuted = true;
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
        actionExecuted.ShouldBeTrue();
    }

    /// <summary>
    /// Tests the TapAsync method to ensure it does not execute an asynchronous action on a failure result and returns the same failure result.
    /// </summary>
    /// <returns></returns>

    [Fact]
    public async Task TapAsync_WithFailureResult_DoesNotExecuteActionAndReturnsSameResult()
    {
        // Arrange
        var initialResult = Result<int>.WithFailure("error");
        var actionExecuted = false;

        // Act - Use ResultAsync.TapAsync for async side effects
        var result = await ResultAsync.TapAsync(
            Task.FromResult(initialResult),
            async value =>
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
                actionExecuted = true;
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("error");
        actionExecuted.ShouldBeFalse();
    }
}

