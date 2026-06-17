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
/// Unit tests for the CL-33 logo-presence rule (Story 5.3).
/// All tests are pure in-memory — no PDF fixtures required.
/// </summary>
public sealed class Cl33LogoPresenceRuleTests
{
    private static IVecValidationRule GetCl33Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();
        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>().Single(r => r.CheckId == "CL-33");
    }

    private const string ProductId = "TC-CL33-TEST";

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

    private static PageInspectionFacts PageFact(int pageNumber, int imageCount) =>
        new(PageNumber: pageNumber,
            HasContent: true,
            ImageCount: imageCount,
            ContainsCardNumber: false,
            PaginationCurrent: null,
            PaginationTotal: null,
            Locator: FieldLocator.PageHint(pageNumber));

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(), resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null, toleranceConfig: null,
            statementModel: model);

    [Fact]
    public void Evaluate_AllPagesHaveImages_ReturnsPass()
    {
        var model = ModelWithPages([
            PageFact(1, imageCount: 2),
            PageFact(2, imageCount: 1),
        ]);
        var rule = GetCl33Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-33");
    }

    [Fact]
    public void Evaluate_PageWithZeroImages_ReturnsFail()
    {
        var model = ModelWithPages([
            PageFact(1, imageCount: 1),
            PageFact(2, imageCount: 0),
        ]);
        var rule = GetCl33Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("2");
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetCl33Rule();
        var result = rule.Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-33");
    }

    [Fact]
    public void Evaluate_EmptyPages_ReturnsInsufficientData()
    {
        var model = ModelWithPages([]);
        var rule = GetCl33Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl33Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = rule.Evaluate(Ctx(model: null), cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }
}
