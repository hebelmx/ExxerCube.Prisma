using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Processes a collection of <see cref="StatementSubmission"/> items concurrently with bounded
/// parallelism, isolating individual failures on an exception queue so the batch always completes.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must:
/// <list type="bullet">
///   <item>Respect <see cref="BatchOptions.MaxDegreeOfParallelism"/> via a semaphore.</item>
///   <item>Resolve a fresh <see cref="IVerificationPipeline"/> per item from a new DI scope
///         to avoid captive-dependency issues with scoped services (e.g. <c>DbContext</c>).</item>
///   <item>Add items that throw exceptions or return failure <c>Result</c>s to the exception
///         queue without aborting the rest of the batch.</item>
///   <item>Report progress via the <c>progress</c> callback after each item completes.</item>
/// </list>
/// </para>
/// </remarks>
public interface IBatchProcessor
{
    /// <summary>
    /// Processes all <paramref name="batch"/> items with bounded concurrency.
    /// </summary>
    /// <param name="batch">The ordered list of statements to verify.</param>
    /// <param name="options">Concurrency and scheduling options for this run.</param>
    /// <param name="progress">
    /// Optional progress sink; receives a <see cref="BatchProgress"/> snapshot after each
    /// item completes. May be <see langword="null"/> when callers do not need progress updates.
    /// </param>
    /// <param name="ct">
    /// Cancellation token. A cancellation signal before the batch starts returns a cancelled
    /// result immediately. Items already in-flight complete normally.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the <see cref="BatchReport"/> once
    /// all items have been processed or queued; a cancelled result when <paramref name="ct"/>
    /// is signalled before the batch begins.
    /// </returns>
    Task<Result<BatchReport>> ProcessBatchAsync(
        IReadOnlyList<StatementSubmission> batch,
        BatchOptions options,
        IProgress<BatchProgress>? progress,
        CancellationToken ct = default);
}
