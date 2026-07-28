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
/// NSubstitute unit tests for the <c>VerificationPipeline</c> persist stage (stage 8).
/// <para>
/// These tests verify pipeline wiring behaviour WITHOUT a database and WITHOUT a real PDF —
/// all heavy dependencies (ingestion, extractor, binder, engine) are mocked via NSubstitute.
/// The real <c>VerdictAggregator</c> runs so a genuine <see cref="Domain.Enums.VerdictSignal"/>
/// is computed; only the <see cref="IVerdictPersistenceService"/> is the substituted SUT.
/// </para>
/// </summary>
[Collection(MetricsIsolationCollection.Name)]
public sealed class PipelinePersistStageTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Initialises the test class.</summary>
    public PipelinePersistStageTests(ITestOutputHelper output)
    {
        _output = output;
        _ = _output; // suppress unused-field warning; may be used in future log assertions
    }

    // -----------------------------------------------------------------------
    // DI helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> wired for a happy-path pipeline run with:
    /// <list type="bullet">
    ///   <item>Mocked ingestion → returns a stub <see cref="VerificationJob"/>.</item>
    ///   <item>Mocked extractor → returns a stub <see cref="StatementModel"/> (all fields missing).</item>
    ///   <item>Mocked binder → returns a <see cref="VerificationContext"/> with a minimal bundle.</item>
    ///   <item>Mocked engine → returns two <see cref="RuleFinding"/> items (one Pass, one Fail).</item>
    ///   <item>Real <c>VerdictAggregator</c> — produces a genuine <see cref="VerdictSignal"/>.</item>
    ///   <item>Caller-supplied <see cref="IVerdictPersistenceService"/> mock.</item>
    /// </list>
    /// </summary>
    private ServiceCollection BuildServices(IVerdictPersistenceService verdictPersistence)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ── Mock: ingestion ──────────────────────────────────────────────────
        var jobId = Guid.NewGuid();
        var job = new VerificationJob(
            id: jobId,
            contentHash: "test-hash-persist-stage",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

        var ingestion = Substitute.For<IStatementIngestionService>();
        ingestion
            .IngestAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationJob>.WithSuccess(job)));
        services.AddSingleton(ingestion);

        // ── Mock: field extractor ────────────────────────────────────────────
        // StatementModel with all fields "missing" (no actual field values needed — the
        // engine is also mocked, so this model is only used to satisfy the VerificationContext ctor).
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
        // Minimal bundle + product so VerificationContext ctor is satisfied.
        var bundleMetadata = new BundleMetadata(
            SchemaVersion: "1.0.0",
            Institution: "Test Bank",
            BundleId: "test-bundle-persist",
            GeneratedAt: "2025-01-01T00:00:00Z",
            Period: new PeriodRange("Jan 2025", "2025-01-01", "2025-01-31"),
            Source: new BundleSource("manual", "unit-test", null));
        var product = new VecProduct(
            ProductId: "TC-PERSIST-TEST",
            ProductName: "Tarjeta Persist Test",
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
        // Two findings: Pass + Fail — so the aggregator produces a RED verdict.
        IReadOnlyList<RuleFinding> ruleFindings =
        [
            RuleFinding.Pass("CL-TEST-P", TechniqueClass.Deterministic, "1.0.0", "ok"),
            RuleFinding.Fail("CL-TEST-F", TechniqueClass.Deterministic, FindingSeverity.Critical,
                "1.0.0", "expected", "observed"),
        ];

        // Real VerdictAggregator (produces genuine VerdictSignal from the two findings above).
        services.AddVeriqanVerdict();

        // Real TenantProfileResolver + LegalToleranceProvider (from Validation layer).
        // AddVeriqanValidation also registers IVecValidationEngine — we Replace it with our mock below.
        services.AddVeriqanValidation();

        // Replace the real engine with a mock that returns the pre-built findings.
        var engine = Substitute.For<IVecValidationEngine>();
        engine
            .RunAsync(Arg.Any<VerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<IReadOnlyList<RuleFinding>>.WithSuccess(ruleFindings)));
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // Active TenantProfile: legal baseline (no overrides → no deviations).
        // Both floor guards are disabled (= 0) so the all-missing / empty-text-layer mock
        // StatementModel does not trigger them.  These tests exercise the persist stage, not
        // the guard behaviours (those are in ExtractionCoverageFloorTests / TextLayerDensityGuardTests).
        services.AddSingleton(new TenantProfile(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0));
        services.AddSingleton(TimeProvider.System);

        // In-memory persistence stubs for IVerificationJobRepository, IDispositionRepository.
        services.AddVeriqanInMemoryPersistence();

        // Override IVerdictPersistenceService with the caller's mock.
        services.Replace(ServiceDescriptor.Scoped<IVerdictPersistenceService>(
            _ => verdictPersistence));

        // ── Stub: report generator (best-effort stage — returns success, no-op) ──────
        var reportGenerator = Substitute.For<IMarkedPdfGenerator>();
        reportGenerator
            .Generate(Arg.Any<byte[]>(), Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<byte[]>.WithSuccess(Array.Empty<byte>()));
        services.AddSingleton(reportGenerator);

        // ── Stub: alert service (best-effort stage — returns success, no-op) ─────────
        var alertService = Substitute.For<IVecAlertService>();
        alertService
            .SendRedAlertAsync(Arg.Any<Application.Verdict.VerdictSummary>(), Arg.Any<AlertContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success()));
        services.AddSingleton(alertService);

        // ── AlertOptions: empty recipients (no SMTP needed in unit tests) ────────────
        services.AddSingleton<IOptions<AlertOptions>>(
            Options.Create(new AlertOptions()));

        // Metrics + pipeline.
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

    /// <summary>
    /// Builds on <see cref="BuildServices"/> but wires a real (non-null) reference bundle into
    /// <see cref="IVecReferenceDataProvider.GetBundleAsync"/> — mirroring the pipeline's Stage 1b
    /// catalog pre-resolve success path (VerificationPipeline.cs ~line 199-210) — so the
    /// <c>referenceBundleVersion</c> threaded into <see cref="IVerdictPersistenceService.PersistAsync"/>
    /// (VERIQAN-E3-S4) can be asserted by the caller against <paramref name="expectedReferenceBundleVersion"/>.
    /// </summary>
    private ServiceCollection BuildServicesWithResolvedCatalogBundle(
        IVerdictPersistenceService verdictPersistence,
        out string expectedReferenceBundleVersion)
    {
        var services = BuildServices(verdictPersistence);

        var bundleMetadata = new BundleMetadata(
            SchemaVersion: "2.3.1-catalog-test",
            Institution: "Test Bank",
            BundleId: "catalog-pre-resolve-bundle",
            GeneratedAt: "2025-01-01T00:00:00Z",
            Period: null,
            Source: null);
        var bundle = new VecReferenceBundle(
            BundleMetadata: bundleMetadata,
            Products: [],
            InterestRates: [],
            ClientAccounts: [],
            ToleranceConfig: null,
            ValidationConstants: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            PriorStatements: null,
            ExpectedTransactions: null);
        // The pipeline stamps BundleId in preference to SchemaVersion (SchemaVersion is a schema
        // constant shared by all bundles; BundleId discriminates the bundle instance — RC6 W2.4).
        expectedReferenceBundleVersion = bundleMetadata.BundleId!;

        // Replace the tier-map provider with one that ALSO resolves a catalog bundle
        // (GetBundleAsync) — BuildServices' provider only stubs GetChecklistTiersAsync, leaving
        // GetBundleAsync unconfigured (NSubstitute default → catalogBundle stays null downstream).
        var tierProvider = Substitute.For<IVecReferenceDataProvider>();
        tierProvider
            .GetChecklistTiersAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                    new Dictionary<string, ChecklistTier>() as IReadOnlyDictionary<string, ChecklistTier>)));
        tierProvider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VecReferenceBundle>.WithSuccess(bundle)));
        services.Replace(ServiceDescriptor.Singleton<IVecReferenceDataProvider>(_ => tierProvider));

        // BuildServices' extractor mock only matches a null 3rd argument (the default the
        // pipeline supplies when no catalog bundle resolves) — now that GetBundleAsync resolves a
        // real bundle, re-stub with a matcher accepting any catalog bundle so extraction still
        // succeeds regardless of what Stage 1b resolved.
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
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>(), Arg.Any<VecReferenceBundle?>())
            .Returns(ci => Task.FromResult(Result<StatementModel>.WithSuccess(statementModel)));
        services.Replace(ServiceDescriptor.Singleton<IStatementFieldExtractor>(_ => extractor));

        return services;
    }

    /// <summary>
    /// Builds a <see cref="ServiceCollection"/> where the binder returns a BLOCKED outcome,
    /// wired with a caller-supplied <see cref="IVerdictPersistenceService"/> so persist
    /// behaviour can be asserted or injected with a failure.
    /// </summary>
    private static ServiceCollection BuildBlockedServices(IVerdictPersistenceService verdictPersistence)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ── Mock: ingestion ──────────────────────────────────────────────────
        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: "test-hash-blocked-persist",
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

        // ── Mock: binder — returns a BLOCKED failure ─────────────────────────
        var blockedError = new BlockedOutcome(BlockReason.InvalidBundle, "No matching bundle found").ToErrorString();
        var binder = Substitute.For<IBundleBinder>();
        binder
            .BindAsync(Arg.Any<VerificationJob>(), Arg.Any<StatementContextKey>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<VerificationContext>.WithFailure(blockedError)));
        services.AddSingleton(binder);

        // Real verdict aggregator (used by the BLOCKED path to aggregate the BLOCKED verdict).
        services.AddVeriqanVerdict();
        services.AddVeriqanValidation();

        // Engine — not called on BLOCKED path, but required by ctor.
        var engine = Substitute.For<IVecValidationEngine>();
        services.Replace(ServiceDescriptor.Singleton<IVecValidationEngine>(_ => engine));

        // Both floor guards disabled so the empty-text-layer mock model does not trigger them.
        services.AddSingleton(new TenantProfile(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: null,
            minFieldConfidence: TenantProfile.LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: 0,
            minTextLayerWordCount: 0));
        services.AddSingleton(TimeProvider.System);
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
        services.AddSingleton(alertService);

        services.AddSingleton<IOptions<AlertOptions>>(
            Options.Create(new AlertOptions()));

        // Metrics + pipeline.
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
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Happy path: when all earlier stages succeed and the persist service returns success,
    /// <see cref="IVerdictPersistenceService.PersistAsync"/> is called exactly once and the
    /// pipeline returns a successful <see cref="VerificationOutcome"/>.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_AllStagesSucceed_CallsPersistExactlyOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var fakeVerdict = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Red);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(fakeVerdict)));

        var services = BuildServices(verdictPersistence);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "persist-stage-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline returns success
        result.IsSuccess.ShouldBeTrue("Pipeline should return success when all stages succeed.");
        result.Value.ShouldNotBeNull();

        // Assert — persist was called exactly once
        await verdictPersistence.Received(1).PersistAsync(
            Arg.Any<Guid>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Any<string>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Persist-failure guard: when <see cref="IVerdictPersistenceService.PersistAsync"/> returns
    /// a failure, the pipeline must propagate that failure (not swallow it and return success).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_PersistFails_PipelineReturnsFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string persistError = "DB connection lost during persist";

        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithFailure(persistError)));

        var services = BuildServices(verdictPersistence);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "persist-fail-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline propagates the persist failure
        result.IsFailure.ShouldBeTrue(
            "Pipeline must return failure when IVerdictPersistenceService returns failure.");
        result.Error.ShouldNotBeNull();
        (result.Error!.Contains(persistError)).ShouldBeTrue(
            $"Pipeline failure message '{result.Error}' must include the persist-service error '{persistError}'.");
    }

    // -----------------------------------------------------------------------
    // BLOCKED-path persist tests (fix for adversarial defect: persist was not
    // called on the binder-BLOCKED early-exit path — VERIQAN-E1-S5 invariant).
    // -----------------------------------------------------------------------

    /// <summary>
    /// ExtractionGap-from-binder path (Story 4.2): <see cref="IVerdictPersistenceService.PersistAsync"/>
    /// must be called exactly once even when the pipeline exits early via the binder
    /// <see cref="BlockReason.InvalidBundle"/> outcome (which now routes to ExtractionGap).
    /// This verifies the VERIQAN-E1-S5 invariant ("persist is non-optional") on the ExtractionGap path.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ExtractionGapFromBinder_CallsPersistExactlyOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var fakeVerdict = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.ExtractionGap);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(fakeVerdict)));

        var services = BuildBlockedServices(verdictPersistence);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "blocked-persist-once-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline returns success with ExtractionGap signal (Story 4.2)
        result.IsSuccess.ShouldBeTrue("Pipeline should return success on ExtractionGap verdict.");
        result.Value.ShouldNotBeNull();
        result.Value!.Summary.Signal.ShouldBe(VerdictSignal.ExtractionGap,
            "The outcome signal must be ExtractionGap (Story 4.2 — InvalidBundle routes to ExtractionGap).");

        // Assert — persist was called exactly once with ExtractionGap signal
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
    /// BLOCKED-from-binder persist-failure guard: when
    /// <see cref="IVerdictPersistenceService.PersistAsync"/> returns a failure on the BLOCKED
    /// path, the pipeline must propagate that failure — persist is fatal on ALL paths.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_BlockedFromBinder_PersistFails_PipelineReturnsFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string persistError = "DB connection lost during blocked persist";

        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithFailure(persistError)));

        var services = BuildBlockedServices(verdictPersistence);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "blocked-persist-fail-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline propagates the persist failure on the BLOCKED path
        result.IsFailure.ShouldBeTrue(
            "Pipeline must return failure when IVerdictPersistenceService returns failure on BLOCKED path.");
        result.Error.ShouldNotBeNull();
        (result.Error!.Contains(persistError)).ShouldBeTrue(
            $"Pipeline failure message '{result.Error}' must include the persist-service error '{persistError}'.");
    }

    // -----------------------------------------------------------------------
    // VERIQAN-E3-S4: EngineVersion / ReferenceBundleVersion provenance threading.
    // -----------------------------------------------------------------------

    /// <summary>
    /// VERIQAN-E3-S4: when the reference-data catalog pre-resolve stage (Stage 1b) resolves a
    /// bundle, <see cref="VerificationPipeline"/> threads that bundle's
    /// <c>BundleMetadata.SchemaVersion</c> into <see cref="IVerdictPersistenceService.PersistAsync"/>
    /// as <c>referenceBundleVersion</c>, and always supplies a non-null, non-empty
    /// <c>engineVersion</c> (the pipeline's assembly version).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CatalogBundleResolved_PersistAsyncReceivesEngineVersionAndReferenceBundleVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var fakeVerdict = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Red);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(fakeVerdict)));

        var services = BuildServicesWithResolvedCatalogBundle(verdictPersistence, out var expectedReferenceBundleVersion);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "persist-stage-catalog-bundle-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert — pipeline still returns success
        result.IsSuccess.ShouldBeTrue("Pipeline should return success when all stages succeed.");

        // Assert — PersistAsync was called with the resolved bundle's SchemaVersion as
        // referenceBundleVersion, and a non-null/non-empty engineVersion.
        await verdictPersistence.Received(1).PersistAsync(
            Arg.Any<Guid>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Is<string>(v => !string.IsNullOrWhiteSpace(v)),
            Arg.Any<VerdictSignal>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
            Arg.Any<CancellationToken>(),
            expectedReferenceBundleVersion);
    }

    /// <summary>
    /// VERIQAN-E3-S4 graceful-degradation path: when the reference-data catalog pre-resolve stage
    /// does NOT resolve a bundle (the default in <see cref="BuildServices"/>, where
    /// <c>GetBundleAsync</c> is left unconfigured), <see cref="IVerdictPersistenceService.PersistAsync"/>
    /// is still called with a non-null <c>engineVersion</c> but a <see langword="null"/>
    /// <c>referenceBundleVersion</c> — the pipeline must not fault just because no bundle resolved.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_NoCatalogBundleResolved_PersistAsyncReceivesNullReferenceBundleVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var fakeVerdict = new JobVerdict(Guid.NewGuid(), Guid.NewGuid(), VerdictSignal.Red);
        var verdictPersistence = Substitute.For<IVerdictPersistenceService>();
        verdictPersistence
            .PersistAsync(Arg.Any<Guid>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(), Arg.Any<string>(),
                Arg.Any<VerdictSignal>(), Arg.Any<VerdictSignal>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
                Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult(Result<JobVerdict>.WithSuccess(fakeVerdict)));

        var services = BuildServices(verdictPersistence);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
        var submission = new StatementSubmission(
            Pdf: System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 unit-test"),
            FileName: "persist-stage-no-catalog-bundle-test.pdf",
            ContextKey: new StatementContextKey("Test Bank", "Jan 2025"));

        // Act
        var result = await pipeline.ProcessAsync(submission, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue("Pipeline should return success when all stages succeed.");

        await verdictPersistence.Received(1).PersistAsync(
            Arg.Any<Guid>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyList<RuleFinding>>(),
            Arg.Is<string>(v => !string.IsNullOrWhiteSpace(v)),
            Arg.Any<VerdictSignal>(),
            Arg.Any<VerdictSignal>(),
            Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>(),
            Arg.Any<CancellationToken>(),
            (string?)null);
    }
}
