using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Default <see cref="IBatchProcessor"/> implementation that processes statements with bounded
/// concurrency using a <see cref="SemaphoreSlim"/> and resolves a fresh
/// <see cref="IVerificationPipeline"/> per item via <see cref="IServiceScopeFactory"/>
/// to avoid captive-dependency issues with scoped services.
/// </summary>
/// <remarks>
/// Registered as <b>Singleton</b> because it owns no mutable per-request state — only
/// the scope factory is held across calls.
/// </remarks>
internal sealed class BatchProcessor : IBatchProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchProcessor> _logger;

    /// <summary>
    /// Initializes a new <see cref="BatchProcessor"/>.
    /// </summary>
    public BatchProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<BatchProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<BatchReport>> ProcessBatchAsync(
        IReadOnlyList<StatementSubmission> batch,
        BatchOptions options,
        IProgress<BatchProgress>? progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(options);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<BatchReport>();

        int total = batch.Count;
        int maxParallelism = options.EffectiveParallelism;

        _logger.LogInformation(
            "Batch starting: {Total} items, MaxDegreeOfParallelism={MaxParallelism}",
            total,
            maxParallelism);

        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var outcomes = new ConcurrentBag<VerificationOutcome>();
        var exceptionQueue = new ConcurrentBag<ExceptionQueueEntry>();

        // Interlocked counters for thread-safe progress tracking
        int pending = total;
        int inProgress = 0;
        int completed = 0;
        int blocked = 0;
        int failed = 0;

        void ReportProgress()
        {
            progress?.Report(new BatchProgress(
                Total: total,
                Pending: Volatile.Read(ref pending),
                InProgress: Volatile.Read(ref inProgress),
                Completed: Volatile.Read(ref completed),
                Failed: Volatile.Read(ref failed),
                Blocked: Volatile.Read(ref blocked)));
        }

        var tasks = new List<Task>(total);

        foreach (var submission in batch)
        {
            if (ct.IsCancellationRequested)
                break;

            // Capture loop variable for the closure
            var item = submission;

            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);

                Interlocked.Decrement(ref pending);
                Interlocked.Increment(ref inProgress);
                ReportProgress();

                try
                {
                    Result<VerificationOutcome> result;

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

                    result = await pipeline.ProcessAsync(item, ct).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        var outcome = result.Value!;
                        outcomes.Add(outcome);

                        Interlocked.Increment(ref completed);

                        if (outcome.Summary.Signal == VerdictSignal.Blocked)
                            Interlocked.Increment(ref blocked);
                    }
                    else
                    {
                        // Pipeline returned a failure Result (not an exception)
                        exceptionQueue.Add(new ExceptionQueueEntry(
                            Submission: item,
                            ErrorMessage: result.Error ?? "Pipeline returned failure",
                            IsException: false));

                        Interlocked.Increment(ref failed);

                        _logger.LogWarning(
                            "Pipeline failure queued for {FileName}: {Error}",
                            item.FileName,
                            result.Error);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Unexpected exception — isolate to the exception queue
                    exceptionQueue.Add(new ExceptionQueueEntry(
                        Submission: item,
                        ErrorMessage: ex.Message,
                        IsException: true));

                    Interlocked.Increment(ref failed);

                    _logger.LogError(
                        ex,
                        "Unexpected exception processing {FileName}",
                        item.FileName);
                }
                finally
                {
                    Interlocked.Decrement(ref inProgress);
                    semaphore.Release();
                    ReportProgress();
                }
            }, ct);

            tasks.Add(task);
        }

        // Await all tasks; individual failures are already captured in the queues above
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var outcomeList = new List<VerificationOutcome>(outcomes);
        var exceptionList = new List<ExceptionQueueEntry>(exceptionQueue);

        int greenCount = 0;
        int redCount = 0;
        int blockedCount = 0;

        foreach (var o in outcomeList)
        {
            switch (o.Summary.Signal)
            {
                case VerdictSignal.Green:
                    greenCount++;
                    break;
                case VerdictSignal.Red:
                    redCount++;
                    break;
                case VerdictSignal.Blocked:
                    blockedCount++;
                    break;
            }
        }

        var report = new BatchReport(
            Outcomes: outcomeList,
            ExceptionQueue: exceptionList,
            TotalSubmitted: total,
            CompletedCount: outcomeList.Count,
            BlockedCount: blockedCount,
            FailedCount: exceptionList.Count,
            GreenCount: greenCount,
            RedCount: redCount);

        _logger.LogInformation(
            "Batch complete: Total={Total} Completed={Completed} Green={Green} Red={Red} Blocked={Blocked} Failed={Failed}",
            report.TotalSubmitted,
            report.CompletedCount,
            report.GreenCount,
            report.RedCount,
            report.BlockedCount,
            report.FailedCount);

        return Result<BatchReport>.WithSuccess(report);
    }
}
