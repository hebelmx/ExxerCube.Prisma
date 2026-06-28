using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.InMemory;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="BatchProcessor"/> covering concurrency bounds, exception isolation,
/// progress reporting, and cancellation.
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class BatchProcessorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StatementSubmission MakeSubmission(string name = "test.pdf") =>
        new(Pdf: [0x25, 0x50, 0x44, 0x46], FileName: name,
            ContextKey: new ExxerCube.Prisma.Veriqan.Application.Ports.StatementContextKey("Bank A"));

    private static VerificationOutcome MakeGreenOutcome()
    {
        var job = new VerificationJob(
            Guid.NewGuid(), "abc123", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        var summary = aggregator.Aggregate(Array.Empty<RuleFinding>()).Value!;
        return new VerificationOutcome(job, summary, Array.Empty<RuleFinding>());
    }

    private static VerificationOutcome MakeYellowOutcome()
    {
        var job = new VerificationJob(
            Guid.NewGuid(), "abc-yellow", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        // Bank-only tier map: the fail is mapped to ChecklistTier.Bank, so condusefFails is empty
        // → bankTier=Yellow, condusefTier=Green, overall=Yellow.
        var tiers = new Dictionary<string, ChecklistTier> { ["CL-BANK-ONLY"] = ChecklistTier.Bank };
        var findings = new List<RuleFinding>
        {
            RuleFinding.Fail("CL-BANK-ONLY", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0", "e", "o"),
        };
        var summary = aggregator.Aggregate(findings, checklistTiers: tiers).Value!;
        return new VerificationOutcome(job, summary, findings);
    }

    private static VerificationOutcome MakeExtractionGapOutcome()
    {
        var job = new VerificationJob(
            Guid.NewGuid(), "abc-gap", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        // InsufficientTextLayer → ExtractionGap (Story 4.2)
        var blocked = new BlockedOutcome(
            BlockReason.InsufficientTextLayer, "zero words in text layer");
        var summary = aggregator.Aggregate(Array.Empty<RuleFinding>(), blocked).Value!;
        return new VerificationOutcome(job, summary, Array.Empty<RuleFinding>());
    }

    private static IBatchProcessor BuildBatchProcessor(IVerificationPipeline pipeline)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<BatchProcessorOptions>(_ => { });
        services.AddScoped<IVerificationPipeline>(_ => pipeline);
        // IVerificationResultStore is required by BatchProcessor (used when Resume=true)
        services.AddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.AddSingleton<VeriqanMetrics>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();
        return services.BuildServiceProvider().GetRequiredService<IBatchProcessor>();
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessBatch_AllSucceed_AllCompleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithSuccess(greenOutcome));

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("a.pdf"),
            MakeSubmission("b.pdf"),
            MakeSubmission("c.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.CompletedCount.ShouldBe(3);
        report.FailedCount.ShouldBe(0);
        report.GreenCount.ShouldBe(3);
        report.ExceptionQueue.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessBatch_OneItemThrows_IsolatedToExceptionQueue_BatchContinues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        // Serialize execution so call order is deterministic
        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 2) throw new InvalidOperationException("boom");
                return Task.FromResult(Result<VerificationOutcome>.WithSuccess(greenOutcome));
            });

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("first.pdf"),
            MakeSubmission("middle.pdf"),
            MakeSubmission("last.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act — MaxDegreeOfParallelism=1 gives deterministic call ordering
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: 1), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.ExceptionQueue.Count.ShouldBe(1);
        report.ExceptionQueue[0].IsException.ShouldBeTrue();
        report.ExceptionQueue[0].ErrorMessage.ShouldContain("boom");
        report.CompletedCount.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessBatch_OneItemReturnsFailure_ExceptionQueued_OthersComplete()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 2)
                    return Task.FromResult(Result<VerificationOutcome>.WithFailure("stage failed"));
                return Task.FromResult(Result<VerificationOutcome>.WithSuccess(greenOutcome));
            });

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("first.pdf"),
            MakeSubmission("middle.pdf"),
            MakeSubmission("last.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: 1), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.ExceptionQueue.Count.ShouldBe(1);
        report.ExceptionQueue[0].IsException.ShouldBeFalse();
        report.ExceptionQueue[0].ErrorMessage.ShouldBe("stage failed");
        report.CompletedCount.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessBatch_RespectsMaxConcurrency()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const int maxDop = 2;

        // Use instance fields captured in closure to track concurrency
        var tracker = new ConcurrencyTracker();
        var greenOutcome = MakeGreenOutcome();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<BatchProcessorOptions>(_ => { });
        services.AddScoped<IVerificationPipeline>(_ =>
            new DelayedPipeline(tracker, delayMs: 30, greenOutcome));
        // IVerificationResultStore required by BatchProcessor constructor
        services.AddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.AddSingleton<VeriqanMetrics>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();
        var processor = services.BuildServiceProvider().GetRequiredService<IBatchProcessor>();

        var batch = new List<StatementSubmission>();
        for (var i = 0; i < 5; i++)
            batch.Add(MakeSubmission($"item{i}.pdf"));

        // Act
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: maxDop), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        tracker.Peak.ShouldBeLessThanOrEqualTo(maxDop,
            $"Peak concurrent calls {tracker.Peak} exceeded MaxDegreeOfParallelism {maxDop}");
    }

    [Fact]
    public async Task ProcessBatch_Progress_ReportsCounts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithSuccess(greenOutcome));

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("a.pdf"),
            MakeSubmission("b.pdf"),
            MakeSubmission("c.pdf"),
        };

        var progressReports = new System.Collections.Concurrent.ConcurrentBag<BatchProgress>();
        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));

        var processor = BuildBatchProcessor(fakePipeline);

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress, ct);

        // Give Progress<T> callbacks time to execute (it posts to SynchronizationContext)
        await Task.Delay(80, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.CompletedCount.ShouldBe(3);
        progressReports.Count.ShouldBeGreaterThan(0, "Expected at least one progress report");
    }

    [Fact]
    public async Task ProcessBatch_Cancelled_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        var processor = BuildBatchProcessor(fakePipeline);
        var batch = new List<StatementSubmission> { MakeSubmission() };

        // Act
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(), progress: null, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Yellow signal — counter correctness (adversarial fix)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a batch contains a YELLOW outcome, <see cref="BatchReport.YellowCount"/> must
    /// increment and the Green/Red/Blocked counters must remain unchanged.
    /// Regression guard: before this fix the switch had no Yellow case and the count was lost.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_YellowOutcome_IncrementsYellowCountAndNotOthers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();
        var yellowOutcome = MakeYellowOutcome();

        yellowOutcome.Summary.Signal.ShouldBe(VerdictSignal.Yellow,
            "Pre-condition: MakeYellowOutcome must produce a Yellow summary.");

        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // item 1 → Green, item 2 → Yellow, item 3 → Green
                var n = Interlocked.Increment(ref callCount);
                return n == 2
                    ? Task.FromResult(Result<VerificationOutcome>.WithSuccess(yellowOutcome))
                    : Task.FromResult(Result<VerificationOutcome>.WithSuccess(greenOutcome));
            });

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("a.pdf"),
            MakeSubmission("b-yellow.pdf"),
            MakeSubmission("c.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act — serial to keep call order deterministic
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: 1), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.CompletedCount.ShouldBe(3);
        report.GreenCount.ShouldBe(2);
        report.YellowCount.ShouldBe(1, "Exactly one Yellow outcome must increment YellowCount.");
        report.RedCount.ShouldBe(0, "Red counter must not be affected by a Yellow outcome.");
        report.BlockedCount.ShouldBe(0, "Blocked counter must not be affected by a Yellow outcome.");
        report.ExtractionGapCount.ShouldBe(0, "ExtractionGapCount must not be affected by a Yellow outcome.");
        report.TransientFailureCount.ShouldBe(0, "TransientFailureCount must not be affected by a Yellow outcome.");
        report.FailedCount.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // Story 4.2 — ExtractionGap / TransientFailure tally tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// ExtractionGap outcomes must increment <see cref="BatchReport.ExtractionGapCount"/>
    /// and NOT affect <see cref="BatchReport.GreenCount"/>, <see cref="BatchReport.RedCount"/>,
    /// or <see cref="BatchReport.BlockedCount"/>.
    /// Abstain-safety: ExtractionGap must never be counted as a compliance pass or fail.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_ExtractionGapOutcome_IncrementsExtractionGapCountAndNotOthers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();
        var gapOutcome = MakeExtractionGapOutcome();

        gapOutcome.Summary.Signal.ShouldBe(VerdictSignal.ExtractionGap,
            "Pre-condition: MakeExtractionGapOutcome must produce an ExtractionGap summary.");

        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // item 1 → Green, item 2 → ExtractionGap, item 3 → Green
                var n = Interlocked.Increment(ref callCount);
                return n == 2
                    ? Task.FromResult(Result<VerificationOutcome>.WithSuccess(gapOutcome))
                    : Task.FromResult(Result<VerificationOutcome>.WithSuccess(greenOutcome));
            });

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("a.pdf"),
            MakeSubmission("b-gap.pdf"),
            MakeSubmission("c.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act — serial to keep call order deterministic
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: 1), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.CompletedCount.ShouldBe(3);
        report.GreenCount.ShouldBe(2, "Two Green outcomes.");
        report.ExtractionGapCount.ShouldBe(1, "Exactly one ExtractionGap outcome must increment ExtractionGapCount.");
        report.RedCount.ShouldBe(0, "Red counter must not be affected by an ExtractionGap outcome.");
        report.BlockedCount.ShouldBe(0, "Blocked counter must not be affected by an ExtractionGap outcome.");
        report.YellowCount.ShouldBe(0, "Yellow counter must not be affected by an ExtractionGap outcome.");
        report.TransientFailureCount.ShouldBe(0, "TransientFailureCount must not be affected by an ExtractionGap outcome.");
        report.FailedCount.ShouldBe(0);
    }

    /// <summary>
    /// ExtractionGap must be excluded from compliance tallies — it is a non-verdict
    /// ("could this document, resubmitted tomorrow unchanged, produce a verdict? No.")
    /// and must never make a batch appear to pass compliance.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_ExtractionGapOutcome_IsNeverCountedAsGreenOrRed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var gapOutcome = MakeExtractionGapOutcome();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(gapOutcome)));

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("scanned-1.pdf"),
            MakeSubmission("scanned-2.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert — ExtractionGap items must NOT appear in Green or Red
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.ExtractionGapCount.ShouldBe(2, "Both items must be counted as ExtractionGap.");
        report.GreenCount.ShouldBe(0, "ExtractionGap must never be counted as Green (abstain-safety).");
        report.RedCount.ShouldBe(0, "ExtractionGap must never be counted as Red (abstain-safety).");
        report.YellowCount.ShouldBe(0);
        report.BlockedCount.ShouldBe(0);
        report.TransientFailureCount.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // VERIQAN-E1-S11 — Channel-based streaming consumer tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies the Channel-based streaming path still processes a multi-item batch correctly
    /// without regressing the core correctness guarantee (issue #58).
    /// The ExceptionQueue entry for a poison-PDF-style failure (pipeline returning failure) must
    /// carry <c>FileSizeLimitExceeded</c> in the error message when the extractor guard fires.
    /// Here we simulate this by having the pipeline return a failure with that reason.
    /// </summary>
    [Fact]
    public async Task ChannelConsumer_OversizePdfFailure_ExceptionQueueContainsFileSizeLimitExceeded()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 2)
                    // Simulate the extractor returning FileSizeLimitExceeded via the pipeline.
                    return Task.FromResult(
                        Result<VerificationOutcome>.WithFailure(
                            "FileSizeLimitExceeded: PDF size 52428801 bytes exceeds the configured limit of 52428800 bytes."));
                return Task.FromResult(Result<VerificationOutcome>.WithSuccess(greenOutcome));
            });

        var batch = new List<StatementSubmission>
        {
            MakeSubmission("ok1.pdf"),
            MakeSubmission("oversize.pdf"),
            MakeSubmission("ok2.pdf"),
        };

        var processor = BuildBatchProcessor(fakePipeline);

        // Act — MaxDegreeOfParallelism=1 gives deterministic ordering through the channel.
        var result = await processor.ProcessBatchAsync(
            batch, new BatchOptions(MaxDegreeOfParallelism: 1), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.TotalSubmitted.ShouldBe(3);
        report.CompletedCount.ShouldBe(2);
        report.FailedCount.ShouldBe(1);
        report.ExceptionQueue.Count.ShouldBe(1);
        report.ExceptionQueue[0].ErrorMessage.ShouldContain("FileSizeLimitExceeded");
        report.ExceptionQueue[0].IsException.ShouldBeFalse("A guard failure is a Result failure, not an exception.");
    }

    // -----------------------------------------------------------------------
    // Private helper types
    // -----------------------------------------------------------------------

    private sealed class ConcurrencyTracker
    {
        private int _current;
        private int _peak;

        public int Peak => _peak;

        public void Enter()
        {
            var newVal = Interlocked.Increment(ref _current);
            // Update peak with a CAS loop
            int observed;
            do
            {
                observed = _peak;
                if (newVal <= observed) break;
            }
            while (Interlocked.CompareExchange(ref _peak, newVal, observed) != observed);
        }

        public void Exit() => Interlocked.Decrement(ref _current);
    }

    private sealed class DelayedPipeline : IVerificationPipeline
    {
        private readonly ConcurrencyTracker _tracker;
        private readonly int _delayMs;
        private readonly VerificationOutcome _outcome;

        public DelayedPipeline(ConcurrencyTracker tracker, int delayMs, VerificationOutcome outcome)
        {
            _tracker = tracker;
            _delayMs = delayMs;
            _outcome = outcome;
        }

        public async Task<Result<VerificationOutcome>> ProcessAsync(
            StatementSubmission submission,
            CancellationToken ct = default)
        {
            _tracker.Enter();
            try
            {
                await Task.Delay(_delayMs, ct).ConfigureAwait(false);
                return Result<VerificationOutcome>.WithSuccess(_outcome);
            }
            finally
            {
                _tracker.Exit();
            }
        }
    }
}
