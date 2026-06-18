using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for <c>MandatorySectionsPresenceRule</c> (CheckId = "LAW-SEC-PRESENCE", Story 10.1).
/// </summary>
/// <remarks>
/// <para>
/// All tests construct <see cref="StatementModel"/> and <see cref="VerificationContext"/>
/// objects directly — no real PDF extraction is performed.
/// Rules are discovered via Scrutor DI (production path).
/// </para>
/// </remarks>
public sealed class MandatorySectionsPresenceRuleTests
{
    private const string CheckId = "LAW-SEC-PRESENCE";
    private const string ProductId = "TC-SECP-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle MinimalBundle() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static StatementModel ModelWithSections(IReadOnlyList<DetectedSection> sections)
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            Sections = sections,
        };
    }

    private static VerificationContext Ctx(StatementModel? model)
    {
        var bundle = MinimalBundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == CheckId)
                return rule;

        throw new InvalidOperationException($"Rule '{CheckId}' not found in DI — check Scrutor registration.");
    }

    /// <summary>
    /// Builds a full 28-section list with all sections present.
    /// </summary>
    private static List<DetectedSection> AllPresent()
    {
        return Enumerable.Range(1, 28)
            .Select(n => new DetectedSection(
                SectionNumber: n,
                Name: $"Section {n}",
                IsPresent: true,
                IsApplicable: true,
                Locator: P1()))
            .ToList();
    }

    /// <summary>
    /// Builds a full 28-section list with all sections present,
    /// but the specified section numbers are overridden to absent.
    /// Conditional sections (§16, §23, §25) that are absent get IsApplicable = false.
    /// </summary>
    private static List<DetectedSection> WithAbsent(params int[] absentNumbers)
    {
        var conditionals = new HashSet<int> { 16, 23, 25 };
        return Enumerable.Range(1, 28)
            .Select(n =>
            {
                if (absentNumbers.Contains(n))
                {
                    return new DetectedSection(
                        SectionNumber: n,
                        Name: $"Section {n}",
                        IsPresent: false,
                        IsApplicable: !conditionals.Contains(n),
                        Locator: FieldLocator.NoPage());
                }

                return new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: true,
                    IsApplicable: true,
                    Locator: P1());
            })
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Tests: InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Evaluate_EmptySectionsList_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWithSections([]);  // empty — detection pass did not run
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections (unreadable text layer) must yield InsufficientData, never Fail.");
    }

    [Fact]
    public void Evaluate_EmptySections_IsNotFail()
    {
        // Regression: ensure the rule NEVER returns Fail when Sections is empty.
        // A false Fail here would halt a billing run.
        var rule = GetRule();
        var model = ModelWithSections([]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail — only InsufficientData.");
    }

    // -----------------------------------------------------------------------
    // Tests: Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllSectionsPresent_ReturnsPass()
    {
        var rule = GetRule();
        var model = ModelWithSections(AllPresent());
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "All 28 sections present must yield Pass.");
        result.Value.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Evaluate_ConditionalSectionsAbsent_StillReturnsPass()
    {
        // §16, §23, §25 absent (their trigger is not present) — must NOT count as missing.
        var rule = GetRule();
        var model = ModelWithSections(WithAbsent(16, 23, 25));
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Conditional §16/§23/§25 absent without their triggers must NOT be counted missing.");
    }

    // -----------------------------------------------------------------------
    // Tests: Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MandatorySection6Absent_ReturnsFail()
    {
        // §6 ("Cuánto pagarías") is unconditional — missing it is Critical.
        var rule = GetRule();
        var model = ModelWithSections(WithAbsent(6));
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNull("Observed must be populated on Fail.");
        finding.Observed!.ShouldContain("§6");
    }

    [Fact]
    public void Evaluate_MultipleMandatoryAbsent_FailNamesAllMissing()
    {
        // §3, §8, §19, §20 all absent — Fail must enumerate all four.
        var rule = GetRule();
        var model = ModelWithSections(WithAbsent(3, 8, 19, 20));
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        var observed = finding.Observed;
        observed.ShouldNotBeNull("Observed must list the missing sections.");
        observed.ShouldContain("§3");
        observed.ShouldContain("§8");
        observed.ShouldContain("§19");
        observed.ShouldContain("§20");
    }

    [Fact]
    public void Evaluate_ConditionalAbsentAndMandatoryAbsent_FailsForMandatoryOnly()
    {
        // §25 absent (conditional, not-applicable) + §19 absent (mandatory) → Fail for §19 only.
        var sections = WithAbsent(19, 25);
        // §25 is already IsApplicable=false from WithAbsent (it's in conditionals set).

        var rule = GetRule();
        var model = ModelWithSections(sections);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        var observed = finding.Observed;
        observed.ShouldNotBeNull();
        observed.ShouldContain("§19");
        // Conditional §25 (IsApplicable=false) must NOT appear in the missing list.
        observed.Contains("§25").ShouldBeFalse(
            "Conditional §25 must not be counted missing when IsApplicable=false.");
    }

    // -----------------------------------------------------------------------
    // Tests: rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_CheckId_IsLawSecPresence()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe("LAW-SEC-PRESENCE");
    }

    [Fact]
    public void Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule();
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace("DofNumeral must be non-empty per IVecValidationRule contract.");
    }

    [Fact]
    public void Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule();
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked,
            "Mandatory section presence is law-defined; tenants may not relax it.");
    }

    [Fact]
    public void Rule_Technique_IsDeterministic()
    {
        var rule = GetRule();
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }
}
