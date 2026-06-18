using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-45: Bidirectional transaction description matching — verifies that the set of
/// movement descriptions on the statement matches the expected transaction descriptions
/// in the reference bundle.
/// </summary>
/// <remarks>
/// <para>
/// Descriptions are compared in normalized form (upper-cased, whitespace and punctuation
/// collapsed to single spaces).  A mismatch is reported when:
/// <list type="bullet">
///   <item>An expected description is absent from the statement ("missing from printed").</item>
///   <item>A printed description has no match in the expected set ("extra on printed").</item>
/// </list>
/// </para>
/// <para>
/// The first <see cref="Domain.ReferenceData.ExpectedTransactionGroup"/> in
/// <see cref="Domain.ReferenceData.VecReferenceBundle.ExpectedTransactions"/> is used.
/// Account-level resolution is deferred to a future story.
/// </para>
/// <para>ToleranceApplied is null (description matching has no numeric tolerance).</para>
/// </remarks>
internal sealed class Cl45TransactionDescriptionMatchRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-45";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §22";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated.");

        if (model.MovementsStatus != MovementsExtractionStatus.Extracted || model.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {model.MovementsStatus}).");

        var expectedGroups = ctx.Bundle.ExpectedTransactions;
        if (expectedGroups is null || expectedGroups.Count == 0)
            return InsufficientData("No ExpectedTransactions in bundle for CL-45.");

        // Use the first group (account resolution deferred).
        var group = expectedGroups[0];
        if (group.Transactions.Count == 0)
            return InsufficientData("ExpectedTransactionGroup has no transactions.");

        var printedNorms = model.Movements
            .Select(m => Normalize(m.Description))
            .ToHashSet(StringComparer.Ordinal);

        var expectedNorms = group.Transactions
            .Select(t => Normalize(t.Description))
            .ToHashSet(StringComparer.Ordinal);

        var missingFromPrinted = expectedNorms.Except(printedNorms).ToList();
        var extraOnPrinted = printedNorms.Except(expectedNorms).ToList();
        var allUnmatched = missingFromPrinted.Concat(extraOnPrinted).ToList();

        if (allUnmatched.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {expectedNorms.Count} transaction description(s) matched bidirectionally.",
                    toleranceApplied: null));
        }

        var detail = string.Join("; ", allUnmatched.Take(10));
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Descriptions match (expected {expectedNorms.Count}, printed {printedNorms.Count})",
                observed: $"Unmatched ({allUnmatched.Count}): {detail}",
                toleranceApplied: null));
    }

    /// <summary>
    /// Normalizes a description for comparison: upper-cased, whitespace and punctuation
    /// replaced by a single space.
    /// </summary>
    internal static string Normalize(string s) =>
        Regex.Replace(s.ToUpperInvariant(), @"[\s\p{P}]+", " ").Trim();

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
