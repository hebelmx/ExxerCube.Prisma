using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    private static IBatchProcessor BuildBatchProcessor(IVerificationPipeline pipeline)
    {
        var services = new ServiceCollection();
        services.AddLogging();
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
