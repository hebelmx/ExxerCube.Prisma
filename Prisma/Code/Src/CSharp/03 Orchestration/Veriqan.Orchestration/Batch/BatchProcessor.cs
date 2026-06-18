using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
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
/// <para>
/// Registered as <b>Singleton</b> because it owns no mutable per-request state — only
/// the scope factory is held across calls.
/// </para>
/// <para>
/// <b>Resume mode (<see cref="BatchOptions.Resume"/> = <see langword="true"/>):</b>
/// Before dispatching each item to the pipeline the processor queries
/// <see cref="IVerificationResultStore.IsCompletedAsync"/>.  Items whose content hash is
/// already completed are counted as <see cref="BatchReport.AlreadyCompletedCount"/> and
/// skipped — the pipeline is never called for them.  Newly-processed items are saved via
/// <see cref="IVerificationResultStore.SaveOutcomeAsync"/> (first-write-wins, idempotent).
/// Resuming a fully-completed batch therefore performs zero pipeline invocations.
/// </para>
/// </remarks>
internal sealed class BatchProcessor : IBatchProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVerificationResultStore _resultStore;
    private readonly ILogger<BatchProcessor> _logger;

    /// <summary>
    /// Initializes a new <see cref="BatchProcessor"/>.
    /// </summary>
    public BatchProcessor(
        IServiceScopeFactory scopeFactory,
        IVerificationResultStore resultStore,
        ILogger<BatchProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));
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
            "Batch starting: {Total} items, MaxDegreeOfParallelism={MaxParallelism}, Resume={Resume}",
            total,
            maxParallelism,
            options.Resume);

        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var outcomes = new ConcurrentBag<VerificationOutcome>();
        var exceptionQueue = new ConcurrentBag<ExceptionQueueEntry>();

        // Interlocked counters for thread-safe progress tracking
        int pending = total;
        int inProgress = 0;
        int completed = 0;
        int blocked = 0;
        int failed = 0;
        int alreadyCompleted = 0;

        void ReportProgress()
        {
            progress?.Report(new BatchProgress(
                Total: total,
                Pending: Volatile.Read(ref pending),
                InProgress: Volatile.Read(ref inProgress),
                Completed: Volatile.Read(ref completed),
                Failed: Volatile.Read(ref failed),
                Blocked: Volatile.Read(ref blocked),
                AlreadyCompleted: Volatile.Read(ref alreadyCompleted)));
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
                    // -----------------------------------------------------------
                    // Resume check: skip items already completed (FR-22)
                    // -----------------------------------------------------------
                    if (options.Resume)
                    {
                        var contentHash = ComputeSha256Hex(item.Pdf);
                        var isCompletedResult = await _resultStore
                            .IsCompletedAsync(contentHash, ct)
                            .ConfigureAwait(false);

                        if (isCompletedResult.IsCancelled())
                        {
                            // Propagate cancellation — don't count as failed
                            return;
                        }

                        if (isCompletedResult.IsSuccess && isCompletedResult.Value)
                        {
                            _logger.LogDebug(
                                "Resume: skipping {FileName} — content hash {ContentHash} already completed.",
                                item.FileName, contentHash);

                            Interlocked.Increment(ref alreadyCompleted);
                            ReportProgress();
                            return;
                        }
                    }

                    // -----------------------------------------------------------
                    // Normal pipeline processing
                    // -----------------------------------------------------------
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

                        // Persist the completed outcome when in resume mode
                        if (options.Resume)
                        {
                            var contentHash = ComputeSha256Hex(item.Pdf);
                            var saveResult = await _resultStore
                                .SaveOutcomeAsync(contentHash, outcome, ct)
                                .ConfigureAwait(false);

                            if (saveResult.IsFailure)
                            {
                                _logger.LogWarning(
                                    "Could not persist resume outcome for {FileName}: {Error}",
                                    item.FileName, saveResult.Error);
                            }
                        }
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
            RedCount: redCount,
            AlreadyCompletedCount: Volatile.Read(ref alreadyCompleted));

        _logger.LogInformation(
            "Batch complete: Total={Total} Completed={Completed} AlreadyCompleted={AlreadyCompleted} Green={Green} Red={Red} Blocked={Blocked} Failed={Failed}",
            report.TotalSubmitted,
            report.CompletedCount,
            report.AlreadyCompletedCount,
            report.GreenCount,
            report.RedCount,
            report.BlockedCount,
            report.FailedCount);

        return Result<BatchReport>.WithSuccess(report);
    }

    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="bytes"/> and returns it as a lowercase
    /// 64-character hex string. Mirrors <c>StatementIngestionService.ComputeSha256Hex</c>.
    /// </summary>
    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
