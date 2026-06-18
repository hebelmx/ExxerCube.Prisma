using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-49: Promotions-currency verification — flags promotional inserts whose validity
/// window has expired relative to the statement billing period (FR-26).
/// </summary>
/// <remarks>
/// <para>
/// <b>What this rule checks:</b> for each promotion in the bundle whose
/// <c>AppliesToProducts</c> list includes the resolved product, the rule tests whether
/// the promotion was already expired when the billing period began — i.e.
/// <c>ValidTo &lt; PeriodStart</c>.  A promotion whose validity window ends before the
/// period starts should not appear as an active insert in that statement.
/// </para>
/// <para>
/// <b>Presence detection — approach (b) chosen:</b> VEC promotional inserts are
/// catalog images (printed flyers physically inserted into the mailing envelope).
/// They appear in the statement PDF as image objects, not as machine-readable text.
/// Because <see cref="StatementModel.NormalizedFullText"/> contains only the PDF text
/// layer, and because <see cref="Promotion.PromotionId"/> is an internal catalog key
/// that is not printed verbatim anywhere in the statement body, there is <b>no reliable
/// text-presence signal</b> that would let this rule confirm whether a given promotional
/// insert is physically present in the document.
/// </para>
/// <para>
/// Consequence: detecting whether an expired promotion is actually present requires
/// image-based catalog matching (pHash/dHash against <see cref="Promotion.Image"/>),
/// which is the <b>deferred, client-gated work in Story 5.4</b>.  Emitting a hard
/// <see cref="Domain.Enums.FindingVerdict.Fail"/> without confirming presence would risk a false FAIL
/// on every statement that simply does not include the promotion at all — which is the
/// normal, correct case for an expired promotion.
/// </para>
/// <para>
/// Therefore, v1 of this rule applies the following policy:
/// <list type="bullet">
///   <item>
///     If NO applicable promotion is expired → <b>Pass</b> (all current promotions
///     are within their validity windows for this period).
///   </item>
///   <item>
///     If one or more applicable promotions are expired for this period →
///     <b>InsufficientData</b> (rather than a hard fail), with a reason string that names the
///     promotion id(s) and the boundary dates so that a reviewer can cross-check manually.
///     This verdict is escalated to Fail only after Story 5.4 confirms insert presence.
///   </item>
/// </list>
/// This design guarantees the rule <b>never emits a false FAIL</b> for a promotion that
/// may simply not be printed in the statement.
/// </para>
/// <para>
/// <b>InsufficientData gates:</b>
/// <list type="bullet">
///   <item>No <c>promotions</c> section in the bundle (null or empty list).</item>
///   <item>No <see cref="StatementModel"/> or no <see cref="PeriodSummary"/>.</item>
///   <item>
///     <see cref="PeriodSummary.PeriodStart"/> was not extracted
///     (<see cref="ExtractionStatus.NotExtracted"/>).
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Product scoping:</b> a promotion with a null or empty <c>AppliesToProducts</c>
/// list is treated as applying to <b>all products</b> (consistent with the bundle schema
/// convention used by <c>mandatoryLegends</c>).
/// </para>
/// </remarks>
internal sealed class Cl49PromotionsCurrencyRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-49";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §18";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Gate 1: bundle must have a non-empty promotions section.
        var promotions = ctx.Bundle.Promotions;
        if (promotions is null || promotions.Count == 0)
            return InsufficientData("no promotions reference data in bundle.");

        // Gate 2: statement model and PeriodSummary must be available.
        var sm = ctx.StatementModel;
        if (sm is null)
            return InsufficientData("StatementModel is not populated.");

        var ps = sm.PeriodSummary;
        if (ps is null)
            return InsufficientData("PeriodSummary is not available (extraction stage has not run).");

        // Gate 3: PeriodStart must have been extracted.
        if (ps.PeriodStart.Status != ExtractionStatus.Extracted || ps.PeriodStart.Value == default)
            return InsufficientData("PeriodStart was not extracted from the statement.");

        var periodStart = ps.PeriodStart.Value;
        var productId = ctx.ResolvedProduct.ProductId;

        // Evaluate each promotion that applies to the resolved product.
        var expiredIds = new List<string>();

        foreach (var promo in promotions)
        {
            // Skip promotions that do not apply to this product.
            if (!AppliesToProduct(promo, productId))
                continue;

            // Parse ValidTo date. Malformed dates are skipped defensively.
            if (!DateOnly.TryParse(promo.ValidTo, CultureInfo.InvariantCulture, out var validTo))
                continue;

            // A promotion is expired for this period if its validity ended before the period began.
            if (validTo < periodStart)
                expiredIds.Add(promo.PromotionId);
        }

        if (expiredIds.Count == 0)
        {
            // No applicable promotion is expired relative to this period → Pass.
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"No applicable promotion is expired for period starting {periodStart:yyyy-MM-dd}."));
        }

        // One or more applicable promotions are expired.
        // We CANNOT confirm presence without image matching (Story 5.4), so we emit
        // InsufficientData rather than a hard Fail to avoid false negatives.
        var idList = string.Join(", ", expiredIds);
        var reason = $"Promotion(s) [{idList}] are expired for this period " +
                     $"(periodStart={periodStart:yyyy-MM-dd}); " +
                     $"insert-presence confirmation requires catalog image matching, " +
                     $"deferred to Story 5.4.";

        return InsufficientData(reason);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the promotion applies to the given product.
    /// A null or empty <c>AppliesToProducts</c> means the promotion applies to all products.
    /// </summary>
    private static bool AppliesToProduct(Promotion promotion, string productId)
    {
        var scope = promotion.AppliesToProducts;
        if (scope is null || scope.Count == 0)
            return true;   // all-products scope

        foreach (var id in scope)
        {
            if (string.Equals(id, productId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
