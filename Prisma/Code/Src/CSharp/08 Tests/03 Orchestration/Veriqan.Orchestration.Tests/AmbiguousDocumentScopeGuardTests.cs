using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit and integration tests for the ambiguous-document-scope guard introduced by Story 4.2-B.
/// <para>
/// Two categories:
/// <list type="number">
///   <item><b>Unit tests on <see cref="VerificationPipeline.CountStatementBoundarySignals"/>.</b>
///     These test the page-count proxy directly without constructing the full pipeline — fast,
///     isolated, no DI required.</item>
///   <item><b>Integration tests on the full pipeline.</b>
///     These wire the real pipeline via DI with a mocked extractor that returns a model whose
///     <see cref="StatementModel.PageCount"/> is set to zero, one, or two pages above the
///     configured threshold, asserting the resulting <see cref="VerdictSignal"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Guard semantics (Story 4.2-B):</b> a PDF containing multiple credit-card statements
/// can cause the extractor to read fields from the WRONG statement, yielding a confident but
/// wrong verdict.  The heuristic uses <see cref="StatementModel.PageCount"/> as a cheap proxy
/// for multi-statement detection.  When the page count exceeds
/// <see cref="TenantProfile.MaxStatementBoundarySignalCount"/> (default: 20) the pipeline
/// routes to <see cref="VerdictSignal.ExtractionGap"/> /
/// <c>BlockReason.AmbiguousDocumentScope</c> before binding or running any rules.
/// </para>
/// <para>
/// <b>STUB limitation:</b> page-count proxy only — full multi-statement detection is
/// deferred.  The threshold is configurable so tests can exercise downstream stages by setting
/// it to <see cref="int.MaxValue"/>.
/// </para>
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class AmbiguousDocumentScopeGuardTests
{
    // =========================================================================
    // Part 1 — CountStatementBoundarySignals unit tests (page-count proxy)
    // =========================================================================

    /// <summary>
    /// A model with zero pages (not yet extracted) yields zero boundary signals.
    /// </summary>
    [Fact]
    public void CountStatementBoundarySignals_ZeroPageCount_ReturnsZero()
    {
        // Arrange — default PageCount = 0 (before ExtractFullAsync completes)
        var model = BuildModelWithPageCount(0);

        // Act
        var count = VerificationPipeline.CountStatementBoundarySignals(model);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>
    /// A model with exactly one page yields a boundary signal count of 1 —
    /// at a threshold of 1 the guard condition is (1 > 1) = false, so the guard does NOT fire.
    /// </summary>
    [Fact]
    public void CountStatementBoundarySignals_OnePageCount_ReturnsOne()
    {
        // Arrange — single-page document
        var model = BuildModelWithPageCount(1);

        // Act
        var count = VerificationPipeline.CountStatementBoundarySignals(model);

        // Assert
        count.ShouldBe(1);
    }

    /// <summary>
    /// A model with ten pages (typical CONDUSEF statement) yields ten — well below the
    /// default threshold of 20 so the guard would NOT fire in the real pipeline.
    /// </summary>
    [Fact]
    public void CountStatementBoundarySignals_TenPageCount_ReturnsTen()
    {
        // Arrange
        var model = BuildModelWithPageCount(10);

        // Act
        var count = VerificationPipeline.CountStatementBoundarySignals(model);

        // Assert
        count.ShouldBe(10, "A 10-page single statement is well within the default 20-page threshold.");
    }

    /// <summary>
    /// A model with 25 pages (multi-statement bundle) yields 25 — above the default
    /// threshold of 20, so the guard WOULD fire in the pipeline.
    /// </summary>
    [Fact]
    public void CountStatementBoundarySignals_TwentyFivePageCount_ReturnsTwentyFive()
    {
        // Arrange — 3 statements × ~8 pages = 25 pages total
        var model = BuildModelWithPageCount(25);

        // Act
        var count = VerificationPipeline.CountStatementBoundarySignals(model);

        // Assert
        count.ShouldBe(25);
    }

    // =========================================================================
    // Part 2 — Pipeline integration tests (guard fires / does not fire)
    // =========================================================================

    /// <summary>
    /// When the extracted model has a page count of two and the tenant threshold is one,
    /// the pipeline must return a successful <see cref="Result{T}"/> whose
    /// <see cref="VerdictSignal"/> is <see cref="VerdictSignal.ExtractionGap"/>.
    /// The binder must NOT have been called (guard fires before bind).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_PageCountExceedsThreshold_EmitsExtractionGapAndDoesNotReachBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile: threshold = 1 page; model has PageCount = 2 → 2 > 1 → guard fires.
        // Disable coverage and text-layer floors so only the boundary guard is exercised.
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-AMBIGUOUS-DOC",
            tenantName: "Test Tenant Ambiguous Document",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0,
            maxStatementBoundarySignalCount: 1);

        // Model with PageCount = 2 — above the threshold of 1.
        var multiPageModel = BuildModelWithPageCount(2);

        var binder = Substitute.For<IBundleBinder>();
        var persistResult = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.ExtractionGap);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(persistResult)));

        var services = BuildAmbiguousDocumentTestServices(
            tenantProfile,
            multiPageModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "multi-statement-archive.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline returns success (ExtractionGap is a valid non-verdict outcome)
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline must return success on ExtractionGap verdict. Error: {result.Error ?? "<none>"}");

        // Assert — signal is ExtractionGap (Story 4.2-B: multi-page PDF cannot be safely verified)
        var outcome = result.Value!;
        outcome.Summary.Signal.ShouldBe(
            VerdictSignal.ExtractionGap,
            "A PDF whose page count exceeds the threshold must produce " +
            "VerdictSignal.ExtractionGap (AmbiguousDocumentScope) — never Red or Green.");

        // Assert — binder was NOT called (boundary guard fires before bind stage)
        await binder.DidNotReceive().BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Assert — persist WAS called once with ExtractionGap signal (persist is non-optional)
        await verdictPersistence.Received(1).PersistAsync(
            Arg.Any<Guid>(),
            VerdictSignal.ExtractionGap,
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<string>(),
            VerdictSignal.ExtractionGap,
            VerdictSignal.ExtractionGap,
            Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the extracted model has a page count exactly at the threshold (PageCount == threshold),
    /// the guard must NOT fire — the condition is strictly greater-than.
    /// The pipeline must proceed to the binder.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_PageCountAtThreshold_DoesNotFireGuardAndReachesBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile: threshold = 1; model has PageCount = 1 → 1 > 1 = false → guard does NOT fire.
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-AT-THRESHOLD",
            tenantName: "Test Tenant At Threshold",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0,
            maxStatementBoundarySignalCount: 1);

        // Model with PageCount = 1 — at the threshold, NOT exceeded (guard fires only when count > max).
        var singlePageModel = BuildModelWithPageCount(1);

        // Binder — returns a BLOCKED failure (UnknownProduct) so the test doesn't need
        // a full bundle; we only need to confirm the binder was REACHED (called once).
        var binderBlockError = new BlockedOutcome(
            BlockReason.UnknownProduct, "No product matched").ToErrorString();
        var binder = Substitute.For<IBundleBinder>();
        binder
            .BindAsync(Arg.Any<VerificationJob>(), Arg.Any<StatementContextKey>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationContext>.WithFailure(binderBlockError)));

        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<JobVerdict>.WithSuccess(
                    new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.ExtractionGap))));

        var services = BuildAmbiguousDocumentTestServices(
            tenantProfile,
            singlePageModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "single-page-statement.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — binder was reached (boundary guard did NOT fire)
        await binder.Received(1).BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // The binder returned UnknownProduct → ExtractionGap for a different reason.
        // We only need to confirm the binder was reached; the specific signal is a bonus assert.
        result.IsSuccess.ShouldBeTrue("Pipeline should succeed even when binder returns ExtractionGap.");
        result.Value!.Summary.Signal.ShouldBe(
            VerdictSignal.ExtractionGap,
            "Binder UnknownProduct produces VerdictSignal.ExtractionGap (Story 4.2) — " +
            "NOT the AmbiguousDocumentScope guard.");
    }

    /// <summary>
    /// A model with zero pages (PageCount = 0, e.g. before ExtractFullAsync runs) must
    /// never be flagged by the guard — zero is well below any sane threshold.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ZeroPageCount_DoesNotFireGuardAndReachesBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var tenantProfile = new TenantProfile(
            tenantId: "TEST-ZERO-PAGES",
            tenantName: "Test Tenant Zero Pages",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0,
            maxStatementBoundarySignalCount: 1);

        // Model with PageCount = 0 — never triggers the guard.
        var zeroPageModel = BuildModelWithPageCount(0);

        var binderBlockError = new BlockedOutcome(
            BlockReason.UnknownProduct, "No product matched").ToErrorString();
        var binder = Substitute.For<IBundleBinder>();
        binder
            .BindAsync(Arg.Any<VerificationJob>(), Arg.Any<StatementContextKey>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationContext>.WithFailure(binderBlockError)));

        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<JobVerdict>.WithSuccess(
                    new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.ExtractionGap))));

        var services = BuildAmbiguousDocumentTestServices(
            tenantProfile,
            zeroPageModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "zero-page-statement.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — binder was reached (zero pages never triggers the guard)
        await binder.Received(1).BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        result.IsSuccess.ShouldBeTrue();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> whose header fields are all
    /// <see cref="ExtractionStatus.NotExtracted"/> and whose
    /// <see cref="StatementModel.PageCount"/> is set to <paramref name="pageCount"/>.
    /// This isolates the page-count boundary signal as the only variable under test.
    /// </summary>
    private static StatementModel BuildModelWithPageCount(int pageCount)
    {
        var locator = FieldLocator.PageHint(1);
        return new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(locator),
            address: ExtractedField<ExtractedAddress>.Missing(locator),
            branchNumber: ExtractedField<string>.Missing(locator),
            cardNumber: ExtractedField<string>.Missing(locator),
            clabe: ExtractedField<string>.Missing(locator),
            clientNumber: ExtractedField<string>.Missing(locator),
            rfc: ExtractedField<string>.Missing(locator))
        {
            PageCount = pageCount,
        };
    }

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> wired for the ambiguous-document-scope
    /// integration tests: mocked ingestion, a caller-supplied extractor model, a caller-supplied
    /// binder mock, and a caller-supplied persist mock.  Uses the real <c>VerdictAggregator</c>.
    /// Mirrors <c>TextLayerDensityGuardTests.BuildTextLayerTestServices</c> exactly.
    /// </summary>
    private static ServiceCollection BuildAmbiguousDocumentTestServices(
        TenantProfile tenantProfile,
        StatementModel extractedModel,
        IBundleBinder binder,
        IVerdictPersistenceService verdictPersistence)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ── Mock: ingestion ──────────────────────────────────────────────────
        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: "test-hash-ambiguous-doc",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

        var ingestion = Substitute.For<IStatementIngestionService>();
        ingestion
            .IngestAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationJob>.WithSuccess(job)));
        services.AddSingleton(ingestion);

        // ── Mock: field extractor — returns caller-supplied model ────────────
        var extractor = Substitute.For<IStatementFieldExtractor>();
        extractor
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<StatementModel>.WithSuccess(extractedModel)));
        services.AddSingleton(extractor);

        // ── Caller-supplied binder ───────────────────────────────────────────
        services.AddSingleton(binder);

        // ── Real verdict aggregator + validation layer ───────────────────────
        services.AddVeriqanVerdict();
        services.AddVeriqanValidation();

        // Engine — not reached when the guard fires, but required by ctor.
        var engine = Substitute.For<IVecValidationEngine>();
        engine
            .RunAsync(Arg.Any<VerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<IReadOnlyList<RuleFinding>>.WithSuccess(Array.Empty<RuleFinding>())));
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // ── Caller-supplied TenantProfile (carries the boundary-signal threshold) ──
        services.AddSingleton(tenantProfile);
        services.AddSingleton(TimeProvider.System);

        // ── In-memory persistence stubs ──────────────────────────────────────
        services.AddVeriqanInMemoryPersistence();

        // Override IVerdictPersistenceService with the caller's mock.
        services.Replace(ServiceDescriptor.Scoped<IVerdictPersistenceService>(
            _ => verdictPersistence));

        // ── Stub: report generator (best-effort, success no-op) ──────────────
        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));
        services.AddSingleton(reportGenerator);

        // ── Stub: alert service (not called on ExtractionGap path) ───────────
        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(
                Arg.Any<Application.Verdict.VerdictSummary>(),
                Arg.Any<AlertContext>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));
        services.AddSingleton(alertService);

        services.AddSingleton<IOptions<AlertOptions>>(
            Options.Create(new AlertOptions()));

        // ── Metrics + pipeline ───────────────────────────────────────────────
        services.AddSingleton<VeriqanMetrics>();

        // Stub: tier-map provider — returns empty map so the pipeline degrades to single-tier.
        var tierProvider = Substitute.For<IVecReferenceDataProvider>();
        tierProvider
            .GetChecklistTiersAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                    new Dictionary<string, ChecklistTier>() as IReadOnlyDictionary<string, ChecklistTier>)));
        services.AddSingleton<IVecReferenceDataProvider>(tierProvider);

        services.AddScoped<IVerificationPipeline, VerificationPipeline>();

        return services;
    }
}
