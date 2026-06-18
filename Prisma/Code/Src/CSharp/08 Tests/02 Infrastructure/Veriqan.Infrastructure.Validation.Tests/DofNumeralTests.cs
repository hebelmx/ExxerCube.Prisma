using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Tests for Story 9.2: DOF Acuerdo numeral on findings (NFR-7 auditability).
/// Covers:
/// <list type="bullet">
///   <item>Engine stamps <see cref="RuleFinding.DofNumeral"/> from <see cref="IVecValidationRule.DofNumeral"/>.</item>
///   <item>Coverage map returned by <see cref="IVecValidationEngine.GetCoverageMap"/> — all entries non-empty.</item>
/// </list>
/// The all-35-rules enforcement test (including Visual assembly) lives in
/// <c>Veriqan.Orchestration.Tests</c> because only that project references both assemblies.
/// </summary>
public sealed class DofNumeralTests
{
    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata MinimalMetadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct OneProduct() =>
        new(
            ProductId: "TC-NUMERAL-TEST",
            ProductName: "Numeral Test Product",
            Aliases: null,
            HasRewardsProgram: false,
            CardImage: null,
            ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle MinimalBundle() =>
        new(
            BundleMetadata: MinimalMetadata(),
            Products: [OneProduct()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext BuildContext() =>
        new(
            bundle: MinimalBundle(),
            resolvedProduct: OneProduct(),
            availability: ReferenceDataAvailability.FromBundle(MinimalBundle()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: null);

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        return services.BuildServiceProvider();
    }

    // -----------------------------------------------------------------------
    // Test 1: Engine stamps DofNumeral from rule onto each finding
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every finding returned by <see cref="VecValidationEngine.RunAsync"/> must carry a
    /// non-empty <see cref="RuleFinding.DofNumeral"/> stamped from the rule (Story 9.2 / NFR-7).
    /// This verifies the single-point stamping logic in the engine, not in individual rules.
    /// </summary>
    [Fact]
    public async Task Engine_RunAsync_StampsDofNumeralOntoEveryFinding()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext();

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Engine failed: {result.Error}");
        var findings = result.Value!;

        findings.Count.ShouldBeGreaterThan(0, "Engine must produce at least one finding");

        foreach (var finding in findings)
        {
            finding.DofNumeral.ShouldNotBeNullOrWhiteSpace(
                $"Finding for {finding.CheckId} has an empty DofNumeral — " +
                "rule must declare a non-empty IVecValidationRule.DofNumeral " +
                "and the engine must stamp it onto the finding.");
        }
    }

    /// <summary>
    /// Engine stamps the numeral even when a rule returns a failure <c>Result</c>
    /// (captured as InsufficientData by the engine — NFR-6 batch isolation).
    /// The InsufficientData finding must still carry the rule's DofNumeral (Story 9.2).
    /// </summary>
    [Fact]
    public async Task Engine_RuleReturningFailure_InsufficientDataFindingStillCarriesDofNumeral()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddTransient<IVecValidationEngine, VecValidationEngine>();
        services.AddTransient<IVecValidationRule, NumeralTestFailingRule>();

        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();
        var ctx = BuildContext();

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue("Engine must succeed even when rule returns failure");
        var findings = result.Value!;

        findings.Count.ShouldBe(1);
        var finding = findings[0];
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Rule that returns failure Result must be captured as InsufficientData");
        finding.DofNumeral.ShouldBe(NumeralTestFailingRule.ExpectedNumeral,
            "Engine must stamp DofNumeral from rule even when creating the InsufficientData fallback finding");
    }

    // -----------------------------------------------------------------------
    // Test 2: Coverage map — all registered validation-assembly rules non-empty
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="IVecValidationEngine.GetCoverageMap"/> returns one entry per registered rule
    /// and every entry has a non-empty numeral.
    /// </summary>
    [Fact]
    public async Task GetCoverageMap_AllValidationRules_HaveNonEmptyDofNumeral()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        var map = engine.GetCoverageMap();

        map.Count.ShouldBeGreaterThan(0, "At least one rule must be registered");

        foreach (var (checkId, dofNumeral) in map)
        {
            dofNumeral.ShouldNotBeNullOrWhiteSpace(
                $"Rule '{checkId}' has an empty DofNumeral in the coverage map. " +
                "Every rule must declare a non-empty IVecValidationRule.DofNumeral.");
        }

        // Entries must be sorted by CheckId (matches RunAsync ordering — NFR-5)
        var checkIds = map.Select(m => m.CheckId).ToList();
        var sorted = checkIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        checkIds.ShouldBe(sorted, "Coverage map must be sorted by CheckId");

        // Suppress 'await' warning — the test is async for consistency with the test framework
        await Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Helper rule for Test 1b
    // -----------------------------------------------------------------------

    /// <summary>Helper rule that always returns a failure Result to exercise the engine's InsufficientData fallback stamping.</summary>
    private sealed class NumeralTestFailingRule : IVecValidationRule
    {
        public const string ExpectedNumeral = "Acuerdo §9";

        public string CheckId => "TEST-NUMERAL-FAIL";
        public string DofNumeral => ExpectedNumeral;
        public TechniqueClass Technique => TechniqueClass.Deterministic;

        public IndQuestResults.Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default) =>
            IndQuestResults.Result<RuleFinding>.WithFailure("simulated failure for DofNumeral stamping test");
    }
}
