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
/// Unit tests for the CL-29 section-header styling rule (Story 5.2).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class Cl29HeaderStylingRuleTests
{
    // -----------------------------------------------------------------------
    // DI factory
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetCl29Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == "CL-29");
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-CL29-TEST";

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
        new(
            BundleMetadata: new("1.0.0", "Test Bank", null, null, null, null),
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

    private static StatementModel EmptyModel(
        IReadOnlyList<SectionHeaderStyle>? headers = null) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            SectionHeaderStyles = headers ?? [],
        };

    private static SectionHeaderStyle Header(
        string text,
        bool isBold,
        bool isUppercase,
        int page = 1) =>
        new(text, isBold, isUppercase, page, new FieldLocator(page, 15.0, 400.0, 200.0, 12.0));

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    // -----------------------------------------------------------------------
    // Test 1: All headers bold + uppercase → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllHeadersBoldAndUppercase_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(headers:
        [
            Header("RESUMEN DE CARGOS Y ABONOS DEL PERIODO", isBold: true, isUppercase: true, page: 1),
            Header("DESGLOSE DE MOVIMIENTOS DEL PERIODO", isBold: true, isUppercase: true, page: 2),
        ]);
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-29");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("2");
    }

    // -----------------------------------------------------------------------
    // Test 2: A non-bold header → Fail naming the header and "bold"
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NonBoldHeader_ReturnsFail_NamingHeaderAndAttribute()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(headers:
        [
            Header("NIVEL DE USO DE TU TARJETA", isBold: false, isUppercase: true, page: 1),
        ]);
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.CheckId.ShouldBe("CL-29");
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("NIVEL DE USO DE TU TARJETA");
        finding.Observed.ShouldContain("bold");
        finding.Locator.ShouldNotBeNull();
        finding.Locator!.PageNumber.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 3: A non-uppercase header → Fail naming the header and "uppercase"
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NonUppercaseHeader_ReturnsFail_NamingHeaderAndAttribute()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(headers:
        [
            Header("Resumen de Cargos y Abonos del Periodo", isBold: true, isUppercase: false, page: 1),
        ]);
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("uppercase");
        finding.Observed.ShouldContain("Resumen de Cargos");
    }

    // -----------------------------------------------------------------------
    // Test 4: No headers detected → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NoHeadersDetected_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(headers: []);
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-29");
    }

    // -----------------------------------------------------------------------
    // Test 5: Null StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model: null), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-29");
    }

    // -----------------------------------------------------------------------
    // Test 6: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl29Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(Ctx(model: null), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Test 7: Header neither bold nor uppercase → Fail with "bold or uppercase"
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_HeaderNeitherBoldNorUppercase_ReturnsFail_BothAttributesNamed()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(headers:
        [
            Header("Desglose de Movimientos del Periodo", isBold: false, isUppercase: false, page: 3),
        ]);
        var rule = GetCl29Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Observed!.ShouldContain("bold or uppercase");
        finding.Locator!.PageNumber.ShouldBe(3);
    }
}
