using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Integration tests for <see cref="VecValidationEngine"/> covering:
/// DI assembly-scan discovery (FR-6 base), determinism (NFR-5),
/// batch isolation (NFR-6), InsufficientData path (FR-20), factory contracts,
/// and cancellation.
/// </summary>
public sealed class VecValidationEngineTests
{
    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata MinimalMetadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct OneProduct() =>
        new(
            ProductId: "TC-TEST",
            ProductName: "Test Product",
            Aliases: null,
            HasRewardsProgram: false,
            CardImage: null,
            ImportantMessageImage: null,
            Tariffs: null);

    /// <summary>
    /// Builds a bundle with NO interest-rate section so that
    /// <see cref="ReferenceCapability.Rate"/> is InsufficientData.
    /// </summary>
    private static VecReferenceBundle BundleWithoutRate() =>
        new(
            BundleMetadata: MinimalMetadata(),
            Products: [OneProduct()],
            InterestRates: null,           // ← Rate capability absent
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    /// <summary>
    /// Builds a bundle WITH an interest-rate section so that
    /// <see cref="ReferenceCapability.Rate"/> is Available.
    /// </summary>
    private static VecReferenceBundle BundleWithRate() =>
        new(
            BundleMetadata: MinimalMetadata(),
            Products: [OneProduct()],
            InterestRates:
            [
                new InterestRateEntry("TC-TEST",
                [
                    new RateByPeriod(0.1975m, "Sep Oct", "2025-09-01", "2025-10-31")
                ])
            ],
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext BuildContext(VecReferenceBundle bundle) =>
        new(
            bundle: bundle,
            resolvedProduct: OneProduct(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: null);

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> with <see cref="AddVeriqanValidation"/> applied,
    /// which exercises the Scrutor assembly scan path.
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        return services.BuildServiceProvider();
    }

    // -----------------------------------------------------------------------
    // Test 1: DI discovery — Scrutor finds and registers all Story 4.2 rules
    // -----------------------------------------------------------------------

    /// <summary>
    /// After <see cref="AddVeriqanValidation"/> the engine discovers and runs all rules
    /// registered via Scrutor scan and returns a finding per rule (CL-10, CL-18 through CL-26).
    /// </summary>
    [Fact]
    public async Task Engine_DiscoversAndRunsRegisteredRules_ReturnsFindingPerRule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Engine failed: {result.Error}");
        var findings = result.Value!;

        // Story 4.2 ships 10 rules: CL-10, CL-18, CL-19, CL-20, CL-21, CL-22, CL-23, CL-24, CL-25, CL-26
        findings.Count.ShouldBeGreaterThanOrEqualTo(10);

        // All Story 4.2 check-ids must appear
        var checkIds = findings.Select(f => f.CheckId).ToList();
        checkIds.ShouldContain("CL-10");
        checkIds.ShouldContain("CL-18");
        checkIds.ShouldContain("CL-19");
        checkIds.ShouldContain("CL-20");
        checkIds.ShouldContain("CL-21");
        checkIds.ShouldContain("CL-22");
        checkIds.ShouldContain("CL-23");
        checkIds.ShouldContain("CL-24");
        checkIds.ShouldContain("CL-25");
        checkIds.ShouldContain("CL-26");
    }

    // -----------------------------------------------------------------------
    // Test 2: Determinism — same context, same output twice (NFR-5)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Running the engine twice with the same context produces identical findings
    /// in the same order (NFR-5: determinism).
    /// </summary>
    [Fact]
    public async Task Engine_SameContextTwice_ProducesIdenticalFindings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var first = (await engine.RunAsync(ctx, ct)).Value!;
        var second = (await engine.RunAsync(ctx, ct)).Value!;

        first.Count.ShouldBe(second.Count);

        for (var i = 0; i < first.Count; i++)
        {
            first[i].CheckId.ShouldBe(second[i].CheckId,
                $"Index {i}: CheckId mismatch — order is not deterministic");
            first[i].Verdict.ShouldBe(second[i].Verdict,
                $"Index {i}: Verdict changed between identical runs");
        }

        // Verify that CheckIds are in ascending ordinal order
        var checkIds = first.Select(f => f.CheckId).ToList();
        var sorted = checkIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        checkIds.ShouldBe(sorted, "Findings must be sorted by CheckId (NFR-5)");
    }

