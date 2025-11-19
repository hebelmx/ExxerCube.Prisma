namespace ExxerCube.Prisma.SignalR.Abstractions.Common;

/// <summary>
/// Extension methods for Result types to support cancellation semantics.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Standard error message for cancelled operations.
    /// </summary>
    public const string OperationCancelled = "Operation was cancelled by the user.";

    /// <summary>
    /// Creates a cancelled result.
    /// </summary>
    /// <returns>A cancelled result.</returns>
    public static Result Cancelled() =>
        Result.WithFailure(OperationCancelled);

    /// <summary>
    /// Creates a cancelled result with a value type.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <returns>A cancelled result.</returns>
    public static Result<T> Cancelled<T>() =>
        Result<T>.WithFailure(OperationCancelled);

    /// <summary>
    /// Determines if a result represents a cancelled operation.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns>True if the result represents cancellation; otherwise, false.</returns>
    public static bool IsCancelled(this Result result) =>
        result.IsFailure && result.Error == OperationCancelled;

    /// <summary>
    /// Determines if a result represents a cancelled operation.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>True if the result represents cancellation; otherwise, false.</returns>
    public static bool IsCancelled<T>(this Result<T> result) =>
        result.IsFailure && result.Error == OperationCancelled;
}

