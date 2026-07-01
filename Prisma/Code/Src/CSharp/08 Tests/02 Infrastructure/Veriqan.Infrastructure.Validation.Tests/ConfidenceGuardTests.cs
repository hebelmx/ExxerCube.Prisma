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
/// Tests for <see cref="ConfidenceGuard"/> (Story 9.5) and the confidence-guard
/// integration in <c>Cl10CatRule</c>.
/// </summary>
public sealed class ConfidenceGuardTests
{
    // -----------------------------------------------------------------------
    // ConfidenceGuard.BelowThreshold — pure helper unit tests
    // -----------------------------------------------------------------------

    [Fact]
    public void BelowThreshold_ConfidenceLessThanThreshold_ReturnsTrue()
    {
        var field = ExtractedField<decimal>.InvalidFormat(0.5m, FieldLocator.PageHint(1));
        // confidence = 0.7, threshold = 0.8

        var result = ConfidenceGuard.BelowThreshold(field, 0.8);

        result.ShouldBeTrue("0.70 < 0.80 → BelowThreshold must return true");
    }

    [Fact]
    public void BelowThreshold_ConfidenceExactlyAtThreshold_ReturnsFalse()
    {
        // Construct a field with confidence exactly at the threshold.
        // ExtractedField constructor accepts any value in [0, 1].
        var field = new ExtractedField<decimal>(
            value: 1.5m,
            confidence: 0.8,
            locator: FieldLocator.PageHint(1),
            status: ExtractionStatus.Extracted);

        var result = ConfidenceGuard.BelowThreshold(field, 0.8);

        result.ShouldBeFalse("0.80 >= 0.80 → boundary passes: BelowThreshold must return false");
    }

    [Fact]
    public void BelowThreshold_ConfidenceAboveThreshold_ReturnsFalse()
    {
        var field = ExtractedField<decimal>.Found(1.5m, FieldLocator.PageHint(1));
        // confidence = 1.0, threshold = 0.8

        var result = ConfidenceGuard.BelowThreshold(field, 0.8);

        result.ShouldBeFalse("1.00 >= 0.80 → BelowThreshold must return false");
    }

    [Fact]
    public void BelowThreshold_ZeroConfidence_ReturnsTrue()
    {
        var field = ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        // confidence = 0.0, threshold = 0.8

        var result = ConfidenceGuard.BelowThreshold(field, 0.8);

        result.ShouldBeTrue("0.00 < 0.80 → Missing field must be below threshold");
    }

    [Fact]
    public void BelowThreshold_ZeroThreshold_NeverBelowExceptNegative()
    {
        var field = ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        // confidence = 0.0, threshold = 0.0 → boundary: 0.0 >= 0.0 → false

        var result = ConfidenceGuard.BelowThreshold(field, 0.0);

        result.ShouldBeFalse("0.00 >= 0.00 → exact zero threshold: even Missing passes at zero threshold");
    }

    // -----------------------------------------------------------------------
    // ConfidenceGuard.Reason — format verification
    // -----------------------------------------------------------------------

    [Fact]
    public void Reason_FormatsObservedAndRequiredCorrectly()
    {
        var reason = ConfidenceGuard.Reason("CAT", 0.70, 0.80);

        reason.ShouldBe("CAT field confidence 0.70 < required 0.80");
    }

    [Fact]
    public void Reason_NullFieldName_Throws()
    {
        Should.Throw<ArgumentException>(() => ConfidenceGuard.Reason(null!, 0.7, 0.8));
    }

    [Fact]
    public void Reason_WhiteSpaceFieldName_Throws()
    {
        Should.Throw<ArgumentException>(() => ConfidenceGuard.Reason("   ", 0.7, 0.8));
    }

    // -----------------------------------------------------------------------
    // TenantProfile.MinFieldConfidence — property and default
    // -----------------------------------------------------------------------

    [Fact]
    public void TenantProfile_DefaultMinFieldConfidence_IsLegalDefault()
    {
        var profile = new TenantProfile("T-001", "Test Tenant");

        profile.MinFieldConfidence.ShouldBe(TenantProfile.LegalMinFieldConfidenceDefault,
            "omitting minFieldConfidence must default to the legal floor (0.8)");
    }

