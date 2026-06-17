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
/// Unit tests for the CL-31 pagination consistency rule (Story 5.3).
/// All tests are pure in-memory — no PDF fixtures required.
/// </summary>
public sealed class Cl31PaginationRuleTests
{
    private static IVecValidationRule GetCl31Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();
        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>().Single(r => r.CheckId == "CL-31");
    }

    private const string ProductId = "TC-CL31-TEST";

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

    private static StatementModel ModelWithPages(IReadOnlyList<PageInspectionFacts> pages, int? pageCount = null) =>
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
            PageCount = pageCount ?? pages.Count,
        };

    private static PageInspectionFacts PageFact(int pageNumber, int? current, int? total) =>
        new(PageNumber: pageNumber,
            HasContent: true,
            ImageCount: 1,
            ContainsCardNumber: false,
            PaginationCurrent: current,
            PaginationTotal: total,
            Locator: FieldLocator.PageHint(pageNumber));

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(), resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null, toleranceConfig: null,
            statementModel: model);

    [Fact]
    public void Evaluate_ConsistentPaginationMatchesPageCount_ReturnsPass()
    {
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 2),
            PageFact(2, current: 2, total: 2),
        ], pageCount: 2);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-31");
    }

    [Fact]
    public void Evaluate_DeclaredTotalDoesNotMatchPageCount_ReturnsFail()
    {
        // Pages say "1 de 3" and "2 de 3" but document only has 2 pages.
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 3),
            PageFact(2, current: 2, total: 3),
        ], pageCount: 2);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("3");
        result.Value.Observed!.ShouldContain("2");
    }

    [Fact]
    public void Evaluate_ConflictingTotals_ReturnsFail()
    {
        // Page 1 says "1 de 2", page 2 says "2 de 4" — conflict.
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 2),
            PageFact(2, current: 2, total: 4),
        ], pageCount: 2);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("Conflicting");
    }

    [Fact]
    public void Evaluate_DuplicatePaginationCurrents_ReturnsFail()
    {
        // Two pages both labeled as "1 de 2".
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 2),
            PageFact(2, current: 1, total: 2),  // duplicate current
        ], pageCount: 2);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed!.ShouldContain("Duplicate");
    }

    [Fact]
    public void Evaluate_NoPaginationLabels_ReturnsInsufficientData()
    {
        var model = ModelWithPages([
            PageFact(1, current: null, total: null),
            PageFact(2, current: null, total: null),
        ]);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-31");
    }

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl31Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = rule.Evaluate(Ctx(model: null), cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-31 gap tests (Epic 5 adversarial-review finding)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A labeled sequence of {1, 3, 4} de 4 is missing page 2 — the contiguity check
    /// must catch this even though there are no duplicates and the declared total matches.
    /// Non-vacuity: if the gap check is removed this test will regress to Pass.
    /// </summary>
    [Fact]
    public void Evaluate_GappedPaginationSequence_ReturnsFail()
    {
        // Pages labeled "1 de 4", "3 de 4", "4 de 4" — gap at 2.
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 4),
            PageFact(2, current: 3, total: 4),
            PageFact(3, current: 4, total: 4),
        ], pageCount: 4);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.CheckId.ShouldBe("CL-31");
        // The finding must mention the gap (keyword "gap" or "Gap").
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ToLowerInvariant().ShouldContain("gap");
    }

    /// <summary>
    /// A perfectly contiguous labeled sequence must still pass the new contiguity check.
    /// </summary>
    [Fact]
    public void Evaluate_ContiguousPaginationSequence_ReturnsPass()
    {
        var model = ModelWithPages([
            PageFact(1, current: 1, total: 3),
            PageFact(2, current: 2, total: 3),
            PageFact(3, current: 3, total: 3),
        ], pageCount: 3);
        var rule = GetCl31Rule();
        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }
}
