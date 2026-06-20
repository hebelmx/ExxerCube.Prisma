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
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for the CL-48 blank-page detection rule (Story 5.3).
/// All tests are pure in-memory — no PDF fixtures required.
/// </summary>
public sealed class Cl48BlankPageRuleTests
{
    private static IVecValidationRule GetCl48Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();
        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>().Single(r => r.CheckId == "CL-48");
    }

    private const string ProductId = "TC-CL48-TEST";

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
        new(
            BundleMetadata: new("1.0.0", "Test Bank", null, null, null, null),
            Products: [Product()],
            InterestRates: null, MandatoryLegends: null, SequentialImages: null,
            Promotions: null, ClientAccounts: null, PriorStatements: null,
            ExpectedTransactions: null, ToleranceConfig: null, ValidationConstants: null);

    private static FieldLocator P(int page = 1) => FieldLocator.PageHint(page);

    private static StatementModel ModelWithPages(IReadOnlyList<PageInspectionFacts> pages) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P()),
            address: ExtractedField<ExtractedAddress>.Missing(P()),
            branchNumber: ExtractedField<string>.Missing(P()),
            cardNumber: ExtractedField<string>.Missing(P()),
            clabe: ExtractedField<string>.Missing(P()),
            clientNumber: ExtractedField<string>.Missing(P()),
            rfc: ExtractedField<string>.Missing(P()))
        {
            Pages = pages,
            PageCount = pages.Count,
        };

    private static PageInspectionFacts PageFact(
        int pageNumber,
        bool hasContent,
        int imageCount,
        double maxVerticalGapPoints = 0.0) =>
        new(PageNumber: pageNumber,
            HasContent: hasContent,
            ImageCount: imageCount,
            ContainsCardNumber: false,
            PaginationCurrent: null,
            PaginationTotal: null,
            Locator: FieldLocator.PageHint(pageNumber),
            MaxVerticalGapPoints: maxVerticalGapPoints);

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(), resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null, toleranceConfig: null,
            statementModel: model);

    [Fact]
    public void Evaluate_AllPagesHaveContent_ReturnsPass()
    {
        var model = ModelWithPages([
            PageFact(1, hasContent: true, imageCount: 1),
            PageFact(2, hasContent: true, imageCount: 1),
        ]);
        var rule = GetCl48Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-48");
    }

    [Fact]
    public void Evaluate_PageWithNoContentAndNoImages_ReturnsFail()
    {
        var model = ModelWithPages([
            PageFact(1, hasContent: true, imageCount: 1),
            PageFact(2, hasContent: false, imageCount: 0),  // blank
        ]);
        var rule = GetCl48Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.CheckId.ShouldBe("CL-48");
        result.Value.Observed!.ShouldContain("2");
    }

    [Fact]
    public void Evaluate_PageWithNoContentButHasImage_ReturnsPass()
    {
        // A page with no text but has image is NOT blank (image counts as content)
        var model = ModelWithPages([
            PageFact(1, hasContent: true, imageCount: 1),
            PageFact(2, hasContent: false, imageCount: 1),  // no text but has image -> not blank
        ]);
        var rule = GetCl48Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetCl48Rule();
        var result = rule.Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-48");
    }

    [Fact]
    public void Evaluate_EmptyPages_ReturnsInsufficientData()
    {
        var model = ModelWithPages([]);
        var rule = GetCl48Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl48Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = rule.Evaluate(Ctx(model: null), cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // S11: Whitespace-only content treated as blank (rule-level)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A page whose extractor set HasContent=false (because only whitespace/invisible glyphs
    /// were present — the whitespace filter ran upstream) and ImageCount=0 must be reported
    /// as blank by CL-48.
    /// This test verifies the rule-level contract: HasContent=false + ImageCount=0 → FAIL.
    /// </summary>
    [Fact]
    public void Evaluate_PageWithWhitespaceOnlyContent_TreatedAsBlank_ReturnsFail()
    {
        // Arrange: the extractor (after S11 whitespace filter) set HasContent=false for page 2
        // because its only words were whitespace tokens.  We simulate that outcome here.
        var model = ModelWithPages([
            PageFact(1, hasContent: true,  imageCount: 1),
            PageFact(2, hasContent: false, imageCount: 0),  // whitespace-only → blank
        ]);
        var rule = GetCl48Rule();

        // Act
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.CheckId.ShouldBe("CL-48");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("2"); // page number mentioned
    }

    // -----------------------------------------------------------------------
    // S11: 2 cm vertical-gap checks (rule-level, MaxVerticalGapPoints)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A page with a 2.5 cm vertical gap (≈ 70.9 pt = 2.5 × 28.35) must cause CL-48 to FAIL.
    /// 70.9 pt > 56.7 pt threshold.
    /// </summary>
    [Fact]
    public void Evaluate_PageWithGapExceeding2Cm_ReturnsFail()
    {
        // 2.5 cm × 28.35 pt/cm = 70.875 pt ≈ 70.9 pt.
        const double gapPt = 2.5 * 28.35;

        var model = ModelWithPages([
            PageFact(1, hasContent: true, imageCount: 0, maxVerticalGapPoints: gapPt),
        ]);
        var rule = GetCl48Rule();

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.CheckId.ShouldBe("CL-48");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        // Observed message must mention the gap condition and page 1.
        result.Value.Observed!.ShouldContain("page 1");
        result.Value.Observed!.ShouldContain("pt");
    }

    /// <summary>
    /// A page with a 1.5 cm vertical gap (≈ 42.5 pt) must NOT cause CL-48 to FAIL.
    /// 42.5 pt ≤ 56.7 pt threshold — within the allowance.
    /// </summary>
    [Fact]
    public void Evaluate_PageWithGapBelow2Cm_ReturnsPass()
    {
        // 1.5 cm × 28.35 pt/cm = 42.525 pt.
        const double gapPt = 1.5 * 28.35;

        var model = ModelWithPages([
            PageFact(1, hasContent: true, imageCount: 0, maxVerticalGapPoints: gapPt),
        ]);
        var rule = GetCl48Rule();

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-48");
    }

    /// <summary>
    /// When both a whole-blank page and a large-gap page are present, the observed message
    /// must identify both conditions.
    /// </summary>
    [Fact]
    public void Evaluate_BlankPageAndGapPage_FailMessageIdentifiesBothConditions()
    {
        const double gapPt = 3.0 * 28.35; // 3 cm — well above threshold

        var model = ModelWithPages([
            PageFact(1, hasContent: false, imageCount: 0),                          // whole-blank
            PageFact(2, hasContent: true,  imageCount: 0, maxVerticalGapPoints: gapPt), // large gap
            PageFact(3, hasContent: true,  imageCount: 0),                          // clean
        ]);
        var rule = GetCl48Rule();

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        // Observed must mention both "blank" and the gap condition.
        result.Value.Observed!.ShouldContain("blank");
        result.Value.Observed!.ShouldContain("gap");
    }
}
