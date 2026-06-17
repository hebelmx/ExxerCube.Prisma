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
/// Unit tests for the Story 4.3 cross-period validation rules:
/// CL-17, CL-36, CL-37, CL-39, CL-40, CL-41.
/// </summary>
/// <remarks>
/// <para>
/// CL-17 is the only rule that produces real PASS/FAIL verdicts in Story 4.3 because its
/// inputs are available (AdeudoPeriodoAnterior is extracted; prior pago is in the bundle).
/// </para>
/// <para>
/// CL-36/37/39 degrade to InsufficientData for non-rewards products (BSSB fixtures) and
/// also for rewards products whose extraction is not yet implemented.
/// </para>
/// <para>
/// CL-40/41 are always InsufficientData pending Story 4.4 installment-table extraction.
/// </para>
/// </remarks>
public sealed class CrossPeriodRulesTests
{
    private const decimal CurrTol = 0.50m;
    private const string ProductIdNoRewards = "TC-BSSB-TEST";
    private const string ProductIdRewards = "TC-REWARDS-TEST";
    private const string AccountRef = "REF-CROSS-001";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct ProductNoRewards() =>
        new(ProductId: ProductIdNoRewards, ProductName: "BSSB Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null, Tariffs: null);

    private static VecProduct ProductRewards() =>
        new(ProductId: ProductIdRewards, ProductName: "Rewards Card",
            Aliases: null, HasRewardsProgram: true,
            CardImage: null, ImportantMessageImage: null, Tariffs: null);

    private static ToleranceConfig Tolerance() =>
        new(CurrencyToleranceMxn: CurrTol, PointsTolerance: 1.00m,
            RewardsPesosToleranceMxn: 1.00m, PointsToPesosExchangeRate: 0.10m);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static ExtractedField<decimal> Found(decimal value) =>
        ExtractedField<decimal>.Found(value, P1());

    private static ExtractedField<decimal> Missing() =>
        ExtractedField<decimal>.Missing(P1());

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> wrapping the supplied <see cref="PeriodSummary"/>.
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
    /// Builds a minimal <see cref="PeriodSummary"/> with the given AdeudoPeriodoAnterior value
    /// and all other fields set to Missing.
    /// </summary>
    private static PeriodSummary SummaryWith(ExtractedField<decimal>? adeudo = null)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var dayCount = new DayCountVerification(
            PrintedDays: 31,
            ComputedSpanDays: 30,
            IsConsistent: true);

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
            adeudoPeriodoAnterior: adeudo);
    }

    /// <summary>
    /// Builds a prior statement with the given PagoParaNoGenerarIntereses closing balance.
    /// </summary>
    private static PriorStatement PriorWith(decimal? pagoParaNoGenerarIntereses)
    {
        var closingBalances = new ClosingBalances(
            PagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses,
            SaldoDeudorTotal: null,
            RewardsPointsBalance: null,
            RewardsPesosBalance: null);

        return new PriorStatement(
            AccountRef: AccountRef,
            Period: new PeriodRange("Prior Period", "2025-06-01", "2025-06-30"),
            ClosingBalances: closingBalances,
            Installments: null,
            DocumentRef: null);
    }

    /// <summary>
    /// Builds a bundle with ToleranceConfig and the given prior statement (may be null).
    /// </summary>
    private static VecReferenceBundle BundleWith(PriorStatement? prior)
    {
        IReadOnlyList<PriorStatement>? priorList = prior is not null
            ? [prior]
            : null;

        return new VecReferenceBundle(
            BundleMetadata: Metadata(),
            Products: null,
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: priorList,
            ExpectedTransactions: null,
            ToleranceConfig: Tolerance(),
            ValidationConstants: null);
    }

    /// <summary>Bundle with NO ToleranceConfig.</summary>
    private static VecReferenceBundle BundleNoTolerance() =>
        new(
            BundleMetadata: Metadata(),
            Products: null,
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext Ctx(
        VecReferenceBundle bundle,
        VecProduct product,
        PriorStatement? priorStatement,
        StatementModel? model = null) =>
        new(
            bundle: bundle,
            resolvedProduct: product,
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: priorStatement,
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
        throw new InvalidOperationException($"Rule {checkId} not registered in DI.");
    }

    // -----------------------------------------------------------------------
    // CL-17: Adeudo del periodo anterior vs prior PagoParaNoGenerarIntereses
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl17_AdeudoMatchesPriorPagoWithinTolerance_ReturnsPass()
    {
        // Prior statement pago = 32446.69; statement adeudo = 32446.69 → diff 0.00 ≤ 0.50
        var prior = PriorWith(pagoParaNoGenerarIntereses: 32446.69m);
        var ps = SummaryWith(adeudo: Found(32446.69m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-17");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(CurrTol);
    }

    [Fact]
    public void Cl17_AdeudoWithinHalfPesoOfPriorPago_ReturnsPass()
    {
        // Prior pago = 1000.00; statement adeudo = 1000.49 → diff 0.49 ≤ 0.50
        var prior = PriorWith(pagoParaNoGenerarIntereses: 1000.00m);
        var ps = SummaryWith(adeudo: Found(1000.49m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(CurrTol);
    }

    [Fact]
    public void Cl17_AdeudoDiffExceedsHalfPeso_ReturnsFail()
    {
        // Prior pago = 32446.69; statement adeudo = 30000.00 → diff 2446.69 > 0.50
        var prior = PriorWith(pagoParaNoGenerarIntereses: 32446.69m);
        var ps = SummaryWith(adeudo: Found(30000.00m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(CurrTol);
        result.Value.Expected.ShouldBe("32446.69");
        result.Value.Observed.ShouldBe("30000.00");
    }

    [Fact]
    public void Cl17_NoPriorStatement_ReturnsInsufficientData()
    {
        // ctx.PriorStatement is null → no prior for this account
        var ps = SummaryWith(adeudo: Found(32446.69m));
        var ctx = Ctx(BundleWith(null), ProductNoRewards(), priorStatement: null, model: ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("prior statement");
    }

    [Fact]
    public void Cl17_NullToleranceConfig_ReturnsInsufficientData()
    {
        var ps = SummaryWith(adeudo: Found(32446.69m));
        var ctx = new VerificationContext(
            bundle: BundleNoTolerance(),
            resolvedProduct: ProductNoRewards(),
            availability: ReferenceDataAvailability.FromBundle(BundleNoTolerance()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("ToleranceConfig");
    }

    [Fact]
    public void Cl17_AdeudoNotExtracted_ReturnsInsufficientData()
    {
        // AdeudoPeriodoAnterior = Missing (NotExtracted)
        var prior = PriorWith(pagoParaNoGenerarIntereses: 32446.69m);
        var ps = SummaryWith(adeudo: Missing());   // NotExtracted
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("AdeudoPeriodoAnterior");
    }

    [Fact]
    public void Cl17_PriorClosingBalancesNullPago_ReturnsInsufficientData()
    {
        // Prior statement has ClosingBalances with null PagoParaNoGenerarIntereses
        var prior = PriorWith(pagoParaNoGenerarIntereses: null);
        var ps = SummaryWith(adeudo: Found(32446.69m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("PagoParaNoGenerarIntereses");
    }

    [Fact]
    public void Cl17_ToleranceRecorded_OnPass()
    {
        // ADR-V3: Pass findings must record the applied tolerance
        var prior = PriorWith(pagoParaNoGenerarIntereses: 5000.00m);
        var ps = SummaryWith(adeudo: Found(5000.00m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(CurrTol,
            "ADR-V3: CL-17 must record the applied tolerance on every Pass");
    }

    [Fact]
    public void Cl17_ToleranceRecorded_OnFail()
    {
        // ADR-V3: Fail findings must also record the tolerance that was exceeded
        var prior = PriorWith(pagoParaNoGenerarIntereses: 5000.00m);
        var ps = SummaryWith(adeudo: Found(1.00m));
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = GetRule("CL-17").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.ToleranceApplied.ShouldBe(CurrTol,
            "ADR-V3: CL-17 must record the applied tolerance on Fail");
    }

    // -----------------------------------------------------------------------
    // CL-17: Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl17_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var rule = GetRule("CL-17");
        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue("cancelled token must propagate to a cancelled Result");
    }

    // -----------------------------------------------------------------------
    // CL-36: Saldo inicial puntos — non-rewards vs rewards
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl36_NonRewardsProduct_ReturnsInsufficientDataNotApplicable()
    {
        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-36").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-36");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("not applicable");
        result.Value.Observed!.ShouldContain("rewards program");
    }

    [Fact]
    public void Cl36_RewardsProductExtractionPending_ReturnsInsufficientData()
    {
        var ctx = Ctx(BundleWith(null), ProductRewards(), null);

        var result = GetRule("CL-36").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("rewards extraction pending");
    }

    [Fact]
    public void Cl36_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-36").Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-37: Tipo de cambio rewards
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl37_NonRewardsProduct_ReturnsInsufficientDataNotApplicable()
    {
        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-37").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-37");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("not applicable");
    }

    [Fact]
    public void Cl37_RewardsProductExtractionPending_ReturnsInsufficientData()
    {
        var ctx = Ctx(BundleWith(null), ProductRewards(), null);

        var result = GetRule("CL-37").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("rewards extraction pending");
    }

    [Fact]
    public void Cl37_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-37").Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-39: Saldo total puntos
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl39_NonRewardsProduct_ReturnsInsufficientDataNotApplicable()
    {
        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-39").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-39");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("not applicable");
    }

    [Fact]
    public void Cl39_RewardsProductExtractionPending_ReturnsInsufficientData()
    {
        var ctx = Ctx(BundleWith(null), ProductRewards(), null);

        var result = GetRule("CL-39").Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("rewards extraction pending");
    }

    [Fact]
    public void Cl39_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule("CL-39").Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-40: Saldo pendiente (installments — always InsufficientData in Story 4.3)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-40")]
    [InlineData("CL-41")]
    public void Cl40_41_AlwaysInsufficientDataPendingStory44(string checkId)
    {
        var prior = PriorWith(pagoParaNoGenerarIntereses: 5000.00m);
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior);

        var result = GetRule(checkId).Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(checkId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            $"{checkId} must emit InsufficientData until Story 4.4 installment extraction is done.");
        result.Value.Observed!.ShouldContain("Story 4.4");
    }

    [Theory]
    [InlineData("CL-40")]
    [InlineData("CL-41")]
    public void Cl40_41_CancelledToken_ReturnsCancelledResult(string checkId)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ctx = Ctx(BundleWith(null), ProductNoRewards(), null);

        var result = GetRule(checkId).Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Engine end-to-end: all 6 new cross-period rules run in the engine
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Engine_CrossPeriodRules_YieldExpectedVerdicts()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        // Prior statement with a pago that matches the statement's adeudo exactly
        var prior = PriorWith(pagoParaNoGenerarIntereses: 32446.69m);
        var ps = SummaryWith(adeudo: Found(32446.69m));

        // Non-rewards product so CL-36/37/39 → InsufficientData("not applicable")
        var ctx = Ctx(BundleWith(prior), ProductNoRewards(), prior, ModelWith(ps));

        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Engine failed: {result.Error}");
        var findings = result.Value!;

        // CL-17 should Pass (adeudo == prior pago, within tolerance)
        findings.Single(f => f.CheckId == "CL-17").Verdict
            .ShouldBe(FindingVerdict.Pass, "CL-17: matching adeudo and prior pago → Pass");

        // CL-36/37/39: InsufficientData because product has no rewards program
        foreach (var id in new[] { "CL-36", "CL-37", "CL-39" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.InsufficientData, $"{id} should be InsufficientData (no rewards)");

        // CL-40/41: InsufficientData because installment table not yet extracted (Story 4.4)
        foreach (var id in new[] { "CL-40", "CL-41" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.InsufficientData, $"{id} should be InsufficientData (Story 4.4 pending)");
    }
}
