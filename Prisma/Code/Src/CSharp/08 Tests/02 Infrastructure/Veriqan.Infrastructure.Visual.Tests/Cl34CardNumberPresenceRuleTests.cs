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
/// Unit tests for the CL-34 card-number per-page presence rule (Story 5.3).
/// All tests are pure in-memory — no PDF fixtures required.
/// </summary>
public sealed class Cl34CardNumberPresenceRuleTests
{
    private static IVecValidationRule GetCl34Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();
        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>().Single(r => r.CheckId == "CL-34");
    }

    private const string ProductId = "TC-CL34-TEST";

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

    private static StatementModel ModelWithPages(
        IReadOnlyList<PageInspectionFacts> pages,
        ExtractedField<string>? cardNumber = null) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P()),
            address: ExtractedField<ExtractedAddress>.Missing(P()),
            branchNumber: ExtractedField<string>.Missing(P()),
            cardNumber: cardNumber ?? ExtractedField<string>.Found("1234567890123456", P()),
            clabe: ExtractedField<string>.Missing(P()),
            clientNumber: ExtractedField<string>.Missing(P()),
            rfc: ExtractedField<string>.Missing(P()))
        {
            Pages = pages,
            PageCount = pages.Count,
        };

    private static PageInspectionFacts PageFact(int pageNumber, bool containsCardNumber) =>
        new(PageNumber: pageNumber,
            HasContent: true,
            ImageCount: 1,
            ContainsCardNumber: containsCardNumber,
            PaginationCurrent: null,
            PaginationTotal: null,
            Locator: FieldLocator.PageHint(pageNumber));

    /// <summary>
    /// Creates a <see cref="PageInspectionFacts"/> with explicit <paramref name="hasContent"/> control.
    /// Used for image-only pages (HasContent=false) in page-1 propagation and abstain-safety tests.
    /// </summary>
    private static PageInspectionFacts PageFactEx(int pageNumber, bool containsCardNumber, bool hasContent) =>
        new(PageNumber: pageNumber,
            HasContent: hasContent,
            ImageCount: hasContent ? 0 : 1,
            ContainsCardNumber: containsCardNumber,
            PaginationCurrent: null,
            PaginationTotal: null,
            Locator: FieldLocator.PageHint(pageNumber));

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(), resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null, toleranceConfig: null,
            statementModel: model);

    [Fact]
    public void Evaluate_AllPagesContainCardNumber_ReturnsPass()
    {
        var model = ModelWithPages([
            PageFact(1, containsCardNumber: true),
            PageFact(2, containsCardNumber: true),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-34");
    }

    [Fact]
    public void Evaluate_PageMissingCardNumber_ReturnsFail()
    {
        var model = ModelWithPages([
            PageFact(1, containsCardNumber: true),
            PageFact(2, containsCardNumber: false),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("2");
    }

    [Fact]
    public void Evaluate_CardNumberNotExtracted_ReturnsInsufficientData()
    {
        var model = ModelWithPages(
            [PageFact(1, containsCardNumber: false)],
            cardNumber: ExtractedField<string>.Missing(P()));
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-34");
    }

    [Fact]
    public void Evaluate_EmptyPages_ReturnsInsufficientData()
    {
        var model = ModelWithPages([]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl34Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = rule.Evaluate(Ctx(model: null), cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Story VERIQAN-E2-S6: page-1 propagation + abstain-safety + masked last-4
    // -----------------------------------------------------------------------

    /// <summary>
    /// AC-S6-1: Card found in text on page 1, later pages are image-only (no text layer).
    /// Page-1 propagation must return PASS for all pages rather than failing on
    /// the image-only pages where the text extractor cannot see the card.
    /// </summary>
    [Fact]
    public void Evaluate_CardOnPage1TextLayerLaterPagesImageOnly_ReturnsPass()
    {
        // page 1: card in text layer (ContainsCardNumber=true, HasContent=true)
        // page 2: image-only — no text extracted at all (HasContent=false → ContainsCardNumber=false)
        var model = ModelWithPages([
            PageFactEx(1, containsCardNumber: true,  hasContent: true),
            PageFactEx(2, containsCardNumber: false, hasContent: false),
            PageFactEx(3, containsCardNumber: false, hasContent: false),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-34");
        result.Value.Observed!.ShouldContain("page 1");
    }

    /// <summary>
    /// AC-S6-2: Card is only inside a header graphic on all pages — no text layer carries it.
    /// All pages are either image-only or yield no card match from the text layer.
    /// The rule must emit InsufficientData (NOT Fail) because we cannot distinguish a
    /// genuine absence from a card printed exclusively in an embedded image.
    /// </summary>
    [Fact]
    public void Evaluate_CardOnlyInImageNoTextLayerOnAnyPage_ReturnsInsufficientData()
    {
        // No page has any text content at all — fully image-based statement.
        var model = ModelWithPages([
            PageFactEx(1, containsCardNumber: false, hasContent: false),
            PageFactEx(2, containsCardNumber: false, hasContent: false),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-34");
    }

    /// <summary>
    /// AC-S6-3: Masked card number in the header (e.g. XXXX-XXXX-XXXX-1234).
    /// The extractor stores the raw masked value as ExtractedInvalidFormat (16-digit check fails).
    /// ContainsCardNumber on the page is true because the extractor matched the last-4
    /// via the card-group pattern guard.  The rule must return PASS rather than
    /// treating ExtractedInvalidFormat as InsufficientData.
    /// </summary>
    [Fact]
    public void Evaluate_MaskedCardNumberHeaderLastFourMatchesPageText_ReturnsPass()
    {
        // Simulate the extractor having stored a masked card value (ExtractedInvalidFormat)
        // and the per-page ContainsCardNumber=true computed via the last-4 pattern guard.
        var maskedCard = ExtractedField<string>.InvalidFormat("XXXX-XXXX-XXXX-1234", P());
        var model = ModelWithPages(
            [
                PageFact(1, containsCardNumber: true),
                PageFact(2, containsCardNumber: true),
            ],
            cardNumber: maskedCard);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-34");
    }

    // -----------------------------------------------------------------------
    // MAJOR-3 fix (adversarial review): MinBy(PageNumber) first-page propagation
    // Sliced statements may not contain an absolute page 1 — propagation must use
    // the page with the minimum PageNumber rather than page numbered exactly 1.
    // -----------------------------------------------------------------------

    /// <summary>
    /// MAJOR-3 regression guard: a statement slice whose pages start at PageNumber 2
    /// (no page numbered 1 exists) must still trigger first-page propagation when
    /// the lowest-numbered page carries the card and all other missing-card pages
    /// are image-only.
    /// </summary>
    [Fact]
    public void Evaluate_SliceStartingAtPage2CardOnFirstPage_PropagatesPass()
    {
        // Pages start at 2 — there is no absolute page 1.
        // page 2: card in text layer (first page by PageNumber).
        // page 3: image-only — no text extracted (HasContent=false).
        var model = ModelWithPages([
            PageFactEx(2, containsCardNumber: true,  hasContent: true),
            PageFactEx(3, containsCardNumber: false, hasContent: false),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-34");
    }

    /// <summary>
    /// Companion to the MAJOR-3 guard: a text page (HasContent=true) that genuinely
    /// lacks the card number must still produce Fail even in a slice starting at page 2.
    /// Confirms that propagation does NOT override genuine text-layer evidence.
    /// </summary>
    [Fact]
    public void Evaluate_SliceStartingAtPage2TextPageMissingCard_ReturnsFail()
    {
        // page 2: card present (first page by PageNumber).
        // page 3: HAS text content (HasContent=true) but card absent — genuine failure.
        var model = ModelWithPages([
            PageFactEx(2, containsCardNumber: true,  hasContent: true),
            PageFactEx(3, containsCardNumber: false, hasContent: true),
        ]);
        var rule = GetCl34Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("3");
    }
}
