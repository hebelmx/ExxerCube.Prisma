using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.InMemory;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for resume (FR-22) and explicit reprocess behaviour:
/// <list type="bullet">
///   <item>Resume skips already-completed hashes and processes only the remainder.</item>
///   <item>Resuming a fully-completed batch does zero work (idempotent).</item>
///   <item>Reprocess replaces the prior result — no duplicate is created.</item>
///   <item>Reprocess appends an audit entry with actor + before/after verdict.</item>
///   <item>Reprocess with a blank actor returns a typed failure.</item>
///   <item>Cancellation propagates correctly.</item>
/// </list>
/// </summary>
public sealed class ResumeAndReprocessTests
{
    // -----------------------------------------------------------------------
    // Helpers — minimal PDF bytes (valid magic header) that produce distinct hashes
    // -----------------------------------------------------------------------

    /// <summary>Builds minimal PDF bytes with a distinguishing suffix so each submission has a unique content hash.</summary>
    private static byte[] MakePdf(byte discriminator) =>
        new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, discriminator }; // %PDF-<n>

    private static StatementSubmission MakeSubmission(byte discriminator, string name = "test.pdf") =>
        new(Pdf: MakePdf(discriminator), FileName: name,
            ContextKey: new StatementContextKey("Bank A"));

    private static VerificationOutcome MakeGreenOutcome(byte discriminator = 0)
    {
        var job = new VerificationJob(
            Guid.NewGuid(),
            $"hash{discriminator:x2}",
            DateTimeOffset.UtcNow,
            VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        var summary = aggregator.Aggregate(Array.Empty<RuleFinding>()).Value!;
        return new VerificationOutcome(job, summary, Array.Empty<RuleFinding>());
    }

    private static VerificationOutcome MakeRedOutcome(byte discriminator = 0)
    {
        var job = new VerificationJob(
            Guid.NewGuid(),
            $"hashRed{discriminator:x2}",
            DateTimeOffset.UtcNow,
            VerificationJobStatus.Pending);
        var aggregator = new VerdictAggregator();
        // Build a single Fail finding to force a Red signal (using the static factory)
        var finding = RuleFinding.Fail("CL-99", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0");
        var summary = aggregator.Aggregate(new[] { finding }).Value!;
        return new VerificationOutcome(job, summary, new[] { finding });
    }

    // -----------------------------------------------------------------------
    // BatchProcessor + IVerificationResultStore wiring helper
    // -----------------------------------------------------------------------

    private static (IBatchProcessor Processor, IVerificationResultStore Store) BuildResumeProcessor(
        IVerificationPipeline pipeline)
    {
        var store = new InMemoryVerificationResultStore();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IVerificationPipeline>(_ => pipeline);
        services.AddSingleton<IVerificationResultStore>(store);
        services.AddSingleton<IBatchProcessor, BatchProcessor>();

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IBatchProcessor>(), store);
    }

    // -----------------------------------------------------------------------
    // Helper to pre-seed the store with a completed outcome for given PDF bytes
    // -----------------------------------------------------------------------

    private static async Task SeedCompletedAsync(
        IVerificationResultStore store,
        byte[] pdf,
        VerificationOutcome outcome,
        CancellationToken ct)
    {
        // Compute hash the same way BatchProcessor does (SHA-256, lowercase hex)
        var hash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(pdf));
        await store.SaveOutcomeAsync(hash, outcome, ct);
    }

    // -----------------------------------------------------------------------
    // 1. Resume_SkipsAlreadyCompleted_ProcessesOnlyRemainder
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResumeBatch_SkipsAlreadyCompleted_ProcessesOnlyRemainder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var greenOutcome = MakeGreenOutcome();
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithSuccess(greenOutcome));

        var (processor, store) = BuildResumeProcessor(fakePipeline);

        // Submissions A (discriminator 1) and B (discriminator 2) are pre-completed;
        // submission C (discriminator 3) is new.
        var subA = MakeSubmission(1, "a.pdf");
        var subB = MakeSubmission(2, "b.pdf");
        var subC = MakeSubmission(3, "c.pdf");

        await SeedCompletedAsync(store, subA.Pdf, greenOutcome, ct);
        await SeedCompletedAsync(store, subB.Pdf, greenOutcome, ct);

        var batch = new List<StatementSubmission> { subA, subB, subC };

        // Act
        var result = await processor.ProcessBatchAsync(
            batch,
            new BatchOptions(MaxDegreeOfParallelism: 1, Resume: true),
            progress: null,
            ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;

        report.TotalSubmitted.ShouldBe(3);
        report.AlreadyCompletedCount.ShouldBe(2, "A and B were pre-seeded as completed");
        report.CompletedCount.ShouldBe(1, "Only C should be processed by the pipeline");
        report.FailedCount.ShouldBe(0);

        // Pipeline must be called exactly once (for C only)
        await fakePipeline.Received(1)
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // 2. Resume_FullyCompletedBatch_DoesZeroWork
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResumeBatch_FullyCompleted_DoesZeroWork()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var greenOutcome = MakeGreenOutcome();
        var fakePipeline = Substitute.For<IVerificationPipeline>();

        var (processor, store) = BuildResumeProcessor(fakePipeline);

        var subA = MakeSubmission(10, "a.pdf");
        var subB = MakeSubmission(11, "b.pdf");

        await SeedCompletedAsync(store, subA.Pdf, greenOutcome, ct);
        await SeedCompletedAsync(store, subB.Pdf, greenOutcome, ct);

        var batch = new List<StatementSubmission> { subA, subB };

        // Act
        var result = await processor.ProcessBatchAsync(
            batch,
            new BatchOptions(MaxDegreeOfParallelism: 1, Resume: true),
            progress: null,
            ct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var report = result.Value!;

        report.AlreadyCompletedCount.ShouldBe(2, "Both statements already completed");
        report.CompletedCount.ShouldBe(0, "Pipeline should not be called at all");

        // Pipeline must never be invoked — idempotent zero-work
        await fakePipeline.DidNotReceive()
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // 3. Reprocess_ReplacesPriorResult_NoDuplicate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reprocess_ReplacesPriorResult_NoDuplicate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var sub = MakeSubmission(20, "stmt.pdf");
        var contentHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(sub.Pdf));

        var originalOutcome = MakeGreenOutcome();
        var newOutcome = MakeRedOutcome();

        var store = new InMemoryVerificationResultStore();
        await store.SaveOutcomeAsync(contentHash, originalOutcome, ct);

        // The pipeline now returns a RED outcome on reprocess
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithSuccess(newOutcome));

        var auditRepo = new InMemoryReprocessAuditRepository();
        var timeProvider = new FakeTimeProvider();

        var svc = new ReprocessServiceAccessor(fakePipeline, store, auditRepo, timeProvider);

        // Act
        var result = await svc.ReprocessAsync(sub, "reviewer@bank.mx", "Corrected extraction", ct);

        // Assert — reprocess succeeded
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.Red);

        // The store must contain EXACTLY ONE entry for this hash (no duplicate)
        var storedResult = await store.GetOutcomeAsync(contentHash, ct);
        storedResult.IsSuccess.ShouldBeTrue();
        storedResult.Value.ShouldNotBeNull();
        storedResult.Value!.Summary.Signal.ShouldBe(VerdictSignal.Red,
            "Store entry must be the new (Red) outcome — prior Green replaced");

        // Verify pipeline was called exactly once
        await fakePipeline.Received(1)
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // 4. Reprocess_WritesAudit_WithActorAndBeforeAfter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reprocess_WritesAudit_WithActorAndBeforeAfter()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var sub = MakeSubmission(30, "audit.pdf");
        var contentHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(sub.Pdf));

        var originalOutcome = MakeGreenOutcome();
        var newOutcome = MakeRedOutcome();

        var store = new InMemoryVerificationResultStore();
        await store.SaveOutcomeAsync(contentHash, originalOutcome, ct);

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationOutcome>.WithSuccess(newOutcome));

        var auditRepo = new InMemoryReprocessAuditRepository();
        var fixedTime = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(fixedTime);

        var svc = new ReprocessServiceAccessor(fakePipeline, store, auditRepo, timeProvider);

        // Act
        var result = await svc.ReprocessAsync(sub, "ops-user-42", "Scheduled re-check", ct);

        // Assert — operation succeeded
        result.IsSuccess.ShouldBeTrue();

        // Audit log must have exactly one entry
        var auditResult = await auditRepo.GetForContentHashAsync(contentHash, ct);
        auditResult.IsSuccess.ShouldBeTrue();
        var entries = auditResult.Value!;
        entries.Count.ShouldBe(1, "Exactly one audit entry should be appended");

        var entry = entries[0];
        entry.Actor.ShouldBe("ops-user-42");
        entry.BeforeSignal.ShouldBe(VerdictSignal.Green, "Before-verdict must capture the prior Green signal");
        entry.AfterSignal.ShouldBe(VerdictSignal.Red, "After-verdict must reflect the new Red outcome");
        entry.Reason.ShouldBe("Scheduled re-check");
        entry.ContentHash.ShouldBe(contentHash);
        entry.ReprocessedAtUtc.ShouldBe(fixedTime);
    }

    // -----------------------------------------------------------------------
    // 5. Reprocess_AppendsAudit_SecondReprocessAddsSecondEntry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reprocess_MultipleReprocesses_AppendsTwoAuditEntries()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var sub = MakeSubmission(40, "multi.pdf");
        var contentHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(sub.Pdf));

        var outcome1 = MakeGreenOutcome();
        var outcome2 = MakeRedOutcome();

        var store = new InMemoryVerificationResultStore();
        await store.SaveOutcomeAsync(contentHash, outcome1, ct);

        int callCount = 0;
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        fakePipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var n = Interlocked.Increment(ref callCount);
                return Task.FromResult(n == 1
                    ? Result<VerificationOutcome>.WithSuccess(outcome2)
                    : Result<VerificationOutcome>.WithSuccess(outcome1));
            });

        var auditRepo = new InMemoryReprocessAuditRepository();
        var svc = new ReprocessServiceAccessor(fakePipeline, store, auditRepo, new FakeTimeProvider());

        // Act — two sequential reprocesses
        await svc.ReprocessAsync(sub, "analyst-1", "First pass", ct);
        await svc.ReprocessAsync(sub, "analyst-2", "Second pass", ct);

        // Assert — two audit entries, not one overwritten
        var auditResult = await auditRepo.GetForContentHashAsync(contentHash, ct);
        auditResult.IsSuccess.ShouldBeTrue();
        auditResult.Value!.Count.ShouldBe(2, "Each reprocess appends a new audit entry");
    }

    // -----------------------------------------------------------------------
    // 6. Reprocess_NoActor_ReturnsFailure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reprocess_BlankActor_ReturnsFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var sub = MakeSubmission(50, "blank-actor.pdf");
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        var store = new InMemoryVerificationResultStore();
        var auditRepo = new InMemoryReprocessAuditRepository();

        var svc = new ReprocessServiceAccessor(fakePipeline, store, auditRepo, new FakeTimeProvider());

        // Act — blank actor
        var result = await svc.ReprocessAsync(sub, actor: "   ", reason: null, ct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrWhiteSpace();

        // Pipeline must never be invoked
        await fakePipeline.DidNotReceive()
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reprocess_EmptyActor_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var sub = MakeSubmission(51, "empty-actor.pdf");
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        var svc = new ReprocessServiceAccessor(
            fakePipeline,
            new InMemoryVerificationResultStore(),
            new InMemoryReprocessAuditRepository(),
            new FakeTimeProvider());

        var result = await svc.ReprocessAsync(sub, actor: string.Empty, reason: null, ct);

        result.IsFailure.ShouldBeTrue();
        await fakePipeline.DidNotReceive()
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // 7. Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reprocess_CancelledToken_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var sub = MakeSubmission(60, "cancel.pdf");
        var fakePipeline = Substitute.For<IVerificationPipeline>();
        var svc = new ReprocessServiceAccessor(
            fakePipeline,
            new InMemoryVerificationResultStore(),
            new InMemoryReprocessAuditRepository(),
            new FakeTimeProvider());

        // Act
        var result = await svc.ReprocessAsync(sub, "operator", null, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
        await fakePipeline.DidNotReceive()
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeBatch_CancelledToken_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var fakePipeline = Substitute.For<IVerificationPipeline>();
        var (processor, _) = BuildResumeProcessor(fakePipeline);

        var batch = new List<StatementSubmission> { MakeSubmission(70) };

        // Act
        var result = await processor.ProcessBatchAsync(
            batch,
            new BatchOptions(Resume: true),
            progress: null,
            cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Helper — expose internal ReprocessService for unit testing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Thin accessor that constructs a <see cref="ReprocessService"/> from test-visible
    /// in-memory components without requiring DI wiring.
    /// </summary>
    private sealed class ReprocessServiceAccessor
    {
        private readonly IReprocessService _svc;

        public ReprocessServiceAccessor(
            IVerificationPipeline pipeline,
            IVerificationResultStore store,
            IReprocessAuditRepository auditRepo,
            TimeProvider timeProvider)
        {
            // Build a minimal DI container to construct the internal service
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<IVerificationPipeline>(_ => pipeline);
            services.AddSingleton(store);
            services.AddSingleton(auditRepo);
            services.AddSingleton(timeProvider);
            services.AddScoped<IReprocessService, ReprocessService>();

            _svc = services.BuildServiceProvider().GetRequiredService<IReprocessService>();
        }

        public Task<Result<VerificationOutcome>> ReprocessAsync(
            StatementSubmission submission,
            string actor,
            string? reason,
            CancellationToken ct) =>
            _svc.ReprocessAsync(submission, actor, reason, ct);
    }
}