    [Fact]
    public void TenantProfile_CustomMinFieldConfidence_IsStored()
    {
        var profile = new TenantProfile("T-001", "Test Tenant", minFieldConfidence: 0.95);

        profile.MinFieldConfidence.ShouldBe(0.95);
    }

    [Fact]
    public void TenantProfile_MinFieldConfidenceOutOfRange_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TenantProfile("T-001", "Test Tenant", minFieldConfidence: 1.1));
    }

    [Fact]
    public void TenantProfile_LegalBaseline_HasDefaultConfidence()
    {
        var profile = TenantProfile.LegalBaseline();

        profile.MinFieldConfidence.ShouldBe(TenantProfile.LegalMinFieldConfidenceDefault);
    }

    // -----------------------------------------------------------------------
    // ResolvedTenantProfile.MinFieldConfidence — property and default
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolvedTenantProfile_DefaultMinFieldConfidence_IsLegalDefault()
    {
        var resolved = new ResolvedTenantProfile(
            tenantId: "T-001",
            tenantName: "Test",
            effectiveTolerances: new Dictionary<string, decimal>(),
            deviations: []);

        resolved.MinFieldConfidence.ShouldBe(TenantProfile.LegalMinFieldConfidenceDefault);
    }

    [Fact]
    public void ResolvedTenantProfile_CustomMinFieldConfidence_IsStored()
    {
        var resolved = new ResolvedTenantProfile(
            tenantId: "T-001",
            tenantName: "Test",
            effectiveTolerances: new Dictionary<string, decimal>(),
            deviations: [],
            minFieldConfidence: 0.92);

        resolved.MinFieldConfidence.ShouldBe(0.92);
    }

    // -----------------------------------------------------------------------
    // TenantProfile.VerbatimSimilarityThreshold — property, default, and bounds
    // -----------------------------------------------------------------------

    [Fact]
    public void TenantProfile_DefaultVerbatimSimilarityThreshold_Is0_82()
    {
        var profile = new TenantProfile("T-001", "Test Tenant");

        profile.VerbatimSimilarityThreshold.ShouldBe(TenantProfile.LegalVerbatimSimilarityThresholdDefault,
            "omitting verbatimSimilarityThreshold must default to 0.82");
    }

    [Fact]
    public void TenantProfile_CustomVerbatimSimilarityThreshold_IsStored()
    {
        var profile = new TenantProfile("T-001", "Test Tenant", verbatimSimilarityThreshold: 0.95);

        profile.VerbatimSimilarityThreshold.ShouldBe(0.95);
    }

    [Fact]
    public void TenantProfile_VerbatimSimilarityThresholdBelowZero_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TenantProfile("T-001", "Test Tenant", verbatimSimilarityThreshold: -0.01));
    }

    [Fact]
    public void TenantProfile_VerbatimSimilarityThresholdAboveOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TenantProfile("T-001", "Test Tenant", verbatimSimilarityThreshold: 1.01));
    }

    [Fact]
    public void TenantProfile_LegalBaseline_HasDefaultVerbatimSimilarityThreshold()
    {
        var profile = TenantProfile.LegalBaseline();

        profile.VerbatimSimilarityThreshold.ShouldBe(TenantProfile.LegalVerbatimSimilarityThresholdDefault);
    }

    // -----------------------------------------------------------------------
    // ResolvedTenantProfile.VerbatimSimilarityThreshold — property and default
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolvedTenantProfile_DefaultVerbatimSimilarityThreshold_Is0_82()
    {
        var resolved = new ResolvedTenantProfile(
            tenantId: "T-001",
            tenantName: "Test",
            effectiveTolerances: new Dictionary<string, decimal>(),
            deviations: []);

        resolved.VerbatimSimilarityThreshold.ShouldBe(TenantProfile.LegalVerbatimSimilarityThresholdDefault);
    }

    [Fact]
    public void ResolvedTenantProfile_CustomVerbatimSimilarityThreshold_IsStored()
    {
        var resolved = new ResolvedTenantProfile(
            tenantId: "T-001",
            tenantName: "Test",
            effectiveTolerances: new Dictionary<string, decimal>(),
            deviations: [],
            verbatimSimilarityThreshold: 0.90);

        resolved.VerbatimSimilarityThreshold.ShouldBe(0.90);
    }

    // -----------------------------------------------------------------------
    // CL-10 confidence-guard integration tests
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-CONF-TEST";
    private const decimal CreditLine = 100_000m;
    private const decimal Tasa = 0.2736m;

    // Formula: ((100000 * 0.2736 + 1500) / 100000) * 100 = 28.86%
    private const decimal ComputedCatPercent = 28.86m;

    private static decimal CatForDiff(decimal diff) => (ComputedCatPercent + diff) / 100m;

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    /// <summary>
    /// Constructs an <see cref="ExtractedField{T}"/> with an arbitrary confidence value,
    /// bypassing the factory helpers (which pin confidence to 0.0, 0.7, or 1.0).
    /// </summary>
    private static ExtractedField<decimal> FieldWithConfidence(decimal value, double confidence) =>
        new(value: value,
            confidence: confidence,
            locator: P1(),
            status: ExtractionStatus.Extracted);

    private static ExtractedField<decimal> Found(decimal v) => ExtractedField<decimal>.Found(v, P1());
    private static ExtractedField<decimal> Missing() => ExtractedField<decimal>.Missing(P1());

    private static StatementModel ModelWith(ExtractedField<decimal> cat, ExtractedField<decimal> tasa)
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
            tasa: tasa,
            cat: cat,
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

    private static VecReferenceBundle Bundle() =>
        new(
            BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
            Products: [new VecProduct(ProductId, "Test Card", null, false, null, null, null)],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts:
            [
                new ClientAccount("C-001", null, null, null, null,
                    [new AccountEntry("REF-001", ProductId, null, null, null, CreditLine, null)])
            ],
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext Ctx(
        ExtractedField<decimal> cat,
        ExtractedField<decimal> tasa,
        ResolvedTenantProfile? tenantProfile = null)
    {
        var bundle = Bundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: new VecProduct(ProductId, "Test Card", null, false, null, null, null),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: ModelWith(cat, tasa),
            tenantProfile: tenantProfile);
    }

    private static ResolvedTenantProfile ProfileWithConfidence(double minConfidence) =>
        new(
            tenantId: "TEST-TENANT",
            tenantName: "Test Tenant",
            effectiveTolerances: new Dictionary<string, decimal>(),
            deviations: [],
            minFieldConfidence: minConfidence);

    private static IVecValidationRule GetCl10Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        foreach (var r in sp.GetServices<IVecValidationRule>())
            if (r.CheckId == "CL-10") return r;
        throw new InvalidOperationException("CL-10 rule not found in DI.");
    }

    /// <summary>
    /// A CAT field with confidence 0.70 (InvalidFormat) is below the default threshold 0.80.
    /// CL-10 must abstain (InsufficientData) even when the arithmetic would otherwise Fail.
    /// The finding's Observed text must cite the field name, observed, and required confidence.
    /// </summary>
    [Fact]
    public void Cl10CatRule_CatBelowDefaultThreshold_AbstainsWithReason_NotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // CAT confidence 0.70 < 0.80 default threshold; TASA is clean (1.0)
        var cat = FieldWithConfidence(CatForDiff(0.60m), confidence: 0.7); // would Fail arithmetically
        var tasa = Found(Tasa);
        var ctx = Ctx(cat, tasa, tenantProfile: null);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "CAT confidence 0.70 < 0.80 threshold → rule must abstain, not Fail");
        var observed = finding.Observed;
        observed.ShouldNotBeNull("InsufficientData finding must carry an Observed reason string");
        observed!.ShouldContain("CAT");   // reason cites the field name
        observed.ShouldContain("0.70");   // reason cites observed confidence
        observed.ShouldContain("0.80");   // reason cites required threshold
    }

    /// <summary>
    /// A TASA field with confidence 0.70 is below the default threshold 0.80.
    /// CL-10 must abstain even though CAT confidence is fine.
    /// </summary>
    [Fact]
    public void Cl10CatRule_TasaBelowDefaultThreshold_AbstainsWithReason_NotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // TASA confidence 0.70 < 0.80; CAT is clean (1.0)
        var cat = Found(CatForDiff(0.60m)); // would Fail arithmetically
        var tasa = FieldWithConfidence(Tasa, confidence: 0.7);
        var ctx = Ctx(cat, tasa, tenantProfile: null);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "TASA confidence 0.70 < 0.80 threshold → rule must abstain, not Fail");
        var tasaObserved = finding.Observed;
        tasaObserved.ShouldNotBeNull("InsufficientData finding must carry an Observed reason string");
        tasaObserved!.ShouldContain("TASA");   // reason cites the field name
        tasaObserved.ShouldContain("0.70");    // reason cites observed confidence
    }

    /// <summary>
    /// Fields with confidence exactly at the threshold (0.80) must pass the guard
    /// and proceed to normal evaluation.
    /// </summary>
    [Fact]
    public void Cl10CatRule_CatAtExactThreshold_ProceedsToNormalEval()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // CAT confidence exactly 0.80 (boundary); diff = 0.10 → within legal tolerance 0.50 → Pass
        var cat = FieldWithConfidence(CatForDiff(0.10m), confidence: 0.8);
        var tasa = Found(Tasa);
        var ctx = Ctx(cat, tasa, tenantProfile: null);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Pass,
            "confidence 0.80 equals the threshold → guard passes → normal arithmetic → Pass");
    }

    /// <summary>
    /// Fields with confidence 1.0 (Found) always pass the guard — this is the existing behaviour
    /// for all current tests. Regression check: no existing Pass or Fail becomes InsufficientData.
    /// </summary>
    [Fact]
    public void Cl10CatRule_CleanFoundField_PassesConfidenceGuard_ExistingBehaviourUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // diff = 0.30 → within legal 0.50 → Pass (identical to existing Cl10DualVerdictTests)
        var cat = Found(CatForDiff(0.30m)); // confidence 1.0
        var tasa = Found(Tasa);             // confidence 1.0
        var ctx = Ctx(cat, tasa, tenantProfile: null);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldNotBe(FindingVerdict.InsufficientData,
            "Found fields (confidence 1.0) must not trigger the confidence guard");
    }

    /// <summary>
    /// A tightened tenant profile with MinFieldConfidence = 0.90 causes a CAT field with
    /// confidence 0.85 (which passes the default 0.80 guard) to abstain.
    /// </summary>
    [Fact]
    public void Cl10CatRule_TightenedTenantThreshold_PreviouslyOkFieldNowAbstains()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // CAT confidence 0.85 → passes default (0.80) but fails tightened (0.90)
        var cat = FieldWithConfidence(CatForDiff(0.10m), confidence: 0.85);
        var tasa = Found(Tasa);
        var tenantProfile = ProfileWithConfidence(0.90);
        var ctx = Ctx(cat, tasa, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "confidence 0.85 < tightened threshold 0.90 → rule must abstain under tenant profile");
        var tightenedObserved = finding.Observed;
        tightenedObserved.ShouldNotBeNull("InsufficientData finding must carry an Observed reason string");
        tightenedObserved!.ShouldContain("0.85");   // reason cites observed confidence
        tightenedObserved.ShouldContain("0.90");    // reason cites tightened threshold
    }

    /// <summary>
    /// Threshold is read from the context's tenant profile.
    /// When the profile has the default 0.8 threshold, a field with confidence 0.85 passes.
    /// </summary>
    [Fact]
    public void Cl10CatRule_DefaultTenantThreshold_ConfidenceAboveDefault_ProceedsNormally()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl10Rule();

        // CAT confidence 0.85 → passes default threshold 0.80
        var cat = FieldWithConfidence(CatForDiff(0.10m), confidence: 0.85); // diff=0.10 < 0.50 → Pass
        var tasa = Found(Tasa);
        var tenantProfile = ProfileWithConfidence(TenantProfile.LegalMinFieldConfidenceDefault); // 0.8
        var ctx = Ctx(cat, tasa, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Pass,
            "confidence 0.85 >= default threshold 0.80 → passes guard → arithmetic Pass");
    }
}
