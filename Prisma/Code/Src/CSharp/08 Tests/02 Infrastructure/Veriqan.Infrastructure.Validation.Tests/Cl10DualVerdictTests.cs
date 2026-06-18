using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Tests for <c>Cl10CatRule</c> dual-verdict behaviour (Story 9.3b).
/// CL-10 is the single rule that demonstrates verdict divergence between the legal baseline
/// and the tenant-profile bar in this story.
/// </summary>
public sealed class Cl10DualVerdictTests
{
    // -----------------------------------------------------------------------
    // CL-10 tolerance: LegalDefault=0.50, Min=0.00, Max=1.00
    // -----------------------------------------------------------------------

    private const decimal LegalDefault = 0.50m;
    private const string ProductId = "TC-DUAL-TEST";
    private const decimal CreditLine = 100_000m;

    // Formulaic parameters for predictable test values
    // CAT formula: ((creditLine * tasa + annualCommission) / creditLine) * 100
    // DefaultAnnualCommission = 1500
    // With tasa=0.2736 and creditLine=100000:
    //   computedCatPercent = ((100000 * 0.2736 + 1500) / 100000) * 100
    //                       = (27360 + 1500) / 100000 * 100
    //                       = 28860 / 100000 * 100
    //                       = 28.86%
    // extractedCatPercent  = extractedCat * 100
    //
    // To get diff = 0.30: extractedCat = (28.86 + 0.30) / 100 = 0.2916
    // To get diff = 0.10: extractedCat = (28.86 + 0.10) / 100 = 0.2896

    private const decimal Tasa = 0.2736m;
    private const decimal ComputedCatPercent = 28.86m;   // = ((100000*0.2736+1500)/100000)*100

    private static decimal ExtractedCatForDiff(decimal diff) =>
        (ComputedCatPercent + diff) / 100m;

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle BundleWithAccount() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts:
            [
                new ClientAccount(
                    ClientId: "C-001",
                    ClientName: null,
                    Rfc: null,
                    ClientNumber: null,
                    Address: null,
                    Accounts:
                    [
                        new AccountEntry(
                            AccountRef: "REF-001",
                            ProductId: ProductId,
                            CardNumber: null,
                            Clabe: null,
                            BranchNumber: null,
                            CreditLine: CreditLine,
                            AccountOpenDate: null)
                    ])
            ],
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,   // no bundle tolerance — rule uses LegalDefault
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);
    private static ExtractedField<decimal> Found(decimal v) => ExtractedField<decimal>.Found(v, P1());
    private static ExtractedField<decimal> Missing() => ExtractedField<decimal>.Missing(P1());

    private static StatementModel ModelWithCat(decimal catDecimalFraction)
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var dayCount = new DayCountVerification(PrintedDays: 31, ComputedSpanDays: 30, IsConsistent: true);

        var ps = new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: Missing(),
            pagoMinimo: Missing(),
            pagoMinimoMasMeses: Missing(),
            tasa: Found(Tasa),
            cat: Found(catDecimalFraction),
            saldoDeudorTotal: Missing(),
            creditoDisponible: Missing(),
            adeudoPeriodoAnterior: null,
            cargosRegularesNoMeses: null,
            cargosComprasAMesesCapital: null,
            montoIntereses: null,
            montoComisiones: null,
            ivaInteresesYComisiones: null,
            pagosYAbonos: null,
            saldoCargosRegulares: null,
            saldoCargosAMeses: null);

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            PeriodSummary = ps
        };
    }

    /// <summary>
    /// Builds a <see cref="ResolvedTenantProfile"/> with the given CL-10 effective tolerance.
    /// </summary>
    private static ResolvedTenantProfile TenantWithCl10Tolerance(decimal effectiveTolerance) =>
        new(
            tenantId: "TEST-TENANT",
            tenantName: "Test Tenant",
            effectiveTolerances: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CL-10"] = effectiveTolerance
            },
            deviations: []);

    private static VerificationContext Ctx(
        decimal catDecimalFraction,
        ResolvedTenantProfile? tenantProfile = null)
    {
        var bundle = BundleWithAccount();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: ModelWithCat(catDecimalFraction),
            tenantProfile: tenantProfile);
    }

    private static IVecValidationRule GetCl10Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>();
        foreach (var r in rules)
            if (r.CheckId == "CL-10") return r;
        throw new System.InvalidOperationException("CL-10 rule not found in DI.");
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// A CAT diff of 0.30 is within the legal tolerance (0.50) but exceeds the tenant
    /// tolerance (0.20). LegalBaselineVerdict must be Pass; Verdict (tenant) must be Fail.
    /// </summary>
    [Fact]
    public void Cl10CatRule_TighteningTenant_DiffBetweenLegalAndTenantTolerance_LegalPassTenantFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // diff = 0.30 → within legal 0.50 but outside tenant 0.20
        var cat = ExtractedCatForDiff(0.30m);
        var tenantProfile = TenantWithCl10Tolerance(0.20m);
        var ctx = Ctx(cat, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.30 is within legal tolerance 0.50 → LegalBaselineVerdict must be Pass");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.30 exceeds tenant tolerance 0.20 → effective verdict must be Fail");
        finding.ToleranceApplied.ShouldBe(0.20m, "effective tolerance recorded is the tenant value");
    }

    /// <summary>
    /// When TenantProfile is null both verdicts must be identical (legal baseline path).
    /// </summary>
    [Fact]
    public void Cl10CatRule_LegalBaselineProfile_BothVerdictsEqual()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // diff = 0.30 → within legal 0.50; no tenant profile
        var cat = ExtractedCatForDiff(0.30m);
        var ctx = Ctx(cat, tenantProfile: null);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(finding.Verdict,
            "no tenant profile — both verdicts must be equal (legal baseline path)");
    }

    /// <summary>
    /// When diff is below both tolerances (tenant and legal), both verdicts must be Pass.
    /// </summary>
    [Fact]
    public void Cl10CatRule_TighteningTenant_DiffWithinBoth_BothPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // diff = 0.10 → within both legal (0.50) and tenant (0.20)
        var cat = ExtractedCatForDiff(0.10m);
        var tenantProfile = TenantWithCl10Tolerance(0.20m);
        var ctx = Ctx(cat, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
        finding.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    /// <summary>
    /// When diff exceeds both tolerances both verdicts must be Fail.
    /// </summary>
    [Fact]
    public void Cl10CatRule_TighteningTenant_DiffBeyondBoth_BothFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // diff = 0.60 → exceeds both legal (0.50) and tenant (0.20)
        var cat = ExtractedCatForDiff(0.60m);
        var tenantProfile = TenantWithCl10Tolerance(0.20m);
        var ctx = Ctx(cat, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 exceeds legal tolerance 0.50 → LegalBaselineVerdict must be Fail");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 also exceeds tenant tolerance 0.20 → effective verdict must be Fail");
    }
}
