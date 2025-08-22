using System;
using System.Threading.Tasks;

namespace ExxerCube.Prisma.Domain.Common;

/// <summary>
/// Extension methods for Result&lt;T&gt; to enable fluent Railway Oriented Programming operations.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Binds a function to a result, continuing the railway if successful.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to bind.</param>
    /// <returns>A new result.</returns>
    public static async Task<Result<TResult>> Bind<T, TResult>(
        this Result<T> result,
        Func<T, Task<Result<TResult>>> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return await func(result.Value!);
    }

    /// <summary>
    /// Binds a synchronous function to a result, continuing the railway if successful.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to bind.</param>
    /// <returns>A new result.</returns>
    public static Result<TResult> Bind<T, TResult>(
        this Result<T> result,
        Func<T, Result<TResult>> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return func(result.Value!);
    }

    /// <summary>
    /// Maps a function over a successful result.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to map.</param>
    /// <returns>A new result.</returns>
    public static Result<TResult> Map<T, TResult>(
        this Result<T> result,
        Func<T, TResult> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return Result<TResult>.Success(func(result.Value!));
    }

    /// <summary>
    /// Maps an async function over a successful result.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The async function to map.</param>
    /// <returns>A new result.</returns>
    public static async Task<Result<TResult>> MapAsync<T, TResult>(
        this Result<T> result,
        Func<T, Task<TResult>> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        var mappedValue = await func(result.Value!);
        return Result<TResult>.Success(mappedValue);
    }

    /// <summary>
    /// Executes an action on a successful result, returning the same result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The same result.</returns>
    public static Result<T> Tap<T>(
        this Result<T> result,
        Action<T> action)
    {
        if (result.IsSuccess)
            action(result.Value!);

        return result;
    }

    /// <summary>
    /// Executes an async action on a successful result, returning the same result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>The same result.</returns>
    public static async Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value!);

        return result;
    }
}
