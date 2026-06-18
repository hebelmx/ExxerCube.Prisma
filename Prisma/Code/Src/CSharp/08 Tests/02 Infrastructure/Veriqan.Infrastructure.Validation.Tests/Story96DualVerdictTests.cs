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
/// Story 9.6 dual-verdict divergence and confidence-abstain tests for the retrofitted
/// <c>TenantTightenableOnly</c> rules.
/// Covers representative rules: CL-20, CL-22, ITEM-58.
/// </summary>
/// <remarks>
/// <para>
/// Each rule group exercises:
/// <list type="bullet">
///   <item>Dual-verdict divergence: tenant tightens → LegalBaselineVerdict=Pass / Verdict=Fail on a borderline amount.</item>
///   <item>Confidence-abstain: a relevant <c>ExtractedField&lt;decimal&gt;</c> at confidence 0.70 (below legal 0.80 default) → InsufficientData.</item>
///   <item>Both verdicts Pass/Fail when diff is within / outside both tolerances.</item>
/// </list>
/// </para>
/// <para>
/// Legal tolerance default for all tested rules: 0.50 MXN (from the encrypted SQL legal store).
/// Tenant tightened tolerance used throughout: 0.20 MXN.
/// </para>
/// </remarks>
public sealed class Story96DualVerdictTests
{
    // -----------------------------------------------------------------------
    // Legal tolerance (matches the ILegalToleranceProvider SQL seed value)
    // -----------------------------------------------------------------------

    private const decimal LegalTolerance = 0.50m;
    private const decimal TenantTolerance = 0.20m;
    private const string ProductId = "TC-9-6-TEST";
    private const decimal CreditLine = 50_000m;
    private const double LowConfidence = 0.70;   // below the 0.80 legal minimum
    private const double OkConfidence = 1.0;

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    /// <summary>
    /// Returns an <see cref="ExtractedField{T}"/> with confidence 1.0 (fully extracted).
    /// </summary>
    private static ExtractedField<decimal> Found(decimal value) =>
        ExtractedField<decimal>.Found(value, P1());

    /// <summary>
    /// Returns an <see cref="ExtractedField{T}"/> with custom confidence below the 0.80 legal minimum,
    /// status Extracted (value is present but low confidence).
    /// </summary>
    private static ExtractedField<decimal> FoundLowConfidence(decimal value) =>
        new(value, LowConfidence, P1(), ExtractionStatus.Extracted);

    private static ExtractedField<decimal> Missing() =>
        ExtractedField<decimal>.Missing(P1());

