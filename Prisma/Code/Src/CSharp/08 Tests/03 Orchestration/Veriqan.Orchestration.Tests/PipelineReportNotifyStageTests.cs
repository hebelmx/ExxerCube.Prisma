using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
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

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// NSubstitute unit tests for the <c>VerificationPipeline</c> report (stage 9) and
/// notify (stage 10) stages introduced by story VERIQAN-E1-S4.
/// <para>
/// All heavy dependencies are mocked via NSubstitute. The real
/// <c>VerdictAggregator</c> runs so a genuine <see cref="VerdictSignal"/> is computed.
/// <see cref="IMarkedPdfGenerator"/> and <see cref="IVecAlertService"/> are the
/// substituted SUT targets.
/// </para>
/// </summary>
/// <remarks>
/// This class uses the real <c>VerificationPipeline</c> via DI and therefore emits
/// on the global <c>VeriqanMetrics</c> meter — it must run in the
/// <see cref="MetricsIsolationCollection"/> to avoid cross-class measurement interference.
/// </remarks>
[Collection(MetricsIsolationCollection.Name)]
public sealed class PipelineReportNotifyStageTests
{
    // -----------------------------------------------------------------------
    // DI builder — RED / GREEN outcomes (go through engine + verdict aggregator)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> for the full pipeline, wired with:
    /// <list type="bullet">
    ///   <item>Mocked ingestion, extractor, binder, engine → controlled findings list.</item>
    ///   <item>Real <c>VerdictAggregator</c> → genuine <see cref="VerdictSignal"/>.</item>
    ///   <item>In-memory <see cref="IVerdictPersistenceService"/> (success no-op).</item>
    ///   <item>Caller-supplied <see cref="IMarkedPdfGenerator"/> and <see cref="IVecAlertService"/> mocks.</item>
    /// </list>
    /// </summary>
    /// <param name="engineFindings">
    /// Findings the mocked engine returns. Their verdicts determine the aggregated signal.
    /// </param>
    /// <param name="reportGenerator">Caller's <see cref="IMarkedPdfGenerator"/> mock.</param>
    /// <param name="alertService">Caller's <see cref="IVecAlertService"/> mock.</param>
    private static ServiceCollection BuildServices(
        IReadOnlyList<RuleFinding> engineFindings,
        IMarkedPdfGenerator reportGenerator,
        IVecAlertService alertService,
        IReadOnlyDictionary<string, ChecklistTier>? tierMap = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ── Mock: ingestion ──────────────────────────────────────────────────
        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: "test-hash-rn-stage",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

        var ingestion = Substitute.For<IStatementIngestionService>();
        ingestion
            .IngestAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationJob>.WithSuccess(job)));
        services.AddSingleton(ingestion);

        // ── Mock: field extractor ────────────────────────────────────────────
        var locator = FieldLocator.PageHint(1);
        var missingStr = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);
        var statementModel = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr);

        var extractor = Substitute.For<IStatementFieldExtractor>();
        extractor
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<StatementModel>.WithSuccess(statementModel)));
        services.AddSingleton(extractor);

        // ── Mock: bundle binder ──────────────────────────────────────────────
        var bundleMetadata = new BundleMetadata(
            SchemaVersion: "1.0.0",
            Institution: "Test Bank",
            BundleId: "test-bundle-rn",
            GeneratedAt: "2025-01-01T00:00:00Z",
            Period: new PeriodRange("Jan 2025", "2025-01-01", "2025-01-31"),
            Source: new BundleSource("manual", "unit-test", null));
        var product = new VecProduct(
            ProductId: "TC-RN-TEST",
            ProductName: "Tarjeta RN Test",
            Aliases: [],
            HasRewardsProgram: false,
            CardImage: null,
            ImportantMessageImage: null,
            Tariffs: new ProductTariffs(AnnualCommission: 0m, Currency: "MXN", OtherCharges: null));
        var bundle = new VecReferenceBundle(
            BundleMetadata: bundleMetadata,
            Products: [product],
            InterestRates: [],
            ClientAccounts: [],
            ToleranceConfig: null,
            ValidationConstants: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            PriorStatements: null,
            ExpectedTransactions: null);
        var availability = ReferenceDataAvailability.FromBundle(bundle);
        var bindCtx = new VerificationContext(
            bundle: bundle,
            resolvedProduct: product,
            availability: availability,
            priorStatement: null,
            toleranceConfig: null,
            statementModel: statementModel);

        var binder = Substitute.For<IBundleBinder>();
        binder
            .BindAsync(Arg.Any<VerificationJob>(), Arg.Any<StatementContextKey>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationContext>.WithSuccess(bindCtx)));
        services.AddSingleton(binder);

        // ── Mock: validation engine ──────────────────────────────────────────
        services.AddVeriqanVerdict();
        services.AddVeriqanValidation();

        var engine = Substitute.For<IVecValidationEngine>();
        engine
            .RunAsync(Arg.Any<VerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<IReadOnlyList<RuleFinding>>.WithSuccess(engineFindings)));
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // TenantProfile: legal baseline with both floor guards DISABLED (= 0) so neither
        // the extraction-coverage guard nor the text-layer density guard fires on the
        // all-missing / empty-text-layer mock StatementModel.  These tests exercise the
        // report and notify stages, not the guard behaviours; guard behaviour is verified
        // in ExtractionCoverageFloorTests and TextLayerDensityGuardTests respectively.
        services.AddSingleton(new TenantProfile(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0));
        services.AddSingleton(TimeProvider.System);

        // In-memory persistence stubs.
        services.AddVeriqanInMemoryPersistence();

        // Override IVerdictPersistenceService with a success no-op stub so the persist
        // stage does not block report/notify from running.
        var persist = Substitute.For<IVerdictPersistenceService>();
        persist
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<JobVerdict>.WithSuccess(new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Red))));
        services.Replace(ServiceDescriptor.Scoped<IVerdictPersistenceService>(_ => persist));

        // ── Caller-supplied report generator and alert service ────────────────
        services.AddSingleton(reportGenerator);
        services.AddSingleton(alertService);

        // AlertOptions with a test recipient.
        services.AddSingleton<IOptions<AlertOptions>>(
            Options.Create(new AlertOptions { Recipients = ["test@example.com"] }));

        // Metrics + pipeline.
        services.AddSingleton<VeriqanMetrics>();

        // Stub: tier-map provider — returns the caller-supplied map (or empty = single-tier degraded).
        var tierProvider = Substitute.For<IVecReferenceDataProvider>();
        tierProvider
            .GetChecklistTiersAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                    tierMap ?? (new Dictionary<string, ChecklistTier>() as IReadOnlyDictionary<string, ChecklistTier>))));
        services.AddSingleton<IVecReferenceDataProvider>(tierProvider);

        services.AddScoped<IVerificationPipeline, VerificationPipeline>();

        return services;
    }

    // -----------------------------------------------------------------------
    // DI builder — BLOCKED outcome (binder returns a BlockedOutcome failure)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a service collection where the binder returns a BLOCKED outcome.
    /// The pipeline exits the binder stage with BLOCKED, before the engine runs.
    /// </summary>
    private static ServiceCollection BuildBlockedServices(
        IMarkedPdfGenerator reportGenerator,
        IVecAlertService alertService)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ── Mock: ingestion — returns a valid job ──────────────────────────
        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: "test-hash-blocked",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

        var ingestion = Substitute.For<IStatementIngestionService>();
        ingestion
            .IngestAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationJob>.WithSuccess(job)));
        services.AddSingleton(ingestion);

        // ── Mock: field extractor — returns a minimal model ────────────────
        var locator = FieldLocator.PageHint(1);
        var missingStr = ExtractedField<string>.Missing(locator);
        var missingName = ExtractedField<ExtractedClientName>.Missing(locator);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(locator);
        var statementModel = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr);

        var extractor = Substitute.For<IStatementFieldExtractor>();
        extractor
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<StatementModel>.WithSuccess(statementModel)));
        services.AddSingleton(extractor);

        // ── Mock: binder — returns a BLOCKED failure ───────────────────────
        var blockedError = new BlockedOutcome(BlockReason.InvalidBundle, "No matching bundle found").ToErrorString();
        var binder = Substitute.For<IBundleBinder>();
        binder
            .BindAsync(Arg.Any<VerificationJob>(), Arg.Any<StatementContextKey>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationContext>.WithFailure(blockedError)));
        services.AddSingleton(binder);

        // Real verdict aggregator (needed by the pipeline's BLOCKED path).
        services.AddVeriqanVerdict();
        services.AddVeriqanValidation();

        // Engine — not called on the BLOCKED path but required by ctor.
        var engine = Substitute.For<IVecValidationEngine>();
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // TenantProfile: both floor guards disabled (= 0) — the mock model has 0 extracted
        // fields and empty text layer; BLOCKED-from-binder path tests exercise
        // binder/report/notify, not the guard behaviours.
        services.AddSingleton(new TenantProfile(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0));
        services.AddSingleton(TimeProvider.System);
        services.AddVeriqanInMemoryPersistence();

        // Persist stub: success no-op — reached on BLOCKED path (stage 8 is fatal/non-optional
        // on all paths, including BLOCKED). Must return success so report stage can proceed.
        var persist = Substitute.For<IVerdictPersistenceService>();
        persist
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<JobVerdict>.WithSuccess(new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Blocked))));
        services.Replace(ServiceDescriptor.Scoped<IVerdictPersistenceService>(_ => persist));

        services.AddSingleton(reportGenerator);
        services.AddSingleton(alertService);

        services.AddSingleton<IOptions<AlertOptions>>(
            Options.Create(new AlertOptions { Recipients = ["test@example.com"] }));

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

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly byte[] FakePdf =
        System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test");

    private static StatementSubmission MakeSubmission(string fileName = "rn-test.pdf") =>
        new(Pdf: FakePdf, FileName: fileName,
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

    /// <summary>Findings that cause a RED verdict (one Fail, empty/condusef tier map).</summary>
    private static IReadOnlyList<RuleFinding> RedFindings() =>
    [
        RuleFinding.Pass("CL-TEST-P", TechniqueClass.Deterministic, "1.0.0", "ok"),
        RuleFinding.Fail("CL-TEST-F", TechniqueClass.Deterministic, FindingSeverity.Critical,
            "1.0.0", "expected", "observed"),
    ];

    /// <summary>Findings that cause a GREEN verdict (all Pass).</summary>
    private static IReadOnlyList<RuleFinding> GreenFindings() =>
    [
        RuleFinding.Pass("CL-TEST-P", TechniqueClass.Deterministic, "1.0.0", "ok"),
    ];

    /// <summary>
    /// Tier map that, combined with <see cref="RedFindings"/>, produces a YELLOW verdict:
    /// the only failing check is mapped to Bank-only, so condusefFails is empty.
    /// bankTier=Yellow, condusefTier=Green, overall=Yellow.
    /// </summary>
    private static IReadOnlyDictionary<string, ChecklistTier> BankOnlyTierMap =>
        new Dictionary<string, ChecklistTier> { ["CL-TEST-F"] = ChecklistTier.Bank };

    // -----------------------------------------------------------------------
    // Stage 8 (Report) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// RED verdict → <see cref="IMarkedPdfGenerator.Generate"/> called exactly once.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RedVerdict_CallsReportGeneratorExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));

        var services = BuildServices(RedFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on RED verdict.");

        reportGenerator.Received(1).Generate(
            Arg.Any<byte[]>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GREEN verdict → <see cref="IMarkedPdfGenerator.Generate"/> NOT called (no annotation needed).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GreenVerdict_DoesNotCallReportGenerator()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        var alertService = Substitute.For<IVecAlertService>();

        var services = BuildServices(GreenFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on GREEN verdict.");

        reportGenerator.DidNotReceive().Generate(
            Arg.Any<byte[]>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// BLOCKED verdict → <see cref="IMarkedPdfGenerator.Generate"/> IS called (reviewer needs the copy);
    /// <see cref="IVecAlertService.SendRedAlertAsync"/> is NOT called (BLOCKED is not RED).
    /// The BLOCKED path exits the pipeline early (before the engine), so we trigger it via
    /// a binder that returns a <see cref="BlockedOutcome"/> failure.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_BlockedVerdict_CallsReportButNotAlert()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();

        var services = BuildBlockedServices(reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on BLOCKED verdict.");
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.Blocked,
            "The outcome signal must be BLOCKED.");

        // Report: called for BLOCKED
        reportGenerator.Received(1).Generate(
            Arg.Any<byte[]>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<CancellationToken>());

        // Alert: NOT called for BLOCKED
        await alertService.DidNotReceive().SendRedAlertAsync(
            Arg.Any<VerdictSummary>(),
            Arg.Any<AlertContext>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Stage 9 (Notify) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// RED verdict → <see cref="IVecAlertService.SendRedAlertAsync"/> called exactly once.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RedVerdict_CallsAlertServiceExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));

        var services = BuildServices(RedFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on RED verdict.");

        await alertService.Received(1).SendRedAlertAsync(
            Arg.Any<VerdictSummary>(),
            Arg.Any<AlertContext>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GREEN verdict → <see cref="IVecAlertService.SendRedAlertAsync"/> NOT called.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GreenVerdict_DoesNotCallAlertService()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        var alertService = Substitute.For<IVecAlertService>();

        var services = BuildServices(GreenFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on GREEN verdict.");

        await alertService.DidNotReceive().SendRedAlertAsync(
            Arg.Any<VerdictSummary>(),
            Arg.Any<AlertContext>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Best-effort (non-fatal) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <see cref="IMarkedPdfGenerator.Generate"/> returns failure the pipeline must
    /// still return <see cref="Result{T}.IsSuccess"/> — report stage is best-effort / non-fatal.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReportGeneratorFails_PipelineStillSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithFailure("PDF engine crashed"));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));

        var services = BuildServices(RedFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue(
            "A report-generator failure must NOT make the pipeline return failure (best-effort).");

        // The outcome should carry a diagnostic finding for the failure.
        result.Value.ShouldNotBeNull();
        result.Value!.Findings
            .Any(f => f.CheckId == "ReportGenerationFailure")
            .ShouldBeTrue("Outcome findings should include a ReportGenerationFailure diagnostic.");
    }

    /// <summary>
    /// When <see cref="IVecAlertService.SendRedAlertAsync"/> returns failure the pipeline must
    /// still return <see cref="Result{T}.IsSuccess"/> — notify stage is best-effort / non-fatal.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_AlertServiceFails_PipelineStillSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.WithFailure("SMTP unreachable")));

        var services = BuildServices(RedFindings(), reportGenerator, alertService);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue(
            "An alert-service failure must NOT make the pipeline return failure (best-effort).");

        // The outcome should carry a diagnostic finding for the notification failure.
        result.Value.ShouldNotBeNull();
        result.Value!.Findings
            .Any(f => f.CheckId == "NotificationFailure")
            .ShouldBeTrue("Outcome findings should include a NotificationFailure diagnostic.");
    }

    // -----------------------------------------------------------------------
    // Stage 9 + 10 Yellow gate tests (adversarial fix — owner policy)
    // -----------------------------------------------------------------------

    /// <summary>
    /// YELLOW verdict → <see cref="IMarkedPdfGenerator.Generate"/> called exactly once.
    /// Owner policy: Yellow (bank improvement opportunities) must generate a marked-PDF
    /// report just like RED.  Regression guard: before the fix, the report gate only
    /// matched <c>Red or Blocked</c>, silently skipping Yellow.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_YellowVerdict_CallsReportGeneratorExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));

        // Supply a Bank-only tier map so the engine's one Fail → bankTier=Yellow, overall=Yellow.
        var services = BuildServices(RedFindings(), reportGenerator, alertService, BankOnlyTierMap);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on YELLOW verdict.");
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.Yellow,
            "The outcome signal must be YELLOW (pre-condition: tier map correctly wired).");

        // Stage 9 (report): called for YELLOW
        reportGenerator.Received(1).Generate(
            Arg.Any<byte[]>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// YELLOW verdict → <see cref="IVecAlertService.SendRedAlertAsync"/> called exactly once.
    /// Owner policy: Yellow must send an alert (same trigger point as RED; VecAlertService
    /// is responsible for using accurate non-regulatory wording in the email).
    /// Regression guard: before the fix, Stage 10 only matched <c>signal == Red</c>.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_YellowVerdict_CallsAlertServiceExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;

        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));

        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));

        var services = BuildServices(RedFindings(), reportGenerator, alertService, BankOnlyTierMap);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();

        var result = await pipeline.ProcessAsync(MakeSubmission(), ct);

        result.IsSuccess.ShouldBeTrue("Pipeline should succeed on YELLOW verdict.");
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.Yellow,
            "The outcome signal must be YELLOW.");

        // Stage 10 (notify): called for YELLOW
        await alertService.Received(1).SendRedAlertAsync(
            Arg.Is<VerdictSummary>(s => s.Signal == VerdictSignal.Yellow),
            Arg.Any<AlertContext>(),
            Arg.Any<CancellationToken>());
    }
}
