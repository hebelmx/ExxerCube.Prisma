using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Default <see cref="IBatchProcessor"/> implementation that processes statements with bounded
/// concurrency using a <see cref="Channel{T}"/>-based streaming consumer pool and resolves a fresh
/// <see cref="IVerificationPipeline"/> per item via <see cref="IServiceScopeFactory"/>
/// to avoid captive-dependency issues with scoped services.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>Singleton</b> because it owns no mutable per-request state — only
/// the scope factory is held across calls.
/// </para>
/// <para>
/// <b>Channel design:</b> an unbounded <see cref="System.Threading.Channels.Channel{T}"/> is used with a
/// fixed pool of <see cref="BatchProcessorOptions.EffectiveConcurrency"/> consumer <see cref="Task"/>s.
/// The caller's <see cref="IReadOnlyList{T}"/> is already fully in-memory when
/// <see cref="ProcessBatchAsync"/> is called, so bounding the channel provides no useful
/// backpressure.  What prevents heap-materialisation of N closures is that items are consumed
/// lazily — each consumer pulls the next submission from the channel only after it has
/// finished the current one, keeping at most <c>MaxConcurrency</c> closures alive at a time
/// (issue #58).
/// </para>
/// <para>
/// <b>Resume mode (<see cref="BatchOptions.Resume"/> = <see langword="true"/>):</b>
/// Before dispatching each item to the pipeline the processor queries
/// <see cref="IVerificationResultStore.IsCompletedAsync"/>.  Items whose content hash is
/// already completed are counted as <see cref="BatchReport.AlreadyCompletedCount"/> and
/// skipped — the pipeline is never called for them.  Newly-processed items are saved via
/// <see cref="IVerificationResultStore.SaveOutcomeAsync"/> (first-write-wins, idempotent).
/// </para>
/// </remarks>
internal sealed class BatchProcessor : IBatchProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVerificationResultStore _resultStore;
    private readonly VeriqanMetrics _metrics;
    private readonly ILogger<BatchProcessor> _logger;
    private readonly BatchProcessorOptions _processorOptions;

    /// <summary>
    /// Initializes a new <see cref="BatchProcessor"/>.
    /// </summary>
    public BatchProcessor(
        IServiceScopeFactory scopeFactory,
        IVerificationResultStore resultStore,
        VeriqanMetrics metrics,
        ILogger<BatchProcessor> logger,
        IOptions<BatchProcessorOptions> processorOptions)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processorOptions = (processorOptions ?? throw new ArgumentNullException(nameof(processorOptions))).Value;
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

        // Consumer count: an explicit per-call BatchOptions.MaxDegreeOfParallelism (a value
        // other than the default) takes precedence; otherwise the configured global
        // Veriqan:BatchProcessor:MaxConcurrency (default Environment.ProcessorCount) drives it.
        int maxConcurrency = options.MaxDegreeOfParallelism != BatchOptions.DefaultMaxDegreeOfParallelism
            ? options.EffectiveParallelism
            : _processorOptions.EffectiveConcurrency;

        _logger.LogInformation(
            "Batch starting: {Total} items, MaxConcurrency={MaxConcurrency}, Resume={Resume}",
            total,
            maxConcurrency,
            options.Resume);

        // Wall-clock for throughput calculation (NFR-4).
        var batchSw = Stopwatch.StartNew();

        var outcomes = new ConcurrentBag<VerificationOutcome>();
        var exceptionQueue = new ConcurrentBag<ExceptionQueueEntry>();

        // Interlocked counters for thread-safe progress tracking.
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

        // ── Channel setup ──────────────────────────────────────────────────
        // Unbounded channel: the producer writes all submissions immediately;
        // MaxConcurrency consumers read lazily, keeping at most MaxConcurrency
        // closures alive at any time (fixes issue #58 heap-materialisation).
        var channel = Channel.CreateUnbounded<StatementSubmission>(
            new UnboundedChannelOptions
            {
                SingleWriter = true,       // only the producer loop below writes
                SingleReader = false,      // multiple consumer workers read
                AllowSynchronousContinuations = false
            });

        // Producer: write all submissions to the channel, then signal completion.
        async Task ProduceAsync()
        {
            foreach (var item in batch)
            {
                if (ct.IsCancellationRequested)
                    break;

                await channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
            }

            channel.Writer.TryComplete();
        }

        // Consumer: drain the channel until it is completed or cancelled.
        async Task ConsumeAsync()
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
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
                            continue;
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

                        // Story 4.2: the live-progress "blocked" counter intentionally groups both
                        // VerdictSignal.Blocked and VerdictSignal.ExtractionGap for coarse progress
                        // reporting.  TransientFailure is not counted here (no current emitter).
                        // The final BatchReport splits all six signals into individual counts —
                        // use BatchReport for per-signal breakdowns, not this counter.
                        if (outcome.Summary.Signal is VerdictSignal.Blocked or VerdictSignal.ExtractionGap)
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
                        _metrics.RecordException();

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
                    _metrics.RecordException();

                    _logger.LogError(
                        ex,
                        "Unexpected exception processing {FileName}",
                        item.FileName);
                }
                finally
                {
                    Interlocked.Decrement(ref inProgress);
                    ReportProgress();
                }
            }
        }

        // Launch producer + MaxConcurrency consumer tasks concurrently.
        var producerTask = ProduceAsync();
        var consumerTasks = new Task[maxConcurrency];
        for (int i = 0; i < maxConcurrency; i++)
            consumerTasks[i] = ConsumeAsync();

        // Await producer first (it completes the channel), then all consumers.
        await producerTask.ConfigureAwait(false);
        await Task.WhenAll(consumerTasks).ConfigureAwait(false);

        var outcomeList = new List<VerificationOutcome>(outcomes);
        var exceptionList = new List<ExceptionQueueEntry>(exceptionQueue);

        int greenCount = 0;
        int yellowCount = 0;
        int redCount = 0;
        int blockedCount = 0;
        int extractionGapCount = 0;
        int transientFailureCount = 0;

        foreach (var o in outcomeList)
        {
            switch (o.Summary.Signal)
            {
                case VerdictSignal.Green:
                    greenCount++;
                    break;
                case VerdictSignal.Yellow:
                    yellowCount++;
                    break;
                case VerdictSignal.Red:
                    redCount++;
                    break;
                case VerdictSignal.Blocked:
                    // Reserved — no emitter after Story 4.2; kept for future document-defect detection.
                    blockedCount++;
                    break;
                case VerdictSignal.ExtractionGap:
                    // Story 4.2: permanent system/capability gap — persisted, non-verdict, non-compliance.
                    extractionGapCount++;
                    break;
                case VerdictSignal.TransientFailure:
                    // Story 4.2: retryable operational failure — non-verdict, non-compliance.
                    // Currently no emitter; counted here as a final-report category if ever emitted.
                    transientFailureCount++;
                    break;
            }
        }

        batchSw.Stop();
        double elapsedSeconds = batchSw.Elapsed.TotalSeconds;

        // Throughput: completed items / elapsed wall-clock seconds.
        double throughput = (elapsedSeconds > 0 && outcomeList.Count > 0)
            ? outcomeList.Count / elapsedSeconds
            : 0.0;

        // P95 latency: derived from per-outcome ProcessingDuration (non-zero values only).
        double? p95Ms = ComputeP95Ms(outcomeList);

        var report = new BatchReport(
            Outcomes: outcomeList,
            ExceptionQueue: exceptionList,
            TotalSubmitted: total,
            CompletedCount: outcomeList.Count,
            BlockedCount: blockedCount,
            ExtractionGapCount: extractionGapCount,
            TransientFailureCount: transientFailureCount,
            FailedCount: exceptionList.Count,
            GreenCount: greenCount,
            YellowCount: yellowCount,
            RedCount: redCount,
            AlreadyCompletedCount: Volatile.Read(ref alreadyCompleted),
            ThroughputPerSecond: throughput,
            P95LatencyMs: p95Ms);

        _logger.LogInformation(
            "Batch complete: Total={Total} Completed={Completed} AlreadyCompleted={AlreadyCompleted} Green={Green} Yellow={Yellow} Red={Red} ExtractionGap={ExtractionGap} Blocked={Blocked} TransientFailure={TransientFailure} Failed={Failed} ThroughputPerSecond={ThroughputPerSecond:F2} P95LatencyMs={P95LatencyMs}",
            report.TotalSubmitted,
            report.CompletedCount,
            report.AlreadyCompletedCount,
            report.GreenCount,
            report.YellowCount,
            report.RedCount,
            report.ExtractionGapCount,
            report.BlockedCount,
            report.TransientFailureCount,
            report.FailedCount,
            report.ThroughputPerSecond,
            report.P95LatencyMs);

        return Result<BatchReport>.WithSuccess(report);
    }

    /// <summary>
    /// Computes the 95th-percentile latency in milliseconds from the
    /// <see cref="VerificationOutcome.ProcessingDuration"/> values in <paramref name="outcomes"/>.
    /// Returns <see langword="null"/> when no outcome carries a measured duration.
    /// </summary>
    private static double? ComputeP95Ms(IReadOnlyList<VerificationOutcome> outcomes)
    {
        var durations = outcomes
            .Select(o => o.ProcessingDuration.TotalMilliseconds)
            .Where(ms => ms > 0)
            .OrderBy(ms => ms)
            .ToList();

        if (durations.Count == 0)
            return null;

        // Nearest-rank method: index = ceil(p * n) - 1  (0-based)
        int rank = (int)Math.Ceiling(0.95 * durations.Count) - 1;
        rank = Math.Max(0, Math.Min(rank, durations.Count - 1));
        return durations[rank];
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
