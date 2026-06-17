using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for Story 6.3 — CL-49 promotions-currency verification (FR-26).
/// </summary>
/// <remarks>
/// <para>
/// All tests use hand-built <see cref="VerificationContext"/> objects with synthetic
/// bundle <c>Promotions</c> lists and <see cref="PeriodSummary"/> instances.
/// No real PDF extraction or network access is performed.
/// </para>
/// <para>
/// Because promotional inserts are image-based (catalog flyers, not PDF text),
/// <see cref="StatementModel.NormalizedFullText"/> cannot confirm insert presence.
/// The rule uses approach (b): expired applicable promotions produce
/// <see cref="FindingVerdict.InsufficientData"/> rather than a hard Fail, preventing
/// false FAIL verdicts until Story 5.4 adds image-presence confirmation.
/// A <see cref="FindingVerdict.Pass"/> is emitted when no applicable promotion is expired.
/// </para>
/// </remarks>
public sealed class PromotionsCurrencyRuleTests
{
    private const string ProductId = "TC-PROMO-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static readonly VecProduct TestProduct =
        new(ProductId: ProductId, ProductName: "Test Promo Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static readonly BundleMetadata TestMetadata =
        new("1.0.0", "Test Bank", null, null, null, null);

    private static readonly FieldLocator P1 = FieldLocator.PageHint(1);

    /// <summary>
    /// Builds a promotion with the given id, validity window, and optional product scope.
    /// </summary>
    private static Promotion MakePromotion(
        string promotionId,
        string validFrom,
        string validTo,
        string[]? appliesToProducts = null) =>
        new(
            PromotionId: promotionId,
            Image: new ImageRef(null, null, null, null, null, null, null),
            ValidFrom: validFrom,
            ValidTo: validTo,
            AppliesToProducts: appliesToProducts);

    /// <summary>
    /// Builds a <see cref="VecReferenceBundle"/> with the given promotions list.
    /// </summary>
    private static VecReferenceBundle BundleWithPromotions(
        IReadOnlyList<Promotion>? promotions) =>
        new(
            BundleMetadata: TestMetadata,
            Products: [TestProduct],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: promotions,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    /// <summary>
    /// Builds a minimal <see cref="PeriodSummary"/> with the given period start date.
    /// All other fields are set to Missing / default.
    /// </summary>
    private static PeriodSummary MakePeriodSummary(DateOnly? periodStart)
    {
        var missingDec = ExtractedField<decimal>.Missing(P1);
        var missingStr = ExtractedField<string>.Missing(P1);
        var missingInt = ExtractedField<int>.Missing(P1);
        var dayCount = new DayCountVerification(
            PrintedDays: 0,
            ComputedSpanDays: 0,
            IsConsistent: false);

        var startField = periodStart.HasValue
            ? ExtractedField<DateOnly>.Found(periodStart.Value, P1)
            : ExtractedField<DateOnly>.Missing(P1);

        return new PeriodSummary(
            product: missingStr,
            periodStart: startField,
            periodCutDate: ExtractedField<DateOnly>.Missing(P1),
            paymentDueDate: ExtractedField<DateOnly>.Missing(P1),
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: missingDec,
            pagoMinimo: missingDec,
            pagoMinimoMasMeses: missingDec,
            tasa: missingDec,
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec);
    }

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given <see cref="PeriodSummary"/>.
    /// </summary>
    private static StatementModel MakeModel(PeriodSummary? periodSummary)
    {
        var missingStr = ExtractedField<string>.Missing(P1);
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1);

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            PeriodSummary = periodSummary,
        };
    }

    private static VerificationContext MakeContext(
        VecReferenceBundle bundle,
        StatementModel? model = null) =>
        new(
            bundle: bundle,
            resolvedProduct: TestProduct,
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the bundle contains no promotions data (null), the rule must degrade
    /// to InsufficientData rather than producing a spurious verdict.
    /// </summary>
    [Fact]
    public void Cl49_NullPromotionsInBundle_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions(null);
        var model = MakeModel(MakePeriodSummary(new DateOnly(2025, 7, 5)));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-49");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("no promotions reference data");
    }