    private static VecReferenceBundle BundleNoTolerance() =>
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
            ToleranceConfig: null,        // no bundle override — legal default applies
            ValidationConstants: null);

    private static VecReferenceBundle BundleWithExpectedTransactions(
        IReadOnlyList<ExpectedTransaction> transactions) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: [new ExpectedTransactionGroup("REF-001", transactions)],
            ToleranceConfig: null,         // no bundle override — legal default applies
            ValidationConstants: null);

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given <see cref="PeriodSummary"/>.
    /// </summary>
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

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with given movements.
    /// </summary>
    private static StatementModel ModelWithMovements(
        IReadOnlyList<StatementMovement> movements,
        MovementsExtractionStatus status = MovementsExtractionStatus.Extracted)
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
            Movements = movements,
            MovementsStatus = status
        };
    }

    /// <summary>
    /// Builds a minimal <see cref="PeriodSummary"/> for CL-20 tests (pagosYAbonos field).
    /// CL-20 formula: |sum(credit movements) − PagosYAbonos| ≤ tolerance.
    /// </summary>
    private static PeriodSummary SummaryForCl20(ExtractedField<decimal>? pagosYAbonos)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var dayCount = new DayCountVerification(PrintedDays: 31, ComputedSpanDays: 30, IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: Missing(),
            pagoMinimo: Missing(),
            pagoMinimoMasMeses: Missing(),
            tasa: Missing(),
            cat: Missing(),
            saldoDeudorTotal: Missing(),
            creditoDisponible: Missing(),
            pagosYAbonos: pagosYAbonos);
    }

    /// <summary>
    /// Builds a minimal <see cref="PeriodSummary"/> for CL-22 tests
    /// (saldoCargosRegulares and pagoParaNoGenerarIntereses).
    /// </summary>
    private static PeriodSummary SummaryForCl22(
        ExtractedField<decimal> saldoCargosRegulares,
        ExtractedField<decimal>? pagoParaNoGenerarIntereses = null)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var dayCount = new DayCountVerification(PrintedDays: 31, ComputedSpanDays: 30, IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses ?? Missing(),
            pagoMinimo: Missing(),
            pagoMinimoMasMeses: Missing(),
            tasa: Missing(),
            cat: Missing(),
            saldoDeudorTotal: Missing(),
            creditoDisponible: Missing(),
            saldoCargosRegulares: saldoCargosRegulares);
    }

    /// <summary>
    /// Builds a <see cref="ResolvedTenantProfile"/> that overrides the given rule's tolerance
    /// to the tighter <see cref="TenantTolerance"/>.
    /// </summary>
    private static ResolvedTenantProfile TenantWith(string checkId, decimal tolerance) =>
        new(
            tenantId: "TEST-TENANT-9-6",
            tenantName: "Test Tenant Story 9.6",
            effectiveTolerances: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [checkId] = tolerance
            },
            deviations: []);

    private static VerificationContext Ctx(
        VecReferenceBundle bundle,
        StatementModel? model,
        ResolvedTenantProfile? tenantProfile = null) =>
        new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model,
            tenantProfile: tenantProfile);

    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        foreach (var r in sp.GetServices<IVecValidationRule>())
            if (r.CheckId == checkId) return r;
        throw new System.InvalidOperationException($"Rule {checkId} not registered in DI.");
    }

    private static StatementMovement MakeMovement(decimal amount, MovementSign sign, string description) =>
        new(operationDate: null, chargeDate: null, description: description,
            amount: amount, sign: sign, locator: P1());

    // -----------------------------------------------------------------------
    // CL-20: PagosYAbonosSumaDesgloseRule
    //   Formula: |sum(credit movements) − PagosYAbonos| ≤ tolerance
    //   Confidence guard: PagosYAbonos field
    // -----------------------------------------------------------------------

    /// <summary>
    /// CL-20 dual-verdict divergence: diff = 0.30, within legal (0.50) but outside tenant (0.20).
    /// LegalBaselineVerdict must be Pass; Verdict (tenant) must be Fail.
    /// CL-20 formula: |sum(Credit movements) − PagosYAbonos| ≤ tolerance.
    /// </summary>
    [Fact]
    public void Cl20_TighteningTenant_DiffBetweenLegalAndTenantTolerance_LegalPassTenantFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-20");

        // PagosYAbonos (printed) = 1000.00; credit movement sum = 1000.30 → diff = 0.30
        // diff 0.30 > TenantTolerance 0.20 → tenant Fail
        // diff 0.30 ≤ LegalTolerance 0.50  → legal Pass
        var ps = SummaryForCl20(pagosYAbonos: Found(1000.00m));
        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(1000.30m, MovementSign.Credit, "PAGO TARJETA")
        ];
        var model = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            PeriodSummary = ps,
            Movements = movements,
            MovementsStatus = MovementsExtractionStatus.Extracted
        };

        var tenantProfile = TenantWith("CL-20", TenantTolerance);
        var ctx = Ctx(BundleNoTolerance(), model, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.30 ≤ legal 0.50 → LegalBaselineVerdict must be Pass");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.30 > tenant 0.20 → primary Verdict must be Fail");
        finding.ToleranceApplied.ShouldBe(TenantTolerance, "tenant tolerance recorded");
    }

    /// <summary>
    /// CL-20 confidence-abstain: PagosYAbonos at confidence 0.70 (below legal 0.80 threshold) → InsufficientData.
    /// The rule checks the PagosYAbonos field confidence before evaluating movements.
    /// </summary>
    [Fact]
    public void Cl20_PagosYAbonosLowConfidence_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-20");

        // PagosYAbonos is Extracted but confidence 0.70 < 0.80 threshold
        var ps = SummaryForCl20(pagosYAbonos: FoundLowConfidence(1000.00m));
        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(1000.00m, MovementSign.Credit, "PAGO TARJETA")
        ];
        var model = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            PeriodSummary = ps,
            Movements = movements,
            MovementsStatus = MovementsExtractionStatus.Extracted
        };
        var ctx = Ctx(BundleNoTolerance(), model);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "confidence 0.70 < 0.80 legal minimum → abstain");
        result.Value.Observed!.ShouldContain("PagosYAbonos");
    }

    /// <summary>
    /// CL-20: when diff is within both tolerances both verdicts must be Pass.
    /// </summary>
    [Fact]
    public void Cl20_TighteningTenant_DiffWithinBoth_BothPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-20");

        // diff = 0.10 → within both legal (0.50) and tenant (0.20)
        // PagosYAbonos = 1000.00; credit movement sum = 1000.10; diff = 0.10
        var ps = SummaryForCl20(pagosYAbonos: Found(1000.00m));
        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(1000.10m, MovementSign.Credit, "PAGO TARJETA")
        ];
        var model = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            PeriodSummary = ps,
            Movements = movements,
            MovementsStatus = MovementsExtractionStatus.Extracted
        };
        var tenantProfile = TenantWith("CL-20", TenantTolerance);
        var ctx = Ctx(BundleNoTolerance(), model, tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
        finding.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // CL-22: SaldoCargosRegularesRule
    //   Formula: |SaldoCargosRegulares − PagoParaNoGenerarIntereses| ≤ tolerance
    //   Confidence guards: SaldoCargosRegulares, PagoParaNoGenerarIntereses
    // -----------------------------------------------------------------------

    /// <summary>
    /// CL-22 dual-verdict divergence: diff = 0.30, within legal (0.50) but outside tenant (0.20).
    /// LegalBaselineVerdict must be Pass; Verdict (tenant) must be Fail.
    /// </summary>
    [Fact]
    public void Cl22_TighteningTenant_DiffBetweenLegalAndTenantTolerance_LegalPassTenantFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-22");

        // SaldoCargosRegulares = 2000.30; PagoParaNoGenerarIntereses = 2000.00 → diff = 0.30
        // diff 0.30 > TenantTolerance 0.20 → tenant Fail
        // diff 0.30 ≤ LegalTolerance 0.50  → legal Pass
        var ps = SummaryForCl22(
            saldoCargosRegulares: Found(2000.30m),
            pagoParaNoGenerarIntereses: Found(2000.00m));
        var tenantProfile = TenantWith("CL-22", TenantTolerance);
        var ctx = Ctx(BundleNoTolerance(), ModelWith(ps), tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.30 ≤ legal 0.50 → LegalBaselineVerdict must be Pass");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.30 > tenant 0.20 → primary Verdict must be Fail");
        finding.ToleranceApplied.ShouldBe(TenantTolerance, "tenant tolerance recorded");
    }

    /// <summary>
    /// CL-22 confidence-abstain: SaldoCargosRegulares at confidence 0.70 → InsufficientData.
    /// </summary>
    [Fact]
    public void Cl22_SaldoCargosRegularesLowConfidence_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-22");

        // SaldoCargosRegulares is Extracted but confidence 0.70 < 0.80 threshold
        var ps = SummaryForCl22(
            saldoCargosRegulares: FoundLowConfidence(2000.00m),
            pagoParaNoGenerarIntereses: Found(2000.00m));
        var ctx = Ctx(BundleNoTolerance(), ModelWith(ps));

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "confidence 0.70 < 0.80 legal minimum → abstain");
        result.Value.Observed!.ShouldContain("SaldoCargosRegulares");
    }

    /// <summary>
    /// CL-22 confidence-abstain: PagoParaNoGenerarIntereses at confidence 0.70 → InsufficientData.
    /// </summary>
    [Fact]
    public void Cl22_PagoParaNoGenerarInteresesLowConfidence_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-22");

        // PagoParaNoGenerarIntereses is Extracted but confidence 0.70 < 0.80 threshold
        var ps = SummaryForCl22(
            saldoCargosRegulares: Found(2000.00m),
            pagoParaNoGenerarIntereses: FoundLowConfidence(2000.00m));
        var ctx = Ctx(BundleNoTolerance(), ModelWith(ps));

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "confidence 0.70 < 0.80 legal minimum → abstain");
        result.Value.Observed!.ShouldContain("PagoParaNoGenerarIntereses");
    }

    /// <summary>
    /// CL-22: when diff exceeds both tolerances both verdicts must be Fail.
    /// </summary>
    [Fact]
    public void Cl22_TighteningTenant_DiffBeyondBoth_BothFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("CL-22");

        // diff = 0.60 → exceeds both legal (0.50) and tenant (0.20)
        var ps = SummaryForCl22(
            saldoCargosRegulares: Found(2000.60m),
            pagoParaNoGenerarIntereses: Found(2000.00m));
        var tenantProfile = TenantWith("CL-22", TenantTolerance);
        var ctx = Ctx(BundleNoTolerance(), ModelWith(ps), tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 > legal 0.50 → LegalBaselineVerdict must be Fail");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 also > tenant 0.20 → primary Verdict must be Fail");
    }

    // -----------------------------------------------------------------------
    // ITEM-58: TransactionAmountMatchRule
    //   Formula: per expected-transaction, |observed amount − expected amount| ≤ tolerance
    //   No per-movement ExtractedField confidence guard (StatementMovement has no confidence)
    // -----------------------------------------------------------------------

    /// <summary>
    /// ITEM-58 dual-verdict divergence: a matched movement amount diff = 0.30, within legal
    /// (0.50) but outside tenant (0.20). LegalBaselineVerdict must be Pass; Verdict must be Fail.
    /// </summary>
    [Fact]
    public void Item58_TighteningTenant_DiffBetweenLegalAndTenantTolerance_LegalPassTenantFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("ITEM-58");

        // Expected: "CARGO GAS" = 500.00
        // Observed on statement: 500.30 → diff = 0.30
        // diff 0.30 > TenantTolerance 0.20 → tenant Fail
        // diff 0.30 ≤ LegalTolerance 0.50  → legal Pass
        var bundle = BundleWithExpectedTransactions([
            new ExpectedTransaction(
                Description: "CARGO GAS",
                Amount: 500.00m,
                OperationDate: null,
                ChargeDate: null,
                Sign: "-")
        ]);

        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(500.30m, MovementSign.Charge, "CARGO GAS")
        ];

        var tenantProfile = TenantWith("ITEM-58", TenantTolerance);
        var ctx = Ctx(bundle, ModelWithMovements(movements), tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.30 ≤ legal 0.50 → LegalBaselineVerdict must be Pass");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.30 > tenant 0.20 → primary Verdict must be Fail");
        finding.ToleranceApplied.ShouldBe(TenantTolerance, "tenant tolerance recorded");
    }

    /// <summary>
    /// ITEM-58: when all matched amounts are within both tolerances both verdicts must be Pass.
    /// </summary>
    [Fact]
    public void Item58_TighteningTenant_AllAmountsWithinBoth_BothPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("ITEM-58");

        // diff = 0.10 → within both legal (0.50) and tenant (0.20)
        var bundle = BundleWithExpectedTransactions([
            new ExpectedTransaction(
                Description: "CARGO GAS",
                Amount: 500.00m,
                OperationDate: null,
                ChargeDate: null,
                Sign: "-")
        ]);

        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(500.10m, MovementSign.Charge, "CARGO GAS")
        ];

        var tenantProfile = TenantWith("ITEM-58", TenantTolerance);
        var ctx = Ctx(bundle, ModelWithMovements(movements), tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.10 ≤ legal 0.50 → LegalBaselineVerdict must be Pass");
        finding.Verdict.ShouldBe(FindingVerdict.Pass,
            "diff=0.10 ≤ tenant 0.20 → primary Verdict must be Pass");
    }

    /// <summary>
    /// ITEM-58: when diff exceeds both tolerances both verdicts must be Fail.
    /// </summary>
    [Fact]
    public void Item58_TighteningTenant_DiffBeyondBoth_BothFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("ITEM-58");

        // diff = 0.60 → exceeds both legal (0.50) and tenant (0.20)
        var bundle = BundleWithExpectedTransactions([
            new ExpectedTransaction(
                Description: "CARGO GAS",
                Amount: 500.00m,
                OperationDate: null,
                ChargeDate: null,
                Sign: "-")
        ]);

        var movements = (IReadOnlyList<StatementMovement>)
        [
            MakeMovement(500.60m, MovementSign.Charge, "CARGO GAS")
        ];

        var tenantProfile = TenantWith("ITEM-58", TenantTolerance);
        var ctx = Ctx(bundle, ModelWithMovements(movements), tenantProfile);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 > legal 0.50 → LegalBaselineVerdict must be Fail");
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "diff=0.60 also > tenant 0.20 → primary Verdict must be Fail");
    }

    /// <summary>
    /// ITEM-58 abstains (InsufficientData) when movements are not extracted.
    /// No per-movement confidence guard — abstain is via MovementsExtractionStatus.
    /// </summary>
    [Fact]
    public void Item58_MovementsNotExtracted_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetRule("ITEM-58");

        var bundle = BundleWithExpectedTransactions([
            new ExpectedTransaction(
                Description: "CARGO GAS",
                Amount: 500.00m,
                OperationDate: null,
                ChargeDate: null,
                Sign: "-")
        ]);

        // Status is NotExtracted — rule must abstain rather than evaluate amounts
        var model = ModelWithMovements([], MovementsExtractionStatus.SectionNotFound);
        var ctx = Ctx(bundle, model);

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "ITEM-58 must abstain when DESGLOSE not extracted (no per-movement confidence guard applies)");
    }
}
