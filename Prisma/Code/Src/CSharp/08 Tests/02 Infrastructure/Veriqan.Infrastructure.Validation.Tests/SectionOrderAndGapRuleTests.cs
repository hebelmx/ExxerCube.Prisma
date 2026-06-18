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
/// Unit tests for <c>SectionOrderAndGapRule</c> (CheckId = "LAW-SEC-ORDER-GAP", Story 10.2).
/// </summary>
/// <remarks>
/// All tests construct <see cref="StatementModel"/> and <see cref="VerificationContext"/>
/// objects directly — no real PDF extraction is performed.
/// Rules are discovered via Scrutor DI (production path).
/// </remarks>
public sealed class SectionOrderAndGapRuleTests
{
    private const string CheckId = "LAW-SEC-ORDER-GAP";
    private const string ProductId = "TC-SECP-TEST";

    // -----------------------------------------------------------------------
    // Shared DI helpers
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

    private static FieldLocator P(int page, double bottom = 500.0, double height = 12.0) =>
        new(PageNumber: page, Left: 20.0, Bottom: bottom, Width: 200.0, Height: height);

    private static StatementModel ModelWithSectionsAndGaps(
        IReadOnlyList<DetectedSection> sections,
        IReadOnlyList<SectionGap>? gaps = null)
    {
        var missingStr = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var missingName = ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1));

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
            SectionGaps = gaps ?? [],
            PageCount = 5,
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

        throw new InvalidOperationException(
            $"Rule '{CheckId}' not found in DI — check Scrutor registration.");
    }

    // -----------------------------------------------------------------------
    // Builders for 28-section lists with specific geometry
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a section list where the specified sections are present with
    /// reading-order geometry: each present section on page 1, top-to-bottom by index
    /// (Bottom decreasing by 50 pt per section: §1 at bottom=700, §2 at 650, etc.).
    /// Absent sections get NoPage() locator.
    /// </summary>
    private static List<DetectedSection> SectionsInOrder(params int[] presentNumbers)
    {
        var presentSet = new HashSet<int>(presentNumbers);
        var result = new List<DetectedSection>(28);
        double bottom = 700.0;

        for (var n = 1; n <= 28; n++)
        {
            if (presentSet.Contains(n))
            {
                result.Add(new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: true,
                    IsApplicable: true,
                    Locator: P(1, bottom)));
                bottom -= 50.0;
            }
            else
            {
                bool isConditional = n is 16 or 23 or 25;
                result.Add(new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: false,
                    IsApplicable: !isConditional,
                    Locator: FieldLocator.NoPage()));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a 28-section list with the two given sections swapped in reading order —
    /// i.e., the higher-numbered section is placed above the lower-numbered one on the page,
    /// creating an order violation.
    /// </summary>
    private static List<DetectedSection> SectionsOutOfOrder(int lowerNumber, int higherNumber)
    {
        // higherNumber appears ABOVE lowerNumber (wrong order: should be lowerNumber first).
        var result = new List<DetectedSection>(28);

        for (var n = 1; n <= 28; n++)
        {
            if (n == lowerNumber)
            {
                // Placed below (lower Bottom) — comes second in reading order, wrong for lower §
                result.Add(new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: true,
                    IsApplicable: true,
                    Locator: P(1, bottom: 300.0)));  // lower on page
            }
            else if (n == higherNumber)
            {
                // Placed above (higher Bottom) — comes first in reading order, wrong for higher §
                result.Add(new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: true,
                    IsApplicable: true,
                    Locator: P(1, bottom: 600.0)));  // higher on page
            }
            else
            {
                bool isConditional = n is 16 or 23 or 25;
                result.Add(new DetectedSection(
                    SectionNumber: n,
                    Name: $"Section {n}",
                    IsPresent: false,
                    IsApplicable: !isConditional,
                    Locator: FieldLocator.NoPage()));
            }
        }

        return result;
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
    }

    [Fact]
    public void Evaluate_EmptySections_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections: [], gaps: []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections list must yield InsufficientData, never Fail.");
    }

    [Fact]
    public void Evaluate_EmptySections_IsNotFail()
    {
        // Regression: NEVER produce Fail when Sections is empty.
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections: [], gaps: []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail — only InsufficientData.");
    }

    [Fact]
    public void Evaluate_OnlyOnePresentSection_ReturnsInsufficientData()
    {
        // Only one locatable section — cannot determine order.
        var sections = SectionsInOrder(7);  // only §7 present
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Only one present section — not enough geometry to verify order.");
    }

    // -----------------------------------------------------------------------
    // Tests: Pass — in-order + small / zero gaps
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_SectionsInOrderNoGaps_ReturnsPass()
    {
        var sections = SectionsInOrder(1, 5, 10, 15, 22);
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Sections in correct §-order with no gaps must yield Pass.");
    }

    [Fact]
    public void Evaluate_SectionsInOrderWithSmallGaps_ReturnsPass()
    {
        // Gaps of exactly 2 cm should NOT trigger a violation (threshold is strictly >).
        var sections = SectionsInOrder(5, 10, 15);
        var gaps = new List<SectionGap>
        {
            new(AfterSectionNumber: 5, BeforeSectionNumber: 10,  PageNumber: 1, GapPoints: SectionGap.TwoCmInPoints),
            new(AfterSectionNumber: 10, BeforeSectionNumber: 15, PageNumber: 1, GapPoints: 20.0),
        };

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, gaps);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Gap exactly at 2 cm threshold must NOT trigger a Fail (threshold is strictly >).");
    }

    [Fact]
    public void Evaluate_TwoSectionsAcrossPages_ReturnsPass()
    {
        // §5 on page 1, §22 on page 2 — no same-page gap to measure, in order.
        var sections = new List<DetectedSection>
        {
            new(SectionNumber: 5, Name: "S5", IsPresent: true, IsApplicable: true, Locator: P(1, 500)),
            new(SectionNumber: 22, Name: "S22", IsPresent: true, IsApplicable: true, Locator: P(2, 500)),
        };
        // Pad to 28 entries with absent sections
        for (var n = 1; n <= 28; n++)
        {
            if (n is 5 or 22) continue;
            bool isConditional = n is 16 or 23 or 25;
            sections.Add(new DetectedSection(
                SectionNumber: n, Name: $"S{n}", IsPresent: false,
                IsApplicable: !isConditional, Locator: FieldLocator.NoPage()));
        }

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Two sections across different pages with correct order should Pass.");
    }

    // -----------------------------------------------------------------------
    // Tests: Fail — out-of-order sections
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_SectionOutOfOrder_ReturnsFail()
    {
        // §10 placed above §5 on page — §10 comes first in reading order but has higher number.
        var sections = SectionsOutOfOrder(lowerNumber: 5, higherNumber: 10);
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "Section §5 appearing after §10 in reading order is an order violation.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrWhiteSpace("Observed must describe the violation.");
        var outOfOrderObserved = finding.Observed!;
        outOfOrderObserved.ShouldContain("§5");
    }

    [Fact]
    public void Evaluate_SectionOutOfOrder_ObservedDescribesViolation()
    {
        var sections = SectionsOutOfOrder(lowerNumber: 3, higherNumber: 20);
        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, []);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        var observed = result.Value.Observed;
        observed.ShouldNotBeNullOrWhiteSpace();
        // Should mention out-of-order
        (observed!.Contains("§3") || observed.Contains("out-of-order") || observed.Contains("Out-of-order"))
            .ShouldBeTrue("Observed should reference the out-of-order section(s).");
    }

    // -----------------------------------------------------------------------
    // Tests: Fail — gap > 2 cm
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_GapExceeds2Cm_ReturnsFail()
    {
        var sections = SectionsInOrder(5, 10);
        var gaps = new List<SectionGap>
        {
            // 3 cm = 3 * 28.3464567 ≈ 85.04 pt — well above threshold
            new(AfterSectionNumber: 5, BeforeSectionNumber: 10, PageNumber: 1,
                GapPoints: 3.0 * SectionGap.CmToPoints),
        };

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, gaps);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "A gap exceeding 2 cm must produce a Fail.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrWhiteSpace();
        var gapObserved = finding.Observed!;
        gapObserved.ShouldContain("§5");
        gapObserved.ShouldContain("§10");
    }

    [Fact]
    public void Evaluate_GapExactly2CmPlusOnePt_ReturnsFail()
    {
        var sections = SectionsInOrder(7, 22);
        var gaps = new List<SectionGap>
        {
            // Exactly one point over the threshold
            new(AfterSectionNumber: 7, BeforeSectionNumber: 22, PageNumber: 1,
                GapPoints: SectionGap.TwoCmInPoints + 1.0),
        };

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, gaps);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "A gap strictly greater than TwoCmInPoints must Fail.");
    }

    [Fact]
    public void Evaluate_GapExceeds2Cm_ObservedContainsCmValue()
    {
        var sections = SectionsInOrder(1, 2);
        // 4 cm gap
        var gapPt = 4.0 * SectionGap.CmToPoints;
        var gaps = new List<SectionGap>
        {
            new(AfterSectionNumber: 1, BeforeSectionNumber: 2, PageNumber: 1, GapPoints: gapPt),
        };

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, gaps);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        // The observed message should mention "cm" and the page
        var cmObserved = result.Value.Observed!;
        cmObserved.ShouldContain("cm");
        cmObserved.ShouldContain("page 1");
    }

    // -----------------------------------------------------------------------
    // Tests: Fail — both order violation AND gap violation simultaneously
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BothOrderAndGapViolations_ReturnsFail()
    {
        var sections = SectionsOutOfOrder(lowerNumber: 5, higherNumber: 12);
        var gaps = new List<SectionGap>
        {
            new(AfterSectionNumber: 12, BeforeSectionNumber: 5, PageNumber: 1,
                GapPoints: SectionGap.TwoCmInPoints + 20.0),
        };

        var rule = GetRule();
        var model = ModelWithSectionsAndGaps(sections, gaps);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        var observed = result.Value.Observed;
        observed.ShouldNotBeNullOrWhiteSpace();
        var bothObserved = observed!;
        // Should mention both issues
        (bothObserved.Contains("§5") || bothObserved.Contains("out-of-order") || bothObserved.Contains("Out-of-order"))
            .ShouldBeTrue("Order violation must be described.");
        bothObserved.ShouldContain("cm");
    }

    // -----------------------------------------------------------------------
    // Tests: rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_CheckId_IsLawSecOrderGap()
    {
        GetRule().CheckId.ShouldBe("LAW-SEC-ORDER-GAP");
    }

    [Fact]
    public void Rule_DofNumeral_IsNotEmpty()
    {
        GetRule().DofNumeral.ShouldNotBeNullOrWhiteSpace(
            "DofNumeral must be non-empty per IVecValidationRule contract.");
    }

    [Fact]
    public void Rule_Classification_IsBaselineLocked()
    {
        GetRule().Classification.ShouldBe(RuleClassification.BaselineLocked,
            "Fixed section order + gap limits are law-defined; tenants may not relax them.");
    }

    [Fact]
    public void Rule_Technique_IsDeterministic()
    {
        GetRule().Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    [Fact]
    public void Rule_TwoCmInPoints_IsCorrect()
    {
        // 2 cm = 2 / 2.54 × 72 = 56.6929... pt
        // Use 0.01 pt tolerance for floating-point comparison.
        const double expected = 2.0 / 2.54 * 72.0;
        SectionGap.TwoCmInPoints.ShouldBeInRange(expected - 0.01, expected + 0.01);
    }
}
