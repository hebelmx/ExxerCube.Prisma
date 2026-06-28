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
/// Unit and integration tests for the text-layer density abstain guard introduced by Story E2-S1.
/// <para>
/// Two categories:
/// <list type="number">
///   <item><b>Unit tests on <see cref="VerificationPipeline.CountTextLayerWords"/>.</b>
///     These test the counting logic directly without constructing the full pipeline — fast,
///     isolated, no DI required.</item>
///   <item><b>Integration tests on the full pipeline.</b>
///     These wire the real pipeline via DI with a mocked extractor that returns either a
///     zero-word or a sufficient-word model, and assert the resulting
///     <see cref="VerdictSignal"/>.</item>
/// </list>
/// </para>
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class TextLayerDensityGuardTests
{
    // =========================================================================
    // Part 1 — CountTextLayerWords unit tests
    // =========================================================================

    /// <summary>
    /// A <see cref="StatementModel"/> with an empty <see cref="StatementModel.NormalizedFullText"/>
    /// (scanned / image-only PDF) yields a word count of zero.
    /// </summary>
    [Fact]
    public void CountTextLayerWords_EmptyNormalizedFullText_ReturnsZero()
    {
        // Arrange — NormalizedFullText defaults to string.Empty
        var model = BuildModelWithText(string.Empty);

        // Act
        var count = VerificationPipeline.CountTextLayerWords(model);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>
    /// A model whose <see cref="StatementModel.NormalizedFullText"/> is all whitespace yields zero.
    /// </summary>
    [Fact]
    public void CountTextLayerWords_WhitespaceOnlyNormalizedFullText_ReturnsZero()
    {
        // Arrange
        var model = BuildModelWithText("   ");

        // Act
        var count = VerificationPipeline.CountTextLayerWords(model);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>
    /// A model with exactly 5 space-separated words yields a count of 5.
    /// </summary>
    [Fact]
    public void CountTextLayerWords_FiveWords_ReturnsFive()
    {
        // Arrange — normalized form: upper-cased, single spaces
        var model = BuildModelWithText("COMPARA TU TARJETA DE CREDITO");

        // Act
        var count = VerificationPipeline.CountTextLayerWords(model);

        // Assert
        count.ShouldBe(5);
    }

    /// <summary>
    /// A model with exactly 20 words (the default floor) yields exactly 20.
    /// This verifies the floor boundary is inclusive.
    /// </summary>
    [Fact]
    public void CountTextLayerWords_TwentyWords_ReturnsTwenty()
    {
        // Arrange — 20 single-word tokens
        var text = string.Join(' ', System.Linq.Enumerable.Repeat("WORD", 20));
        var model = BuildModelWithText(text);

        // Act
        var count = VerificationPipeline.CountTextLayerWords(model);

        // Assert
        count.ShouldBe(20);
    }

    // =========================================================================
    // Part 2 — Pipeline integration tests (guard fires / does not fire)
    // =========================================================================

    /// <summary>
    /// When the extractor returns a model with zero words in <c>NormalizedFullText</c>
    /// (scanned / image-only PDF), the pipeline must return a successful
    /// <see cref="Result{T}"/> whose <see cref="VerdictSignal"/> is
    /// <see cref="VerdictSignal.Blocked"/> (not Red, not Green).
    /// The binder must NOT have been called (guard fires before bind).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ZeroWordTextLayer_EmitsBlockedVerdictAndDoesNotReachBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile with text-layer floor = 20 (default); zero words falls below it
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-TEXT-LAYER",
            tenantName: "Test Tenant Text Layer",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,  // disable coverage floor so only text-layer guard fires
            minTextLayerWordCount: 20);

        // Model with zero words in text layer
        var zeroWordModel = BuildModelWithText(string.Empty);

        var binder = Substitute.For<IBundleBinder>();
        var persistResult = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Blocked);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(persistResult)));

        var services = BuildTextLayerTestServices(
            tenantProfile,
            zeroWordModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "scanned-image-only.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline returns success (BLOCKED is a valid business outcome, not an error)
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline must return success on BLOCKED verdict. Error: {result.Error ?? "<none>"}");

        // Assert — signal is BLOCKED (not Red, not Green)
        var outcome = result.Value!;
        outcome.Summary.Signal.ShouldBe(
            VerdictSignal.Blocked,
            "A zero-word text layer must produce VerdictSignal.Blocked, not Green or Red.");

        // Assert — binder was NOT called (text-layer guard fired before bind stage)
        await binder.DidNotReceive().BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Assert — persist WAS called once with BLOCKED signal (persist is non-optional)
        await verdictPersistence.Received(1).PersistAsync(
            Arg.Any<Guid>(),
            VerdictSignal.Blocked,
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<string>(),
            VerdictSignal.Blocked,
            VerdictSignal.Blocked,
            Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the extractor returns a model with at least <c>MinTextLayerWordCount</c> words,
    /// the pipeline must proceed past the text-layer guard and reach the binder
    /// (guard does NOT fire).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_SufficientWordCountTextLayer_ProceedsToBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile with text-layer floor = 5; model will have 20 words (above floor)
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-TEXT-LAYER-PASS",
            tenantName: "Test Tenant Text Layer Pass",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,   // disable coverage floor
            minTextLayerWordCount: 5);

        // Model with 20 words — well above the floor of 5
        var text = string.Join(' ', System.Linq.Enumerable.Repeat("PALABRA", 20));
        var sufficientWordModel = BuildModelWithText(text);

        // Binder — returns a BLOCKED failure (UnknownProduct) so the test doesn't need
        // a full bundle; we only need to confirm the binder was REACHED (called once).
        var binderBlockError = new BlockedOutcome(BlockReason.UnknownProduct, "No product matched").ToErrorString();
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
                    new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Blocked))));

        var services = BuildTextLayerTestServices(
            tenantProfile,
            sufficientWordModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "sufficient-words-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — binder was reached (text-layer guard did NOT fire)
        await binder.Received(1).BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // The binder returned UnknownProduct BLOCKED → pipeline outcome is still BLOCKED
        // but for a different reason (UnknownProduct, not InsufficientTextLayer).
        // We only need to confirm the binder was reached; the specific signal is a bonus assert.
        result.IsSuccess.ShouldBeTrue("Pipeline should succeed even when binder returns BLOCKED.");
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.Blocked,
            "Binder BLOCKED outcome produces VerdictSignal.Blocked.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> whose header fields are all
    /// <see cref="ExtractionStatus.NotExtracted"/> and whose
    /// <see cref="StatementModel.NormalizedFullText"/> is set to <paramref name="normalizedText"/>.
    /// This isolates the text-layer word count as the only variable under test.
    /// </summary>
    private static StatementModel BuildModelWithText(string normalizedText)
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
            NormalizedFullText = normalizedText,
        };
    }

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> wired for the text-layer density integration tests:
    /// mocked ingestion, a caller-supplied extractor model, a caller-supplied binder mock, and
    /// a caller-supplied persist mock.  Uses the real <c>VerdictAggregator</c>.
    /// Mirrors <c>ExtractionCoverageFloorTests.BuildCoverageTestServices</c> exactly.
    /// </summary>
    private static ServiceCollection BuildTextLayerTestServices(
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
            contentHash: "test-hash-text-layer",
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

        // ── Caller-supplied TenantProfile (carries the text-layer floor) ─────
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

        // ── Stub: alert service (not called on BLOCKED path) ─────────────────
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
