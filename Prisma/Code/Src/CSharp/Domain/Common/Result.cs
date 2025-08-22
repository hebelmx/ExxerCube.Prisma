namespace ExxerCube.Prisma.Domain.Common;

/// <summary>
/// Represents a result that can be either a success or failure.
/// Implements Railway Oriented Programming pattern for error handling.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the success value if the result is successful.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message if the result is a failure.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <param name="isSuccess">Whether the result is successful.</param>
    /// <param name="error">The error message.</param>
    private Result(T? value, bool isSuccess, string? error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) => new(value, true, null);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failure result.</returns>
    public static Result<T> Failure(string error) => new(default, false, error);

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Result<T>(T value) => Success(value);
}