    // -----------------------------------------------------------------------
    // Test 3: Batch isolation — a rule returning failure does NOT abort the batch (NFR-6)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When one of the registered rules returns a failure <see cref="Result{T}"/>,
    /// the engine still returns findings for the other rules (NFR-6 batch isolation).
    /// </summary>
    [Fact]
    public async Task Engine_RuleReturningFailure_DoesNotAbortBatch_OtherRulesStillRun()
    {
        var ct = TestContext.Current.CancellationToken;

        // Build a service collection with the real engine + one failing rule + one passing rule
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();
        services.AddTransient<IVecValidationRule, AlwaysFailResultRule>();
        services.AddTransient<IVecValidationRule, AlwaysPassInlineRule>();

        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue("Engine should succeed even when a rule returns failure");
        var findings = result.Value!;

        // Both rules must have produced a finding (batch not aborted)
        findings.Count.ShouldBe(2);

        // The failing rule must produce an InsufficientData finding (not a hard failure)
        var failingFinding = findings.Single(f => f.CheckId == AlwaysFailResultRule.Id);
        failingFinding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A rule that returns failure Result must be captured as InsufficientData");

        // The passing rule must still produce a Pass finding
        var passFinding = findings.Single(f => f.CheckId == AlwaysPassInlineRule.Id);
        passFinding.Verdict.ShouldBe(FindingVerdict.Pass,
            "A passing rule must not be affected by another rule's failure");
    }

