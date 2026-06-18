using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// ITEM-58: For each expected transaction whose description matched on the statement (CL-45),
/// validates that the printed amount equals the expected amount within tolerance.
/// </summary>
/// <remarks>
/// <para>
/// Descriptions are matched using the same normalization as CL-45.
/// If a description has no match on the statement it is skipped — CL-45 handles
/// unmatched descriptions; Item-58 only verifies amounts for matched entries.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// <para>
/// <b>Note on confidence guard:</b> individual <see cref="Domain.Extraction.StatementMovement"/>
/// objects are raw domain values (not <c>ExtractedField&lt;T&gt;</c>) and do not carry per-field
/// confidence scores. The rule already abstains via <see cref="MovementsExtractionStatus"/> when
/// the DESGLOSE section was not extracted. No per-movement confidence guard is applied.
/// </para>
/// <para>
/// The first <see cref="ExpectedTransactionGroup"/> in the bundle is used.
/// Account-level resolution is deferred.
/// </para>
/// </remarks>
internal sealed class Item58TransactionAmountMatchRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Item58TransactionAmountMatchRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Item58TransactionAmountMatchRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "ITEM-58";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §22";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Resolve LEGAL tolerance: always use LegalDefault (no bundle override — Story 9.6)
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated.");

        if (model.MovementsStatus != MovementsExtractionStatus.Extracted || model.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {model.MovementsStatus}).");

        var expectedGroups = ctx.Bundle.ExpectedTransactions;
        if (expectedGroups is null || expectedGroups.Count == 0)
            return InsufficientData("No ExpectedTransactions in bundle for ITEM-58.");

        var group = expectedGroups[0];
        if (group.Transactions.Count == 0)
            return InsufficientData("ExpectedTransactionGroup has no transactions.");

        // Build lookup: normalized description → list of movements
        var printedByDesc = model.Movements
            .GroupBy(m => Cl45TransactionDescriptionMatchRule.Normalize(m.Description), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Evaluate with the LEGAL tolerance bar for baseline verdict
        var legalFailures = new List<string>();
        // Evaluate with the EFFECTIVE (tenant) tolerance bar for primary verdict
        var tenantFailures = new List<string>();
        var checkedCount = 0;

        foreach (var expected in group.Transactions)
        {
            var normExpected = Cl45TransactionDescriptionMatchRule.Normalize(expected.Description);

            if (!printedByDesc.TryGetValue(normExpected, out var matched) || matched.Count == 0)
                continue; // unmatched — CL-45 handles this, skip in Item-58

            foreach (var movement in matched)
            {
                checkedCount++;
                var diff = Math.Abs(movement.Amount - expected.Amount);

                if (diff > legalTolerance)
                    legalFailures.Add(
                        $"'{expected.Description}': expected {expected.Amount:F2}, " +
                        $"observed {movement.Amount:F2} (diff {diff:F2})");

                if (diff > effectiveTolerance)
                    tenantFailures.Add(
                        $"'{expected.Description}': expected {expected.Amount:F2}, " +
                        $"observed {movement.Amount:F2} (diff {diff:F2})");
            }
        }

        var legalPasses = legalFailures.Count == 0;
        var tenantPasses = tenantFailures.Count == 0;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {checkedCount} matched transaction amount(s) within ±{effectiveTolerance:F2}",
                    toleranceApplied: effectiveTolerance,
                    legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
        }

        var sb = new StringBuilder();
        foreach (var f in tenantFailures.Take(5))
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(f);
        }

        if (tenantFailures.Count > 5)
            sb.Append($" ... (+{tenantFailures.Count - 5} more)");

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"All amounts within ±{effectiveTolerance:F2}",
                observed: sb.ToString(),
                toleranceApplied: effectiveTolerance,
                legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
