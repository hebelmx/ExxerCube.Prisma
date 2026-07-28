using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
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
/// Unit tests for VERIQAN-E3-S3: the durable <see cref="BatchExceptionLogEntry"/> dead-letter
/// log written by <see cref="BatchProcessor"/> alongside the in-memory
/// <see cref="BatchReport.ExceptionQueue"/> summary.
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class BatchExceptionLogPersistenceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StatementSubmission MakeSubmission(string name = "test.pdf") =>
        new(Pdf: [0x25, 0x50, 0x44, 0x46], FileName: name,
            ContextKey: new StatementContextKey("Bank A"));

    private static VerificationOutcome MakeGreenOutcome()
    {
        var job = new VerificationJob(
            Guid.NewGuid(), "abc123", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        var summary = aggregator.Aggregate(Array.Empty<RuleFinding>()).Value!;
        return new VerificationOutcome(job, summary, Array.Empty<RuleFinding>());
    }

    /// <summary>
    /// Builds an <see cref="IBatchProcessor"/> via DI, exposing the
    /// <see cref="IBatchExceptionLogRepository"/> instance so tests can query the durable log
    /// after the run.
    /// </summary>
    private static (IBatchProcessor Processor, IBatchExceptionLogRepository ExceptionLog) BuildBatchProcessor(
        IVerificationPipeline pipeline,
        IBatchExceptionLogRepository? exceptionLogRepository = null)
    {
        var exceptionLog = exceptionLogRepository ?? new InMemoryBatchExceptionLogRepository();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<BatchProcessorOptions>(_ => { });
        services.AddScoped<IVerificationPipeline>(_ => pipeline);
        services.AddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.AddSingleton(exceptionLog);
        services.AddSingleton<VeriqanMetrics>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IBatchProcessor>(), exceptionLog);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// A pipeline <c>Result</c> failure must be persisted as a durable dead-letter row, stamped
    /// with the batch's <see cref="BatchReport.BatchId"/>, alongside the existing in-memory
    /// <see cref="BatchReport.ExceptionQueue"/> summary.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_PipelineFailure_PersistsDeadLetterRowStampedWithBatchId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var greenOutcome = MakeGreenOutcome();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithFailure("extraction failed"));

        var (processor, exceptionLog) = BuildBatchProcessor(fakePipeline);
        var batch = new List<StatementSubmission> { MakeSubmission("failing.pdf") };

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert — in-memory summary unaffected by this story
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;
        report.ExceptionQueue.Count.ShouldBe(1);
        report.BatchId.ShouldNotBe(Guid.Empty, "BatchProcessor must generate a non-empty BatchId per run.");

        // Assert — durable dead-letter row persisted and stamped with the batch's BatchId
        var logResult = await exceptionLog.GetByBatchIdAsync(report.BatchId, ct);
        logResult.IsSuccess.ShouldBeTrue();
        var rows = logResult.Value!;
        rows.Count.ShouldBe(1, "One failing item must produce exactly one durable dead-letter row.");
        rows[0].BatchId.ShouldBe(report.BatchId);
        rows[0].InstitutionId.ShouldBe("Bank A");
        rows[0].FailureReason.ShouldBe("extraction failed");
        rows[0].StatementHash.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// An unhandled exception (not a <c>Result</c> failure) must also be persisted to the durable
    /// dead-letter log, using <see cref="Exception.Message"/> as the failure reason.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_UnexpectedException_PersistsDeadLetterRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns<Result<VerificationOutcome>>(_ => throw new InvalidOperationException("boom"));

        var (processor, exceptionLog) = BuildBatchProcessor(fakePipeline);
        var batch = new List<StatementSubmission> { MakeSubmission("throws.pdf") };

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;

        var logResult = await exceptionLog.GetByBatchIdAsync(report.BatchId, ct);
        logResult.IsSuccess.ShouldBeTrue();
        var rows = logResult.Value!;
        rows.Count.ShouldBe(1);
        rows[0].FailureReason.ShouldBe("boom");
    }

    /// <summary>
    /// Two separate <c>ProcessBatchAsync</c> runs must generate two distinct <see cref="BatchReport.BatchId"/>
    /// values, and each run's dead-letter rows must be scoped to its own BatchId.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_TwoRuns_ProduceDistinctBatchIdsAndScopedDeadLetterRows()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sharedLog = new InMemoryBatchExceptionLogRepository();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithFailure("run failure"));

        var (processor1, _) = BuildBatchProcessor(fakePipeline, sharedLog);
        var (processor2, _) = BuildBatchProcessor(fakePipeline, sharedLog);

        // Act
        var result1 = await processor1.ProcessBatchAsync(
            new List<StatementSubmission> { MakeSubmission("run1.pdf") }, new BatchOptions(), progress: null, ct);
        var result2 = await processor2.ProcessBatchAsync(
            new List<StatementSubmission> { MakeSubmission("run2.pdf") }, new BatchOptions(), progress: null, ct);

        // Assert
        result1.Value!.BatchId.ShouldNotBe(result2.Value!.BatchId,
            "Each ProcessBatchAsync call must generate a fresh BatchId.");

        var run1Rows = (await sharedLog.GetByBatchIdAsync(result1.Value!.BatchId, ct)).Value!;
        var run2Rows = (await sharedLog.GetByBatchIdAsync(result2.Value!.BatchId, ct)).Value!;

        run1Rows.Count.ShouldBe(1);
        run2Rows.Count.ShouldBe(1);
        run1Rows[0].BatchId.ShouldBe(result1.Value!.BatchId);
        run2Rows[0].BatchId.ShouldBe(result2.Value!.BatchId);
    }

    /// <summary>
    /// A failure reason longer than <see cref="BatchExceptionLogEntry.MaxFailureReasonLength"/>
    /// must be truncated before the durable row is constructed.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_LongFailureReason_TruncatedToMaxFailureReasonLength()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var longReason = new string('x', BatchExceptionLogEntry.MaxFailureReasonLength + 500);

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithFailure(longReason));

        var (processor, exceptionLog) = BuildBatchProcessor(fakePipeline);
        var batch = new List<StatementSubmission> { MakeSubmission("long-reason.pdf") };

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert
        var rows = (await exceptionLog.GetByBatchIdAsync(result.Value!.BatchId, ct)).Value!;
        rows.Count.ShouldBe(1);
        rows[0].FailureReason.Length.ShouldBe(BatchExceptionLogEntry.MaxFailureReasonLength,
            "FailureReason must be truncated to MaxFailureReasonLength before persisting.");
        rows[0].FailureReason.ShouldBe(longReason[..BatchExceptionLogEntry.MaxFailureReasonLength]);
    }

    /// <summary>
    /// A failing <see cref="IBatchExceptionLogRepository.AppendAsync"/> (Result failure) must
    /// NEVER abort or fail the batch — the dead-letter write guard logs a warning and the batch
    /// still completes successfully with its normal <see cref="BatchReport.ExceptionQueue"/>.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_AppendAsyncReturnsFailure_BatchStillCompletesSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var failingLog = Substitute.For<IBatchExceptionLogRepository>();
        failingLog
            .AppendAsync(Arg.Any<BatchExceptionLogEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result<BatchExceptionLogEntry>.WithFailure("db unavailable"));

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithFailure("pipeline failure"));

        var (processor, _) = BuildBatchProcessor(fakePipeline, failingLog);
        var batch = new List<StatementSubmission> { MakeSubmission("guarded.pdf") };

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert — batch completes normally; only the durable write was affected.
        result.IsSuccess.ShouldBeTrue(
            "A failing dead-letter write must never fail the batch's overall Result.");
        result.Value!.ExceptionQueue.Count.ShouldBe(1,
            "The in-memory ExceptionQueue summary must still record the pipeline failure.");
        result.Value!.FailedCount.ShouldBe(1);
    }

    /// <summary>
    /// An <see cref="IBatchExceptionLogRepository.AppendAsync"/> that throws an unexpected
    /// exception must also be swallowed by the dead-letter write guard — the batch must still
    /// complete successfully.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_AppendAsyncThrows_BatchStillCompletesSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var throwingLog = Substitute.For<IBatchExceptionLogRepository>();
        throwingLog
            .AppendAsync(Arg.Any<BatchExceptionLogEntry>(), Arg.Any<CancellationToken>())
            .Returns<Result<BatchExceptionLogEntry>>(_ => throw new InvalidOperationException("connection reset"));

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithFailure("pipeline failure"));

        var (processor, _) = BuildBatchProcessor(fakePipeline, throwingLog);
        var batch = new List<StatementSubmission> { MakeSubmission("guarded-throw.pdf") };

        // Act
        var result = await processor.ProcessBatchAsync(batch, new BatchOptions(), progress: null, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue(
            "An exception from the dead-letter repository must never propagate and fail the batch.");
        result.Value!.FailedCount.ShouldBe(1);
    }
}
