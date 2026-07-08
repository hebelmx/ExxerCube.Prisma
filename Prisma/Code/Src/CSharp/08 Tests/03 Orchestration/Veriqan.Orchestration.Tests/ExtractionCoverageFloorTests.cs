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
/// Unit tests for the minimum-extraction-coverage floor guard introduced by Story E1-S10.
/// <para>
/// Two categories:
/// <list type="number">
///   <item><b>Unit tests on <see cref="VerificationPipeline.CountExtractedFields"/>.</b>
///     These test the counting logic directly without constructing the full pipeline — fast,
///     isolated, no DI required.</item>
///   <item><b>Integration tests on the full pipeline.</b>
///     These wire the real pipeline via DI with a mocked extractor that returns either a
///     near-zero-field or a sufficient-field model, and assert the resulting
///     <see cref="VerdictSignal"/>.</item>
/// </list>
/// </para>
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class ExtractionCoverageFloorTests
{
    // =========================================================================
    // Part 1 — CountExtractedFields unit tests
    // =========================================================================

    /// <summary>
    /// A <see cref="StatementModel"/> in which all 7 header fields are
    /// <see cref="ExtractionStatus.NotExtracted"/> yields a count of zero.
    /// </summary>
    [Fact]
    public void CountExtractedFields_AllHeaderFieldsMissing_ReturnsZero()
    {
        // Arrange — all header fields missing, no PeriodSummary
        var model = BuildAllMissingModel();

        // Act
        var count = VerificationPipeline.CountExtractedFields(model);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>
    /// A model with one header field set to <see cref="ExtractionStatus.Extracted"/>
    /// yields a count of 1.
    /// </summary>
    [Fact]
    public void CountExtractedFields_OneExtractedHeaderField_ReturnsOne()
    {
        // Arrange — only ClientNumber is Extracted; rest are Missing
        var locator = FieldLocator.PageHint(1);
        var missing = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missing,
            cardNumber: missing,
            clabe: missing,
            clientNumber: ExtractedField<string>.Found("12345678", locator),
            rfc: missing);

        // Act
        var count = VerificationPipeline.CountExtractedFields(model);

        // Assert
        count.ShouldBe(1);
    }

    /// <summary>
    /// A field with status <see cref="ExtractionStatus.ExtractedInvalidFormat"/> is
    /// counted (the text layer was readable even though the value is mal-formed).
    /// </summary>
    [Fact]
    public void CountExtractedFields_InvalidFormatField_IsCounted()
    {
        // Arrange — CardNumber is InvalidFormat (16-digit check failed), rest missing
        var locator = FieldLocator.PageHint(1);
        var missing = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missing,
            cardNumber: ExtractedField<string>.InvalidFormat("1234-bad", locator),
            clabe: missing,
            clientNumber: missing,
            rfc: missing);

        // Act
        var count = VerificationPipeline.CountExtractedFields(model);

        // Assert — InvalidFormat counts as extracted
        count.ShouldBe(1);
    }

    /// <summary>
    /// All 7 header fields extracted yields a count of exactly 7.
    /// </summary>
    [Fact]
    public void CountExtractedFields_AllSevenHeaderFieldsExtracted_ReturnsSeven()
    {
        // Arrange
        var locator = FieldLocator.PageHint(1);
        var fullName = new ExtractedClientName("PÉREZ GARCÍA JUAN", "Juan", "Pérez García");
        var fullAddr = new ExtractedAddress("Av. Insurgentes 100, CDMX", null, null, null, null);
        var model = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Found(fullName, locator),
            address: ExtractedField<ExtractedAddress>.Found(fullAddr, locator),
            branchNumber: ExtractedField<string>.Found("910", locator),
            cardNumber: ExtractedField<string>.Found("4111111111111111", locator),
            clabe: ExtractedField<string>.Found("032180000118359719", locator),
            clientNumber: ExtractedField<string>.Found("12345678", locator),
            rfc: ExtractedField<string>.Found("PEGJ800101ABC", locator));

        // Act
        var count = VerificationPipeline.CountExtractedFields(model);

        // Assert
        count.ShouldBe(7);
    }

    /// <summary>
    /// E7.S7.2/S7.3 owner ruling 4 (provenance-aware floor): a <see cref="PeriodSummary.Product"/>
    /// field with status <see cref="ExtractionStatus.ExtractedByInference"/> — the status stamped
    /// on a header-image-OCR-recovered product (<c>StageId.HeaderImageOcr</c>) — must NOT count
    /// toward the extraction-coverage floor, even though every other field is missing. This is the
    /// exact shape of a scanned/image-only document whose only recoverable field is an
    /// OCR-recovered Product: the floor must stay unmet so the pipeline still resolves to
    /// <c>ExtractionGap</c> rather than being pushed over the floor by a second-source inference.
    /// </summary>
    [Fact]
    public void CountExtractedFields_ProductExtractedByInference_DoesNotCountTowardFloor()
    {
        // Arrange — all header fields missing; PeriodSummary.Product is present but
        // ExtractedByInference (OCR-recovered); every other PeriodSummary field is missing.
        var locator = FieldLocator.PageHint(1);
        var missingHeader = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);
        var missingDate = ExtractedField<DateOnly>.Missing(locator);
        var missingInt = ExtractedField<int>.Missing(locator);
        var missingDecimal = ExtractedField<decimal>.Missing(locator);

        var ocrRecoveredProduct = new ExtractedField<string>(
            "Tarjeta de Crédito COSTCO BANAMEX",
            confidence: 0.9,
            locator: locator,
            status: ExtractionStatus.ExtractedByInference,
            provenance: new ExtractionProvenance(StageId.HeaderImageOcr));

        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingHeader,
            cardNumber: missingHeader,
            clabe: missingHeader,
            clientNumber: missingHeader,
            rfc: missingHeader)
        {
            PeriodSummary = new PeriodSummary(
                product: ocrRecoveredProduct,
                periodStart: missingDate,
                periodCutDate: missingDate,
                paymentDueDate: missingDate,
                dayCountPrinted: missingInt,
                dayCount: new DayCountVerification(null, null, false),
                pagoParaNoGenerarIntereses: missingDecimal,
                pagoMinimo: missingDecimal,
                pagoMinimoMasMeses: missingDecimal,
                tasa: missingDecimal,
                cat: missingDecimal,
                saldoDeudorTotal: missingDecimal,
                creditoDisponible: missingDecimal),
        };

        // Act
        var count = VerificationPipeline.CountExtractedFields(model);

        // Assert — the OCR-recovered Product is present but must not be counted.
        count.ShouldBe(0,
            "An ExtractedByInference Product (header-image OCR) must not count toward the " +
            "extraction-coverage floor — the floor measures positional text-layer coverage.");
    }

    // =========================================================================
    // Part 2 — Pipeline integration tests (floor fires / does not fire)
    // =========================================================================

    /// <summary>
    /// When the extractor returns a model with fewer extracted fields than the tenant's
    /// configured floor, the pipeline must return a successful
    /// <see cref="Result{T}"/> whose <see cref="VerdictSignal"/> is
    /// <see cref="VerdictSignal.ExtractionGap"/> with reason
    /// <see cref="BlockReason.InsufficientExtractionCoverage"/> (Story 4.2).
    /// The binder must NOT have been called (guard fires before bind).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_BelowExtractionCoverageFloor_EmitsExtractionGapAndDoesNotReachBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile with floor = 5 (above zero-extraction count of 0)
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-FLOOR",
            tenantName: "Test Tenant Floor",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 5);

        // Extractor returns a model where ALL header fields are Missing → 0 extracted fields
        var zeroFieldModel = BuildAllMissingModel();

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

        var services = BuildCoverageTestServices(
            tenantProfile,
            zeroFieldModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "zero-fields-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline returns success (ExtractionGap is a valid non-verdict outcome)
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline must return success on ExtractionGap verdict. Error: {result.Error ?? "<none>"}");

        // Assert — signal is ExtractionGap (Story 4.2: was Blocked before S4.2)
        var outcome = result.Value!;
        outcome.Summary.Signal.ShouldBe(
            VerdictSignal.ExtractionGap,
            "A near-zero-field extraction must produce VerdictSignal.ExtractionGap (Story 4.2), not Green or Red.");

        // Assert — binder was NOT called (guard fired before bind stage)
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
    /// When the extractor returns a model with AT LEAST as many extracted fields as the
    /// configured floor, the pipeline must proceed past the floor guard and reach
    /// the binder (floor does NOT fire).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_AtOrAboveExtractionCoverageFloor_ProceedsToBinder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // TenantProfile with extraction floor = 3 (below the 7 header fields that are all Extracted)
        // and text-layer floor disabled (= 0) so the text-layer density guard does not fire on the
        // model's empty NormalizedFullText — this test exercises the extraction-coverage floor only.
        var tenantProfile = new TenantProfile(
            tenantId: "TEST-FLOOR-PASS",
            tenantName: "Test Tenant Floor Pass",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 3,
            minTextLayerWordCount: 0);

        // Extractor returns a model with ALL 7 header fields Extracted → count 7 ≥ floor 3
        var locator = FieldLocator.PageHint(1);
        var fullName = new ExtractedClientName("PÉREZ GARCÍA JUAN", "Juan", "Pérez García");
        var fullAddr = new ExtractedAddress("Av. Insurgentes 100, CDMX", null, null, null, null);
        var sufficientModel = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Found(fullName, locator),
            address: ExtractedField<ExtractedAddress>.Found(fullAddr, locator),
            branchNumber: ExtractedField<string>.Found("910", locator),
            cardNumber: ExtractedField<string>.Found("4111111111111111", locator),
            clabe: ExtractedField<string>.Found("032180000118359719", locator),
            clientNumber: ExtractedField<string>.Found("12345678", locator),
            rfc: ExtractedField<string>.Found("PEGJ800101ABC", locator));

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
                    new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.ExtractionGap))));

        var services = BuildCoverageTestServices(
            tenantProfile,
            sufficientModel,
            binder,
            verdictPersistence);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "sufficient-fields-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline reached and called the binder (floor guard did NOT fire)
        await binder.Received(1).BindAsync(
            Arg.Any<VerificationJob>(),
            Arg.Any<StatementContextKey>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // The binder returned UnknownProduct → pipeline outcome is ExtractionGap (Story 4.2)
        // for a different reason (UnknownProduct, not InsufficientExtractionCoverage).
        // We only need to confirm the binder was reached; the specific signal is a bonus assert.
        result.IsSuccess.ShouldBeTrue("Pipeline should succeed even when binder returns ExtractionGap.");
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.ExtractionGap,
            "Binder UnknownProduct produces VerdictSignal.ExtractionGap (Story 4.2).");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a <see cref="StatementModel"/> in which every header field has status
    /// <see cref="ExtractionStatus.NotExtracted"/> and there is no
    /// <see cref="PeriodSummary"/>. Simulates an encrypted or blank PDF.
    /// </summary>
    private static StatementModel BuildAllMissingModel()
    {
        var locator = FieldLocator.PageHint(1);
        return new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(locator),
            address: ExtractedField<ExtractedAddress>.Missing(locator),
            branchNumber: ExtractedField<string>.Missing(locator),
            cardNumber: ExtractedField<string>.Missing(locator),
            clabe: ExtractedField<string>.Missing(locator),
            clientNumber: ExtractedField<string>.Missing(locator),
            rfc: ExtractedField<string>.Missing(locator));
    }

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> wired for the coverage-floor integration tests:
    /// mocked ingestion, a caller-supplied extractor model, a caller-supplied binder mock, and
    /// a caller-supplied persist mock.  Uses the real <c>VerdictAggregator</c>.
    /// </summary>
    private static ServiceCollection BuildCoverageTestServices(
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
            contentHash: "test-hash-coverage-floor",
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

        // Engine — not reached when the floor fires, but required by ctor.
        var engine = Substitute.For<IVecValidationEngine>();
        engine
            .RunAsync(Arg.Any<VerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<IReadOnlyList<RuleFinding>>.WithSuccess(Array.Empty<RuleFinding>())));
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // ── Caller-supplied TenantProfile (carries the coverage floor) ───────
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