    // -----------------------------------------------------------------------
    // Test 4: InsufficientData path — null ToleranceConfig (FR-20, ADR-V3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Story 9.4: arithmetic rules no longer require a non-null <c>ToleranceConfig</c>;
    /// the legal default is applied when the bundle has no override.
    /// When <c>statementModel</c> is also null (as here), all arithmetic rules emit
    /// <see cref="FindingVerdict.InsufficientData"/> because of the missing statement model,
    /// not because of the missing tolerance config.
    /// FR-20: missing required reference data (statement model) still blocks evaluation.
    /// </summary>
    [Fact]
    public async Task Engine_NullToleranceConfig_NoStatementModel_AllArithmeticRulesEmitInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        // Bundle without ToleranceConfig; statementModel is also null in BuildContext.
        // Story 9.4: legal default tolerance applies automatically; InsufficientData here
        // is caused by the absent StatementModel, not the absent ToleranceConfig.
        var ctx = BuildContext(BundleWithoutRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var findings = result.Value!;

        // All arithmetic rules must still emit InsufficientData (due to null StatementModel).
        foreach (var arithmeticId in new[] { "CL-10", "CL-18", "CL-19", "CL-20", "CL-21", "CL-22", "CL-23", "CL-24", "CL-25", "CL-26" })
        {
            var finding = findings.SingleOrDefault(f => f.CheckId == arithmeticId);
            finding.ShouldNotBeNull($"Rule {arithmeticId} must have been discovered and run");
            finding!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
                $"Rule {arithmeticId} must emit InsufficientData when StatementModel is absent");
        }
    }

    // -----------------------------------------------------------------------
    // Test 5: RuleFinding factory contracts
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="RuleFinding.Pass"/>, <see cref="RuleFinding.Fail"/>,
    /// and <see cref="RuleFinding.InsufficientData"/> factories populate all fields correctly.
    /// </summary>
    [Fact]
    public void RuleFinding_Factories_SetVerdictAndFields()
    {
        const string checkId = "CL-FACTORY-TEST";
        const string version = "2.0.0";
        const decimal tolerance = 0.50m;

        // Pass factory
        var pass = RuleFinding.Pass(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            engineVersion: version,
            observed: "100.00",
            toleranceApplied: tolerance,
            locator: Domain.Extraction.FieldLocator.PageHint(2));

        pass.CheckId.ShouldBe(checkId);
        pass.Verdict.ShouldBe(FindingVerdict.Pass);
        pass.Technique.ShouldBe(TechniqueClass.Deterministic);
        pass.Severity.ShouldBe(FindingSeverity.Info);
        pass.EngineVersion.ShouldBe(version);
        pass.Observed.ShouldBe("100.00");
        pass.ToleranceApplied.ShouldBe(tolerance);
        pass.Locator.ShouldNotBeNull();
        pass.Locator!.PageNumber.ShouldBe(2);
        pass.Expected.ShouldBeNull();

        // Fail factory
        var fail = RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.LightweightCv,
            severity: FindingSeverity.Critical,
            engineVersion: version,
            expected: "200.00",
            observed: "150.00",
            toleranceApplied: tolerance);

        fail.CheckId.ShouldBe(checkId);
        fail.Verdict.ShouldBe(FindingVerdict.Fail);
        fail.Technique.ShouldBe(TechniqueClass.LightweightCv);
        fail.Severity.ShouldBe(FindingSeverity.Critical);
        fail.EngineVersion.ShouldBe(version);
        fail.Expected.ShouldBe("200.00");
        fail.Observed.ShouldBe("150.00");
        fail.ToleranceApplied.ShouldBe(tolerance);
        fail.Locator.ShouldBeNull();

        // InsufficientData factory
        var insufficient = RuleFinding.InsufficientData(
            checkId: checkId,
            technique: TechniqueClass.Ml,
            engineVersion: version,
            reason: "model-not-loaded");

        insufficient.CheckId.ShouldBe(checkId);
        insufficient.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        insufficient.Technique.ShouldBe(TechniqueClass.Ml);
        insufficient.Severity.ShouldBe(FindingSeverity.Warning);
        insufficient.EngineVersion.ShouldBe(version);
        insufficient.Observed.ShouldBe("model-not-loaded");
        insufficient.Expected.ShouldBeNull();
        insufficient.ToleranceApplied.ShouldBeNull();
        insufficient.Locator.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Test 6: Cancellation — pre-cancelled token → cancelled result
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the <see cref="CancellationToken"/> is already cancelled before
    /// <see cref="IVecValidationEngine.RunAsync"/> is called, the engine returns a
    /// cancelled result without running any rules.
    /// </summary>
    [Fact]
    public async Task Engine_Cancelled_ReturnsCancelled()
    {
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await engine.RunAsync(ctx, cts.Token);

        result.IsFailure.ShouldBeTrue("A cancelled run must return a failure result");
        result.IsCancelled().ShouldBeTrue("The result must be specifically marked as cancelled");
        result.Value.ShouldBeNull("No findings should be returned on cancellation");
    }

    // -----------------------------------------------------------------------
    // Helper rules used only by the isolation test (Test 3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Inline rule that always returns a failure <see cref="Result{T}"/> (not InsufficientData —
    /// a genuine internal error path) to exercise engine batch isolation (NFR-6).
    /// </summary>
    private sealed class AlwaysFailResultRule : IVecValidationRule
    {
        public const string Id = "TEST-FAIL-RESULT";

        public string CheckId => Id;
        public string DofNumeral => "Acuerdo §1";
        public RuleClassification Classification => RuleClassification.BaselineLocked;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default) =>
            Result<RuleFinding>.WithFailure("simulated rule internal error");
    }

    /// <summary>
    /// Inline rule that always returns Pass — used alongside <see cref="AlwaysFailResultRule"/>
    /// to confirm the passing rule's finding is present even after the other rule fails.
    /// </summary>
    private sealed class AlwaysPassInlineRule : IVecValidationRule
    {
        public const string Id = "TEST-ALWAYS-PASS";

        public string CheckId => Id;
        public string DofNumeral => "Acuerdo §2";
        public RuleClassification Classification => RuleClassification.BaselineLocked;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default) =>
            Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: "test-1.0.0"));
    }

    // -----------------------------------------------------------------------
    // Story 4.1: engine stamps finding.Confidence from ctx.ConsumedConfidenceOrFull()
    // -----------------------------------------------------------------------

    /// <summary>
    /// A rule that never calls <c>ConfidenceBelowThreshold</c> (no guarded fields)
    /// must receive Confidence = 1.0 — "full confidence by default".
    /// </summary>
    [Fact]
    public async Task Engine_RuleThatConsumesNoFields_StampsFullConfidence()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();
        services.AddTransient<IVecValidationRule, NoFieldConsumerRule>();

        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!.Single(f => f.CheckId == NoFieldConsumerRule.Id);
        finding.Confidence.ShouldBe(1.0,
            "A rule that consumes no guarded fields must receive Confidence = 1.0");
    }

    /// <summary>
    /// A rule that consumes a field at confidence 0.82 via
    /// <c>ctx.ConfidenceBelowThreshold</c> (field passes the threshold) must receive
    /// Confidence ≈ 0.82 stamped by the engine.
    /// </summary>
    [Fact]
    public async Task Engine_RuleThatConsumesFieldAt0_82_StampsConfidence0_82()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();
        services.AddTransient<IVecValidationRule, FieldAt082ConsumerRule>();

        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!.Single(f => f.CheckId == FieldAt082ConsumerRule.Id);
        finding.Confidence.ShouldBe(0.82,
            "Engine must stamp Confidence = 0.82 from the single field consumed at that confidence");
    }

    /// <summary>
    /// A rule that abstains (InsufficientData) because a field is below threshold must
    /// carry the low field confidence in the finding's Confidence property.
    /// </summary>
    [Fact]
    public async Task Engine_AbstainRule_LowConfidenceField_StampsLowConfidence()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();
        services.AddTransient<IVecValidationRule, LowConfidenceAbstainRule>();

        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext(BundleWithRate());

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!.Single(f => f.CheckId == LowConfidenceAbstainRule.Id);
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Rule must abstain because the field confidence is below the threshold");
        finding.Confidence.ShouldBe(0.40,
            "Engine must stamp the low field confidence even on an abstain finding");
    }

    // -----------------------------------------------------------------------
    // Inline helper rules for Story 4.1 engine-stamp tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// A rule that passes without consuming any guarded field — exercises the "no field consumed → 1.0" path.
    /// </summary>
    private sealed class NoFieldConsumerRule : IVecValidationRule
    {
        public const string Id = "TEST-NO-FIELD-CONSUMER";

        public string CheckId => Id;
        public string DofNumeral => "Acuerdo §1";
        public RuleClassification Classification => RuleClassification.BaselineLocked;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default) =>
            // Deliberately never calls ctx.ConfidenceBelowThreshold — no fields consumed.
            Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(CheckId, Technique, "test-1.0.0"));
    }

    /// <summary>
    /// A rule that consumes one field at confidence 0.82 (above threshold) and passes.
    /// </summary>
    private sealed class FieldAt082ConsumerRule : IVecValidationRule
    {
        public const string Id = "TEST-FIELD-AT-082";

        private const double TestConfidence = 0.82;
        private const double Threshold = 0.80;

        public string CheckId => Id;
        public string DofNumeral => "Acuerdo §1";
        public RuleClassification Classification => RuleClassification.BaselineLocked;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
        {
            // Construct a field at confidence 0.82 — passes the 0.80 threshold.
            var field = new Domain.Extraction.ExtractedField<decimal>(
                value: 42m,
                confidence: TestConfidence,
                locator: Domain.Extraction.FieldLocator.PageHint(1),
                status: Domain.Extraction.ExtractionStatus.Extracted);

            if (ctx.ConfidenceBelowThreshold(field, Threshold))
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.InsufficientData(CheckId, Technique, "test-1.0.0", "low confidence"));

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(CheckId, Technique, "test-1.0.0", observed: "42"));
        }
    }

    /// <summary>
    /// A rule that consumes one field at confidence 0.40 (below default threshold 0.80)
    /// and abstains (InsufficientData).
    /// </summary>
    private sealed class LowConfidenceAbstainRule : IVecValidationRule
    {
        public const string Id = "TEST-LOW-CONFIDENCE-ABSTAIN";

        private const double TestConfidence = 0.40;
        private const double Threshold = 0.80;

        public string CheckId => Id;
        public string DofNumeral => "Acuerdo §1";
        public RuleClassification Classification => RuleClassification.BaselineLocked;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
        {
            var field = new Domain.Extraction.ExtractedField<decimal>(
                value: 7m,
                confidence: TestConfidence,
                locator: Domain.Extraction.FieldLocator.PageHint(1),
                status: Domain.Extraction.ExtractionStatus.Extracted);

            if (ctx.ConfidenceBelowThreshold(field, Threshold))
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.InsufficientData(CheckId, Technique, "test-1.0.0",
                        reason: ConfidenceGuard.Reason("FIELD", TestConfidence, Threshold)));

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(CheckId, Technique, "test-1.0.0", observed: "7"));
        }
    }
}
