using System.Text.RegularExpressions;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for the Story 4.4 movement-detail validation rules:
/// CL-42, CL-44, CL-45, ITEM-58, and the updated CL-18, CL-19, CL-20.
/// </summary>
/// <remarks>
/// All rules are discovered via Scrutor DI (the same path used in production).
/// Tests construct minimal <see cref="StatementModel"/> / <see cref="VerificationContext"/>
/// objects to drive specific code paths.
/// </remarks>
public sealed class MovementRulesTests
{
    private const decimal Tol = 0.50m;
    private const string ProductId = "TC-MOV-TEST";

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

    private static ToleranceConfig Tolerance() =>
        new(CurrencyToleranceMxn: Tol, PointsTolerance: null,
            RewardsPesosToleranceMxn: null, PointsToPesosExchangeRate: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static ExtractedField<decimal> Found(decimal value) =>
        ExtractedField<decimal>.Found(value, P1());

    private static ExtractedField<decimal> Missing() =>
        ExtractedField<decimal>.Missing(P1());

    private static ExtractedField<DateOnly> DateFound(int year, int month, int day) =>
        ExtractedField<DateOnly>.Found(new DateOnly(year, month, day), P1());

    private static ExtractedField<DateOnly> DateMissing() =>
        ExtractedField<DateOnly>.Missing(P1());

    /// <summary>
    /// Builds a VecReferenceBundle with ToleranceConfig and a single client account.
    /// </summary>
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
                            CreditLine: 100_000m,
                            AccountOpenDate: null)
                    ])
            ],
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: Tolerance(),
            ValidationConstants: null);

    /// <summary>Bundle with NO ToleranceConfig.</summary>
    private static VecReferenceBundle BundleNoTolerance() =>
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

    /// <summary>Bundle with ToleranceConfig and expected transactions.</summary>
    private static VecReferenceBundle BundleWithExpected(
        IReadOnlyList<ExpectedTransaction> transactions,
        string accountRef = "REF-001") =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: [new ExpectedTransactionGroup(accountRef, transactions)],
            ToleranceConfig: Tolerance(),
            ValidationConstants: null);

    /// <summary>Bundle with ToleranceConfig but no expected transactions.</summary>
    private static VecReferenceBundle BundleNoExpected() =>
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
            ToleranceConfig: Tolerance(),
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
        var rules = sp.GetServices<IVecValidationRule>();
        foreach (var r in rules)
            if (r.CheckId == checkId) return r;
        throw new InvalidOperationException($"Rule {checkId} not found in DI.");
    }

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the supplied <see cref="PeriodSummary"/>
    /// and empty movements.
    /// </summary>
    private static StatementModel ModelWith(PeriodSummary? ps)
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
    /// Builds a <see cref="StatementModel"/> with the supplied movements and extraction status.
    /// </summary>
    private static StatementModel ModelWithMovements(
        PeriodSummary? ps,
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
            PeriodSummary = ps,
            Movements = movements,
            MovementsStatus = status,
        };
    }

    /// <summary>Creates a <see cref="PeriodSummary"/> with just the period dates populated.</summary>
    private static PeriodSummary MakeSummaryWithDates(
        ExtractedField<DateOnly>? periodStart = null,
        ExtractedField<DateOnly>? periodCutDate = null,
        ExtractedField<decimal>? cargosRegularesNoMeses = null,
        ExtractedField<decimal>? cargosComprasAMesesCapital = null,
        ExtractedField<decimal>? pagosYAbonos = null,
        ExtractedField<decimal>? totalCargos = null,
        ExtractedField<decimal>? totalAbonos = null)
    {
        var missingDate = DateMissing();
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var dayCount = new DayCountVerification(PrintedDays: 31, ComputedSpanDays: 30, IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: periodStart ?? missingDate,
            periodCutDate: periodCutDate ?? missingDate,
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
            cargosRegularesNoMeses: cargosRegularesNoMeses,
            cargosComprasAMesesCapital: cargosComprasAMesesCapital,
            pagosYAbonos: pagosYAbonos,
            totalCargos: totalCargos,
            totalAbonos: totalAbonos);
    }

    private static StatementMovement MakeMovement(
        decimal amount,
        MovementSign sign,
        string description,
        DateOnly? opDate = null,
        DateOnly? chargeDate = null) =>
        new(opDate, chargeDate, description, amount, sign, FieldLocator.PageHint(3));

    // -----------------------------------------------------------------------
    // CL-42 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl42_AllDatesInPeriod_ReturnsPass()
    {
        var rule = GetRule("CL-42");
        var periodStart = new DateOnly(2025, 7, 5);
        var periodCut = new DateOnly(2025, 8, 4);

        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", new DateOnly(2025, 7, 5), null),
            MakeMovement(200m, MovementSign.Credit, "ABONO",  new DateOnly(2025, 7, 20), null),
            MakeMovement(300m, MovementSign.Charge, "COMPRA", new DateOnly(2025, 8, 4),  null),
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 7, 5),
            periodCutDate: DateFound(2025, 8, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-42");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl42_OneOutOfRangeDate_ReturnsFail()
    {
        var rule = GetRule("CL-42");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "OK",      new DateOnly(2025, 7, 10), null),
            MakeMovement(200m, MovementSign.Charge, "TOO_LATE", new DateOnly(2025, 8, 5), null), // after cut
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 7, 5),
            periodCutDate: DateFound(2025, 8, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("2025-08-05");
    }

    [Fact]
    public void Cl42_NullPeriodDates_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-42");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "ANY", new DateOnly(2025, 7, 10), null),
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateMissing(),        // ← not extracted
            periodCutDate: DateFound(2025, 8, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl42_NoMovements_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-42");
        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 7, 5),
            periodCutDate: DateFound(2025, 8, 4));

        // ModelWith(ps) has no movements, status = SectionNotFound
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl42_CancellationRequested_ReturnsCancelled()
    {
        var rule = GetRule("CL-42");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWithAccount());
        var result = rule.Evaluate(ctx, cts.Token);

        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-42 — RC1.S4.b residual fix (chunk B4): ChargeDate is the period-membership column
    // -----------------------------------------------------------------------

    /// <summary>
    /// Real-corpus residual (B-2026-06, triage class c): a weekend purchase (OperationDate in
    /// the PREVIOUS period) legitimately posts on the first day of the current period
    /// (ChargeDate in-period). The printed statement is correct; CL-42 must pass because period
    /// membership is defined on ChargeDate, not OperationDate.
    /// </summary>
    [Fact]
    public void Cl42_OperationDateBeforePeriodStart_ButChargeDateInPeriod_ReturnsPass()
    {
        var rule = GetRule("CL-42");
        var periodStart = new DateOnly(2025, 5, 5);
        var periodCut = new DateOnly(2025, 6, 4);

        var movements = new List<StatementMovement>
        {
            // Saturday purchase in the previous period, posts on the first day of this period.
            MakeMovement(150m, MovementSign.Charge, "COMPRA FIN DE SEMANA",
                opDate: new DateOnly(2025, 5, 2), chargeDate: new DateOnly(2025, 5, 5)),
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 5, 5),
            periodCutDate: DateFound(2025, 6, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "OperationDate preceding PeriodStart must not fail CL-42 when ChargeDate is in-period " +
            "— real bank behavior lets weekend/previous-period purchases legitimately post in-period");
    }

    /// <summary>
    /// A movement whose ChargeDate is provably outside the period must fail CL-42, even though
    /// this is the primary (not the supplementary) invariant.
    /// </summary>
    [Fact]
    public void Cl42_ChargeDateOutsidePeriod_ReturnsFail()
    {
        var rule = GetRule("CL-42");
        var periodStart = new DateOnly(2025, 5, 5);
        var periodCut = new DateOnly(2025, 6, 4);

        var movements = new List<StatementMovement>
        {
            MakeMovement(150m, MovementSign.Charge, "CARGO TARDIO",
                opDate: new DateOnly(2025, 5, 20), chargeDate: new DateOnly(2025, 6, 10)), // after cut
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 5, 5),
            periodCutDate: DateFound(2025, 6, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("2025-06-10");
    }

    /// <summary>
    /// Supplementary sanity bound: a movement with no ChargeDate (unparseable) but an
    /// OperationDate provably after the period's CutDate is still flagged — a transaction
    /// cannot be dated in the future relative to the period it is billed in.
    /// </summary>
    [Fact]
    public void Cl42_NullChargeDate_ButOperationDateAfterCut_ReturnsFail()
    {
        var rule = GetRule("CL-42");
        var periodStart = new DateOnly(2025, 5, 5);
        var periodCut = new DateOnly(2025, 6, 4);

        var movements = new List<StatementMovement>
        {
            MakeMovement(150m, MovementSign.Charge, "FUTURO",
                opDate: new DateOnly(2025, 6, 10), chargeDate: null), // future op, unparseable charge date
        };

        var ps = MakeSummaryWithDates(
            periodStart: DateFound(2025, 5, 5),
            periodCutDate: DateFound(2025, 6, 4));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "a future-dated OperationDate is still suspicious even when ChargeDate is unparseable");
    }

    // -----------------------------------------------------------------------
    // CL-44 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl44_PrintedTotalsMatchSum_ReturnsPass()
    {
        var rule = GetRule("CL-44");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge,  "CARGO1",  null, null),
            MakeMovement(200m, MovementSign.Charge,  "CARGO2",  null, null),
            MakeMovement(50m,  MovementSign.Credit,  "ABONO1",  null, null),
        };

        var ps = MakeSummaryWithDates(
            totalCargos: Found(300m),   // 100 + 200
            totalAbonos: Found(50m));   // 50

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl44_CargosTotalMismatch_ReturnsFail()
    {
        var rule = GetRule("CL-44");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "CARGO1", null, null),
            MakeMovement(200m, MovementSign.Charge, "CARGO2", null, null),
            MakeMovement(50m,  MovementSign.Credit, "ABONO1", null, null),
        };

        var ps = MakeSummaryWithDates(
            totalCargos: Found(999m),   // should be 300 — off by 699 > tolerance
            totalAbonos: Found(50m));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl44_TotalCargosNotExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-44");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "CARGO", null, null),
        };

        var ps = MakeSummaryWithDates(
            totalCargos: Missing(),     // not extracted
            totalAbonos: Found(0m));

        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl44_NoMovements_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-44");
        var ps = MakeSummaryWithDates(
            totalCargos: Found(300m),
            totalAbonos: Found(50m));

        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    /// <summary>
    /// Story 9.4: null ToleranceConfig no longer blocks CL-44 (legal default 0.50 MXN applies).
    /// When the movements exactly match the printed totals the rule now emits Pass.
    /// </summary>
    [Fact]
    public void Cl44_NullToleranceConfig_LegalDefaultApplies_MatchingTotalsEmitsPass()
    {
        var rule = GetRule("CL-44");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "CARGO", null, null),
        };

        var ps = MakeSummaryWithDates(
            totalCargos: Found(100m),
            totalAbonos: Found(0m));

        // null ToleranceConfig → legal default (0.50 MXN) is resolved automatically by story 9.4.
        // The movements match the totals exactly → Pass.
        var ctx = Ctx(BundleNoTolerance(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(0.50m); // legal default
    }

    // -----------------------------------------------------------------------
    // CL-45 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl45_AllDescriptionsMatch_ReturnsPass()
    {
        var rule = GetRule("CL-45");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX COM CR",    null, null),
            MakeMovement(200m, MovementSign.Credit, "SU ABONO GRACIAS",  null, null),
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX COM CR",   100m, null, null, "+"),
            new("SU ABONO GRACIAS", 200m, null, null, "-"),
        };

        var ctx = Ctx(BundleWithExpected(expected), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl45_MissingMovement_ReturnsFail()
    {
        var rule = GetRule("CL-45");
        // Expected has "BANCA DIGITAL" but movements don't
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX COM CR", null, null),
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX COM CR",  100m, null, null, "+"),
            new("BANCA DIGITAL",   500m, null, null, "-"),  // ← missing from statement
        };

        var ctx = Ctx(BundleWithExpected(expected), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl45_ExtraMovement_ReturnsFail()
    {
        var rule = GetRule("CL-45");
        // Statement has "COMPRA EXTRA" but expected doesn't
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX COM CR", null, null),
            MakeMovement(999m, MovementSign.Charge, "COMPRA EXTRA",   null, null),  // ← extra
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX COM CR", 100m, null, null, "+"),
        };

        var ctx = Ctx(BundleWithExpected(expected), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl45_NoExpectedTransactions_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-45");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", null, null),
        };

        var ctx = Ctx(BundleNoExpected(), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl45_CancellationRequested_ReturnsCancelled()
    {
        var rule = GetRule("CL-45");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWithAccount());
        var result = rule.Evaluate(ctx, cts.Token);

        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // ITEM-58 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Item58_AmountsMatch_ReturnsPass()
    {
        var rule = GetRule("ITEM-58");
        var movements = new List<StatementMovement>
        {
            MakeMovement(329.00m, MovementSign.Charge, "NETFLIX COM CR", null, null),
            MakeMovement(500.00m, MovementSign.Credit, "SU ABONO",       null, null),
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX COM CR", 329.00m, null, null, "+"),
            new("SU ABONO",       500.00m, null, null, "-"),
        };

        var ctx = Ctx(BundleWithExpected(expected), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Item58_AmountExceedsTolerance_ReturnsFail()
    {
        var rule = GetRule("ITEM-58");
        // Movement amount = 330.00, expected = 329.00 → diff = 1.00 > 0.50
        var movements = new List<StatementMovement>
        {
            MakeMovement(330.00m, MovementSign.Charge, "NETFLIX COM CR", null, null),
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX COM CR", 329.00m, null, null, "+"),
        };

        var ctx = Ctx(BundleWithExpected(expected), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Item58_NoExpectedTransactions_ReturnsInsufficientData()
    {
        var rule = GetRule("ITEM-58");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", null, null),
        };

        var ctx = Ctx(BundleNoExpected(), ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    /// <summary>
    /// Story 9.4: null ToleranceConfig no longer blocks ITEM-58 (legal default 0.50 MXN applies).
    /// When the movement amount exactly matches the expected amount the rule now emits Pass.
    /// </summary>
    [Fact]
    public void Item58_NullToleranceConfig_LegalDefaultApplies_MatchingAmountsReturnPass()
    {
        var rule = GetRule("ITEM-58");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", null, null),
        };

        var expected = new List<ExpectedTransaction>
        {
            new("NETFLIX", 100m, null, null, "+"),
        };

        // Bundle with no tolerance → legal default (0.50 MXN) is resolved automatically by story 9.4.
        // The movement amount matches the expected amount exactly → Pass.
        var bundle = new VecReferenceBundle(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: [new ExpectedTransactionGroup("REF-001", expected)],
            ToleranceConfig: null,
            ValidationConstants: null);

        var ctx = Ctx(bundle, ModelWithMovements(null, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(0.50m); // legal default
    }

    // -----------------------------------------------------------------------
    // CL-18 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl18_NonMsiChargesMatchTarget_ReturnsPass()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX COM CR",          null, null),  // non-MSI
            MakeMovement(200m, MovementSign.Charge, "BODEGA AURRERA",          null, null),  // non-MSI
            MakeMovement(500m, MovementSign.Credit, "ABONO",                   null, null),  // credit
        };

        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(300m));   // 100 + 200
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl18_NonMsiChargesDeviate_ReturnsFail()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", null, null),
            MakeMovement(200m, MovementSign.Charge, "BODEGA",  null, null),
        };

        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(999m));   // off by 699
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl18_MsiMovementsExcluded_CorrectlyClassified()
    {
        var rule = GetRule("CL-18");
        // "005 de 012" matches MSI pattern — should NOT count in CL-18 sum
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "REGULAR PURCHASE",             null, null),
            MakeMovement(500m, MovementSign.Charge, "DON COLCHON CUMBRES 005 de 012", null, null), // MSI
        };

        // target = 100 (only the regular charge, MSI excluded)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(100m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // CL-18 — RC1.S4.a interest/commission/IVA exclusion (real-corpus triage, class c)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Real-layout-shaped case: DESGLOSE contains interest, commission, and IVA-on-interest/
    /// commission rows alongside a regular charge. Only the regular charge counts toward
    /// "Cargos regulares (no a meses)" — the other three have their own dedicated RESUMEN lines
    /// and must be excluded, mirroring the printed statement (real-corpus triage evidence:
    /// Observed−Expected == MontoIntereses+MontoComisiones+IvaInteresesYComisiones to the cent).
    /// </summary>
    [Fact]
    public void Cl18_InterestCommissionIvaRowsExcluded_MatchesRegularOnlyTarget_ReturnsPass()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m,   MovementSign.Charge, "NETFLIX COM CR",                    null, null), // regular
            MakeMovement(50m,    MovementSign.Charge, "MONTO DE INTERESES",                null, null), // interest — excluded
            MakeMovement(20m,    MovementSign.Charge, "COMISION ANUALIDAD",                null, null), // commission — excluded
            MakeMovement(11.20m, MovementSign.Charge, "IVA POR INTERESES Y/O COMISIONES",  null, null), // IVA — excluded
        };

        // target = 100 (only the regular charge; interest/commission/IVA rows excluded)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(100m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "interest/commission/IVA DESGLOSE rows must be excluded from the CL-18 sum");
    }

    /// <summary>
    /// Baseline/regression: without the RC1.S4.a exclusion, the same fixture would sum
    /// 100+50+20+11.20 = 181.20 against a target of 100 → Fail. This test documents the
    /// pre-fix failure mode and proves the exclusion is load-bearing (not vacuous).
    /// </summary>
    [Fact]
    public void Cl18_InterestCommissionIvaRowsExcluded_WithoutFixWouldHaveFailed()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m,   MovementSign.Charge, "NETFLIX COM CR",                   null, null),
            MakeMovement(50m,    MovementSign.Charge, "MONTO DE INTERESES",               null, null),
            MakeMovement(20m,    MovementSign.Charge, "COMISION ANUALIDAD",               null, null),
            MakeMovement(11.20m, MovementSign.Charge, "IVA POR INTERESES Y/O COMISIONES", null, null),
        };

        // Target set to the OLD (pre-fix) expectation of summing everything (181.20).
        // Post-fix, the rule now excludes the 3 non-regular rows, so the sum is 100 —
        // deviating from 181.20 by 81.20 > tolerance → Fail. This is the mirror assertion
        // of the Pass test above and proves both branches are reachable.
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(181.20m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "post-fix the rule correctly excludes interest/commission/IVA rows, so a target " +
            "computed the old (unfixed) way must now fail");
    }

    /// <summary>
    /// Fail-honest guard: a charge row whose description does not match the interest/commission/
    /// IVA keyword pattern must remain IN the sum — the exclusion is conservative by design
    /// (ambiguous rows are never silently dropped).
    /// </summary>
    [Fact]
    public void Cl18_AmbiguousChargeDescription_NotMatchingExclusionPattern_StaysIncluded()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX COM CR",       null, null),
            MakeMovement(50m,  MovementSign.Charge, "TIENDA DEPARTAMENTAL", null, null), // ambiguous, not I/C/IVA
        };

        // target = 150 (both charges counted; neither matches the exclusion pattern)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(150m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "a row with no interest/commission/IVA keyword must stay in the sum (fail-honest)");
    }

    // -----------------------------------------------------------------------
    // CL-18 — RC1.S4.b residual fix (chunk B4): prefix-anchored fee-row pattern,
    // merchant-name false-positive guard
    // -----------------------------------------------------------------------

    /// <summary>
    /// Real-corpus residual (B-2026-06, triage class c): a merchant purchase row whose NAME
    /// merely contains the word "Comisión" — "COMISION ESTATAL DE AG CEA 800313C95MX" (Comisión
    /// Estatal de Aguas, a state water utility, carrying an RFC-shaped token) — must NOT be
    /// excluded from the CL-18 "Cargos regulares" sum. The old bare <c>\bCOMISION\b</c> keyword
    /// wrongly excluded it (delta exactly −230.00); the new prefix-anchored pattern does not
    /// match, because the description does not begin with a known bank-fee phrase.
    /// </summary>
    [Fact]
    public void Cl18_MerchantNameContainingComision_WithRfcShapedToken_StaysIncluded()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(230.00m, MovementSign.Charge,
                "COMISION ESTATAL DE AG CEA 800313C95MX", null, null), // merchant, not a fee row
        };

        // target = 230 (the merchant purchase counts toward regular charges)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(230.00m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "a merchant name that merely contains the word 'Comisión' must not be classified " +
            "as a bank fee row — CL-18 real-corpus residual (B-2026-06)");
    }

    /// <summary>
    /// Genuine bank-fee rows, prefixed exactly as printed across the 4 real Banamex "B" months,
    /// remain excluded under the new prefix-anchored pattern.
    /// </summary>
    [Fact]
    public void Cl18_RealCorpusFeeRowPrefixes_StillExcluded()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m,  MovementSign.Charge, "NETFLIX COM CR",                        null, null), // regular
            MakeMovement(11.20m, MovementSign.Charge, "IVA POR INTERESES Y/O COMISIONES",       null, null), // fee — excluded
            MakeMovement(45.30m, MovementSign.Charge, "INTERES GRAVAB. DISPONIBLE BANAM",       null, null), // fee — excluded
            MakeMovement(12.10m, MovementSign.Charge, "INTERES EXENTO DISPONIBLE BANAM",        null, null), // fee — excluded
        };

        // target = 100 (only the regular charge; the three real-corpus fee-row prefixes excluded)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(100m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "the real-corpus fee-row description prefixes must still be excluded from the sum");
    }

    [Fact]
    public void Cl18_NoMovements_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-18");
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(300m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl18_TargetNotExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(100m, MovementSign.Charge, "NETFLIX", null, null),
        };

        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Missing());    // ← not extracted
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // CL-19 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl19_MsiChargesMatchTarget_ReturnsPass()
    {
        var rule = GetRule("CL-19");
        var movements = new List<StatementMovement>
        {
            MakeMovement(985.39m, MovementSign.Charge, "MSI CARGO 001 de 012",  null, null),  // MSI
            MakeMovement(100m,    MovementSign.Charge, "REGULAR PURCHASE",       null, null),  // non-MSI
        };

        var ps = MakeSummaryWithDates(cargosComprasAMesesCapital: Found(985.39m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl19_MsiChargesDeviate_ReturnsFail()
    {
        var rule = GetRule("CL-19");
        var movements = new List<StatementMovement>
        {
            MakeMovement(985.39m, MovementSign.Charge, "MSI 001 de 012", null, null),
        };

        var ps = MakeSummaryWithDates(cargosComprasAMesesCapital: Found(1000m)); // off by 14.61
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl19_MsiPatternDetected_CorrectlyClassified()
    {
        var rule = GetRule("CL-19");
        // Only the MSI charge should count in CL-19 sum
        var movements = new List<StatementMovement>
        {
            MakeMovement(985.39m, MovementSign.Charge, "DON COLCHON 005 de 012", null, null),  // MSI
            MakeMovement(100m,    MovementSign.Charge, "REGULAR PURCHASE",        null, null),  // non-MSI
        };

        var ps = MakeSummaryWithDates(cargosComprasAMesesCapital: Found(985.39m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // MovementClassifier — MSI regex negative test (Epic 4 adversarial review)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Documents a known edge-case in the MSI regex pattern (<c>\b\d{1,3}\s+de\s+\d{1,3}\b</c>):
    /// a description that contains "N de N" digits but is NOT an MSI installment — e.g. an
    /// address fragment "LOC 5 de ENE" or a product name "3 de ENERO" — will currently match the
    /// pattern and be incorrectly classified as MSI.
    /// <br/>
    /// This test pins current (known) behavior: such descriptions ARE classified as MSI.
    /// The owner decision is to document the limitation rather than tighten the regex, because:
    /// <list type="bullet">
    ///   <item>Real Banamex fixtures show no such ambiguous descriptions in practice.</item>
    ///   <item>Tightening the regex risks false negatives on real MSI rows.</item>
    ///   <item>CL-19 / CL-18 have low practical error risk given the corpus.</item>
    /// </list>
    /// If a future corpus introduces ambiguous descriptions, revisit this test and the regex.
    /// </summary>
    [Fact]
    public void MovementClassifier_NNN_De_NNN_NonMsiDescription_CurrentlyMatchesMsiPattern()
    {
        // "5 de 12" embedded in what could be a non-MSI context.
        // e.g. "COMPRA LOCAL 5 de 12 CALZ" — syntactically matches but semantically not MSI.
        // Current regex: \b\d{1,3}\s+de\s+\d{1,3}\b → matches "5 de 12"
        // This test PINS current behavior (match = true) so any future regex change is visible.
        const string edgeDescription = "COMPRA LOCAL 5 de 12 CALZ";

        // Access via CL-18 PASS/FAIL to indirectly exercise the classifier.
        // If classified as MSI, CL-18 excludes it → non-MSI sum = 0 → mismatch with target 300.
        // If NOT classified as MSI, CL-18 includes it → non-MSI sum = 300 → matches target.
        var rule = GetRule("CL-18");
        var movements = new List<StatementMovement>
        {
            MakeMovement(300m, MovementSign.Charge, edgeDescription, null, null),
        };

        // Target = 300 (if edge description treated as non-MSI → sum = 300 → Pass)
        // Target = 0  (if edge description treated as MSI     → sum = 0   → Fail)
        var ps = MakeSummaryWithDates(cargosRegularesNoMeses: Found(300m));
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        // CURRENT BEHAVIOR: "5 de 12" matches the MSI pattern, so the charge is EXCLUDED from
        // the CL-18 non-MSI sum → mismatch → Fail.
        // This test pins this behavior. If the regex is tightened to avoid false positives,
        // this test will flip to Pass — update it and the summary comment accordingly.
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Current MSI regex matches '5 de 12' in non-MSI descriptions — " +
            "known limitation, pinned behavior (Epic 4 review finding)");
    }

    // -----------------------------------------------------------------------
    // CL-20 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl20_AbonoCreditSumMatchesPagosYAbonos_ReturnsPass()
    {
        var rule = GetRule("CL-20");
        var movements = new List<StatementMovement>
        {
            MakeMovement(6523.00m,  MovementSign.Credit, "PROGRAMA FOR LIFE",  null, null),
            MakeMovement(61273.35m, MovementSign.Credit, "SU ABONO GRACIAS",   null, null),
            MakeMovement(100m,      MovementSign.Charge, "NETFLIX",             null, null),  // not counted
        };

        var ps = MakeSummaryWithDates(pagosYAbonos: Found(67796.35m));  // 6523 + 61273.35
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl20_AbonoSumDeviates_ReturnsFail()
    {
        var rule = GetRule("CL-20");
        var movements = new List<StatementMovement>
        {
            MakeMovement(6523.00m, MovementSign.Credit, "PROGRAMA FOR LIFE", null, null),
        };

        var ps = MakeSummaryWithDates(pagosYAbonos: Found(99999m)); // off by a lot
        var ctx = Ctx(BundleWithAccount(), ModelWithMovements(ps, movements));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl20_NoMovements_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-20");
        var ps = MakeSummaryWithDates(pagosYAbonos: Found(67796.35m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));
        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl20_CancellationRequested_ReturnsCancelled()
    {
        var rule = GetRule("CL-20");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWithAccount());
        var result = rule.Evaluate(ctx, cts.Token);

        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled().ShouldBeTrue();
    }
}
