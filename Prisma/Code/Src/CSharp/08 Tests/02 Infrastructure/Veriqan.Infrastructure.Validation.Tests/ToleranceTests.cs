using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Story 9.4 — unit tests for <see cref="Tolerance"/>, <see cref="ToleranceResolution"/>,
/// and <see cref="ILegalToleranceProvider"/> (via <c>DefaultLegalToleranceProvider</c>).
/// Also contains regression tests showing that migrated rules still use the legal default
/// when the bundle carries no override.
/// </summary>
public sealed class ToleranceTests
{
    // -----------------------------------------------------------------------
    // Tolerance.Resolve tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Tolerance_Resolve_NullOverride_ReturnsLegalDefault()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        var resolution = tol.Resolve(null);

        resolution.EffectiveValue.ShouldBe(0.50m);
        resolution.OverrideRejected.ShouldBeFalse();
        resolution.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void Tolerance_Resolve_InRangeOverride_ReturnsOverride()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        var resolution = tol.Resolve(0.25m); // in [0.00, 1.00]

        resolution.EffectiveValue.ShouldBe(0.25m);
        resolution.OverrideRejected.ShouldBeFalse();
        resolution.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void Tolerance_Resolve_OverrideAtMinBoundary_IsAccepted()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        var resolution = tol.Resolve(0.00m);

