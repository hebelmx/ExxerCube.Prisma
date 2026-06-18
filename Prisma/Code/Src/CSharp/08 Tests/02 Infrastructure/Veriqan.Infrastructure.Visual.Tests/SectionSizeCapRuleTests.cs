using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for LAW-SEC-SIZECAP: section size caps rule (Story 12.4).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
/// <remarks>
/// Page geometry uses PDF coordinate convention: origin is BOTTOM-LEFT.
/// Higher Bottom value = higher position on the page.
/// US-Letter page height = 792 pt; A4 = 842 pt.
///
/// Section extent = targetSection.Bottom - nextPresentSection.Bottom
/// (positive when target is above next in the page, i.e. target.Bottom > next.Bottom).
/// </remarks>
public sealed class SectionSizeCapRuleTests
{
    private const string CheckId = "LAW-SEC-SIZECAP";
    private const string ProductId = "TC-SIZECAP-TEST";

    // US-Letter page height in PDF points.
    private const double PageHeight = 792.0;

    // -----------------------------------------------------------------------
    // DI factory — mirrors production Scrutor wiring
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == CheckId);
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
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

    private static VerificationContext CtxWithModel(StatementModel? model)
    {
        var bundle = Bundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given section list and page list.
    /// </summary>
    private static StatementModel ModelWith(
        IReadOnlyList<DetectedSection> sections,
        IReadOnlyList<PageInspectionFacts> pages)
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
            Pages = pages,
        };
    }

    /// <summary>
    /// Builds a <see cref="PageInspectionFacts"/> for page <paramref name="pageNumber"/>
    /// with <paramref name="height"/> (in PDF points). Width set to 612 pt (US-Letter).
    /// </summary>
    private static PageInspectionFacts Page(int pageNumber, double height = PageHeight) =>
        new(
            PageNumber: pageNumber,
            HasContent: true,
            ImageCount: 0,
            ContainsCardNumber: false,
            PaginationCurrent: pageNumber,
            PaginationTotal: null,
            Locator: FieldLocator.PageHint(pageNumber),
            Width: 612.0,
            Height: height);

    /// <summary>
    /// Builds a Present <see cref="DetectedSection"/> with precise geometry.
    /// <paramref name="bottom"/> is the Y coordinate of the section heading's bottom edge (PDF points).
    /// </summary>
    private static DetectedSection PresentWithGeometry(int number, int pageNumber, double bottom) =>
        new(
            SectionNumber: number,
            Name: $"Section {number}",
            IsPresent: true,
            IsApplicable: true,
            Locator: new FieldLocator(
                PageNumber: pageNumber,
                Left: 72.0,
                Bottom: bottom,
                Width: 468.0,
                Height: 12.0))
        {
            DetectionStatus = SectionDetectionStatus.Present,
            SectionText = $"Content of section {number}.",
        };

    /// <summary>
    /// Builds a Present <see cref="DetectedSection"/> with only a page-level hint (no geometry).
    /// </summary>
    private static DetectedSection PresentNoGeometry(int number, int pageNumber) =>
        new(
            SectionNumber: number,
            Name: $"Section {number}",
            IsPresent: true,
            IsApplicable: true,
            Locator: FieldLocator.PageHint(pageNumber))
        {
            DetectionStatus = SectionDetectionStatus.Present,
            SectionText = $"Content of section {number}.",
        };

    /// <summary>
    /// Builds an Absent <see cref="DetectedSection"/>.
    /// </summary>
    private static DetectedSection AbsentSection(int number) =>
        new(
            SectionNumber: number,
            Name: $"Section {number}",
            IsPresent: false,
            IsApplicable: true,
            Locator: FieldLocator.NoPage())
        {
            DetectionStatus = SectionDetectionStatus.Absent,
            SectionText = string.Empty,
        };

    // -----------------------------------------------------------------------
    // (a) InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Evaluate_NullSections_ReturnsInsufficientData()
    {
        // Sections defaults to [] in StatementModel; providing empty list covers null/empty guard.
        var rule = GetRule();
        var model = ModelWith(sections: [], pages: [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_EmptySections_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWith(sections: [], pages: [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections means Epic 10 did not run — rule must abstain.");
    }

    [Fact]
    public void Evaluate_NullPages_ReturnsInsufficientData()
    {
        // Pages defaults to [] in StatementModel; empty covers the null/empty guard.
        var rule = GetRule();
        var model = ModelWith(
            sections: [PresentWithGeometry(17, 1, 600.0), PresentWithGeometry(18, 1, 400.0)],
            pages: []);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Pages means no page geometry — rule must abstain.");
    }

    [Fact]
    public void Evaluate_EmptyPages_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWith(
            sections: [PresentWithGeometry(17, 1, 600.0), PresentWithGeometry(18, 1, 400.0)],
            pages: []);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // (b) §17 spanning ~0.15 of page height → Pass (well within ¼)
    //
    // Setup: §17 heading at Bottom=650, §18 heading at Bottom=531.2
    // extent = 650 - 531.2 = 118.8 pt; fraction = 118.8 / 792 ≈ 0.15 (15%)
    // threshold = 0.25 + 0.02 = 0.27 → 0.15 < 0.27 → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section17Spanning15Percent_ReturnsPass()
    {
        // §17 bottom = 650; §18 bottom = 531.2 → extent = 118.8 / 792 ≈ 15%
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, 1, bottom: 650.0),
            PresentWithGeometry(18, 1, bottom: 531.2),
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§17 at ~15% of page is well within the ¼-page cap.");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // (c) §17 spanning ~0.40 of page height → Fail (exceeds ¼ + epsilon)
    //
    // Setup: §17 bottom=700, §18 bottom=383.2
    // extent = 700 - 383.2 = 316.8; fraction = 316.8 / 792 ≈ 0.40 (40%)
    // threshold = 0.25 + 0.02 = 0.27 → 0.40 > 0.27 → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section17Spanning40Percent_ReturnsFail_Critical()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, 1, bottom: 700.0),
            PresentWithGeometry(18, 1, bottom: 383.2),
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "§17 at ~40% of page far exceeds the ¼-page cap.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Expected.ShouldNotBeNullOrEmpty();
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("§17");
        finding.Locator.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // (d) §21 spanning ~0.40 of page height → Fail (exceeds ⅓ + epsilon)
    //
    // Setup: §21 bottom=700, §22 bottom=383.2
    // extent = 316.8; fraction ≈ 0.40; threshold = 1/3 + 0.02 ≈ 0.353 → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section21Spanning40Percent_ReturnsFail_Critical()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(21, 1, bottom: 700.0),
            PresentWithGeometry(22, 1, bottom: 383.2),
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "§21 at ~40% of page exceeds the ⅓-page cap.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("§21");
        finding.Locator.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // (e) §21 spanning ~0.30 of page height → Pass (within ⅓ + epsilon)
    //
    // Setup: §21 bottom=700, §22 bottom=462.4
    // extent = 237.6; fraction = 237.6 / 792 ≈ 0.30 (30%)
    // threshold = 1/3 + 0.02 ≈ 0.353 → 0.30 < 0.353 → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section21Spanning30Percent_ReturnsPass()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(21, 1, bottom: 700.0),
            PresentWithGeometry(22, 1, bottom: 462.4),
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§21 at ~30% of page is within the ⅓-page cap (+ 0.02 epsilon).");
    }

    // -----------------------------------------------------------------------
    // (f) §17 present but next present section is on a different page → abstained;
    //     if all three sections abstain → InsufficientData
    //
    // Setup: §17 on page 1, §18 on page 2 (cross-page gap, no same-page successor)
    // §21 and §28 are absent. All three abstain → InsufficientData.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section17NextOnDifferentPage_AllAbstain_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, pageNumber: 1, bottom: 400.0),
            PresentWithGeometry(18, pageNumber: 2, bottom: 600.0), // different page
            AbsentSection(21),
            AbsentSection(28),
        };
        var pages = new List<PageInspectionFacts>
        {
            Page(1),
            Page(2),
        };
        var model = ModelWith(sections, pages);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "When the only measurable section has no same-page successor and §21/§28 are absent, all abstain → InsufficientData.");
    }

    // -----------------------------------------------------------------------
    // (f-partial) §17 abstains (cross-page), but §21 is measurable and within cap → Pass
    //
    // §17 next section is on page 2 → §17 abstains.
    // §21 has a same-page successor §22 → measured within cap → Pass.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section17CrossPage_Section21WithinCap_ReturnsPass()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, pageNumber: 1, bottom: 400.0),
            PresentWithGeometry(18, pageNumber: 2, bottom: 600.0), // different page, §17 abstains
            // §21 and §22 on page 2 — extent = 600 - 362.4 = 237.6 → 237.6/792 ≈ 30% < 35.3%
            PresentWithGeometry(21, pageNumber: 2, bottom: 600.0),
            PresentWithGeometry(22, pageNumber: 2, bottom: 362.4),
            AbsentSection(28),
        };
        var pages = new List<PageInspectionFacts>
        {
            Page(1),
            Page(2),
        };
        var model = ModelWith(sections, pages);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§17 abstains (cross-page) but §21 is measurable and within cap → Pass.");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // (g) Page Height == 0 for the section's page → that section abstains
    //     §17 on page with Height=0; §21 absent; §28 absent → all abstain → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PageHeightZero_Section17Abstains_AllAbstain_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, pageNumber: 1, bottom: 600.0),
            PresentWithGeometry(18, pageNumber: 1, bottom: 400.0),
            AbsentSection(21),
            AbsentSection(28),
        };
        // Page 1 has Height = 0 (geometry not populated).
        var pages = new List<PageInspectionFacts>
        {
            Page(pageNumber: 1, height: 0.0),
        };
        var model = ModelWith(sections, pages);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "When page Height is 0, the section cannot be measured — must abstain, never Fail.");
    }

    // -----------------------------------------------------------------------
    // Additional: §28 spanning ~0.40 → Fail (exceeds ⅓)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section28Spanning40Percent_ReturnsFail_Critical()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            AbsentSection(17),
            AbsentSection(21),
            // §28 bottom=700, no §29 (last section), but we need a successor...
            // Use a helper section §29 on the same page so the measurement is available.
            PresentWithGeometry(28, 1, bottom: 700.0),
            PresentWithGeometry(29, 1, bottom: 383.2), // extent = 316.8, 316.8/792 ≈ 40%
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "§28 at ~40% of page exceeds the ⅓-page cap.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed!.ShouldContain("§28");
    }

    // -----------------------------------------------------------------------
    // Additional: Epsilon boundary — §17 exactly at 0.27 (= 0.25 + 0.02) → Pass
    //
    // threshold + epsilon = 0.27; fraction must be STRICTLY greater to Fail.
    // extent = 0.27 × 792 = 213.84 pt exactly
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section17AtExactEpsilonBoundary_ReturnsPass()
    {
        // extent = 0.27 * 792 = 213.84 → fraction exactly = 0.27 = threshold + epsilon → NOT > → Pass
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentWithGeometry(17, 1, bottom: 700.0),
            PresentWithGeometry(18, 1, bottom: 700.0 - 213.84), // = 486.16
        };
        var model = ModelWith(sections, [Page(1)]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Fraction exactly equal to threshold + epsilon must NOT Fail (strictly greater required).");
    }

    // -----------------------------------------------------------------------
    // Additional: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Additional: Rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_CheckId_IsLawSecSizecap()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule();
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule();
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
    }

    [Fact]
    public void Rule_Technique_IsDeterministic()
    {
        var rule = GetRule();
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // Additional: DI registration — discovered by Scrutor
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanVisual_ContainsLawSecSizecapRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        rules.ShouldContain(
            r => r.CheckId == CheckId,
            $"Expected {CheckId} to be discovered by Scrutor.");
    }
}