    /// <summary>
    /// When the bundle contains an empty promotions list, the rule must degrade
    /// to InsufficientData (same as null — no reference data is available).
    /// </summary>
    [Fact]
    public void Cl49_EmptyPromotionsInBundle_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions([]);
        var model = MakeModel(MakePeriodSummary(new DateOnly(2025, 7, 5)));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    /// <summary>
    /// When StatementModel is null (extraction stage has not run), the rule must
    /// degrade to InsufficientData rather than throwing or producing a verdict.
    /// </summary>
    [Fact]
    public void Cl49_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-001", "2025-01-01", "2025-06-30",
                appliesToProducts: [ProductId]),
        ]);
        var ctx = MakeContext(bundle, model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("StatementModel");
    }

    /// <summary>
    /// When the StatementModel is present but has no PeriodSummary (extraction stage
    /// only ran the header pass), the rule must degrade to InsufficientData.
    /// </summary>
    [Fact]
    public void Cl49_NullPeriodSummary_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-001", "2025-01-01", "2025-06-30",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(periodSummary: null);   // no PeriodSummary
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("PeriodSummary");
    }

    /// <summary>
    /// When the PeriodSummary exists but PeriodStart was not extracted (e.g. the date
    /// label was not found in the PDF), the rule must degrade to InsufficientData.
    /// </summary>
    [Fact]
    public void Cl49_PeriodStartNotExtracted_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-001", "2025-01-01", "2025-06-30",
                appliesToProducts: [ProductId]),
        ]);
        // Pass null → PeriodStart field is Missing (NotExtracted).
        var model = MakeModel(MakePeriodSummary(periodStart: null));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed!.ShouldContain("PeriodStart");
    }

    /// <summary>
    /// When an applicable promotion's ValidTo is before PeriodStart, the rule emits
    /// InsufficientData (approach b) that names the promotion id and the period boundary.
    /// This avoids a false FAIL — presence cannot be confirmed without image matching.
    /// </summary>
    [Fact]
    public void Cl49_ApplicablePromotionExpiredBeforePeriod_ReturnsInsufficientData_WithPromoIdAndBoundary()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        // Period starts 2025-07-05; promotion expired 2025-06-30 (before period start).
        var periodStart = new DateOnly(2025, 7, 5);
        const string promoId = "PROMO-EXPIRED";
        var bundle = BundleWithPromotions(
        [
            MakePromotion(promoId, "2025-04-01", "2025-06-30",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-49");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "expired promotion cannot produce a hard FAIL without image-presence confirmation");

        var observed = result.Value.Observed;
        observed.ShouldNotBeNull();
        observed!.ShouldContain(promoId);
        observed.ShouldContain("2025-07-05");
        observed.ShouldContain("Story 5.4");
    }

    /// <summary>
    /// When an applicable promotion's ValidTo equals PeriodStart (boundary: last day of
    /// validity is the first day of the period), the promotion is NOT considered expired
    /// (ValidTo &lt; PeriodStart is false when they are equal).
    /// Expect Pass.
    /// </summary>
    [Fact]
    public void Cl49_ApplicablePromotionValidToEqualsPeriodStart_ReturnsPass()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        var periodStart = new DateOnly(2025, 7, 5);
        var bundle = BundleWithPromotions(
        [
            // ValidTo == PeriodStart: not expired (strictly-before condition is false).
            MakePromotion("PROMO-BOUNDARY", "2025-06-01", "2025-07-05",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-49");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "promotion valid through period start day is not expired");
    }

    /// <summary>
    /// When an applicable promotion's ValidTo is after PeriodStart (promotion is within
    /// or beyond the billing period), the rule returns Pass.
    /// </summary>
    [Fact]
    public void Cl49_ApplicablePromotionCurrentWithinPeriod_ReturnsPass()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        var periodStart = new DateOnly(2025, 7, 5);
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-CURRENT", "2025-07-01", "2025-08-31",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "promotion still valid during billing period must not be flagged");
        result.Value.Observed!.ShouldContain("2025-07-05");
    }

    /// <summary>
    /// A promotion whose AppliesToProducts list does NOT include the resolved product
    /// must be ignored entirely; it should not affect the verdict.
    /// Even if the promotion is expired, the result is Pass (not applicable).
    /// </summary>
    [Fact]
    public void Cl49_PromotionForDifferentProduct_IsIgnored_ReturnsPass()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        var periodStart = new DateOnly(2025, 7, 5);
        var bundle = BundleWithPromotions(
        [
            // Applies only to "OTHER-PRODUCT", not to ProductId.
            MakePromotion("PROMO-OTHER", "2025-01-01", "2025-03-31",
                appliesToProducts: ["OTHER-PRODUCT"]),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-49");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "expired promotion for a different product must not affect the resolved product's verdict");
    }

    /// <summary>
    /// A promotion with a null or empty AppliesToProducts list applies to all products.
    /// If such a promotion is expired, the rule emits InsufficientData, naming the
    /// promotion id and the period boundary.
    /// </summary>
    [Fact]
    public void Cl49_AllProductsScopePromotionExpired_ReturnsInsufficientData()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        var periodStart = new DateOnly(2025, 7, 5);
        const string promoId = "PROMO-ALL-EXPIRED";
        var bundle = BundleWithPromotions(
        [
            // AppliesToProducts is null → applies to all products.
            MakePromotion(promoId, "2025-01-01", "2025-06-01",
                appliesToProducts: null),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);

        var observed = result.Value.Observed;
        observed.ShouldNotBeNull();
        observed!.ShouldContain(promoId);
        observed.ShouldContain("2025-07-05");
    }

    /// <summary>
    /// When multiple promotions are in the bundle and only one is expired for the
    /// product, the expired one's id appears in the finding and the verdict is
    /// InsufficientData. The non-expired promotion id must not appear in the finding.
    /// </summary>
    [Fact]
    public void Cl49_MixedExpiredAndCurrentPromotions_ReportsOnlyExpired()
    {
        var rule = new Cl49PromotionsCurrencyRule();

        var periodStart = new DateOnly(2025, 7, 5);
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-EXPIRED-A", "2025-01-01", "2025-05-31",
                appliesToProducts: [ProductId]),
            MakePromotion("PROMO-CURRENT-B", "2025-06-01", "2025-09-30",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(MakePeriodSummary(periodStart));
        var ctx = MakeContext(bundle, model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);

        var observed = result.Value.Observed;
        observed.ShouldNotBeNull();
        observed!.ShouldContain("PROMO-EXPIRED-A");
        // Non-expired promotion must not appear in the finding.
        observed.Contains("PROMO-CURRENT-B").ShouldBeFalse(
            "non-expired promotions must not appear in the finding");
    }

    /// <summary>
    /// Cancellation before the rule starts executing must return a cancelled Result.
    /// </summary>
    [Fact]
    public void Cl49_Cancellation_ReturnsCancelled()
    {
        var rule = new Cl49PromotionsCurrencyRule();
        var bundle = BundleWithPromotions(
        [
            MakePromotion("PROMO-001", "2025-01-01", "2025-12-31",
                appliesToProducts: [ProductId]),
        ]);
        var model = MakeModel(MakePeriodSummary(new DateOnly(2025, 7, 5)));
        var ctx = MakeContext(bundle, model);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