        resolution.EffectiveValue.ShouldBe(0.00m);
        resolution.OverrideRejected.ShouldBeFalse();
    }

    [Fact]
    public void Tolerance_Resolve_OverrideAtMaxBoundary_IsAccepted()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        var resolution = tol.Resolve(1.00m);

        resolution.EffectiveValue.ShouldBe(1.00m);
        resolution.OverrideRejected.ShouldBeFalse();
    }

    [Fact]
    public void Tolerance_Resolve_OverrideAboveLegalCeiling_IsRejected_LegalDefaultApplied()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        var resolution = tol.Resolve(1.50m); // exceeds Max=1.00

        resolution.EffectiveValue.ShouldBe(0.50m); // falls back to legal default
        resolution.OverrideRejected.ShouldBeTrue();
        resolution.RejectionReason.ShouldNotBeNullOrEmpty();
        resolution.RejectionReason!.ShouldContain("1.50");
        resolution.RejectionReason.ShouldContain("1.00"); // Max
    }

    [Fact]
    public void Tolerance_Resolve_OverrideBelowLegalFloor_IsRejected_LegalDefaultApplied()
    {
        // A spec where min > 0 (e.g. exchange rate cannot be zero)
        var tol = new Tolerance(legalDefault: 0.10m, min: 0.05m, max: 0.20m);

        var resolution = tol.Resolve(0.01m); // below Min=0.05

        resolution.EffectiveValue.ShouldBe(0.10m); // falls back to legal default
        resolution.OverrideRejected.ShouldBeTrue();
        resolution.RejectionReason.ShouldNotBeNullOrEmpty();
        resolution.RejectionReason!.ShouldContain("0.01");
        resolution.RejectionReason.ShouldContain("0.05"); // Min (floor)
    }

    [Fact]
    public void Tolerance_Resolve_DoesNotThrow_WhenOutOfRange()
    {
        var tol = new Tolerance(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        // Must not throw — rejection is expressed as data
        Should.NotThrow(() => tol.Resolve(999m));
        Should.NotThrow(() => tol.Resolve(-1m));
    }

    // -----------------------------------------------------------------------
    // Tolerance construction guard tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Tolerance_Construction_MinGreaterThanLegalDefault_ThrowsArgumentException()
    {
        var ex = Should.Throw<ArgumentException>(
            () => new Tolerance(legalDefault: 0.50m, min: 0.75m, max: 1.00m));

        ex.ParamName.ShouldBe("min");
    }

    [Fact]
    public void Tolerance_Construction_LegalDefaultGreaterThanMax_ThrowsArgumentException()
    {
        var ex = Should.Throw<ArgumentException>(
            () => new Tolerance(legalDefault: 1.50m, min: 0.00m, max: 1.00m));

        ex.ParamName.ShouldBe("legalDefault");
    }

    [Fact]
    public void Tolerance_Construction_MinEqualsLegalDefaultEqualsMax_IsValid()
    {
        // Degenerate case: exact value enforced
        Should.NotThrow(() => new Tolerance(legalDefault: 0.50m, min: 0.50m, max: 0.50m));
    }

    // -----------------------------------------------------------------------
    // DefaultLegalToleranceProvider tests
    // -----------------------------------------------------------------------

    private static ILegalToleranceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddVeriqanValidation();
        return services.BuildServiceProvider().GetRequiredService<ILegalToleranceProvider>();
    }

    [Theory]
    [InlineData("CL-10")]
    [InlineData("CL-17")]
    [InlineData("CL-18")]
    [InlineData("CL-19")]
    [InlineData("CL-20")]
    [InlineData("CL-21")]
    [InlineData("CL-22")]
    [InlineData("CL-24")]
    [InlineData("CL-25")]
    [InlineData("CL-44")]
    [InlineData("ITEM-58")]
    public void DefaultLegalToleranceProvider_For_CurrencyRule_ReturnsCurrencyMxnSpec(string checkId)
    {
        var provider = BuildProvider();

        var tol = provider.For(checkId);

        tol.LegalDefault.ShouldBe(0.50m);
        tol.Min.ShouldBe(0.00m);
        tol.Max.ShouldBe(1.00m);
        provider.Has(checkId).ShouldBeTrue();
    }

    [Theory]
    [InlineData("CL-39")]
    public void DefaultLegalToleranceProvider_For_PointsRule_ReturnsPointsSpec(string checkId)
    {
        var provider = BuildProvider();

        var tol = provider.For(checkId);

        tol.LegalDefault.ShouldBe(1.00m);
        tol.Min.ShouldBe(0.00m);
        tol.Max.ShouldBe(2.00m);
        provider.Has(checkId).ShouldBeTrue();
    }

    [Theory]
    [InlineData("CL-37")]
    public void DefaultLegalToleranceProvider_For_ExchangeRateRule_ReturnsExchangeRateSpec(string checkId)
    {
        var provider = BuildProvider();

        var tol = provider.For(checkId);

        tol.LegalDefault.ShouldBe(0.10m);
        tol.Min.ShouldBe(0.05m);
        tol.Max.ShouldBe(0.20m);
        provider.Has(checkId).ShouldBeTrue();
    }

    [Fact]
    public void DefaultLegalToleranceProvider_Has_UnknownCheckId_ReturnsFalse()
    {
        var provider = BuildProvider();

        provider.Has("CL-UNKNOWN-999").ShouldBeFalse();
        provider.Has("").ShouldBeFalse();
        provider.Has("   ").ShouldBeFalse();
    }

    [Fact]
    public void DefaultLegalToleranceProvider_For_UnknownCheckId_ThrowsArgumentException()
    {
        var provider = BuildProvider();

        var ex = Should.Throw<ArgumentException>(() => provider.For("CL-UNKNOWN-999"));
        ex.ParamName.ShouldBe("checkId");
    }

    [Fact]
    public void DefaultLegalToleranceProvider_For_IsCaseInsensitive()
    {
        var provider = BuildProvider();

        // All variants must find the same spec
        provider.For("cl-10").LegalDefault.ShouldBe(0.50m);
        provider.For("CL-10").LegalDefault.ShouldBe(0.50m);
        provider.For("Cl-10").LegalDefault.ShouldBe(0.50m);
    }

    /// <summary>
    /// DefaultLegalToleranceProvider.IvaRate must return 0.16 (16%) as the Mexican IVA
    /// constant mandated by Banxico Circular 13/2011. This is the S13 configurable-rate
    /// default — changing it here would break §6/§16 recomputation for all statements.
    /// </summary>
    [Fact]
    public void DefaultLegalToleranceProvider_IvaRate_Returns16Percent()
    {
        var provider = BuildProvider();

        provider.IvaRate.ShouldBe(0.16m,
            "Mexican IVA rate per Banxico Circular 13/2011 must default to 0.16 (16%)");
    }

    // -----------------------------------------------------------------------
    // Rule-level regression tests (story 9.4)
    // -----------------------------------------------------------------------

    // Shared fixtures
    private const string ProductId = "TC-TOL-TEST";
    private const decimal CreditLine = 100_000m;

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Tolerance Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static ExtractedField<decimal> Found(decimal value) =>
        ExtractedField<decimal>.Found(value, P1());

    private static ExtractedField<decimal> Missing() =>
        ExtractedField<decimal>.Missing(P1());

    private static DayCountVerification DayCount() =>
        new(PrintedDays: 30, ComputedSpanDays: 30, IsConsistent: true);

    private static PeriodSummary MinimalSummary(
        ExtractedField<decimal>? saldoCargosRegulares = null,
        ExtractedField<decimal>? saldoCargosAMeses = null,
        ExtractedField<decimal>? saldoDeudorTotal = null,
        ExtractedField<decimal>? pagoParaNoGenerarIntereses = null)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: DayCount(),
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses ?? Missing(),
            pagoMinimo: Missing(),
            pagoMinimoMasMeses: Missing(),
            tasa: Missing(),
            cat: Missing(),
            saldoDeudorTotal: saldoDeudorTotal ?? Missing(),
            creditoDisponible: Missing(),
            adeudoPeriodoAnterior: null,
            cargosRegularesNoMeses: null,
            cargosComprasAMesesCapital: null,
            montoIntereses: null,
            montoComisiones: null,
            ivaInteresesYComisiones: null,
            pagosYAbonos: null,
            saldoCargosRegulares: saldoCargosRegulares,
            saldoCargosAMeses: saldoCargosAMeses);
    }

    private static StatementModel ModelWith(PeriodSummary ps)
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
            PeriodSummary = ps
        };
    }

    private static VecReferenceBundle BundleWith(ToleranceConfig? toleranceConfig) =>
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
            ToleranceConfig: toleranceConfig,
            ValidationConstants: null);

    private static VerificationContext Ctx(VecReferenceBundle bundle, StatementModel? model = null) =>
        new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: bundle.ToleranceConfig,
            statementModel: model);

    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        foreach (var r in sp.GetServices<IVecValidationRule>())
            if (r.CheckId == checkId) return r;
        throw new InvalidOperationException($"Rule {checkId} not found in DI.");
    }

    /// <summary>
    /// CL-22 with no bundle ToleranceConfig at all: legal default (0.50 MXN) must apply,
    /// and matching values must produce Pass.
    /// </summary>
    [Fact]
    public void Cl22_NoToleranceConfigInBundle_LegalDefaultApplies_MatchingValuesReturnPass()
    {
        var rule = GetRule("CL-22");
        // SaldoCargosRegulares == PagoParaNoGenerarIntereses exactly
        var ps = MinimalSummary(
            saldoCargosRegulares: Found(32446.69m),
            pagoParaNoGenerarIntereses: Found(32446.69m));
        // Bundle has null ToleranceConfig → legal default 0.50 MXN used
        var ctx = Ctx(BundleWith(null), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(0.50m); // legal default
    }

    /// <summary>
    /// CL-22 with an out-of-range override (2.00m exceeds Max=1.00):
    /// the override is rejected and the legal default (0.50m) applies.
    /// The pass/fail outcome depends on the legal default, not the rejected override.
    /// </summary>
    [Fact]
    public void Cl22_OutOfRangeBundleOverride_IsRejected_LegalDefaultApplied()
    {
        var rule = GetRule("CL-22");
        // Difference = 0.80m (between legal default 0.50 and rejected override 2.00)
        // With legal default 0.50: 0.80 > 0.50 → Fail
        // With rejected override 2.00: 0.80 ≤ 2.00 → would be Pass (wrong behavior)
        var ps = MinimalSummary(
            saldoCargosRegulares: Found(32446.69m),
            pagoParaNoGenerarIntereses: Found(32447.49m)); // diff = 0.80m
        var bundle = BundleWith(new ToleranceConfig(
            CurrencyToleranceMxn: 2.00m,   // outside Max=1.00 → rejected
            PointsTolerance: null,
            RewardsPesosToleranceMxn: null,
            PointsToPesosExchangeRate: null));
        var ctx = Ctx(bundle, ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        // Legal default (0.50m) is used: diff 0.80 > 0.50 → Fail
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.ToleranceApplied.ShouldBe(0.50m); // legal default, not the rejected 2.00m
    }

    /// <summary>
    /// CL-22 with a tenant-profile override of 0.20m (tighter than legal 0.50m):
    /// a diff of 0.30m exceeds the tenant bar (0.20m) → Fail;
    /// the legal baseline (0.50m) still passes → LegalBaselineVerdict = Pass.
    /// Story 9.6: bundle ToleranceConfig is no longer used to drive effective tolerance;
    /// the tightened bar comes from <c>ResolvedTenantProfile.GetEffectiveTolerance</c> instead.
    /// </summary>
    [Fact]
    public void Cl22_InRangeTightenedOverride_Applies_DiffBeyondTightenedToleranceFails()
    {
        var rule = GetRule("CL-22");
        // Diff = 0.30m; with legal default 0.50 → Pass; with tenant tightened 0.20m → Fail
        var ps = MinimalSummary(
            saldoCargosRegulares: Found(32446.69m),
            pagoParaNoGenerarIntereses: Found(32446.99m)); // diff = 0.30m

        // Story 9.6: effective tolerance comes from TenantProfile, NOT bundle ToleranceConfig.
        // Bundle may carry any (or no) ToleranceConfig — the rule ignores it for evaluation.
        var bundle = BundleWith(null);
        var tenantProfile = new ResolvedTenantProfile(
            tenantId: "TEST-TENANT",
            tenantName: "Test Tenant",
            effectiveTolerances: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CL-22"] = 0.20m   // tighter than legal 0.50m
            },
            deviations: []);

        var ctx = new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: bundle.ToleranceConfig,
            statementModel: ModelWith(ps),
            tenantProfile: tenantProfile);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        // Tightened override (0.20m) from TenantProfile: diff 0.30 > 0.20 → Fail
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.ToleranceApplied.ShouldBe(0.20m); // tenant tightened tolerance applied
        // Legal baseline (0.50m): diff 0.30 ≤ 0.50 → LegalBaselineVerdict = Pass (divergence)
        result.Value.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
    }

    /// <summary>
    /// CL-24 with no bundle ToleranceConfig at all: legal default must apply,
    /// and matching sums must produce Pass.
    /// </summary>
    [Fact]
    public void Cl24_NoToleranceConfigInBundle_LegalDefaultApplies_MatchingSumsReturnPass()
    {
        var rule = GetRule("CL-24");
        // SaldoCargosRegulares(32446.69) + SaldoCargosAMeses(19941.16) = 52387.85
        var ps = MinimalSummary(
            saldoCargosRegulares: Found(32446.69m),
            saldoCargosAMeses: Found(19941.16m),
            saldoDeudorTotal: Found(52387.85m));
        var ctx = Ctx(BundleWith(null), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(0.50m);
    }
}
